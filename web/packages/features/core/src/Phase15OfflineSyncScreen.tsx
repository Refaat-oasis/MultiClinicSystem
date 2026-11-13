import React, { useMemo, useState } from 'react';
import {
  Wifi,
  WifiOff,
  RefreshCw,
  AlertTriangle,
  CheckCircle2,
  Hash,
  Smartphone,
  Settings,
  Clock,
  ShieldAlert,
} from 'lucide-react';
import { GlassCard, GlassButton, AlertBanner } from '@clinic/ui';
import {
  useOfflineMutation,
  useSyncStatusStore,
  flushOutbox,
  prefetchReceiptBlock,
} from '@clinic/api-client';

export interface SyncConflictItemData {
  id: string;
  clinicId: string;
  operationId: string;
  deviceId: string;
  entityType: string;
  entityId: string;
  conflictClass: string;
  conflictCode: string;
  reason: string;
  clientPayloadJson: string;
  serverSnapshotJson: string;
  status: string;
  resolvedByUserId?: string | null;
  resolvedAtUtc?: string | null;
  resolutionNote?: string | null;
  createdAtUtc: string;
}

export interface SyncDeviceItemData {
  id: string;
  clinicId: string;
  deviceId: string;
  userId: string;
  lastSyncCursor: number;
  lastPushAtUtc?: string | null;
  lastPullAtUtc?: string | null;
  lastOnlineAuthAtUtc: string;
  maxClockSkewSecondsObserved: number;
  pendingOperationsReported: number;
  isApproved: boolean;
  isRevoked: boolean;
}

export interface SyncNumberBlockData {
  id: string;
  clinicId: string;
  deviceId: string;
  kind: string;
  fromNumber: number;
  toNumber: number;
  nextNumber: number;
  remainingCount: number;
  status: string;
  issuedAtUtc: string;
}

export interface ClinicSyncSettingData {
  clinicId: string;
  offlineSessionMaxHours: number;
  receiptBlockSize: number;
  receiptBlockLowWaterMark: number;
  clockSkewWarningSeconds: number;
  updatedAtUtc: string;
}

export interface Phase15OfflineSyncScreenProps {
  clinicId: string;
  deviceId?: string;
  enabledModules: string[];
  readOnlyInspectMode?: boolean;
  inspectData?: {
    settings?: ClinicSyncSettingData;
    devices?: SyncDeviceItemData[];
    conflicts?: SyncConflictItemData[];
    numberBlocks?: SyncNumberBlockData[];
  };
}

type SyncSubTab = 'conflicts' | 'outbox' | 'settings';

export const Phase15OfflineSyncScreen: React.FC<Phase15OfflineSyncScreenProps> = ({
  clinicId,
  deviceId = 'web-browser',
  enabledModules,
  readOnlyInspectMode = false,
  inspectData,
}) => {
  const isModuleEnabled = enabledModules.includes('offline-sync');
  const syncStore = useSyncStatusStore();

  const [activeSubTab, setActiveSubTab] = useState<SyncSubTab>('conflicts');
  const [feedback, setFeedback] = useState<{ variant: 'success' | 'error' | 'info'; message: string } | null>(null);
  const [resolutionNotes, setResolutionNotes] = useState<Record<string, string>>({});

  const [settings, setSettings] = useState<ClinicSyncSettingData>(
    inspectData?.settings ?? {
      clinicId,
      offlineSessionMaxHours: 24,
      receiptBlockSize: 50,
      receiptBlockLowWaterMark: 10,
      clockSkewWarningSeconds: 300,
      updatedAtUtc: new Date().toISOString(),
    }
  );

  const [conflicts, setConflicts] = useState<SyncConflictItemData[]>(
    inspectData?.conflicts ?? [
      {
        id: '019a0000-0000-7000-8000-000000001501',
        clinicId,
        operationId: '019a0000-0000-7000-8000-000000001502',
        deviceId: 'reception-tablet-2',
        entityType: 'Appointment',
        entityId: '019a0000-0000-7000-8000-000000001503',
        conflictClass: 'ServerValidated',
        conflictCode: 'SLOT_TAKEN',
        reason: 'الموعد محجوز مسبقاً عبر جهاز آخر أو بوابة المريض',
        clientPayloadJson: '{"startUtc":"2026-10-16T10:00:00Z"}',
        serverSnapshotJson: '{"status":"Booked","source":"Portal"}',
        status: 'Open',
        createdAtUtc: '2026-10-15T10:15:00Z',
      },
    ]
  );

  const [devices] = useState<SyncDeviceItemData[]>(
    inspectData?.devices ?? [
      {
        id: '019a0000-0000-7000-8000-000000001601',
        clinicId,
        deviceId,
        userId: 'current-user',
        lastSyncCursor: syncStore.lastCursor,
        lastPushAtUtc: syncStore.lastSyncAtUtc,
        lastPullAtUtc: syncStore.lastSyncAtUtc,
        lastOnlineAuthAtUtc: new Date().toISOString(),
        maxClockSkewSecondsObserved: 12,
        pendingOperationsReported: syncStore.pendingCount,
        isApproved: true,
        isRevoked: false,
      },
    ]
  );

  const [blocks, setBlocks] = useState<SyncNumberBlockData[]>(
    inspectData?.numberBlocks ?? [
      {
        id: '019a0000-0000-7000-8000-000000001701',
        clinicId,
        deviceId,
        kind: 'Receipt',
        fromNumber: 1,
        toNumber: 50,
        nextNumber: 4,
        remainingCount: 47,
        status: 'Active',
        issuedAtUtc: '2026-10-15T09:00:00Z',
      },
    ]
  );

  const openConflictsCount = useMemo(
    () => conflicts.filter((c) => c.status === 'Open').length,
    [conflicts]
  );

  const resolveConflictMutation = useOfflineMutation<SyncConflictItemData, { conflictId: string; resolution: string; resolutionNote: string }>({
    endpoint: `/clinics/${clinicId}/sync/conflicts`,
    method: 'POST',
    onlineOnly: true,
    onSuccess: (_res, vars) => {
      setConflicts((prev) =>
        prev.map((c) =>
          c.id === vars.conflictId
            ? {
                ...c,
                status: vars.resolution,
                resolutionNote: vars.resolutionNote,
                resolvedAtUtc: new Date().toISOString(),
              }
            : c
        )
      );
      setFeedback({ variant: 'success', message: 'تم حل التعارض وتسجيله في سجل المراجعة.' });
    },
    onError: (err) => {
      setFeedback({ variant: 'error', message: err.detail || 'تعذر حل التعارض (يتطلب اتصالاً بالإنترنت).' });
    },
  });

  const updateSettingsMutation = useOfflineMutation<ClinicSyncSettingData, ClinicSyncSettingData>({
    endpoint: `/clinics/${clinicId}/sync/settings`,
    method: 'PUT',
    onlineOnly: true,
    onSuccess: (updated) => {
      setSettings(updated);
      setFeedback({ variant: 'success', message: 'تم حفظ إعدادات المزامنة دون اتصال.' });
    },
    onError: (err) => {
      setFeedback({ variant: 'error', message: err.detail || 'يتطلب تعديل إعدادات المزامنة اتصالاً بالإنترنت.' });
    },
  });

  if (!isModuleEnabled) {
    return (
      <GlassCard>
        <div className="p-6 text-center space-y-2" dir="rtl">
          <WifiOff className="w-8 h-8 mx-auto text-slate-400" />
          <h3 className="text-lg font-semibold text-slate-800 dark:text-slate-100">
            وحدة المزامنة دون اتصال غير مفعلة (offline-sync)
          </h3>
          <p className="text-sm text-slate-500 dark:text-slate-400">
            يمكن لمشرف المنصة تفعيل وحدة المزامنة دون اتصال لهذه العيادة من لوحة إدارة المنصة.
          </p>
        </div>
      </GlassCard>
    );
  }

  const handleResolveLocalFallback = (conflictId: string, resolution: 'ResolvedKeepServer' | 'Dismissed') => {
    if (readOnlyInspectMode) return;
    const note = resolutionNotes[conflictId] ?? '';
    setConflicts((prev) =>
      prev.map((c) =>
        c.id === conflictId
          ? { ...c, status: resolution, resolutionNote: note, resolvedAtUtc: new Date().toISOString() }
          : c
      )
    );
    setFeedback({ variant: 'success', message: `تم تحديث حالة التعارض إلى ${resolution}.` });
    if (typeof navigator === 'undefined' || navigator.onLine) {
      resolveConflictMutation.mutate({ conflictId, resolution, resolutionNote: note });
    }
  };

  const handleFlushNow = async () => {
    if (readOnlyInspectMode) return;
    try {
      const res = await flushOutbox(clinicId, deviceId);
      setFeedback({
        variant: 'info',
        message: res
          ? `اكتملت المزامنة: ${res.appliedCount} مطبقة، ${res.conflictCount} تعارضات.`
          : 'لا توجد عمليات معلقة أو الجهاز غير متصل.',
      });
    } catch {
      setFeedback({ variant: 'error', message: 'تعذر رفع العمليات الآن؛ ستتم إعادة المحاولة تلقائياً.' });
    }
  };

  const handleAllocateBlock = async () => {
    if (readOnlyInspectMode) return;
    try {
      const allocated = await prefetchReceiptBlock(clinicId, deviceId, settings.receiptBlockSize);
      setBlocks((prev) => [
        {
          id: allocated.id,
          clinicId: allocated.clinicId,
          deviceId: allocated.deviceId,
          kind: allocated.kind,
          fromNumber: allocated.fromNumber,
          toNumber: allocated.toNumber,
          nextNumber: allocated.nextNumber,
          remainingCount: allocated.remainingCount,
          status: allocated.status,
          issuedAtUtc: new Date().toISOString(),
        },
        ...prev,
      ]);
      setFeedback({
        variant: 'success',
        message: `تم تخصيص كتلة إيصالات ${allocated.fromNumber}..${allocated.toNumber}.`,
      });
    } catch {
      const maxTo = blocks.reduce((m, b) => Math.max(m, b.toNumber), 0);
      setBlocks((prev) => [
        {
          id: `local-${Date.now()}`,
          clinicId,
          deviceId,
          kind: 'Receipt',
          fromNumber: maxTo + 1,
          toNumber: maxTo + settings.receiptBlockSize,
          nextNumber: maxTo + 1,
          remainingCount: settings.receiptBlockSize,
          status: 'Active',
          issuedAtUtc: new Date().toISOString(),
        },
        ...prev,
      ]);
      setFeedback({ variant: 'info', message: 'تم حجز كتلة محلية تجريبية للعرض.' });
    }
  };

  return (
    <div className="space-y-6" dir="rtl">
      {readOnlyInspectMode && (
        <AlertBanner
          variant="info"
          message="وضع الفحص للقراءة فقط (مشرف المنصة): لا يمكن تعديل بيانات المزامنة أو حل التعارضات من هنا."
        />
      )}

      {syncStore.deviceRevokedWiped && (
        <AlertBanner
          variant="error"
          message="تم سحب صلاحية هذا الجهاز؛ تم مسح جميع البيانات المحلية المشفرة فوراً."
        />
      )}

      {feedback && <AlertBanner variant={feedback.variant} message={feedback.message} />}

      {/* 4 Dashboard Widget Cards */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <GlassCard>
          <div className="p-4 flex items-center justify-between">
            <div>
              <p className="text-xs text-slate-500 dark:text-slate-400">الأجهزة المسجلة</p>
              <p className="text-2xl font-bold text-slate-900 dark:text-white">{devices.length}</p>
            </div>
            <Smartphone className="w-6 h-6 text-indigo-500" />
          </div>
        </GlassCard>

        <GlassCard>
          <div className="p-4 flex items-center justify-between">
            <div>
              <p className="text-xs text-slate-500 dark:text-slate-400">حالة الاتصال</p>
              <p className="text-lg font-bold text-slate-900 dark:text-white">
                {syncStore.isOnline ? 'متصل بالإنترنت' : 'وضع عدم الاتصال'}
              </p>
            </div>
            {syncStore.isOnline ? (
              <Wifi className="w-6 h-6 text-emerald-500" />
            ) : (
              <WifiOff className="w-6 h-6 text-amber-500" />
            )}
          </div>
        </GlassCard>

        <GlassCard>
          <div className="p-4 flex items-center justify-between">
            <div>
              <p className="text-xs text-slate-500 dark:text-slate-400">العمليات قيد الرفع</p>
              <p className="text-2xl font-bold text-slate-900 dark:text-white">{syncStore.pendingCount}</p>
            </div>
            <Clock className="w-6 h-6 text-sky-500" />
          </div>
        </GlassCard>

        <GlassCard>
          <div className="p-4 flex items-center justify-between">
            <div>
              <p className="text-xs text-slate-500 dark:text-slate-400">تعارضات مفتوحة</p>
              <p className="text-2xl font-bold text-amber-600 dark:text-amber-400">{openConflictsCount}</p>
            </div>
            <AlertTriangle className="w-6 h-6 text-amber-500" />
          </div>
        </GlassCard>
      </div>

      {/* Sub-navigation Tabs */}
      <div className="flex flex-wrap gap-2">
        <GlassButton
          variant={activeSubTab === 'conflicts' ? 'prominent' : 'default'}
          onClick={() => setActiveSubTab('conflicts')}
        >
          صندوق التعارضات ({openConflictsCount})
        </GlassButton>
        <GlassButton
          variant={activeSubTab === 'outbox' ? 'prominent' : 'default'}
          onClick={() => setActiveSubTab('outbox')}
        >
          قائمة الانتظار وكتل الإيصالات
        </GlassButton>
        <GlassButton
          variant={activeSubTab === 'settings' ? 'prominent' : 'default'}
          onClick={() => setActiveSubTab('settings')}
        >
          الأجهزة والإعدادات
        </GlassButton>
      </div>

      {activeSubTab === 'conflicts' && (
        <GlassCard>
          <div className="p-5 space-y-4">
            <div className="flex items-center justify-between">
              <h3 className="text-base font-semibold text-slate-900 dark:text-white flex items-center gap-2">
                <ShieldAlert className="w-5 h-5 text-amber-500" />
                صندوق تعارضات المزامنة (Conflict Inbox)
              </h3>
            </div>

            {conflicts.length === 0 ? (
              <p className="text-sm text-slate-500 dark:text-slate-400">لا توجد تعارضات مسجلة.</p>
            ) : (
              <div className="space-y-3">
                {conflicts.map((conflict) => (
                  <div
                    key={conflict.id}
                    className="p-4 rounded-xl border border-slate-200 dark:border-slate-700 bg-white/60 dark:bg-slate-900/50 space-y-2"
                  >
                    <div className="flex flex-wrap items-center justify-between gap-2">
                      <div className="flex items-center gap-2">
                        <span className="font-semibold text-slate-900 dark:text-white">
                          {conflict.entityType}
                        </span>
                        <span className="text-xs px-2 py-0.5 rounded-full bg-amber-100 text-amber-800 dark:bg-amber-900/40 dark:text-amber-300">
                          {conflict.conflictClass} · {conflict.conflictCode}
                        </span>
                      </div>
                      <span
                        className={`text-xs font-medium px-2 py-0.5 rounded-full ${
                          conflict.status === 'Open'
                            ? 'bg-rose-100 text-rose-700 dark:bg-rose-900/40 dark:text-rose-300'
                            : 'bg-emerald-100 text-emerald-700 dark:bg-emerald-900/40 dark:text-emerald-300'
                        }`}
                      >
                        {conflict.status}
                      </span>
                    </div>

                    <p className="text-sm text-slate-700 dark:text-slate-300">{conflict.reason}</p>
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-2 text-xs font-mono" dir="ltr">
                      <div className="p-2 rounded bg-slate-100 dark:bg-slate-800 overflow-x-auto">
                        Client ({conflict.deviceId}): {conflict.clientPayloadJson}
                      </div>
                      <div className="p-2 rounded bg-slate-100 dark:bg-slate-800 overflow-x-auto">
                        Server: {conflict.serverSnapshotJson}
                      </div>
                    </div>

                    {conflict.status === 'Open' && !readOnlyInspectMode && (
                      <div className="flex flex-wrap items-center gap-2 pt-2">
                        <input
                          type="text"
                          aria-label="ملاحظة حل التعارض"
                          placeholder="ملاحظة التسوية..."
                          value={resolutionNotes[conflict.id] ?? ''}
                          onChange={(e) =>
                            setResolutionNotes((prev) => ({ ...prev, [conflict.id]: e.target.value }))
                          }
                          className="flex-1 min-w-[200px] px-3 py-1.5 text-sm rounded-lg border border-slate-300 dark:border-slate-700 bg-white dark:bg-slate-800"
                        />
                        <GlassButton
                          onClick={() => handleResolveLocalFallback(conflict.id, 'ResolvedKeepServer')}
                        >
                          <CheckCircle2 className="w-4 h-4 me-1" />
                          اعتماد نسخة الخادم
                        </GlassButton>
                        <GlassButton
                          variant="default"
                          onClick={() => handleResolveLocalFallback(conflict.id, 'Dismissed')}
                        >
                          تجاهل
                        </GlassButton>
                      </div>
                    )}
                  </div>
                ))}
              </div>
            )}
          </div>
        </GlassCard>
      )}

      {activeSubTab === 'outbox' && (
        <GlassCard>
          <div className="p-5 space-y-4">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <h3 className="text-base font-semibold text-slate-900 dark:text-white flex items-center gap-2">
                <Hash className="w-5 h-5 text-sky-500" />
                كتل أرقام الإيصالات المسبقة (Offline Number Blocks)
              </h3>
              {!readOnlyInspectMode && (
                <div className="flex gap-2">
                  <GlassButton variant="default" onClick={() => void handleFlushNow()}>
                    <RefreshCw className="w-4 h-4 me-1" />
                    مزامنة قائمة الانتظار الآن
                  </GlassButton>
                  <GlassButton onClick={() => void handleAllocateBlock()}>
                    حجز كتلة إيصالات جديدة
                  </GlassButton>
                </div>
              )}
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
              {blocks.map((b) => (
                <div
                  key={b.id}
                  className="p-4 rounded-xl border border-slate-200 dark:border-slate-700 bg-white/60 dark:bg-slate-900/50 space-y-1"
                >
                  <div className="flex items-center justify-between">
                    <span className="font-semibold text-slate-900 dark:text-white">
                      {b.kind} · {b.deviceId}
                    </span>
                    <span className="text-xs px-2 py-0.5 rounded-full bg-indigo-100 text-indigo-800 dark:bg-indigo-900/40 dark:text-indigo-300">
                      {b.status}
                    </span>
                  </div>
                  <p className="text-sm text-slate-600 dark:text-slate-300" dir="ltr">
                    Range: {b.fromNumber} – {b.toNumber} (Next: {b.nextNumber}, Remaining: {b.remainingCount})
                  </p>
                </div>
              ))}
            </div>
          </div>
        </GlassCard>
      )}

      {activeSubTab === 'settings' && (
        <div className="space-y-4">
          <GlassCard>
            <div className="p-5 space-y-4">
              <h3 className="text-base font-semibold text-slate-900 dark:text-white flex items-center gap-2">
                <Settings className="w-5 h-5 text-indigo-500" />
                إعدادات المزامنة دون اتصال
              </h3>

              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <label className="block text-sm space-y-1">
                  <span className="text-slate-700 dark:text-slate-300">مدة صلاحية الجلسة دون إنترنت (ساعة)</span>
                  <input
                    type="number"
                    disabled={readOnlyInspectMode}
                    value={settings.offlineSessionMaxHours}
                    onChange={(e) =>
                      setSettings((s) => ({ ...s, offlineSessionMaxHours: Number(e.target.value) || 24 }))
                    }
                    className="w-full px-3 py-2 rounded-lg border border-slate-300 dark:border-slate-700 bg-white dark:bg-slate-800"
                  />
                </label>

                <label className="block text-sm space-y-1">
                  <span className="text-slate-700 dark:text-slate-300">حجم كتلة الإيصالات للجهاز</span>
                  <input
                    type="number"
                    disabled={readOnlyInspectMode}
                    value={settings.receiptBlockSize}
                    onChange={(e) =>
                      setSettings((s) => ({ ...s, receiptBlockSize: Number(e.target.value) || 50 }))
                    }
                    className="w-full px-3 py-2 rounded-lg border border-slate-300 dark:border-slate-700 bg-white dark:bg-slate-800"
                  />
                </label>

                <label className="block text-sm space-y-1">
                  <span className="text-slate-700 dark:text-slate-300">حد إعادة التعبئة التلقائية</span>
                  <input
                    type="number"
                    disabled={readOnlyInspectMode}
                    value={settings.receiptBlockLowWaterMark}
                    onChange={(e) =>
                      setSettings((s) => ({ ...s, receiptBlockLowWaterMark: Number(e.target.value) || 10 }))
                    }
                    className="w-full px-3 py-2 rounded-lg border border-slate-300 dark:border-slate-700 bg-white dark:bg-slate-800"
                  />
                </label>

                <label className="block text-sm space-y-1">
                  <span className="text-slate-700 dark:text-slate-300">حد تنبيه انحراف الساعة (ثانية)</span>
                  <input
                    type="number"
                    disabled={readOnlyInspectMode}
                    value={settings.clockSkewWarningSeconds}
                    onChange={(e) =>
                      setSettings((s) => ({ ...s, clockSkewWarningSeconds: Number(e.target.value) || 300 }))
                    }
                    className="w-full px-3 py-2 rounded-lg border border-slate-300 dark:border-slate-700 bg-white dark:bg-slate-800"
                  />
                </label>
              </div>

              {!readOnlyInspectMode && (
                <GlassButton
                  onClick={() => updateSettingsMutation.mutate(settings)}
                  disabled={updateSettingsMutation.isPending}
                >
                  حفظ إعدادات المزامنة
                </GlassButton>
              )}
            </div>
          </GlassCard>

          <GlassCard>
            <div className="p-5 space-y-3">
              <h3 className="text-base font-semibold text-slate-900 dark:text-white">
                الأجهزة المتزامنة ({devices.length})
              </h3>
              <div className="space-y-2">
                {devices.map((d) => (
                  <div
                    key={d.id}
                    className="p-3 rounded-lg border border-slate-200 dark:border-slate-700 flex flex-wrap items-center justify-between gap-2 text-sm"
                  >
                    <span className="font-medium text-slate-900 dark:text-white">{d.deviceId}</span>
                    <span className="text-slate-600 dark:text-slate-400" dir="ltr">
                      Cursor: {d.lastSyncCursor} · Max Skew: {d.maxClockSkewSecondsObserved}s ·{' '}
                      {d.isRevoked ? 'Revoked' : 'Active'}
                    </span>
                  </div>
                ))}
              </div>
            </div>
          </GlassCard>
        </div>
      )}
    </div>
  );
};

