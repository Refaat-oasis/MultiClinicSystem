import 'fake-indexeddb/auto';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { apiClient } from './ApiClient';
import {
  decryptSensitiveJson,
  encryptSensitiveJson,
  exportKeyBase64,
  generateAesGcmKey,
  importKeyBase64,
} from './cryptoStore';
import { ClinicOfflineDb, wipeAllLocalData } from './offlineDb';
import {
  computeBackoffDelayMs,
  consumeLocalReceiptNumber,
  enqueueOfflineOperation,
  flushOutbox,
  selectEligibleOutboxBatch,
} from './outboxManager';

describe('Phase 15 Offline Client Engine', () => {
  let db: ClinicOfflineDb;

  beforeEach(async () => {
    db = new ClinicOfflineDb(`ClinicOfflineDb_${crypto.randomUUID()}`);
  });

  it('encrypts and decrypts sensitive patient payloads with AES-GCM via WebCrypto', async () => {
    const key = await generateAesGcmKey();
    const exported = await exportKeyBase64(key);
    const reimported = await importKeyBase64(exported);

    const sensitive = {
      patientId: '019a0000-0000-7000-8000-000000000001',
      fullName: 'محمود إبراهيم',
      phone: '01099887766',
      allergies: ['Penicillin'],
    };

    const encrypted = await encryptSensitiveJson(sensitive, key);
    expect(encrypted.v).toBe(1);
    expect(encrypted.cipherText).not.toContain('01099887766');

    const decrypted = await decryptSensitiveJson<typeof sensitive>(encrypted, reimported);
    expect(decrypted).toEqual(sensitive);
  });

  it('preserves strict per-entity outbox ordering when head item is awaiting backoff', async () => {
    const clinicId = '019a0000-0000-7000-8000-000000000010';
    const entityA = '019a0000-0000-7000-8000-0000000000a1';
    const entityB = '019a0000-0000-7000-8000-0000000000b1';

    const seq1 = await enqueueOfflineOperation(
      {
        operationId: '019a0000-0000-7000-8000-000000000101',
        clinicId,
        deviceId: 'dev-1',
        actionType: 'StartVisit',
        entityType: 'Visit',
        entityId: entityA,
        endpoint: `/clinics/${clinicId}/clinical/visits`,
        method: 'POST',
        payloadJson: '{}',
        clientTimestampUtc: '2026-10-15T10:00:00Z',
        clientBaseCursor: 0,
      },
      db
    );
    await db.outbox.update(seq1, { nextRetryAtMs: 5000, status: 'retrying', attemptCount: 1 });

    await enqueueOfflineOperation(
      {
        operationId: '019a0000-0000-7000-8000-000000000102',
        clinicId,
        deviceId: 'dev-1',
        actionType: 'CompleteVisit',
        entityType: 'Visit',
        entityId: entityA,
        endpoint: `/clinics/${clinicId}/clinical/visits/${entityA}/complete`,
        method: 'POST',
        payloadJson: '{}',
        clientTimestampUtc: '2026-10-15T10:05:00Z',
        clientBaseCursor: 0,
      },
      db
    );

    await enqueueOfflineOperation(
      {
        operationId: '019a0000-0000-7000-8000-000000000103',
        clinicId,
        deviceId: 'dev-1',
        actionType: 'RegisterPatient',
        entityType: 'Patient',
        entityId: entityB,
        endpoint: `/clinics/${clinicId}/patients`,
        method: 'POST',
        payloadJson: '{}',
        clientTimestampUtc: '2026-10-15T10:06:00Z',
        clientBaseCursor: 0,
      },
      db
    );

    const eligibleAt3s = await selectEligibleOutboxBatch(clinicId, 3000, db);
    // CompleteVisit on entityA waits behind StartVisit on entityA; entityB proceeds independently!
    expect(eligibleAt3s.map((i) => i.actionType)).toEqual(['RegisterPatient']);

    const eligibleAt6s = await selectEligibleOutboxBatch(clinicId, 6000, db);
    expect(eligibleAt6s.map((i) => i.actionType)).toEqual(['StartVisit', 'CompleteVisit', 'RegisterPatient']);
  });

  it('computes exponential backoff and wipes local IndexedDB on DEVICE_REVOKED', async () => {
    expect(computeBackoffDelayMs(0)).toBe(500);
    expect(computeBackoffDelayMs(1)).toBe(1000);
    expect(computeBackoffDelayMs(2)).toBe(2000);
    expect(computeBackoffDelayMs(10)).toBe(30000);

    const clinicId = '019a0000-0000-7000-8000-000000000011';
    await enqueueOfflineOperation(
      {
        operationId: '019a0000-0000-7000-8000-000000000201',
        clinicId,
        deviceId: 'dev-rev',
        actionType: 'RegisterPatient',
        entityType: 'Patient',
        entityId: '019a0000-0000-7000-8000-000000000202',
        endpoint: `/clinics/${clinicId}/patients`,
        method: 'POST',
        payloadJson: '{}',
        clientTimestampUtc: '2026-10-15T10:00:00Z',
        clientBaseCursor: 0,
      },
      db
    );

    const postSpy = vi.spyOn(apiClient, 'post').mockRejectedValueOnce({
      title: 'Device revoked',
      status: 401,
      code: 'DEVICE_REVOKED',
    });

    await expect(flushOutbox(clinicId, 'dev-rev', Date.now(), db)).rejects.toMatchObject({
      code: 'DEVICE_REVOKED',
    });
    expect(await db.outbox.count()).toBe(0);
    postSpy.mockRestore();
  });

  it('consumes pre-allocated receipt numbers with low-water mark and falls back to provisional receipt', async () => {
    const clinicId = '019a0000-0000-7000-8000-000000000012';
    const deviceId = 'dev-block';
    await db.syncMeta.put({
      clinicId,
      cursor: 0,
      lastSyncAtUtc: null,
      deviceId,
      provisionalSeq: 0,
      lowWaterMark: 1,
      offlineSessionMaxHours: 24,
      lastOnlineAuthAtUtc: null,
    });
    await db.numberBlocks.put({
      id: 'blk-1',
      clinicId,
      deviceId,
      kind: 'Receipt',
      fromNumber: 1,
      toNumber: 2,
      nextNumber: 1,
      status: 'Active',
    });

    const first = await consumeLocalReceiptNumber(clinicId, deviceId, db);
    expect(first).toEqual({ receiptNumber: 'R-000001', isProvisional: false, needsRefill: true });

    const second = await consumeLocalReceiptNumber(clinicId, deviceId, db);
    expect(second).toEqual({ receiptNumber: 'R-000002', isProvisional: false, needsRefill: true });

    const provisional = await consumeLocalReceiptNumber(clinicId, deviceId, db);
    expect(provisional.isProvisional).toBe(true);
    expect(provisional.receiptNumber).toMatch(/^PROV-/);

    await wipeAllLocalData(db);
    expect(await db.numberBlocks.count()).toBe(0);
  });
});

