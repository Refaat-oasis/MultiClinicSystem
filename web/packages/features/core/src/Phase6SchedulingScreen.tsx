import React, { useState } from 'react';
import { GlassCard, GlassButton, GlassInput, AlertBanner } from '@clinic/ui';
import {
  Calendar,
  Clock,
  Users,
  Sparkles,
  CheckCircle2,
  BellRing,
  Lock,
} from 'lucide-react';

export interface SchedulingCategoryItem {
  id: string;
  specialtyCode: string;
  nameAr: string;
  nameEn: string;
  defaultDurationMinutes: number;
  defaultPriceAmount: number;
  defaultPriceCurrency: string;
}

export interface FreeSlotItem {
  doctorId: string;
  doctorName: string;
  roomId?: string | null;
  startUtc: string;
  endUtc: string;
}

export interface AppointmentItem {
  id: string;
  clinicId: string;
  patientId: string;
  patientName: string;
  doctorId: string;
  doctorName: string;
  roomId?: string | null;
  categoryId: string;
  categoryNameAr: string;
  startUtc: string;
  endUtc: string;
  status: string;
  priority: string;
  source: string;
  notes?: string | null;
  allowedNextStatuses: string[];
}

export interface QueueEntryItem {
  id: string;
  appointmentId?: string | null;
  patientId: string;
  patientName: string;
  doctorId: string;
  doctorName: string;
  queueDate: string;
  orderIndex: number;
  status: string;
  priority: string;
  pushCount: number;
  vipJumpCount: number;
}

export interface WaitingListItem {
  id: string;
  patientId: string;
  patientName: string;
  doctorId?: string | null;
  categoryId: string;
  preferredDate: string;
  status: string;
}

export interface SlotOfferItem {
  id: string;
  waitingListEntryId: string;
  patientId: string;
  doctorId: string;
  categoryId: string;
  slotStartUtc: string;
  slotEndUtc: string;
  status: string;
}

export interface DoctorOptionItem {
  id: string;
  fullName: string;
}

export interface PatientOptionItem {
  id: string;
  fullName: string;
  fileNumber: string;
}

export interface Phase6SchedulingScreenProps {
  categories?: SchedulingCategoryItem[];
  doctors?: DoctorOptionItem[];
  patients?: PatientOptionItem[];
  freeSlots?: FreeSlotItem[];
  appointments?: AppointmentItem[];
  queueEntries?: QueueEntryItem[];
  waitingList?: WaitingListItem[];
  slotOffers?: SlotOfferItem[];
  isQueueEnabled?: boolean;
  readOnlyInspectMode?: boolean;
  onBookAppointment?: (payload: {
    patientId: string;
    doctorId: string;
    categoryId: string;
    startUtc: string;
    priority: 'Normal' | 'Vip' | 'Rush';
    notes?: string;
  }) => void;
  onTransitionStatus?: (appointmentId: string, nextStatus: string) => void;
  onReschedule?: (appointmentId: string, presetKey: 'after_week' | 'after_month' | 'custom', customStartUtc?: string) => void;
  onPushLate?: (queueEntryId: string) => void;
  onCallNext?: (queueEntryId: string) => void;
  onAddToWaitingList?: (payload: { patientId: string; doctorId?: string; categoryId: string; preferredDate: string }) => void;
  onAcceptSlotOffer?: (slotOfferId: string) => void;
  onCloseDay?: (startDate: string, reasonAr: string) => void;
}

const STATUS_LABELS_AR: Record<string, string> = {
  Booked: 'محجوز',
  Confirmed: 'تأكيد الحضور',
  Arrived: 'وصول للعيادة',
  InSession: 'داخل الكشف',
  Completed: 'إنهاء الكشف',
  NoShow: 'لم يحضر',
  Cancelled: 'إلغاء الموعد',
  Rescheduled: 'تمت إعادة الجدولة',
};

const PRIORITY_BADGES: Record<string, { label: string; bg: string; color: string }> = {
  Vip: { label: 'VIP أولوية', bg: 'rgba(245, 158, 11, 0.16)', color: '#d97706' },
  Rush: { label: 'طارئ Rush', bg: 'rgba(239, 68, 68, 0.16)', color: '#dc2626' },
  Normal: { label: 'عادي', bg: 'rgba(13, 148, 136, 0.14)', color: 'var(--color-accent-teal)' },
};

export const Phase6SchedulingScreen: React.FC<Phase6SchedulingScreenProps> = ({
  categories = [],
  doctors = [],
  patients = [],
  freeSlots = [],
  appointments = [],
  queueEntries = [],
  waitingList = [],
  slotOffers = [],
  isQueueEnabled = true,
  readOnlyInspectMode = false,
  onBookAppointment,
  onTransitionStatus,
  onReschedule,
  onPushLate,
  onCallNext,
  onAddToWaitingList,
  onAcceptSlotOffer,
  onCloseDay,
}) => {
  const [selectedDoctorFilter, setSelectedDoctorFilter] = useState<string>('');
  const [bookPatientId, setBookPatientId] = useState<string>(patients[0]?.id ?? '');
  const [bookDoctorId, setBookDoctorId] = useState<string>(doctors[0]?.id ?? '');
  const [bookCategoryId, setBookCategoryId] = useState<string>(categories[0]?.id ?? '');
  const [bookPriority, setBookPriority] = useState<'Normal' | 'Vip' | 'Rush'>('Normal');
  const [bookSlotUtc, setBookSlotUtc] = useState<string>(freeSlots[0]?.startUtc ?? '2026-06-01T10:00:00Z');
  const [closeDate, setCloseDate] = useState<string>('2026-06-01');
  const [closeReason, setCloseReason] = useState<string>('عطلة رسمية');
  const [waitDate, setWaitDate] = useState<string>('2026-06-01');

  const visibleAppointments = selectedDoctorFilter
    ? appointments.filter((a) => a.doctorId === selectedDoctorFilter)
    : appointments;

  const visibleQueue = selectedDoctorFilter
    ? queueEntries.filter((q) => q.doctorId === selectedDoctorFilter)
    : queueEntries;

  return (
    <div dir="rtl" style={{ display: 'flex', flexDirection: 'column', gap: '1.25rem' }}>
      {readOnlyInspectMode && (
        <AlertBanner
          variant="warning"
          title="وضع الفحص للقراءة فقط (Inspect Read-Only)"
          message="عرض جدول المواعيد وقائمة الانتظار للمشرف العام فقط — جميع أزرار الحجز والتعديل معطلة."
        />
      )}

      {/* Header + Doctor Filter + Close Day */}
      <GlassCard style={{ padding: '1.25rem' }}>
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            flexWrap: 'wrap',
            gap: '1rem',
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.625rem' }}>
            <Calendar size={22} color="var(--color-accent-teal)" />
            <div>
              <h2 style={{ fontSize: '1.125rem', fontWeight: 700 }}>
                المواعيد وإدارة الانتظار (Scheduling & Queue)
              </h2>
              <p style={{ fontSize: '0.8125rem', color: 'var(--color-text-secondary)' }}>
                محرك الأوقات المتاحة الفوري، المواعيد المتزامنة، وأولوية قائمة الانتظار
              </p>
            </div>
          </div>

          <div style={{ display: 'flex', alignItems: 'center', gap: '0.625rem', flexWrap: 'wrap' }}>
            <select
              aria-label="تصفية حسب الطبيب"
              value={selectedDoctorFilter}
              onChange={(e) => setSelectedDoctorFilter(e.target.value)}
              style={{
                paddingInline: '0.75rem',
                paddingBlock: '0.5rem',
                borderRadius: '0.5rem',
                border: '1px solid var(--glass-border)',
                background: 'var(--glass-surface)',
                color: 'var(--color-text-primary)',
              }}
            >
              <option value="">كل الأطباء / All Doctors</option>
              {doctors.map((d) => (
                <option key={d.id} value={d.id}>
                  {d.fullName}
                </option>
              ))}
            </select>

            {!readOnlyInspectMode && onCloseDay && (
              <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                <GlassInput
                  label="إغلاق يوم"
                  value={closeDate}
                  onChange={(e) => setCloseDate(e.target.value)}
                  dir="ltr"
                />
                <GlassInput
                  label="السبب"
                  value={closeReason}
                  onChange={(e) => setCloseReason(e.target.value)}
                />
                <GlassButton
                  variant="regular"
                  onClick={() => onCloseDay(closeDate, closeReason)}
                >
                  إغلاق اليوم
                </GlassButton>
              </div>
            )}
          </div>
        </div>
      </GlassCard>

      {/* Main Grid: Booking + Appointments */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(340px, 1fr))', gap: '1.25rem' }}>
        {!readOnlyInspectMode && (
          <GlassCard style={{ padding: '1.25rem' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '1rem' }}>
              <Clock size={18} color="var(--color-accent-teal)" />
              <h3 style={{ fontSize: '1rem', fontWeight: 700 }}>حجز موعد جديد / الأوقات المتاحة</h3>
            </div>

            <div style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem' }}>
              <select
                aria-label="اختيار المريض"
                value={bookPatientId}
                onChange={(e) => setBookPatientId(e.target.value)}
                style={{
                  padding: '0.5rem',
                  borderRadius: '0.5rem',
                  border: '1px solid var(--glass-border)',
                  background: 'var(--glass-surface)',
                  color: 'var(--color-text-primary)',
                }}
              >
                {patients.map((p) => (
                  <option key={p.id} value={p.id}>
                    {p.fullName} ({p.fileNumber})
                  </option>
                ))}
              </select>

              <select
                aria-label="اختيار الطبيب"
                value={bookDoctorId}
                onChange={(e) => setBookDoctorId(e.target.value)}
                style={{
                  padding: '0.5rem',
                  borderRadius: '0.5rem',
                  border: '1px solid var(--glass-border)',
                  background: 'var(--glass-surface)',
                  color: 'var(--color-text-primary)',
                }}
              >
                {doctors.map((d) => (
                  <option key={d.id} value={d.id}>
                    {d.fullName}
                  </option>
                ))}
              </select>

              <select
                aria-label="نوع الكشف"
                value={bookCategoryId}
                onChange={(e) => setBookCategoryId(e.target.value)}
                style={{
                  padding: '0.5rem',
                  borderRadius: '0.5rem',
                  border: '1px solid var(--glass-border)',
                  background: 'var(--glass-surface)',
                  color: 'var(--color-text-primary)',
                }}
              >
                {categories.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.nameAr} ({c.defaultDurationMinutes} دقيقة — {c.defaultPriceAmount} {c.defaultPriceCurrency})
                  </option>
                ))}
              </select>

              <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
                {(['Normal', 'Vip', 'Rush'] as const).map((prio) => (
                  <GlassButton
                    key={prio}
                    variant={bookPriority === prio ? 'prominent' : 'regular'}
                    onClick={() => setBookPriority(prio)}
                  >
                    {PRIORITY_BADGES[prio].label}
                  </GlassButton>
                ))}
              </div>

              {freeSlots.length > 0 && (
                <div style={{ display: 'flex', gap: '0.375rem', flexWrap: 'wrap' }}>
                  {freeSlots.slice(0, 6).map((slot) => (
                    <button
                      key={`${slot.doctorId}-${slot.startUtc}`}
                      type="button"
                      onClick={() => setBookSlotUtc(slot.startUtc)}
                      style={{
                        paddingInline: '0.625rem',
                        paddingBlock: '0.375rem',
                        borderRadius: '0.5rem',
                        border:
                          bookSlotUtc === slot.startUtc
                            ? '1px solid var(--color-accent-teal)'
                            : '1px solid var(--glass-border)',
                        background:
                          bookSlotUtc === slot.startUtc
                            ? 'rgba(13, 148, 136, 0.16)'
                            : 'var(--glass-surface)',
                        color: 'var(--color-text-primary)',
                        fontSize: '0.8125rem',
                        cursor: 'pointer',
                      }}
                    >
                      {new Date(slot.startUtc).toLocaleTimeString('ar-EG', { hour: '2-digit', minute: '2-digit' })}
                    </button>
                  ))}
                </div>
              )}

              <GlassInput
                label="توقيت الموعد (UTC)"
                value={bookSlotUtc}
                onChange={(e) => setBookSlotUtc(e.target.value)}
                dir="ltr"
              />

              <GlassButton
                variant="prominent"
                onClick={() =>
                  onBookAppointment?.({
                    patientId: bookPatientId,
                    doctorId: bookDoctorId,
                    categoryId: bookCategoryId,
                    startUtc: bookSlotUtc,
                    priority: bookPriority,
                  })
                }
              >
                تأكيد حجز الموعد
              </GlassButton>
            </div>
          </GlassCard>
        )}

        {/* Appointments List & State Machine Transitions */}
        <GlassCard style={{ padding: '1.25rem' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '1rem' }}>
            <CheckCircle2 size={18} color="var(--color-accent-teal)" />
            <h3 style={{ fontSize: '1rem', fontWeight: 700 }}>
              جدول المواعيد ({visibleAppointments.length})
            </h3>
          </div>

          {visibleAppointments.length === 0 ? (
            <p style={{ color: 'var(--color-text-secondary)', fontSize: '0.9rem' }}>
              لا توجد مواعيد مسجلة حالياً ضمن هذا النطاق.
            </p>
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem' }}>
              {visibleAppointments.map((appt) => {
                const badge = PRIORITY_BADGES[appt.priority] ?? PRIORITY_BADGES.Normal;
                return (
                  <div
                    key={appt.id}
                    data-testid={`appointment-card-${appt.id}`}
                    style={{
                      padding: '0.875rem',
                      borderRadius: '0.75rem',
                      background: 'var(--glass-surface)',
                      border: '1px solid var(--glass-border)',
                    }}
                  >
                    <div
                      style={{
                        display: 'flex',
                        justifyContent: 'space-between',
                        alignItems: 'center',
                        flexWrap: 'wrap',
                        gap: '0.5rem',
                        marginBottom: '0.5rem',
                      }}
                    >
                      <div>
                        <strong>{appt.patientName}</strong>
                        <span style={{ color: 'var(--color-text-muted)', marginInlineStart: '0.5rem', fontSize: '0.8125rem' }}>
                          {appt.doctorName} — {appt.categoryNameAr}
                        </span>
                      </div>

                      <div style={{ display: 'flex', alignItems: 'center', gap: '0.375rem' }}>
                        <span
                          style={{
                            paddingInline: '0.5rem',
                            paddingBlock: '0.125rem',
                            borderRadius: '9999px',
                            background: badge.bg,
                            color: badge.color,
                            fontSize: '0.75rem',
                            fontWeight: 600,
                          }}
                        >
                          {badge.label}
                        </span>
                        <span style={{ fontSize: '0.8125rem', fontWeight: 600 }}>
                          {STATUS_LABELS_AR[appt.status] ?? appt.status}
                        </span>
                      </div>
                    </div>

                    {!readOnlyInspectMode && (
                      <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.375rem', marginTop: '0.5rem' }}>
                        {appt.allowedNextStatuses.map((next) => (
                          <GlassButton
                            key={next}
                            variant={next === 'Cancelled' ? 'regular' : 'prominent'}
                            onClick={() => onTransitionStatus?.(appt.id, next)}
                          >
                            {STATUS_LABELS_AR[next] ?? next}
                          </GlassButton>
                        ))}
                        {onReschedule && (
                          <>
                            <GlassButton variant="regular" onClick={() => onReschedule(appt.id, 'after_week')}>
                              بعد أسبوع
                            </GlassButton>
                            <GlassButton variant="regular" onClick={() => onReschedule(appt.id, 'after_month')}>
                              بعد شهر
                            </GlassButton>
                          </>
                        )}
                      </div>
                    )}
                  </div>
                );
              })}
            </div>
          )}
        </GlassCard>
      </div>

      {/* Live Waiting Queue + Waiting List & Slot Offers (Queue Module) */}
      {!isQueueEnabled ? (
        <GlassCard style={{ padding: '1.25rem' }} data-testid="queue-module-disabled-notice">
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
            <Lock size={18} color="var(--color-text-muted)" />
            <span>
              ميزة طابور الانتظار (Queue) معطلة لهذه العيادة — يستمر نظام حجز المواعيد بالعمل بشكل مستقل.
            </span>
          </div>
        </GlassCard>
      ) : (
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(340px, 1fr))', gap: '1.25rem' }}>
          <GlassCard style={{ padding: '1.25rem' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '1rem' }}>
              <Users size={18} color="var(--color-accent-teal)" />
              <h3 style={{ fontSize: '1rem', fontWeight: 700 }}>لوحة طابور الانتظار المباشر ({visibleQueue.length})</h3>
            </div>

            {visibleQueue.length === 0 ? (
              <p style={{ color: 'var(--color-text-secondary)', fontSize: '0.9rem' }}>
                لا يوجد مرضى في طابور الانتظار حالياً.
              </p>
            ) : (
              <div style={{ display: 'flex', flexDirection: 'column', gap: '0.625rem' }}>
                {visibleQueue.map((q) => {
                  const badge = PRIORITY_BADGES[q.priority] ?? PRIORITY_BADGES.Normal;
                  return (
                    <div
                      key={q.id}
                      data-testid={`queue-entry-${q.id}`}
                      style={{
                        display: 'flex',
                        justifyContent: 'space-between',
                        alignItems: 'center',
                        flexWrap: 'wrap',
                        gap: '0.5rem',
                        padding: '0.75rem',
                        borderRadius: '0.625rem',
                        background: 'var(--glass-surface)',
                        border: '1px solid var(--glass-border)',
                      }}
                    >
                      <div>
                        <strong>#{q.orderIndex} {q.patientName}</strong>
                        <span
                          style={{
                            marginInlineStart: '0.5rem',
                            paddingInline: '0.5rem',
                            paddingBlock: '0.125rem',
                            borderRadius: '9999px',
                            background: badge.bg,
                            color: badge.color,
                            fontSize: '0.75rem',
                          }}
                        >
                          {badge.label}
                        </span>
                        <div style={{ fontSize: '0.75rem', color: 'var(--color-text-muted)' }}>
                          {q.doctorName} | الحالة: {q.status} | التأخير: {q.pushCount} | قفزات VIP: {q.vipJumpCount}
                        </div>
                      </div>

                      {!readOnlyInspectMode && q.status === 'Waiting' && (
                        <div style={{ display: 'flex', gap: '0.375rem' }}>
                          {onCallNext && (
                            <GlassButton variant="prominent" onClick={() => onCallNext(q.id)}>
                              استدعاء للدخول
                            </GlassButton>
                          )}
                          {onPushLate && (
                            <GlassButton variant="regular" onClick={() => onPushLate(q.id)}>
                              تأخير دور (متأخر)
                            </GlassButton>
                          )}
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
              <BellRing size={18} color="var(--color-accent-teal)" />
              <h3 style={{ fontSize: '1rem', fontWeight: 700 }}>
                قائمة الانتظار وعروض المواعيد المتاحة ({slotOffers.length})
              </h3>
            </div>

            {!readOnlyInspectMode && onAddToWaitingList && (
              <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', marginBottom: '1rem', flexWrap: 'wrap' }}>
                <GlassInput
                  label="التاريخ المفضل"
                  value={waitDate}
                  onChange={(e) => setWaitDate(e.target.value)}
                  dir="ltr"
                />
                <GlassButton
                  variant="regular"
                  onClick={() =>
                    onAddToWaitingList({
                      patientId: bookPatientId,
                      doctorId: bookDoctorId || undefined,
                      categoryId: bookCategoryId,
                      preferredDate: waitDate,
                    })
                  }
                >
                  <Sparkles size={15} /> إضافة لقائمة الانتظار
                </GlassButton>
              </div>
            )}

            {slotOffers.length > 0 && (
              <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem', marginBottom: '0.75rem' }}>
                {slotOffers.map((offer) => (
                  <div
                    key={offer.id}
                    data-testid={`slot-offer-${offer.id}`}
                    style={{
                      display: 'flex',
                      justifyContent: 'space-between',
                      alignItems: 'center',
                      padding: '0.625rem',
                      borderRadius: '0.5rem',
                      border: '1px solid rgba(13, 148, 136, 0.35)',
                      background: 'rgba(13, 148, 136, 0.08)',
                    }}
                  >
                    <div style={{ fontSize: '0.8125rem' }}>
                      <strong>عرض موعد متاح ({offer.status})</strong>
                      <div dir="ltr">{new Date(offer.slotStartUtc).toISOString()}</div>
                    </div>
                    {!readOnlyInspectMode && offer.status === 'Pending' && onAcceptSlotOffer && (
                      <GlassButton variant="prominent" onClick={() => onAcceptSlotOffer(offer.id)}>
                        قبول وحجز الموعد
                      </GlassButton>
                    )}
                  </div>
                ))}
              </div>
            )}

            <div style={{ fontSize: '0.8125rem', color: 'var(--color-text-secondary)' }}>
              إجمالي طلبات الانتظار النشطة: {waitingList.filter((w) => w.status === 'Waiting').length}
            </div>
          </GlassCard>
        </div>
      )}
    </div>
  );
};
