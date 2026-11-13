import { apiClient } from './ApiClient';
import {
  ClinicOfflineDb,
  OfflineNumberBlockRecord,
  OfflineOutboxRecord,
  offlineDb,
  wipeAllLocalData,
} from './offlineDb';
import { ProblemDetails } from './ProblemDetails';
import { useSyncStatusStore } from './useSyncStatusStore';

export interface SyncOutcomeDto {
  operationId: string;
  actionType: string;
  entityType: string;
  entityId: string;
  status: 'Applied' | 'DuplicateIgnored' | 'Conflict' | 'Rejected' | number;
  cursorSequence?: number | null;
  errorCode?: string | null;
  errorMessage?: string | null;
  conflictId?: string | null;
  clockSkewSeconds: number;
  isLargeClockSkew: boolean;
  assignedReceiptNumbers?: string[] | null;
}

export interface SyncPushBatchResponse {
  deviceId: string;
  latestCursor: number;
  serverTimestampUtc: string;
  appliedCount: number;
  duplicateCount: number;
  conflictCount: number;
  rejectedCount: number;
  outcomes: SyncOutcomeDto[];
}

export interface AllocatedNumberBlockResponse {
  id: string;
  clinicId: string;
  deviceId: string;
  kind: 'Receipt' | 'PatientFile';
  fromNumber: number;
  toNumber: number;
  nextNumber: number;
  remainingCount: number;
  status: 'Active' | 'Exhausted';
}

export function computeBackoffDelayMs(attempt: number, baseMs = 500, maxMs = 30000): number {
  const clamped = Math.max(0, Math.min(20, attempt));
  return Math.min(maxMs, Math.max(baseMs, baseMs * Math.pow(2, clamped)));
}

export async function refreshSyncCounters(
  clinicId: string,
  db: ClinicOfflineDb = offlineDb
): Promise<void> {
  const pending = await db.outbox.where('clinicId').equals(clinicId).toArray();
  const provisional = pending.filter((p) => Boolean(p.provisionalReceiptNumber)).length;
  const meta = await db.syncMeta.get(clinicId);
  useSyncStatusStore.getState().setSyncSnapshot({
    pendingCount: pending.length,
    provisionalReceiptsCount: provisional,
    lastCursor: meta?.cursor ?? 0,
    lastSyncAtUtc: meta?.lastSyncAtUtc ?? null,
  });
}

export async function consumeLocalReceiptNumber(
  clinicId: string,
  deviceId: string,
  db: ClinicOfflineDb = offlineDb
): Promise<{ receiptNumber: string; isProvisional: boolean; needsRefill: boolean }> {
  const blocks = await db.numberBlocks
    .where('[clinicId+deviceId+kind]')
    .equals([clinicId, deviceId, 'Receipt'])
    .toArray();

  const active = blocks
    .filter((b) => b.status === 'Active' && b.nextNumber <= b.toNumber)
    .sort((a, b) => a.fromNumber - b.fromNumber)[0];

  const meta = await db.syncMeta.get(clinicId);
  const lowWater = meta?.lowWaterMark ?? 10;

  if (active) {
    const num = active.nextNumber;
    const nextNumber = num + 1;
    const status: 'Active' | 'Exhausted' = nextNumber > active.toNumber ? 'Exhausted' : 'Active';
    await db.numberBlocks.update(active.id, { nextNumber, status });
    const remaining = active.toNumber - nextNumber + 1;
    return {
      receiptNumber: `R-${String(num).padStart(6, '0')}`,
      isProvisional: false,
      needsRefill: remaining <= lowWater,
    };
  }

  const nextProv = (meta?.provisionalSeq ?? 0) + 1;
  await db.syncMeta.put({
    clinicId,
    cursor: meta?.cursor ?? 0,
    lastSyncAtUtc: meta?.lastSyncAtUtc ?? null,
    deviceId,
    provisionalSeq: nextProv,
    lowWaterMark: lowWater,
    offlineSessionMaxHours: meta?.offlineSessionMaxHours ?? 24,
    lastOnlineAuthAtUtc: meta?.lastOnlineAuthAtUtc ?? null,
  });

  return {
    receiptNumber: `PROV-${deviceId.slice(-4)}-${String(nextProv).padStart(4, '0')}`,
    isProvisional: true,
    needsRefill: true,
  };
}

export async function enqueueOfflineOperation(
  record: Omit<OfflineOutboxRecord, 'seq' | 'status' | 'attemptCount' | 'nextRetryAtMs'>,
  db: ClinicOfflineDb = offlineDb
): Promise<number> {
  const seq = await db.outbox.add({
    ...record,
    status: 'pending',
    attemptCount: 0,
    nextRetryAtMs: 0,
  });
  await refreshSyncCounters(record.clinicId, db);
  return seq;
}

export async function selectEligibleOutboxBatch(
  clinicId: string,
  nowMs: number = Date.now(),
  db: ClinicOfflineDb = offlineDb
): Promise<OfflineOutboxRecord[]> {
  const all = await db.outbox.where('clinicId').equals(clinicId).sortBy('seq');
  const blockedEntities = new Set<string>();
  const eligible: OfflineOutboxRecord[] = [];

  for (const item of all) {
    if (blockedEntities.has(item.entityId)) {
      continue;
    }
    if (item.nextRetryAtMs > nowMs) {
      // Preserve strict per-entity ordering: later mutations on the same entity wait behind retrying head.
      blockedEntities.add(item.entityId);
      continue;
    }
    eligible.push(item);
    if (eligible.length >= 100) {
      break;
    }
  }

  return eligible;
}

export async function prefetchReceiptBlock(
  clinicId: string,
  deviceId: string,
  requestedSize?: number,
  db: ClinicOfflineDb = offlineDb
): Promise<AllocatedNumberBlockResponse> {
  const allocated = await apiClient.post<AllocatedNumberBlockResponse>(
    `/clinics/${clinicId}/sync/number-blocks`,
    { deviceId, kind: 'Receipt', requestedSize }
  );

  const row: OfflineNumberBlockRecord = {
    id: allocated.id,
    clinicId: allocated.clinicId,
    deviceId: allocated.deviceId,
    kind: allocated.kind,
    fromNumber: allocated.fromNumber,
    toNumber: allocated.toNumber,
    nextNumber: allocated.nextNumber,
    status: allocated.status,
  };
  await db.numberBlocks.put(row);
  return allocated;
}

export async function flushOutbox(
  clinicId: string,
  deviceId: string,
  nowMs: number = Date.now(),
  db: ClinicOfflineDb = offlineDb
): Promise<SyncPushBatchResponse | null> {
  if (typeof navigator !== 'undefined' && navigator.onLine === false) {
    return null;
  }

  const eligible = await selectEligibleOutboxBatch(clinicId, nowMs, db);
  if (eligible.length === 0) {
    return null;
  }

  useSyncStatusStore.getState().setSyncing(true);
  try {
    const response = await apiClient.post<SyncPushBatchResponse>(
      `/clinics/${clinicId}/sync/push`,
      {
        deviceId,
        operations: eligible.map((op) => ({
          operationId: op.operationId,
          actionType: op.actionType,
          entityType: op.entityType,
          entityId: op.entityId,
          payloadJson: op.payloadJson,
          clientTimestampUtc: op.clientTimestampUtc,
          clientBaseCursor: op.clientBaseCursor,
        })),
      }
    );

    const byOpId = new Map(response.outcomes.map((o) => [o.operationId, o]));
    for (const op of eligible) {
      const outcome = byOpId.get(op.operationId);
      if (!outcome || op.seq === undefined) {
        continue;
      }

      if (
        outcome.status === 'Applied' ||
        outcome.status === 'DuplicateIgnored' ||
        outcome.status === 0 ||
        outcome.status === 1
      ) {
        await db.outbox.delete(op.seq);
      } else {
        const nextAttempt = op.attemptCount + 1;
        await db.outbox.update(op.seq, {
          status: 'failed',
          attemptCount: nextAttempt,
          nextRetryAtMs: nowMs + computeBackoffDelayMs(nextAttempt),
          lastErrorCode: outcome.errorCode ?? undefined,
          lastErrorMessage: outcome.errorMessage ?? undefined,
        });
      }
    }

    const meta = await db.syncMeta.get(clinicId);
    await db.syncMeta.put({
      clinicId,
      cursor: Math.max(meta?.cursor ?? 0, response.latestCursor),
      lastSyncAtUtc: response.serverTimestampUtc,
      deviceId,
      provisionalSeq: meta?.provisionalSeq ?? 0,
      lowWaterMark: meta?.lowWaterMark ?? 10,
      offlineSessionMaxHours: meta?.offlineSessionMaxHours ?? 24,
      lastOnlineAuthAtUtc: meta?.lastOnlineAuthAtUtc ?? response.serverTimestampUtc,
    });

    await refreshSyncCounters(clinicId, db);
    return response;
  } catch (err) {
    const problem = err as ProblemDetails | undefined;
    if (problem?.code === 'DEVICE_REVOKED') {
      await wipeAllLocalData(db);
      useSyncStatusStore.getState().setSyncSnapshot({
        pendingCount: 0,
        provisionalReceiptsCount: 0,
        deviceRevokedWiped: true,
      });
      throw problem;
    }

    for (const op of eligible) {
      if (op.seq === undefined) {
        continue;
      }
      const nextAttempt = op.attemptCount + 1;
      await db.outbox.update(op.seq, {
        status: 'retrying',
        attemptCount: nextAttempt,
        nextRetryAtMs: nowMs + computeBackoffDelayMs(nextAttempt),
        lastErrorCode: problem?.code ?? 'NETWORK_RETRY',
        lastErrorMessage: problem?.detail ?? 'Sync push failed; scheduled for retry.',
      });
    }
    await refreshSyncCounters(clinicId, db);
    throw err;
  } finally {
    useSyncStatusStore.getState().setSyncing(false);
  }
}
