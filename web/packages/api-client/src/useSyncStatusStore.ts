import { create } from 'zustand';

export interface SyncStatusState {
  isOnline: boolean;
  isSyncing: boolean;
  pendingCount: number;
  lastSyncAtUtc: string | null;
  lastCursor: number;
  openConflictsCount: number;
  provisionalReceiptsCount: number;
  deviceRevokedWiped: boolean;
  setOnline: (online: boolean) => void;
  setSyncing: (syncing: boolean) => void;
  setPendingCount: (count: number) => void;
  setSyncSnapshot: (patch: Partial<Omit<SyncStatusState, 'setOnline' | 'setSyncing' | 'setPendingCount' | 'setSyncSnapshot'>>) => void;
}

export const useSyncStatusStore = create<SyncStatusState>((set) => ({
  isOnline: typeof navigator === 'undefined' ? true : navigator.onLine,
  isSyncing: false,
  pendingCount: 0,
  lastSyncAtUtc: null,
  lastCursor: 0,
  openConflictsCount: 0,
  provisionalReceiptsCount: 0,
  deviceRevokedWiped: false,
  setOnline: (isOnline) => set({ isOnline }),
  setSyncing: (isSyncing) => set({ isSyncing }),
  setPendingCount: (pendingCount) => set({ pendingCount }),
  setSyncSnapshot: (patch) => set((state) => ({ ...state, ...patch })),
}));

