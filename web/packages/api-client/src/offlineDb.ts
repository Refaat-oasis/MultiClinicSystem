import Dexie, { type Table } from 'dexie';
import type { EncryptedPayload } from './cryptoStore';

export type OutboxItemStatus = 'pending' | 'retrying' | 'failed';

export interface OfflineOutboxRecord {
  seq?: number;
  operationId: string;
  clinicId: string;
  deviceId: string;
  actionType: string;
  entityType: string;
  entityId: string;
  endpoint: string;
  method: string;
  payloadJson: string;
  clientTimestampUtc: string;
  clientBaseCursor: number;
  status: OutboxItemStatus;
  attemptCount: number;
  nextRetryAtMs: number;
  lastErrorCode?: string;
  lastErrorMessage?: string;
  provisionalReceiptNumber?: string;
}

export interface OfflineCachedEntityRecord {
  key: string;
  clinicId: string;
  tableName: 'patients' | 'appointments' | 'priceList' | 'categories' | 'durations' | 'visits';
  updatedAtUtc: string;
  encrypted: boolean;
  encryptedPayload?: EncryptedPayload;
  plainPayloadJson?: string;
}

export interface OfflineNumberBlockRecord {
  id: string;
  clinicId: string;
  deviceId: string;
  kind: 'Receipt' | 'PatientFile';
  fromNumber: number;
  toNumber: number;
  nextNumber: number;
  status: 'Active' | 'Exhausted';
}

export interface OfflineSyncMetaRecord {
  clinicId: string;
  cursor: number;
  lastSyncAtUtc: string | null;
  deviceId: string;
  provisionalSeq: number;
  lowWaterMark: number;
  offlineSessionMaxHours: number;
  lastOnlineAuthAtUtc: string | null;
}

export class ClinicOfflineDb extends Dexie {
  public outbox!: Table<OfflineOutboxRecord, number>;
  public cachedRecords!: Table<OfflineCachedEntityRecord, string>;
  public numberBlocks!: Table<OfflineNumberBlockRecord, string>;
  public syncMeta!: Table<OfflineSyncMetaRecord, string>;

  public constructor(name = 'ClinicOfflineDb') {
    super(name);
    this.version(1).stores({
      outbox: '++seq, &operationId, clinicId, [clinicId+entityId], status, nextRetryAtMs',
      cachedRecords: '&key, clinicId, [clinicId+tableName]',
      numberBlocks: '&id, clinicId, [clinicId+deviceId+kind], status',
      syncMeta: '&clinicId',
    });
  }
}

export const offlineDb = new ClinicOfflineDb();

let inMemorySessionKey: CryptoKey | null = null;

export function setOfflineSessionCryptoKey(key: CryptoKey | null): void {
  inMemorySessionKey = key;
}

export function getOfflineSessionCryptoKey(): CryptoKey | null {
  return inMemorySessionKey;
}

export async function wipeAllLocalData(db: ClinicOfflineDb = offlineDb): Promise<void> {
  inMemorySessionKey = null;
  await db.transaction('rw', db.outbox, db.cachedRecords, db.numberBlocks, db.syncMeta, async () => {
    await db.outbox.clear();
    await db.cachedRecords.clear();
    await db.numberBlocks.clear();
    await db.syncMeta.clear();
  });
}

