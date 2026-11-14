import React, { useEffect, useState } from 'react';
import { GlassCard, GlassButton, GlassInput, AlertBanner, CodeInput, ThemeToggle, LanguageSwitcher } from '@clinic/ui';
import {
  ShieldAlert,
  FileText,
  Download,
  CalendarClock,
  ClipboardList,
  LogOut,
  HeartPulse,
  KeyRound,
  Stethoscope,
} from 'lucide-react';

export interface PortalAllergyItem {
  id: string;
  allergen: string;
  severity: string;
  reaction?: string | null;
}

export interface PortalVisibleNoteItem {
  noteId: string;
  noteText: string;
  createdAtUtc: string;
}

export interface PortalSharedAttachmentItem {
  attachmentId: string;
  category: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  notes?: string | null;
  createdAtUtc: string;
}

export interface PortalPrescriptionItem {
  medicationName: string;
  dosage: string;
  frequency: string;
  duration: string;
}

export interface PortalPrescriptionSummary {
  id: string;
  notes?: string | null;
  items: PortalPrescriptionItem[];
}

export interface PortalVisitSummary {
  visitId: string;
  startedAtUtc: string;
  endedAtUtc?: string | null;
  doctorName: string;
  specialtyCode: string;
  diagnosisSummary?: string | null;
  advice?: string | null;
  followUpDate?: string | null;
  prescriptions: PortalPrescriptionSummary[];
}

export interface PortalUpcomingAppointment {
  appointmentId: string;
  startAtUtc: string;
  endAtUtc: string;
  doctorName?: string | null;
  specialtyCode: string;
  categoryNameAr: string;
  categoryNameEn: string;
  status: string;
}

export interface PortalPlanSession {
  sessionId: string;
  sequenceNumber: number;
  title: string;
  targetDate: string;
  status: string;
  sessionPriceAmount?: number | null;
}

export interface PortalTreatmentPlan {
  planId: string;
  name: string;
  mode: string;
  status: string;
  totalSessionsCount: number;
  completedSessionsCount: number;
  completionPercentage: number;
  showCostToPatient: boolean;
  totalCostToPatient?: number | null;
  paidAmountToPatient?: number | null;
  remainingBalanceToPatient?: number | null;
  currency?: string | null;
  sessions: PortalPlanSession[];
}

export interface PublicClinicLandingData {
  clinicId: string;
  name: string;
  currentSlug: string;
  requestedSlug: string;
  isRedirect: boolean;
  redirectUrl: string;
  address?: string | null;
  mainPhone?: string | null;
  welcomeMessageAr: string;
  welcomeMessageEn: string;
  termsTextAr: string;
  termsTextEn: string;
  availableSections: string[];
}

export interface PortalHomeOverviewData {
  clinicId: string;
  clinicName: string;
  clinicSlug: string;
  welcomeMessageAr: string;
  welcomeMessageEn: string;
  availableSections: string[];
  summary: {
    patientId: string;
    fileNumber: string;
    fullName: string;
    bloodType?: string | null;
    email?: string | null;
    guardianName?: string | null;
  };
  medicalBackground?: {
    allergies: PortalAllergyItem[];
    chronicConditions: string[];
    currentMedications: string[];
    pastSurgeries: string[];
    visibleNotes: PortalVisibleNoteItem[];
    sharedAttachments: PortalSharedAttachmentItem[];
  } | null;
  upcomingAppointments?: PortalUpcomingAppointment[] | null;
  visits?: PortalVisitSummary[] | null;
  treatmentPlans?: PortalTreatmentPlan[] | null;
}

export interface PortalBookableCategoryItem {
  categoryId: string;
  specialtyCode: string;
  nameAr: string;
  nameEn: string;
  durationMinutes: number;
  priceAmount?: number | null;
  priceCurrency?: string | null;
}

export interface PortalBookableDoctorItem {
  doctorId: string;
  displayName?: string | null;
  specialtyCode: string;
}

export interface PortalAvailableSlotItem {
  doctorId: string;
  doctorName?: string | null;
  roomId?: string | null;
  localDate: string;
  startAtUtc: string;
  endAtUtc: string;
  durationMinutes: number;
}

export interface PortalAvailabilityData {
  clinicId: string;
  clinicTimeZone: string;
  bookingMode: 'AutoConfirm' | 'RequiresApproval' | string;
  cancellationCutoffHours: number;
  maxActiveFutureBookingsPerPatient: number;
  activeFutureBookingsCount: number;
  isPatientOnlineBookingBlocked: boolean;
  onlineBookingBlockReason?: string | null;
  categories: PortalBookableCategoryItem[];
  doctors: PortalBookableDoctorItem[];
  slots: PortalAvailableSlotItem[];
}

export interface PortalBookAppointmentPayload {
  slug: string;
  doctorId: string;
  categoryId: string;
  startAtUtc: string;
  notes: string | null;
  operationId: string;
}

export interface PortalCancelAppointmentPayload {
  slug: string;
  appointmentId: string;
  reason: string | null;
}

export interface PortalAppProps {
  slug?: string;
  initialLanding?: PublicClinicLandingData | null;
  initialOverview?: PortalHomeOverviewData | null;
  initialAvailability?: PortalAvailabilityData | null;
  onLoginSubmit?: (payload: { slug: string; email: string; password: string }) => Promise<PortalHomeOverviewData | null> | void;
  onActivateEmailSubmit?: (payload: { slug: string; email: string; code: string; newPassword: string }) => Promise<PortalHomeOverviewData | null> | void;
  onRegisterPinSubmit?: (payload: { slug: string; fileNumber: string; dateOfBirth: string; printedPin: string; email: string }) => Promise<void> | void;
  onDownloadSignedResource?: (kind: 'prescription' | 'attachment', id: string) => Promise<string> | void;
  onBookAppointment?: (payload: PortalBookAppointmentPayload) => Promise<PortalUpcomingAppointment | null> | void;
  onCancelAppointment?: (payload: PortalCancelAppointmentPayload) => Promise<PortalUpcomingAppointment | null> | void;
  onLogout?: () => void;
}

type PortalAuthMode = 'login' | 'activate' | 'pin' | 'forgot' | 'reset';
type PortalTab = 'overview' | 'booking' | 'medical' | 'visits' | 'plans';

const resolveSlugFromLocation = (propSlug?: string): string => {
  if (propSlug) return propSlug;
  if (typeof window !== 'undefined') {
    const match = /^\/c\/([^/]+)/i.exec(window.location.pathname);
    if (match?.[1]) return decodeURIComponent(match[1]);
  }
  return 'smile-dental';
};

export const PortalApp: React.FC<PortalAppProps> = ({
  slug: propSlug,
  initialLanding = null,
  initialOverview = null,
  initialAvailability = null,
  onLoginSubmit,
  onActivateEmailSubmit,
  onRegisterPinSubmit,
  onDownloadSignedResource,
  onBookAppointment,
  onCancelAppointment,
  onLogout,
}) => {
  const slug = resolveSlugFromLocation(propSlug);
  const [landing, setLanding] = useState<PublicClinicLandingData | null>(initialLanding);
  const [overview, setOverview] = useState<PortalHomeOverviewData | null>(initialOverview);
  const [availability, setAvailability] = useState<PortalAvailabilityData | null>(initialAvailability);
  const [selectedCategoryId, setSelectedCategoryId] = useState<string>(
    initialAvailability?.categories[0]?.categoryId ?? '',
  );
  const [selectedDoctorId, setSelectedDoctorId] = useState<string>('');
  const [selectedSlotStartUtc, setSelectedSlotStartUtc] = useState<string>(
    initialAvailability?.slots[0]?.startAtUtc ?? '',
  );
  const [bookingNotes, setBookingNotes] = useState('');
  const [authMode, setAuthMode] = useState<PortalAuthMode>('login');
  const [activeTab, setActiveTab] = useState<PortalTab>('overview');
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [infoMsg, setInfoMsg] = useState<string | null>(null);
  const [downloadUrlBanner, setDownloadUrlBanner] = useState<string | null>(null);

  // Auth form fields
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [verificationCode, setVerificationCode] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [fileNumber, setFileNumber] = useState('');
  const [dateOfBirth, setDateOfBirth] = useState('');
  const [printedPin, setPrintedPin] = useState('');

  useEffect(() => {
    if (typeof document !== 'undefined') {
      document.documentElement.setAttribute('dir', 'rtl');
      document.documentElement.setAttribute('lang', 'ar');
      let meta = document.querySelector('meta[name="robots"]');
      if (!meta) {
        meta = document.createElement('meta');
        meta.setAttribute('name', 'robots');
        document.head.appendChild(meta);
      }
      meta.setAttribute('content', 'noindex, nofollow');
    }
  }, []);

  useEffect(() => {
    if (landing || typeof fetch === 'undefined' || initialLanding === null && typeof window !== 'undefined' && window.location.pathname.startsWith('/c/') === false) {
      return;
    }
    if (typeof window !== 'undefined' && window.location.pathname.startsWith('/c/')) {
      void fetch(`/api/v1/c/${encodeURIComponent(slug)}/portal/landing`, { credentials: 'include' })
        .then(async (res) => (res.ok ? ((await res.json()) as PublicClinicLandingData) : null))
        .then((data) => {
          if (data) setLanding(data);
        })
        .catch(() => undefined);
    }
  }, [landing, initialLanding, slug]);

  const handleLogin = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMsg(null);
    if (onLoginSubmit) {
      const next = await onLoginSubmit({ slug, email, password });
      if (next) setOverview(next);
      return;
    }
    const res = await fetch(`/api/v1/c/${encodeURIComponent(slug)}/portal/login`, {
      method: 'POST',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, password }),
    });
    if (!res.ok) {
      setErrorMsg('بيانات الدخول غير صحيحة أو الحساب معطّل.');
      return;
    }
    const ovRes = await fetch(`/api/v1/c/${encodeURIComponent(slug)}/portal/overview`, { credentials: 'include' });
    if (ovRes.ok) {
      setOverview((await ovRes.json()) as PortalHomeOverviewData);
    }
  };

  const handleActivate = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMsg(null);
    if (onActivateEmailSubmit) {
      const next = await onActivateEmailSubmit({ slug, email, code: verificationCode, newPassword });
      if (next) setOverview(next);
      return;
    }
    const res = await fetch(`/api/v1/c/${encodeURIComponent(slug)}/portal/activate`, {
      method: 'POST',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, code: verificationCode, newPassword }),
    });
    if (!res.ok) {
      setErrorMsg('رمز التفعيل غير صالح أو منتهي الصلاحية.');
      return;
    }
    const ovRes = await fetch(`/api/v1/c/${encodeURIComponent(slug)}/portal/overview`, { credentials: 'include' });
    if (ovRes.ok) {
      setOverview((await ovRes.json()) as PortalHomeOverviewData);
    }
  };

  const handlePinRegister = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMsg(null);
    setInfoMsg(null);
    if (onRegisterPinSubmit) {
      await onRegisterPinSubmit({ slug, fileNumber, dateOfBirth, printedPin, email });
      setInfoMsg('تم التحقق من الرقم السري وإرسال رمز التفعيل المكون من 6 أرقام إلى بريدك.');
      setAuthMode('activate');
      return;
    }
    const res = await fetch(`/api/v1/c/${encodeURIComponent(slug)}/portal/register-pin`, {
      method: 'POST',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ fileNumber, dateOfBirth, printedPin, email }),
    });
    if (!res.ok) {
      setErrorMsg('الرقم السري المطبوع أو تاريخ الميلاد غير متطابق.');
      return;
    }
    setInfoMsg('تم إرسال رمز التفعيل إلى بريدك الإلكتروني.');
    setAuthMode('activate');
  };

  const handleDownload = async (kind: 'prescription' | 'attachment', id: string) => {
    setErrorMsg(null);
    if (onDownloadSignedResource) {
      const url = await onDownloadSignedResource(kind, id);
      if (typeof url === 'string') setDownloadUrlBanner(url);
      return;
    }
    const path =
      kind === 'prescription'
        ? `/api/v1/c/${encodeURIComponent(slug)}/portal/prescriptions/${id}/signed-url`
        : `/api/v1/c/${encodeURIComponent(slug)}/portal/attachments/${id}/signed-url`;
    const res = await fetch(path, { method: 'POST', credentials: 'include' });
    if (!res.ok) {
      setErrorMsg('تعذر إنشاء رابط التنزيل المؤقت.');
      return;
    }
    const data = (await res.json()) as { url: string };
    setDownloadUrlBanner(data.url);
    if (typeof window !== 'undefined') {
      window.open(data.url, '_blank', 'noopener,noreferrer');
    }
  };

  useEffect(() => {
    if (
      !overview ||
      availability ||
      !overview.availableSections?.includes('portal-booking') ||
      typeof fetch === 'undefined' ||
      (typeof window !== 'undefined' && !window.location.pathname.startsWith('/c/'))
    ) {
      return;
    }
    const qs = selectedCategoryId ? `?categoryId=${encodeURIComponent(selectedCategoryId)}` : '';
    void fetch(`/api/v1/c/${encodeURIComponent(slug)}/portal/availability${qs}`, { credentials: 'include' })
      .then(async (res) => (res.ok ? ((await res.json()) as PortalAvailabilityData) : null))
      .then((data) => {
        if (data) {
          setAvailability(data);
          if (!selectedCategoryId && data.categories[0]) {
            setSelectedCategoryId(data.categories[0].categoryId);
          }
          if (!selectedSlotStartUtc && data.slots[0]) {
            setSelectedSlotStartUtc(data.slots[0].startAtUtc);
          }
        }
      })
      .catch(() => undefined);
  }, [overview, availability, selectedCategoryId, selectedSlotStartUtc, slug]);

  const handleBookSlotSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMsg(null);
    setInfoMsg(null);
    if (typeof navigator !== 'undefined' && navigator.onLine === false) {
      setErrorMsg('هذه الخدمة تتطلب اتصالاً نشطاً بالإنترنت (ONLINE_REQUIRED).');
      return;
    }
    if (!availability || !overview) {
      return;
    }
    const chosenSlot =
      availability.slots.find(
        (s) =>
          s.startAtUtc === selectedSlotStartUtc &&
          (!selectedDoctorId || s.doctorId === selectedDoctorId),
      ) ?? availability.slots.find((s) => !selectedDoctorId || s.doctorId === selectedDoctorId);
    if (!chosenSlot) {
      setErrorMsg('يرجى اختيار موعد متاح أولاً.');
      return;
    }
    const categoryId = selectedCategoryId || availability.categories[0]?.categoryId;
    if (!categoryId) {
      setErrorMsg('يرجى اختيار نوع الزيارة.');
      return;
    }
    const operationId =
      typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function'
        ? crypto.randomUUID()
        : `${Date.now()}-${Math.random().toString(16).slice(2)}`;
    const payload: PortalBookAppointmentPayload = {
      slug,
      doctorId: chosenSlot.doctorId,
      categoryId,
      startAtUtc: chosenSlot.startAtUtc,
      notes: bookingNotes.trim() || null,
      operationId,
    };
    if (onBookAppointment) {
      const created = await onBookAppointment(payload);
      if (created) {
        setOverview((prev) =>
          prev
            ? {
                ...prev,
                upcomingAppointments: [created, ...(prev.upcomingAppointments ?? [])],
              }
            : prev,
        );
        setAvailability((prev) =>
          prev
            ? {
                ...prev,
                activeFutureBookingsCount: prev.activeFutureBookingsCount + 1,
                slots: prev.slots.filter(
                  (s) => !(s.doctorId === payload.doctorId && s.startAtUtc === created.startAtUtc),
                ),
              }
            : prev,
        );
        setInfoMsg(
          created.status === 'Requested'
            ? 'تم إرسال طلب الحجز وهو بانتظار موافقة العيادة.'
            : 'تم تأكيد حجز الموعد بنجاح.',
        );
      }
      return;
    }
    const res = await fetch(`/api/v1/c/${encodeURIComponent(slug)}/portal/appointments`, {
      method: 'POST',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        doctorId: payload.doctorId,
        categoryId: payload.categoryId,
        startAtUtc: payload.startAtUtc,
        notes: payload.notes,
        operationId: payload.operationId,
      }),
    });
    if (!res.ok) {
      setErrorMsg('تعذر إتمام الحجز؛ قد يكون الموعد محجوزاً أو تم بلوغ الحد المسموح.');
      return;
    }
    const created = (await res.json()) as PortalUpcomingAppointment;
    setOverview((prev) =>
      prev
        ? {
            ...prev,
            upcomingAppointments: [created, ...(prev.upcomingAppointments ?? [])],
          }
        : prev,
    );
    setInfoMsg(
      created.status === 'Requested'
        ? 'تم إرسال طلب الحجز وهو بانتظار موافقة العيادة.'
        : 'تم تأكيد حجز الموعد بنجاح.',
    );
  };

  const handleCancelBookingClick = async (appointmentId: string) => {
    setErrorMsg(null);
    setInfoMsg(null);
    if (typeof navigator !== 'undefined' && navigator.onLine === false) {
      setErrorMsg('إلغاء الموعد يتطلب اتصالاً نشطاً بالإنترنت (ONLINE_REQUIRED).');
      return;
    }
    const payload: PortalCancelAppointmentPayload = {
      slug,
      appointmentId,
      reason: 'Cancelled by patient from portal',
    };
    if (onCancelAppointment) {
      const updated = await onCancelAppointment(payload);
      setOverview((prev) =>
        prev
          ? {
              ...prev,
              upcomingAppointments: (prev.upcomingAppointments ?? []).map((a) =>
                a.appointmentId === appointmentId
                  ? updated ?? { ...a, status: 'Cancelled' }
                  : a,
              ),
            }
          : prev,
      );
      setInfoMsg('تم إلغاء الموعد وإتاحة الوقت للمرضى الآخرين.');
      return;
    }
    const res = await fetch(
      `/api/v1/c/${encodeURIComponent(slug)}/portal/appointments/${encodeURIComponent(appointmentId)}/cancel`,
      {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ reason: payload.reason }),
      },
    );
    if (!res.ok) {
      setErrorMsg('تعذر إلغاء الموعد؛ قد تكون تجاوزت مهلة الإلغاء المسموحة.');
      return;
    }
    const updated = (await res.json()) as PortalUpcomingAppointment;
    setOverview((prev) =>
      prev
        ? {
            ...prev,
            upcomingAppointments: (prev.upcomingAppointments ?? []).map((a) =>
              a.appointmentId === appointmentId ? updated : a,
            ),
          }
        : prev,
    );
    setInfoMsg('تم إلغاء الموعد وإتاحة الوقت.');
  };

  const handleLogoutClick = () => {
    setOverview(null);
    setAvailability(null);
    onLogout?.();
  };

  const allergies = overview?.medicalBackground?.allergies ?? [];

  return (
    <div
      dir="rtl"
      data-testid="patient-portal-shell"
      style={{
        minHeight: '100vh',
        maxWidth: '48rem',
        marginInline: 'auto',
        paddingBlock: '1rem',
        paddingInline: '1rem',
        display: 'flex',
        flexDirection: 'column',
        gap: '1rem',
      }}
    >
      <header
        className="glass-card"
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          gap: '0.75rem',
          flexWrap: 'wrap',
          padding: '0.875rem 1.25rem',
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
          <div
            style={{
              width: '2.5rem',
              height: '2.5rem',
              borderRadius: 'var(--radius-card)',
              backgroundColor: 'var(--color-accent-teal)',
              color: '#ffffff',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              boxShadow: 'var(--glass-shadow)',
              flexShrink: 0,
            }}
          >
            <Stethoscope size={22} />
          </div>
          <div>
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
              <h1 style={{ fontSize: '1.125rem', fontWeight: 700, margin: 0, color: 'var(--color-text-primary)' }}>
                {overview?.clinicName ?? landing?.name ?? slug}
              </h1>
              <span
                style={{
                  fontSize: '0.6875rem',
                  fontWeight: 600,
                  paddingInline: '0.375rem',
                  paddingBlock: '0.125rem',
                  borderRadius: 'var(--radius-pill)',
                  backgroundColor: 'var(--color-accent-teal-soft)',
                  color: 'var(--color-accent-teal-text)',
                }}
              >
                بوابة المريض
              </span>
            </div>
            <div style={{ fontSize: '0.75rem', color: 'var(--color-text-muted)', margin: '0.125rem 0 0' }}>
              الوصول الطبي الآمن للملف والمواعيد
            </div>
          </div>
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
          <ThemeToggle />
          <LanguageSwitcher />
          {overview && (
            <GlassButton type="button" onClick={handleLogoutClick} aria-label="تسجيل الخروج من بوابة المريض" variant="danger">
              <LogOut size={16} />
              <span>خروج</span>
            </GlassButton>
          )}
        </div>
      </header>

      {landing?.isRedirect && (
        <AlertBanner
          variant="info"
          message={`تم تغيير رابط العيادة الدائم إلى: /c/${landing.currentSlug}`}
        />
      )}

      {errorMsg && <AlertBanner variant="error" message={errorMsg} />}
      {infoMsg && <AlertBanner variant="success" message={infoMsg} />}
      {downloadUrlBanner && (
        <AlertBanner variant="info" message={`رابط التنزيل المؤقت (صالح لفترة محدودة): ${downloadUrlBanner}`} />
      )}

      {!overview ? (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
          <GlassCard style={{ padding: '1.25rem' }}>
            <h2 style={{ fontSize: '1.0625rem', fontWeight: 700, marginBottom: '0.5rem' }}>
              {landing?.welcomeMessageAr ?? 'مرحباً بكم في بوابة المريض'}
            </h2>
            {landing?.address && (
              <div style={{ fontSize: '0.875rem', color: 'var(--color-text-secondary)' }}>
                العنوان: {landing.address}
              </div>
            )}
            {landing?.mainPhone && (
              <div dir="ltr" style={{ fontSize: '0.875rem', color: 'var(--color-text-secondary)', textAlign: 'start' }}>
                {landing.mainPhone}
              </div>
            )}
          </GlassCard>

          <GlassCard style={{ padding: '1.25rem' }}>
            <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap', marginBottom: '1rem' }}>
              <GlassButton variant={authMode === 'login' ? 'prominent' : 'default'} onClick={() => setAuthMode('login')}>
                تسجيل الدخول
              </GlassButton>
              <GlassButton variant={authMode === 'activate' ? 'prominent' : 'default'} onClick={() => setAuthMode('activate')}>
                تفعيل برمز البريد
              </GlassButton>
              <GlassButton variant={authMode === 'pin' ? 'prominent' : 'default'} onClick={() => setAuthMode('pin')}>
                التسجيل بالرقم السري المطبوع
              </GlassButton>
            </div>

            {authMode === 'login' && (
              <form onSubmit={handleLogin} style={{ display: 'flex', flexDirection: 'column', gap: '0.875rem' }}>
                <GlassInput
                  label="البريد الإلكتروني"
                  type="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  required
                />
                <GlassInput
                  label="كلمة المرور"
                  type="password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  required
                />
                <GlassButton type="submit" variant="prominent">
                  دخول لبوابة المريض
                </GlassButton>
              </form>
            )}

            {authMode === 'activate' && (
              <form onSubmit={handleActivate} style={{ display: 'flex', flexDirection: 'column', gap: '0.875rem' }}>
                <GlassInput
                  label="البريد الإلكتروني المسجل"
                  type="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  required
                />
                <div>
                  <div style={{ fontSize: '0.875rem', marginBottom: '0.375rem' }}>
                    رمز التفعيل (6 أرقام من اليسار لليمين)
                  </div>
                  <div dir="ltr" data-testid="portal-code-ltr">
                    <CodeInput value={verificationCode} onChange={setVerificationCode} length={6} />
                  </div>
                </div>
                <GlassInput
                  label="كلمة المرور الجديدة"
                  type="password"
                  value={newPassword}
                  onChange={(e) => setNewPassword(e.target.value)}
                  required
                />
                <GlassButton type="submit" variant="prominent">
                  تفعيل الحساب والدخول
                </GlassButton>
              </form>
            )}

            {authMode === 'pin' && (
              <form onSubmit={handlePinRegister} style={{ display: 'flex', flexDirection: 'column', gap: '0.875rem' }}>
                <GlassInput
                  label="رقم ملف المريض"
                  value={fileNumber}
                  onChange={(e) => setFileNumber(e.target.value)}
                  required
                />
                <GlassInput
                  label="تاريخ الميلاد"
                  type="date"
                  value={dateOfBirth}
                  onChange={(e) => setDateOfBirth(e.target.value)}
                  required
                />
                <GlassInput
                  label="الرقم السري المطبوع (10 خانات)"
                  value={printedPin}
                  onChange={(e) => setPrintedPin(e.target.value)}
                  required
                />
                <GlassInput
                  label="البريد الإلكتروني لاستلام رمز التأكيد"
                  type="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  required
                />
                <GlassButton type="submit" variant="prominent">
                  <KeyRound size={16} />
                  <span>التحقق وإرسال رمز البريد</span>
                </GlassButton>
              </form>
            )}
          </GlassCard>
        </div>
      ) : (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
          {allergies.length > 0 && (
            <div data-testid="portal-allergy-banner">
              <AlertBanner
                variant="error"
                title="تنبيه الحساسية الدوائية والطبية"
                message={allergies.map((a) => `${a.allergen} (${a.severity})`).join(' — ')}
              />
            </div>
          )}

          <nav style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
            <GlassButton variant={activeTab === 'overview' ? 'prominent' : 'default'} onClick={() => setActiveTab('overview')}>
              <CalendarClock size={16} />
              <span>الرئيسية</span>
            </GlassButton>
            {overview.availableSections?.includes('portal-booking') && (
              <GlassButton variant={activeTab === 'booking' ? 'prominent' : 'default'} onClick={() => setActiveTab('booking')}>
                <CalendarClock size={16} />
                <span>حجز موعد أونلاين</span>
              </GlassButton>
            )}
            <GlassButton variant={activeTab === 'medical' ? 'prominent' : 'default'} onClick={() => setActiveTab('medical')}>
              <HeartPulse size={16} />
              <span>الملف الطبي</span>
            </GlassButton>
            <GlassButton variant={activeTab === 'visits' ? 'prominent' : 'default'} onClick={() => setActiveTab('visits')}>
              <FileText size={16} />
              <span>الزيارات والروشتات</span>
            </GlassButton>
            <GlassButton variant={activeTab === 'plans' ? 'prominent' : 'default'} onClick={() => setActiveTab('plans')}>
              <ClipboardList size={16} />
              <span>خطط العلاج</span>
            </GlassButton>
          </nav>

          {activeTab === 'overview' && (
            <GlassCard style={{ padding: '1.25rem' }}>
              <h2 style={{ fontSize: '1.0625rem', fontWeight: 700, marginBottom: '0.5rem' }}>
                مرحباً {overview.summary.fullName}
              </h2>
              <div style={{ fontSize: '0.875rem', color: 'var(--color-text-secondary)', marginBottom: '1rem' }}>
                رقم الملف: <strong dir="ltr">{overview.summary.fileNumber}</strong>
                {overview.summary.bloodType && ` — فصيلة الدم: ${overview.summary.bloodType}`}
              </div>

              <h3 style={{ fontSize: '0.95rem', fontWeight: 700, marginBottom: '0.5rem' }}>المواعيد القادمة</h3>
              {(overview.upcomingAppointments?.length ?? 0) === 0 ? (
                <div style={{ fontSize: '0.875rem', color: 'var(--color-text-muted)' }}>لا توجد مواعيد قادمة.</div>
              ) : (
                overview.upcomingAppointments!.map((appt) => (
                  <div
                    key={appt.appointmentId}
                    data-testid={`portal-upcoming-${appt.appointmentId}`}
                    style={{
                      paddingBlock: '0.5rem',
                      borderBottom: '1px solid rgba(148,163,184,0.2)',
                      display: 'flex',
                      justifyContent: 'space-between',
                      alignItems: 'center',
                      gap: '0.5rem',
                      flexWrap: 'wrap',
                    }}
                  >
                    <div>
                      <strong>{appt.categoryNameAr}</strong> — {appt.doctorName ?? 'الطبيب'} (
                      <span data-testid={`portal-appt-status-${appt.appointmentId}`}>{appt.status}</span>)
                    </div>
                    {(appt.status === 'Booked' || appt.status === 'Requested') && (
                      <GlassButton
                        type="button"
                        onClick={() => void handleCancelBookingClick(appt.appointmentId)}
                      >
                        <span>إلغاء الموعد</span>
                      </GlassButton>
                    )}
                  </div>
                ))
              )}
            </GlassCard>
          )}

          {activeTab === 'booking' && overview.availableSections?.includes('portal-booking') && (
            <GlassCard style={{ padding: '1.25rem', display: 'flex', flexDirection: 'column', gap: '0.875rem' }}>
              <h2 style={{ fontSize: '1.0625rem', fontWeight: 700 }}>حجز موعد عبر بوابة المريض</h2>
              {!availability ? (
                <div style={{ fontSize: '0.875rem', color: 'var(--color-text-muted)' }}>
                  جاري تحميل المواعيد المتاحة...
                </div>
              ) : (
                <>
                  {availability.isPatientOnlineBookingBlocked && (
                    <AlertBanner
                      variant="error"
                      title="الحجز الأونلاين موقوف مؤقتاً"
                      message={availability.onlineBookingBlockReason ?? 'يرجى التواصل مع استقبال العيادة لإعادة التفعيل.'}
                    />
                  )}
                  <div style={{ fontSize: '0.8125rem', color: 'var(--color-text-secondary)', display: 'flex', gap: '1rem', flexWrap: 'wrap' }}>
                    <span data-testid="portal-booking-mode">
                      النظام: {availability.bookingMode === 'RequiresApproval' ? 'يتطلب موافقة العيادة (RequiresApproval)' : 'تأكيد فوري (AutoConfirm)'}
                    </span>
                    <span data-testid="portal-active-limit">
                      حجوزاتك النشطة: {availability.activeFutureBookingsCount} / {availability.maxActiveFutureBookingsPerPatient}
                    </span>
                    <span>مهلة الإلغاء: {availability.cancellationCutoffHours} ساعة</span>
                  </div>

                  <form onSubmit={handleBookSlotSubmit} style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem' }}>
                    <label style={{ display: 'flex', flexDirection: 'column', gap: '0.25rem', fontSize: '0.875rem' }}>
                      <span>نوع الكشف / الزيارة</span>
                      <select
                        aria-label="نوع الكشف"
                        value={selectedCategoryId || availability.categories[0]?.categoryId || ''}
                        onChange={(e) => setSelectedCategoryId(e.target.value)}
                        style={{ padding: '0.5rem', borderRadius: '0.5rem', border: '1px solid rgba(148,163,184,0.35)' }}
                      >
                        {availability.categories.map((cat) => (
                          <option key={cat.categoryId} value={cat.categoryId}>
                            {cat.nameAr} ({cat.durationMinutes} د)
                            {cat.priceAmount != null ? ` — ${cat.priceAmount} ${cat.priceCurrency ?? ''}` : ''}
                          </option>
                        ))}
                      </select>
                    </label>

                    <label style={{ display: 'flex', flexDirection: 'column', gap: '0.25rem', fontSize: '0.875rem' }}>
                      <span>الطبيب</span>
                      <select
                        aria-label="اختر الطبيب"
                        value={selectedDoctorId}
                        onChange={(e) => setSelectedDoctorId(e.target.value)}
                        style={{ padding: '0.5rem', borderRadius: '0.5rem', border: '1px solid rgba(148,163,184,0.35)' }}
                      >
                        <option value="">كل الأطباء المتاحين</option>
                        {availability.doctors.map((doc) => (
                          <option key={doc.doctorId} value={doc.doctorId}>
                            {doc.displayName ?? doc.specialtyCode}
                          </option>
                        ))}
                      </select>
                    </label>

                    <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.5rem' }}>
                      {availability.slots
                        .filter((slot) => !selectedDoctorId || slot.doctorId === selectedDoctorId)
                        .map((slot) => {
                          const isSelected = selectedSlotStartUtc === slot.startAtUtc;
                          return (
                            <GlassButton
                              key={`${slot.doctorId}-${slot.startAtUtc}`}
                              type="button"
                              data-testid={`portal-slot-${slot.startAtUtc}`}
                              variant={isSelected ? 'prominent' : 'default'}
                              onClick={() => setSelectedSlotStartUtc(slot.startAtUtc)}
                            >
                              <span dir="ltr">{slot.startAtUtc.slice(0, 16).replace('T', ' ')}</span>
                              {slot.doctorName ? ` — ${slot.doctorName}` : ''}
                            </GlassButton>
                          );
                        })}
                    </div>

                    <GlassInput
                      label="ملاحظات الحجز (اختياري)"
                      value={bookingNotes}
                      onChange={(e) => setBookingNotes(e.target.value)}
                    />

                    <div>
                      <GlassButton
                        type="submit"
                        variant="prominent"
                        disabled={availability.isPatientOnlineBookingBlocked}
                      >
                        {availability.bookingMode === 'RequiresApproval' ? 'إرسال طلب الحجز' : 'احجز الموعد الآن'}
                      </GlassButton>
                    </div>
                  </form>
                </>
              )}
            </GlassCard>
          )}

          {activeTab === 'medical' && (
            <GlassCard style={{ padding: '1.25rem', display: 'flex', flexDirection: 'column', gap: '0.875rem' }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                <ShieldAlert size={18} color="var(--color-accent-teal)" />
                <h2 style={{ fontSize: '1.0625rem', fontWeight: 700 }}>الخلفية الطبية والملاحظات والمرفقات</h2>
              </div>
              <div>الحالات المزمنة: {overview.medicalBackground?.chronicConditions.join('، ') || 'لا يوجد'}</div>
              <div>الأدوية الحالية: {overview.medicalBackground?.currentMedications.join('، ') || 'لا يوجد'}</div>

              <div>
                <h3 style={{ fontSize: '0.95rem', fontWeight: 700, marginBottom: '0.375rem' }}>الملاحظات المشتركة</h3>
                {(overview.medicalBackground?.visibleNotes ?? []).map((note) => (
                  <div key={note.noteId} style={{ padding: '0.5rem', borderRadius: '0.5rem', background: 'rgba(13,148,136,0.06)', marginBottom: '0.375rem' }}>
                    {note.noteText}
                  </div>
                ))}
              </div>

              <div>
                <h3 style={{ fontSize: '0.95rem', fontWeight: 700, marginBottom: '0.375rem' }}>المرفقات المشتركة</h3>
                {(overview.medicalBackground?.sharedAttachments ?? []).map((att) => (
                  <div key={att.attachmentId} style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', paddingBlock: '0.375rem' }}>
                    <span>{att.fileName}</span>
                    <GlassButton type="button" onClick={() => void handleDownload('attachment', att.attachmentId)}>
                      <Download size={14} />
                      <span>تنزيل</span>
                    </GlassButton>
                  </div>
                ))}
              </div>
            </GlassCard>
          )}

          {activeTab === 'visits' && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem' }}>
              {(overview.visits ?? []).map((visit) => (
                <GlassCard key={visit.visitId} style={{ padding: '1rem' }}>
                  <div style={{ fontWeight: 700, marginBottom: '0.375rem' }}>
                    {visit.doctorName} — {visit.specialtyCode}
                  </div>
                  {visit.diagnosisSummary && <div>التشخيص: {visit.diagnosisSummary}</div>}
                  {visit.advice && <div>إرشادات الطبيب: {visit.advice}</div>}
                  {visit.prescriptions.map((rx) => (
                    <div key={rx.id} style={{ marginTop: '0.625rem', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                      <span>روشتة طبية ({rx.items.length} أصناف)</span>
                      <GlassButton type="button" onClick={() => void handleDownload('prescription', rx.id)}>
                        <Download size={14} />
                        <span>تنزيل الروشتة PDF</span>
                      </GlassButton>
                    </div>
                  ))}
                </GlassCard>
              ))}
            </div>
          )}

          {activeTab === 'plans' && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem' }}>
              {(overview.treatmentPlans ?? []).map((plan) => (
                <GlassCard key={plan.planId} style={{ padding: '1rem' }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', fontWeight: 700 }}>
                    <span>{plan.name}</span>
                    <span>{plan.completedSessionsCount} / {plan.totalSessionsCount} جلسات</span>
                  </div>
                  <div style={{ fontSize: '0.875rem', marginTop: '0.375rem' }}>
                    نسبة الإنجاز: {plan.completionPercentage}%
                  </div>
                  {plan.showCostToPatient && plan.totalCostToPatient != null ? (
                    <div data-testid={`plan-cost-${plan.planId}`} style={{ fontSize: '0.875rem', marginTop: '0.375rem' }}>
                      التكلفة للمريض: {plan.totalCostToPatient} {plan.currency} — المتبقي: {plan.remainingBalanceToPatient}
                    </div>
                  ) : (
                    <div data-testid={`plan-cost-hidden-${plan.planId}`} style={{ fontSize: '0.8125rem', color: 'var(--color-text-muted)', marginTop: '0.375rem' }}>
                      تفاصيل التكلفة مخفية حسب إعدادات العيادة
                    </div>
                  )}
                </GlassCard>
              ))}
            </div>
          )}
        </div>
      )}
    </div>
  );
};
export default PortalApp;

