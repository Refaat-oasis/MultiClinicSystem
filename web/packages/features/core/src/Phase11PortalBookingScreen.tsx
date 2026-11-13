import React, { useMemo, useState } from 'react';
import { GlassCard, GlassButton, GlassInput, AlertBanner } from '@clinic/ui';
import {
  CalendarCheck,
  CalendarClock,
  CheckCircle2,
  XCircle,
  Clock,
  Settings,
  ShieldAlert,
  ShieldCheck,
  RefreshCw,
} from 'lucide-react';

export type PortalBookingMode = 'AutoConfirm' | 'RequiresApproval';

export interface ClinicPortalBookingSettingData {
  clinicId: string;
  mode: PortalBookingMode | string;
  minLeadTimeMinutes: number;
  maxDaysAhead: number;
  slotStepMinutes: number;
  maxActiveFutureBookingsPerPatient: number;
  maxBookingsPerDay: number;
  cancellationCutoffHours: number;
  autoBlockAfterNoShows: number;
  approvalHoldHours: number;
  showPricesWhenAvailable: boolean;
  updatedAtUtc: string;
}

export interface PortalBookingRequestItemData {
  appointmentId: string;
  clinicId: string;
  patientId: string;
  patientFileNumber: string;
  patientName: string;
  doctorId: string;
  doctorName: string;
  categoryId: string;
  categoryNameAr: string;
  categoryNameEn: string;
  startAtUtc: string;
  endAtUtc: string;
  status: string;
  approvalExpiresAtUtc?: string | null;
  isExpired: boolean;
  notes?: string | null;
  reviewNotes?: string | null;
  createdAtUtc: string;
}

export interface PatientBookingBlockTargetData {
  patientId: string;
  patientFileNumber: string;
  patientName: string;
  noShowCount: number;
  isOnlineBookingBlocked: boolean;
  onlineBookingBlockReason?: string | null;
}

export interface ClinicPortalBookingInspectData {
  clinicId: string;
  settings: ClinicPortalBookingSettingData;
  pendingRequestsCount: number;
  todayPortalBookingsCount: number;
  blockedPatientsCount: number;
  pendingRequests: PortalBookingRequestItemData[];
}

export interface UpdateClinicPortalBookingSettingPayload {
  clinicId: string;
  mode: PortalBookingMode | string;
  minLeadTimeMinutes: number;
  maxDaysAhead: number;
  slotStepMinutes: number;
  maxActiveFutureBookingsPerPatient: number;
  maxBookingsPerDay: number;
  cancellationCutoffHours: number;
  autoBlockAfterNoShows: number;
  approvalHoldHours: number;
  showPricesWhenAvailable: boolean;
}

export interface ReviewPortalBookingRequestPayload {
  clinicId: string;
  appointmentId: string;
  action: 'approve' | 'decline';
  reviewNotes: string | null;
}

export interface SetPatientOnlineBookingBlockPayload {
  clinicId: string;
  patientId: string;
  blocked: boolean;
  reason: string | null;
}

export interface Phase11PortalBookingScreenProps {
  clinicId: string;
  readOnlyInspectMode?: boolean;
  initialInspectSnapshot?: ClinicPortalBookingInspectData | null;
  initialSettings?: ClinicPortalBookingSettingData | null;
  initialRequests?: PortalBookingRequestItemData[];
  initialPatientBlockTarget?: PatientBookingBlockTargetData | null;
  onUpdateSettings?: (payload: UpdateClinicPortalBookingSettingPayload) => void;
  onReviewRequest?: (payload: ReviewPortalBookingRequestPayload) => void;
  onExpireStaleRequests?: (clinicId: string) => number | void | Promise<number | void>;
  onSetPatientBlock?: (payload: SetPatientOnlineBookingBlockPayload) => void;
}

const DEFAULT_BOOKING_SETTINGS = (clinicId: string): ClinicPortalBookingSettingData => ({
  clinicId,
  mode: 'AutoConfirm',
  minLeadTimeMinutes: 120,
  maxDaysAhead: 30,
  slotStepMinutes: 15,
  maxActiveFutureBookingsPerPatient: 2,
  maxBookingsPerDay: 20,
  cancellationCutoffHours: 12,
  autoBlockAfterNoShows: 3,
  approvalHoldHours: 12,
  showPricesWhenAvailable: true,
  updatedAtUtc: '2026-08-10T08:00:00Z',
});

const DEFAULT_REQUESTS = (clinicId: string): PortalBookingRequestItemData[] => [
  {
    appointmentId: 'req-demo-1',
    clinicId,
    patientId: 'pat-demo-1',
    patientFileNumber: 'SM-0012',
    patientName: 'سارة محمود',
    doctorId: 'doc-demo-1',
    doctorName: 'د. عمر سالم',
    categoryId: 'cat-demo-1',
    categoryNameAr: 'كشف أسنان',
    categoryNameEn: 'Dental Exam',
    startAtUtc: '2026-08-15T09:00:00Z',
    endAtUtc: '2026-08-15T09:30:00Z',
    status: 'Requested',
    approvalExpiresAtUtc: '2026-08-11T08:00:00Z',
    isExpired: false,
    notes: 'أفضل فترة الصباح',
    reviewNotes: null,
    createdAtUtc: '2026-08-10T08:30:00Z',
  },
];

const DEFAULT_BLOCK_TARGET: PatientBookingBlockTargetData = {
  patientId: 'pat-demo-2',
  patientFileNumber: 'SM-0024',
  patientName: 'كريم عادل',
  noShowCount: 3,
  isOnlineBookingBlocked: true,
  onlineBookingBlockReason: 'تغيّب متكرر (3 مرات عدم حضور)',
};

const REQUEST_STATUS_META: Record<string, { labelAr: string; color: string }> = {
  Requested: { labelAr: 'بانتظار الموافقة', color: '#f59e0b' },
  Booked: { labelAr: 'مؤكد', color: '#10b981' },
  Declined: { labelAr: 'مرفوض', color: '#ef4444' },
  Cancelled: { labelAr: 'ملغي', color: 'var(--color-text-muted)' },
};

export const Phase11PortalBookingScreen: React.FC<Phase11PortalBookingScreenProps> = ({
  clinicId,
  readOnlyInspectMode = false,
  initialInspectSnapshot = null,
  initialSettings = null,
  initialRequests,
  initialPatientBlockTarget,
  onUpdateSettings,
  onReviewRequest,
  onExpireStaleRequests,
  onSetPatientBlock,
}) => {
  const [settings, setSettings] = useState<ClinicPortalBookingSettingData>(
    initialInspectSnapshot?.settings ?? initialSettings ?? DEFAULT_BOOKING_SETTINGS(clinicId),
  );
  const [requests, setRequests] = useState<PortalBookingRequestItemData[]>(
    initialInspectSnapshot?.pendingRequests ?? initialRequests ?? DEFAULT_REQUESTS(clinicId),
  );
  const [reviewNotesByReq, setReviewNotesByReq] = useState<Record<string, string>>({});
  const [patientBlock, setPatientBlock] = useState<PatientBookingBlockTargetData>(
    initialPatientBlockTarget ?? DEFAULT_BLOCK_TARGET,
  );
  const [blockReasonInput, setBlockReasonInput] = useState(
    initialPatientBlockTarget?.onlineBookingBlockReason ?? DEFAULT_BLOCK_TARGET.onlineBookingBlockReason ?? '',
  );
  const [statusMessage, setStatusMessage] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const pendingCount = useMemo(
    () => requests.filter((r) => r.status === 'Requested').length,
    [requests],
  );

  const guardInspect = (): boolean => {
    if (!readOnlyInspectMode) return false;
    setErrorMessage('الكتابة معطلة في وضع الفحص للقراءة فقط (Inspect Mode).');
    return true;
  };

  const handleReview = (appointmentId: string, action: 'approve' | 'decline') => {
    if (guardInspect()) return;
    setErrorMessage(null);
    const notes = reviewNotesByReq[appointmentId]?.trim() || null;
    setRequests((prev) =>
      prev.map((item) =>
        item.appointmentId === appointmentId
          ? {
              ...item,
              status: action === 'approve' ? 'Booked' : 'Declined',
              reviewNotes: notes,
            }
          : item,
      ),
    );
    onReviewRequest?.({ clinicId, appointmentId, action, reviewNotes: notes });
    setStatusMessage(action === 'approve' ? 'تمت الموافقة وتأكيد حجز المريض.' : 'تم رفض طلب الحجز وتحديث الحالة.');
  };

  const handleExpireStale = async () => {
    if (guardInspect()) return;
    setErrorMessage(null);
    const expiredResult = await onExpireStaleRequests?.(clinicId);
    let localExpired = 0;
    setRequests((prev) =>
      prev.map((item) => {
        if (item.status === 'Requested' && item.isExpired) {
          localExpired += 1;
          return { ...item, status: 'Cancelled', reviewNotes: 'انتهت مهلة الانتظار' };
        }
        return item;
      }),
    );
    const count = typeof expiredResult === 'number' ? expiredResult : localExpired;
    setStatusMessage(`تم تنظيف الطلبات المنتهية الصلاحية (${count}).`);
  };

  const handleSaveSettings = (e: React.FormEvent) => {
    e.preventDefault();
    if (guardInspect()) return;
    setErrorMessage(null);
    onUpdateSettings?.({
      clinicId,
      mode: settings.mode,
      minLeadTimeMinutes: Number(settings.minLeadTimeMinutes),
      maxDaysAhead: Number(settings.maxDaysAhead),
      slotStepMinutes: Number(settings.slotStepMinutes),
      maxActiveFutureBookingsPerPatient: Number(settings.maxActiveFutureBookingsPerPatient),
      maxBookingsPerDay: Number(settings.maxBookingsPerDay),
      cancellationCutoffHours: Number(settings.cancellationCutoffHours),
      autoBlockAfterNoShows: Number(settings.autoBlockAfterNoShows),
      approvalHoldHours: Number(settings.approvalHoldHours),
      showPricesWhenAvailable: settings.showPricesWhenAvailable,
    });
    setStatusMessage('تم حفظ قواعد الحجز الأونلاين للعيادة بنجاح.');
  };

  const handleToggleBlock = () => {
    if (guardInspect()) return;
    setErrorMessage(null);
    const nextBlocked = !patientBlock.isOnlineBookingBlocked;
    const reason = nextBlocked ? (blockReasonInput.trim() || 'حظر إداري للحجز الأونلاين') : null;
    setPatientBlock((prev) => ({
      ...prev,
      isOnlineBookingBlocked: nextBlocked,
      onlineBookingBlockReason: reason,
    }));
    onSetPatientBlock?.({
      clinicId,
      patientId: patientBlock.patientId,
      blocked: nextBlocked,
      reason,
    });
    setStatusMessage(nextBlocked ? 'تم إيقاف الحجز الأونلاين للمريض.' : 'تم رفع الحظر وإعادة تفعيل الحجز الأونلاين للمريض.');
  };

  return (
    <div dir="rtl" style={{ display: 'flex', flexDirection: 'column', gap: '1.25rem' }}>
      {readOnlyInspectMode && (
        <AlertBanner
          variant="warning"
          title="وضع الفحص للقراءة فقط — حجوزات البوابة"
          message="أنت تستعرض إعدادات وطلبات الحجز الأونلاين في وضع الفحص فقط؛ التعديل والموافقة معطلان."
        />
      )}

      {errorMessage && <AlertBanner variant="error" message={errorMessage} />}
      {statusMessage && <AlertBanner variant="success" message={statusMessage} />}

      {initialInspectSnapshot && (
        <GlassCard style={{ padding: '1.25rem' }}>
          <div style={{ display: 'flex', gap: '1.5rem', flexWrap: 'wrap', fontSize: '0.875rem' }}>
            <div>طلبات بانتظار المراجعة: <strong>{initialInspectSnapshot.pendingRequestsCount}</strong></div>
            <div>حجوزات اليوم من البوابة: <strong>{initialInspectSnapshot.todayPortalBookingsCount}</strong></div>
            <div>المرضى المحظورون أونلاين: <strong>{initialInspectSnapshot.blockedPatientsCount}</strong></div>
          </div>
        </GlassCard>
      )}

      <GlassCard style={{ padding: '1.25rem' }}>
        <div
          style={{
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center',
            gap: '0.75rem',
            flexWrap: 'wrap',
            marginBottom: '1rem',
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
            <CalendarClock size={20} color="var(--color-accent-teal)" />
            <h2 style={{ fontSize: '1.125rem', fontWeight: 700 }}>
              طلبات الحجز الأونلاين ({pendingCount})
            </h2>
          </div>

          {!readOnlyInspectMode && (
            <GlassButton type="button" onClick={() => void handleExpireStale()}>
              <RefreshCw size={15} />
              <span>إنهاء الطلبات منتهية المهلة</span>
            </GlassButton>
          )}
        </div>

        {requests.length === 0 ? (
          <div style={{ fontSize: '0.875rem', color: 'var(--color-text-muted)' }}>
            لا توجد طلبات حجز مسجلة حالياً.
          </div>
        ) : (
          <div style={{ display: 'flex', flexDirection: 'column', gap: '0.875rem' }}>
            {requests.map((req) => {
              const badge = REQUEST_STATUS_META[req.status] ?? REQUEST_STATUS_META.Requested;
              return (
                <div
                  key={req.appointmentId}
                  data-testid={`portal-request-${req.appointmentId}`}
                  style={{
                    padding: '0.875rem',
                    borderRadius: '0.75rem',
                    border: '1px solid rgba(148, 163, 184, 0.25)',
                    display: 'flex',
                    flexDirection: 'column',
                    gap: '0.625rem',
                  }}
                >
                  <div style={{ display: 'flex', justifyContent: 'space-between', flexWrap: 'wrap', gap: '0.5rem' }}>
                    <div>
                      <strong>{req.patientName}</strong>{' '}
                      <span dir="ltr" style={{ fontSize: '0.8125rem', color: 'var(--color-text-muted)' }}>
                        ({req.patientFileNumber})
                      </span>{' '}
                      — {req.categoryNameAr} ({req.doctorName})
                    </div>
                    <span
                      data-testid={`portal-request-status-${req.appointmentId}`}
                      style={{
                        paddingInline: '0.625rem',
                        paddingBlock: '0.2rem',
                        borderRadius: '9999px',
                        fontSize: '0.75rem',
                        fontWeight: 700,
                        color: badge.color,
                        border: `1px solid ${badge.color}`,
                      }}
                    >
                      {badge.labelAr}
                    </span>
                  </div>

                  <div style={{ display: 'flex', gap: '1rem', flexWrap: 'wrap', fontSize: '0.8125rem', color: 'var(--color-text-secondary)' }}>
                    <span dir="ltr">{req.startAtUtc.slice(0, 16).replace('T', ' ')} UTC</span>
                    {req.isExpired && (
                      <span style={{ color: '#ef4444', fontWeight: 600 }}>
                        <Clock size={12} style={{ display: 'inline', marginInlineEnd: '0.25rem' }} />
                        مهلة الحجز منتهية
                      </span>
                    )}
                    {req.notes && <span>ملاحظة المريض: {req.notes}</span>}
                  </div>

                  {req.status === 'Requested' && !readOnlyInspectMode && (
                    <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap', alignItems: 'flex-end' }}>
                      <div style={{ flex: '1 1 14rem' }}>
                        <GlassInput
                          label={`ملاحظة المراجعة للطلب ${req.patientFileNumber}`}
                          value={reviewNotesByReq[req.appointmentId] ?? ''}
                          onChange={(e) =>
                            setReviewNotesByReq((prev) => ({
                              ...prev,
                              [req.appointmentId]: e.target.value,
                            }))
                          }
                          placeholder="سبب القبول أو الرفض (اختياري)"
                        />
                      </div>
                      <GlassButton
                        type="button"
                        variant="prominent"
                        onClick={() => handleReview(req.appointmentId, 'approve')}
                      >
                        <CheckCircle2 size={15} />
                        <span>موافقة وتأكيد</span>
                      </GlassButton>
                      <GlassButton
                        type="button"
                        onClick={() => handleReview(req.appointmentId, 'decline')}
                      >
                        <XCircle size={15} />
                        <span>رفض الطلب</span>
                      </GlassButton>
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        )}
      </GlassCard>

      <GlassCard style={{ padding: '1.25rem' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '1rem' }}>
          <Settings size={20} color="var(--color-accent-teal)" />
          <h3 style={{ fontSize: '1.0625rem', fontWeight: 700 }}>
            إعدادات وحدود الحجز الأونلاين
          </h3>
        </div>

        <form onSubmit={handleSaveSettings} style={{ display: 'flex', flexDirection: 'column', gap: '0.875rem' }}>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(13rem, 1fr))', gap: '0.75rem' }}>
            <label style={{ display: 'flex', flexDirection: 'column', gap: '0.25rem', fontSize: '0.875rem' }}>
              <span>وضع اعتماد الحجز</span>
              <select
                aria-label="وضع اعتماد الحجز"
                value={settings.mode}
                disabled={readOnlyInspectMode}
                onChange={(e) => setSettings((s) => ({ ...s, mode: e.target.value }))}
                style={{
                  padding: '0.55rem 0.75rem',
                  borderRadius: '0.625rem',
                  border: '1px solid rgba(148, 163, 184, 0.35)',
                  background: 'transparent',
                  color: 'inherit',
                }}
              >
                <option value="AutoConfirm">تأكيد فوري تلقائي (AutoConfirm)</option>
                <option value="RequiresApproval">بانتظار موافقة الاستقبال (RequiresApproval)</option>
              </select>
            </label>

            <GlassInput
              label="أقل مهلة قبل الموعد (دقيقة)"
              type="number"
              value={String(settings.minLeadTimeMinutes)}
              disabled={readOnlyInspectMode}
              onChange={(e) => setSettings((s) => ({ ...s, minLeadTimeMinutes: Number(e.target.value) }))}
            />
            <GlassInput
              label="أقصى أفق للحجز (أيام)"
              type="number"
              value={String(settings.maxDaysAhead)}
              disabled={readOnlyInspectMode}
              onChange={(e) => setSettings((s) => ({ ...s, maxDaysAhead: Number(e.target.value) }))}
            />
            <GlassInput
              label="فاصل المواعيد المتاحة (دقيقة)"
              type="number"
              value={String(settings.slotStepMinutes)}
              disabled={readOnlyInspectMode}
              onChange={(e) => setSettings((s) => ({ ...s, slotStepMinutes: Number(e.target.value) }))}
            />
            <GlassInput
              label="حد الحجوزات النشطة لكل مريض"
              type="number"
              value={String(settings.maxActiveFutureBookingsPerPatient)}
              disabled={readOnlyInspectMode}
              onChange={(e) =>
                setSettings((s) => ({ ...s, maxActiveFutureBookingsPerPatient: Number(e.target.value) }))
              }
            />
            <GlassInput
              label="أقصى حجوزات بوابة باليوم"
              type="number"
              value={String(settings.maxBookingsPerDay)}
              disabled={readOnlyInspectMode}
              onChange={(e) => setSettings((s) => ({ ...s, maxBookingsPerDay: Number(e.target.value) }))}
            />
            <GlassInput
              label="مهلة الإلغاء المسموحة (ساعة)"
              type="number"
              value={String(settings.cancellationCutoffHours)}
              disabled={readOnlyInspectMode}
              onChange={(e) => setSettings((s) => ({ ...s, cancellationCutoffHours: Number(e.target.value) }))}
            />
            <GlassInput
              label="الحظر التلقائي بعد مرات الغياب"
              type="number"
              value={String(settings.autoBlockAfterNoShows)}
              disabled={readOnlyInspectMode}
              onChange={(e) => setSettings((s) => ({ ...s, autoBlockAfterNoShows: Number(e.target.value) }))}
            />
            <GlassInput
              label="مدة حجز طلب الموافقة (ساعة)"
              type="number"
              value={String(settings.approvalHoldHours)}
              disabled={readOnlyInspectMode}
              onChange={(e) => setSettings((s) => ({ ...s, approvalHoldHours: Number(e.target.value) }))}
            />
          </div>

          <label style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', fontSize: '0.875rem' }}>
            <input
              type="checkbox"
              checked={settings.showPricesWhenAvailable}
              disabled={readOnlyInspectMode}
              onChange={(e) => setSettings((s) => ({ ...s, showPricesWhenAvailable: e.target.checked }))}
            />
            <span>عرض أسعار الكشوفات المتاحة عند الحجز الأونلاين</span>
          </label>

          {!readOnlyInspectMode && (
            <div>
              <GlassButton type="submit" variant="prominent">
                <CalendarCheck size={16} />
                <span>حفظ قواعد الحجز الأونلاين</span>
              </GlassButton>
            </div>
          )}
        </form>
      </GlassCard>

      <GlassCard style={{ padding: '1.25rem' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.875rem' }}>
          {patientBlock.isOnlineBookingBlocked ? (
            <ShieldAlert size={20} color="#ef4444" />
          ) : (
            <ShieldCheck size={20} color="#10b981" />
          )}
          <h3 style={{ fontSize: '1.0625rem', fontWeight: 700 }}>
            التحكم في حظر الحجز الأونلاين للمريض
          </h3>
        </div>

        <div style={{ display: 'flex', justifyContent: 'space-between', flexWrap: 'wrap', gap: '0.75rem', marginBottom: '0.75rem' }}>
          <div>
            <strong>{patientBlock.patientName}</strong>{' '}
            <span dir="ltr" style={{ color: 'var(--color-text-muted)' }}>({patientBlock.patientFileNumber})</span>
          </div>
          <div style={{ display: 'flex', gap: '0.75rem', alignItems: 'center' }}>
            <span data-testid="patient-noshow-count" style={{ fontSize: '0.875rem' }}>
              مرات التغيّب (No-Show): <strong>{patientBlock.noShowCount}</strong>
            </span>
            <span
              data-testid="patient-booking-block-badge"
              style={{
                paddingInline: '0.625rem',
                paddingBlock: '0.2rem',
                borderRadius: '9999px',
                fontSize: '0.75rem',
                fontWeight: 700,
                color: patientBlock.isOnlineBookingBlocked ? '#ef4444' : '#10b981',
                border: `1px solid ${patientBlock.isOnlineBookingBlocked ? '#ef4444' : '#10b981'}`,
              }}
            >
              {patientBlock.isOnlineBookingBlocked ? 'محظور من الحجز أونلاين' : 'مسموح له بالحجز أونلاين'}
            </span>
          </div>
        </div>

        <div style={{ display: 'flex', gap: '0.75rem', flexWrap: 'wrap', alignItems: 'flex-end' }}>
          <div style={{ flex: '1 1 16rem' }}>
            <GlassInput
              label="سبب الحظر الأونلاين"
              value={blockReasonInput}
              disabled={readOnlyInspectMode}
              onChange={(e) => setBlockReasonInput(e.target.value)}
            />
          </div>
          {!readOnlyInspectMode && (
            <GlassButton type="button" onClick={handleToggleBlock}>
              {patientBlock.isOnlineBookingBlocked ? 'رفع الحظر وإعادة التفعيل' : 'حظر المريض من الحجز أونلاين'}
            </GlassButton>
          )}
        </div>
      </GlassCard>
    </div>
  );
};
