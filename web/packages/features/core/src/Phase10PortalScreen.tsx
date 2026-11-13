import React, { useMemo, useState } from 'react';
import { GlassCard, GlassButton, GlassInput, AlertBanner } from '@clinic/ui';
import {
  Globe,
  UserPlus,
  Mail,
  KeyRound,
  ShieldCheck,
  ShieldOff,
  Eye,
  EyeOff,
  Settings,
  History,
  RefreshCw,
} from 'lucide-react';

export type PortalAccountStatus = 'NotInvited' | 'Invited' | 'Active' | 'Disabled';

export interface PatientPortalAdminStatusData {
  patientId: string;
  hasAccount: boolean;
  status?: PortalAccountStatus | string | null;
  email?: string | null;
  isEmailConfirmed: boolean;
  isGuardianAccount: boolean;
  guardianName?: string | null;
  consentAcceptedAtUtc?: string | null;
  consentTermsVersion?: string | null;
  lastLoginAtUtc?: string | null;
  generatedPrintedPin?: string | null;
  printedPinExpiresAtUtc?: string | null;
}

export interface ClinicPortalSettingData {
  clinicId: string;
  showPricesByDefault: boolean;
  showDoctorNames: boolean;
  defaultVisitVisibility: boolean;
  termsTextAr: string;
  termsTextEn: string;
  welcomeMessageAr: string;
  welcomeMessageEn: string;
  updatedAtUtc: string;
}

export interface PortalAccessLogItemData {
  id: string;
  timestampUtc: string;
  actorId: string;
  actorType: string;
  reason: string;
  details?: string | null;
}

export interface ClinicPortalInspectData {
  clinicId: string;
  totalAccountsCount: number;
  activeAccountsCount: number;
  invitedAccountsCount: number;
  disabledAccountsCount: number;
  patientViewsLast30DaysCount: number;
  settings: ClinicPortalSettingData;
}

export interface InvitePatientPortalPayload {
  clinicId: string;
  patientId: string;
  email: string | null;
  isGuardianAccount: boolean;
  guardianFullName: string | null;
  consentGiven: boolean;
  consentTermsVersion: string;
  generatePrintedPin: boolean;
}

export interface SetPortalAccountEnabledPayload {
  clinicId: string;
  patientId: string;
  enabled: boolean;
  reason: string | null;
}

export interface UpdateClinicPortalSettingsPayload {
  clinicId: string;
  showPricesByDefault: boolean;
  showDoctorNames: boolean;
  defaultVisitVisibility: boolean;
  termsTextAr: string;
  termsTextEn: string;
  welcomeMessageAr: string;
  welcomeMessageEn: string;
}

export interface SetPortalItemVisibilityPayload {
  clinicId: string;
  patientId: string;
  itemId: string;
  kind: 'note' | 'attachment' | 'visit';
  visibleToPatient: boolean;
}

export interface Phase10PortalScreenProps {
  clinicId: string;
  readOnlyInspectMode?: boolean;
  initialInspectSnapshot?: ClinicPortalInspectData | null;
  initialPatientStatus?: PatientPortalAdminStatusData | null;
  initialSettings?: ClinicPortalSettingData | null;
  initialAccessLogs?: PortalAccessLogItemData[];
  onInvitePatient?: (payload: InvitePatientPortalPayload) => void;
  onResendInvite?: (patientId: string) => void;
  onSetAccountEnabled?: (payload: SetPortalAccountEnabledPayload) => void;
  onUpdateSettings?: (payload: UpdateClinicPortalSettingsPayload) => void;
  onSetVisibility?: (payload: SetPortalItemVisibilityPayload) => void;
}

const DEFAULT_PATIENT_STATUS: PatientPortalAdminStatusData = {
  patientId: 'patient-demo-1',
  hasAccount: false,
  status: 'NotInvited',
  email: 'nadia@example.com',
  isEmailConfirmed: false,
  isGuardianAccount: false,
  guardianName: null,
};

const DEFAULT_PORTAL_SETTINGS = (clinicId: string): ClinicPortalSettingData => ({
  clinicId,
  showPricesByDefault: false,
  showDoctorNames: true,
  defaultVisitVisibility: true,
  termsTextAr: 'بمتابعتك، فإنك توافق على شروط عرض السجل الطبي عبر بوابة المريض.',
  termsTextEn: 'By continuing, you agree to the patient portal access terms.',
  welcomeMessageAr: 'مرحباً بكم في بوابة المريض الخاصة بعيادتنا',
  welcomeMessageEn: 'Welcome to our clinic patient portal',
  updatedAtUtc: '2026-06-08T09:00:00Z',
});

const STATUS_LABELS: Record<string, { labelAr: string; color: string }> = {
  NotInvited: { labelAr: 'غير مدعو', color: 'var(--color-text-muted)' },
  Invited: { labelAr: 'مدعو — بانتظار التفعيل', color: '#f59e0b' },
  Active: { labelAr: 'نشط', color: '#10b981' },
  Disabled: { labelAr: 'معطّل', color: '#ef4444' },
};

export const Phase10PortalScreen: React.FC<Phase10PortalScreenProps> = ({
  clinicId,
  readOnlyInspectMode = false,
  initialInspectSnapshot = null,
  initialPatientStatus,
  initialSettings,
  initialAccessLogs = [],
  onInvitePatient,
  onResendInvite,
  onSetAccountEnabled,
  onUpdateSettings,
  onSetVisibility,
}) => {
  const [patientStatus, setPatientStatus] = useState<PatientPortalAdminStatusData>(
    initialPatientStatus ?? DEFAULT_PATIENT_STATUS,
  );
  const [inviteEmail, setInviteEmail] = useState(patientStatus.email ?? '');
  const [isGuardianAccount, setIsGuardianAccount] = useState(false);
  const [guardianFullName, setGuardianFullName] = useState('');
  const [consentGiven, setConsentGiven] = useState(false);
  const [generatePrintedPin, setGeneratePrintedPin] = useState(false);
  const [statusMessage, setStatusMessage] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const [settings, setSettings] = useState<ClinicPortalSettingData>(
    initialInspectSnapshot?.settings ?? initialSettings ?? DEFAULT_PORTAL_SETTINGS(clinicId),
  );
  const [visitVisible, setVisitVisible] = useState(true);
  const [attachmentShared, setAttachmentShared] = useState(false);

  const [accessLogs] = useState<PortalAccessLogItemData[]>(
    initialAccessLogs.length > 0
      ? initialAccessLogs
      : [
          {
            id: 'log-1',
            timestampUtc: '2026-06-08T10:15:00Z',
            actorId: patientStatus.patientId,
            actorType: 'Patient',
            reason: 'PortalOverviewRead',
            details: 'عرض الصفحة الرئيسية لبوابة المريض',
          },
        ],
  );

  const effectiveStatus = useMemo(
    () => (patientStatus.hasAccount ? patientStatus.status ?? 'Invited' : 'NotInvited'),
    [patientStatus],
  );

  const statusBadge = STATUS_LABELS[effectiveStatus] ?? STATUS_LABELS.NotInvited;

  const handleInviteSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMessage(null);
    setStatusMessage(null);

    if (readOnlyInspectMode) {
      return;
    }

    if (!consentGiven) {
      setErrorMessage('يجب توثيق موافقة المريض أو ولي الأمر قبل إرسال دعوة البوابة.');
      return;
    }

    if (!generatePrintedPin && !inviteEmail.trim()) {
      setErrorMessage('يرجى إدخال البريد الإلكتروني أو اختيار إصدار رقم سري مطبوع.');
      return;
    }

    const generatedPin = generatePrintedPin ? 'A9K4M7P2Q5' : null;
    const payload: InvitePatientPortalPayload = {
      clinicId,
      patientId: patientStatus.patientId,
      email: inviteEmail.trim() ? inviteEmail.trim() : null,
      isGuardianAccount,
      guardianFullName: isGuardianAccount ? guardianFullName.trim() || null : null,
      consentGiven,
      consentTermsVersion: 'v1',
      generatePrintedPin,
    };

    onInvitePatient?.(payload);
    setPatientStatus((prev) => ({
      ...prev,
      hasAccount: true,
      status: 'Invited',
      email: payload.email,
      isGuardianAccount,
      guardianName: payload.guardianFullName,
      generatedPrintedPin: generatedPin,
      printedPinExpiresAtUtc: generatedPin ? '2026-06-11T09:00:00Z' : null,
    }));
    setStatusMessage(
      generatedPin
        ? 'تم إنشاء الدعوة وإصدار رقم سري مطبوع لمرة واحدة.'
        : 'تم إرسال دعوة البوابة ورمز التفعيل عبر البريد الإلكتروني.',
    );
  };

  const handleResend = () => {
    if (readOnlyInspectMode) return;
    onResendInvite?.(patientStatus.patientId);
    setStatusMessage('تمت إعادة إرسال رمز التفعيل إلى بريد المريض.');
  };

  const handleToggleEnabled = () => {
    if (readOnlyInspectMode) return;
    const nextEnabled = effectiveStatus === 'Disabled';
    onSetAccountEnabled?.({
      clinicId,
      patientId: patientStatus.patientId,
      enabled: nextEnabled,
      reason: nextEnabled ? 'StaffEnabled' : 'StaffDisabled',
    });
    setPatientStatus((prev) => ({
      ...prev,
      hasAccount: true,
      status: nextEnabled ? 'Active' : 'Disabled',
    }));
    setStatusMessage(nextEnabled ? 'تم تفعيل حساب بوابة المريض.' : 'تم تعطيل حساب بوابة المريض.');
  };

  const handleSaveSettings = (e: React.FormEvent) => {
    e.preventDefault();
    if (readOnlyInspectMode) return;
    onUpdateSettings?.({
      clinicId,
      showPricesByDefault: settings.showPricesByDefault,
      showDoctorNames: settings.showDoctorNames,
      defaultVisitVisibility: settings.defaultVisitVisibility,
      termsTextAr: settings.termsTextAr,
      termsTextEn: settings.termsTextEn,
      welcomeMessageAr: settings.welcomeMessageAr,
      welcomeMessageEn: settings.welcomeMessageEn,
    });
    setStatusMessage('تم حفظ إعدادات بوابة المريض.');
  };

  const handleToggleVisitVisibility = () => {
    if (readOnlyInspectMode) return;
    const next = !visitVisible;
    setVisitVisible(next);
    onSetVisibility?.({
      clinicId,
      patientId: patientStatus.patientId,
      itemId: 'visit-demo-1',
      kind: 'visit',
      visibleToPatient: next,
    });
  };

  const handleToggleAttachmentVisibility = () => {
    if (readOnlyInspectMode) return;
    const next = !attachmentShared;
    setAttachmentShared(next);
    onSetVisibility?.({
      clinicId,
      patientId: patientStatus.patientId,
      itemId: 'att-demo-1',
      kind: 'attachment',
      visibleToPatient: next,
    });
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '1.25rem' }}>
      {statusMessage && <AlertBanner variant="success" message={statusMessage} />}
      {errorMessage && <AlertBanner variant="error" message={errorMessage} />}

      {initialInspectSnapshot && (
        <GlassCard style={{ padding: '1.25rem' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.75rem' }}>
            <Globe size={20} color="var(--color-accent-teal)" />
            <h3 style={{ fontSize: '1rem', fontWeight: 700 }}>
              إحصائيات بوابة المريض (فحص المدير العام)
            </h3>
          </div>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(150px, 1fr))', gap: '0.75rem' }}>
            <div>إجمالي الحسابات: <strong>{initialInspectSnapshot.totalAccountsCount}</strong></div>
            <div>الحسابات النشطة: <strong>{initialInspectSnapshot.activeAccountsCount}</strong></div>
            <div>بانتظار التفعيل: <strong>{initialInspectSnapshot.invitedAccountsCount}</strong></div>
            <div>المعطّلة: <strong>{initialInspectSnapshot.disabledAccountsCount}</strong></div>
            <div>مشاهدات المريض (30 يوم): <strong>{initialInspectSnapshot.patientViewsLast30DaysCount}</strong></div>
          </div>
        </GlassCard>
      )}

      {!readOnlyInspectMode && (
        <GlassCard style={{ padding: '1.25rem' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '0.75rem', marginBottom: '1rem' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
              <UserPlus size={20} color="var(--color-accent-teal)" />
              <h2 style={{ fontSize: '1.125rem', fontWeight: 700 }}>
                تبويب بوابة المريض في ملف المريض
              </h2>
            </div>
            <span
              data-testid="portal-status-badge"
              style={{
                paddingInline: '0.75rem',
                paddingBlock: '0.25rem',
                borderRadius: '9999px',
                border: `1px solid ${statusBadge.color}`,
                color: statusBadge.color,
                fontSize: '0.875rem',
                fontWeight: 600,
              }}
            >
              {statusBadge.labelAr}
            </span>
          </div>

          <form onSubmit={handleInviteSubmit} style={{ display: 'flex', flexDirection: 'column', gap: '0.875rem' }}>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '0.875rem' }}>
              <GlassInput
                label="البريد الإلكتروني للمريض أو ولي الأمر"
                type="email"
                value={inviteEmail}
                onChange={(e) => setInviteEmail(e.target.value)}
                placeholder="patient@example.com"
                disabled={readOnlyInspectMode}
              />

              <label style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', cursor: 'pointer' }}>
                <input
                  type="checkbox"
                  checked={isGuardianAccount}
                  onChange={(e) => setIsGuardianAccount(e.target.checked)}
                />
                <span>حساب ولي أمر (لمريض قاصر)</span>
              </label>

              {isGuardianAccount && (
                <GlassInput
                  label="اسم ولي الأمر الكامل"
                  value={guardianFullName}
                  onChange={(e) => setGuardianFullName(e.target.value)}
                  placeholder="والد المريض"
                />
              )}
            </div>

            <div style={{ display: 'flex', flexWrap: 'wrap', gap: '1rem' }}>
              <label style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', cursor: 'pointer' }}>
                <input
                  type="checkbox"
                  aria-label="موافقة المريض الموثقة على البوابة"
                  checked={consentGiven}
                  onChange={(e) => setConsentGiven(e.target.checked)}
                />
                <span>توثيق موافقة المريض / ولي الأمر على استخدام البوابة</span>
              </label>

              <label style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', cursor: 'pointer' }}>
                <input
                  type="checkbox"
                  aria-label="إصدار رقم سري مطبوع لمرة واحدة"
                  checked={generatePrintedPin}
                  onChange={(e) => setGeneratePrintedPin(e.target.checked)}
                />
                <span>إصدار رقم سري مطبوع لمرة واحدة (بدون بريد إلكتروني)</span>
              </label>
            </div>

            <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.75rem' }}>
              <GlassButton type="submit" variant="prominent">
                <Mail size={16} />
                <span>دعوة المريض إلى البوابة</span>
              </GlassButton>

              {patientStatus.hasAccount && (
                <>
                  <GlassButton type="button" onClick={handleResend}>
                    <RefreshCw size={16} />
                    <span>إعادة إرسال رمز التفعيل</span>
                  </GlassButton>
                  <GlassButton type="button" onClick={handleToggleEnabled}>
                    {effectiveStatus === 'Disabled' ? <ShieldCheck size={16} /> : <ShieldOff size={16} />}
                    <span>{effectiveStatus === 'Disabled' ? 'تفعيل حساب البوابة' : 'تعطيل حساب البوابة'}</span>
                  </GlassButton>
                </>
              )}
            </div>
          </form>

          {patientStatus.generatedPrintedPin && (
            <div
              data-testid="printed-pin-card"
              style={{
                marginTop: '1rem',
                padding: '1rem',
                borderRadius: '0.75rem',
                border: '1px dashed var(--color-accent-teal)',
                background: 'rgba(13, 148, 136, 0.08)',
              }}
            >
              <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', fontWeight: 700, marginBottom: '0.25rem' }}>
                <KeyRound size={18} color="var(--color-accent-teal)" />
                <span>بطاقة الرقم السري المطبوع (يُستخدم لمرة واحدة فقط)</span>
              </div>
              <div dir="ltr" style={{ fontFamily: 'monospace', fontSize: '1.25rem', letterSpacing: '0.15em', fontWeight: 700 }}>
                {patientStatus.generatedPrintedPin}
              </div>
            </div>
          )}

          <div style={{ marginTop: '1rem', display: 'flex', flexWrap: 'wrap', gap: '0.75rem' }}>
            <GlassButton type="button" onClick={handleToggleVisitVisibility}>
              {visitVisible ? <Eye size={16} /> : <EyeOff size={16} />}
              <span>{visitVisible ? 'الزيارة ظاهرة للمريض (اضغط للإخفاء)' : 'الزيارة مخفية عن المريض'}</span>
            </GlassButton>

            <GlassButton type="button" onClick={handleToggleAttachmentVisibility}>
              {attachmentShared ? <Eye size={16} /> : <EyeOff size={16} />}
              <span>{attachmentShared ? 'المرفق مشارك مع المريض' : 'المرفق خاص بالعيادة (اضغط للمشاركة)'}</span>
            </GlassButton>
          </div>
        </GlassCard>
      )}

      <GlassCard style={{ padding: '1.25rem' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '1rem' }}>
          <Settings size={20} color="var(--color-accent-teal)" />
          <h3 style={{ fontSize: '1.0625rem', fontWeight: 700 }}>
            إعدادات بوابة المريض للعيادة
          </h3>
        </div>
        <form onSubmit={handleSaveSettings} style={{ display: 'flex', flexDirection: 'column', gap: '0.875rem' }}>
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: '1.25rem' }}>
            <label style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
              <input
                type="checkbox"
                checked={settings.showDoctorNames}
                disabled={readOnlyInspectMode}
                onChange={(e) => setSettings((s) => ({ ...s, showDoctorNames: e.target.checked }))}
              />
              <span>إظهار أسماء الأطباء في البوابة</span>
            </label>
            <label style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
              <input
                type="checkbox"
                checked={settings.showPricesByDefault}
                disabled={readOnlyInspectMode}
                onChange={(e) => setSettings((s) => ({ ...s, showPricesByDefault: e.target.checked }))}
              />
              <span>إظهار الأسعار والتكلفة للمريض افتراضياً</span>
            </label>
            <label style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
              <input
                type="checkbox"
                checked={settings.defaultVisitVisibility}
                disabled={readOnlyInspectMode}
                onChange={(e) => setSettings((s) => ({ ...s, defaultVisitVisibility: e.target.checked }))}
              />
              <span>إظهار الزيارات المكتملة للمريض تلقائياً</span>
            </label>
          </div>

          <GlassInput
            label="رسالة الترحيب (بالعربية)"
            value={settings.welcomeMessageAr}
            disabled={readOnlyInspectMode}
            onChange={(e) => setSettings((s) => ({ ...s, welcomeMessageAr: e.target.value }))}
          />

          {!readOnlyInspectMode && (
            <div>
              <GlassButton type="submit" variant="prominent">
                حفظ إعدادات البوابة
              </GlassButton>
            </div>
          )}
        </form>
      </GlassCard>

      <GlassCard style={{ padding: '1.25rem' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.875rem' }}>
          <History size={20} color="var(--color-accent-teal)" />
          <h3 style={{ fontSize: '1.0625rem', fontWeight: 700 }}>
            سجل وصول المريض للبوابة (Patient Access Log)
          </h3>
        </div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem', fontSize: '0.875rem' }}>
          {accessLogs.map((log) => (
            <div
              key={log.id}
              style={{
                display: 'flex',
                justifyContent: 'space-between',
                paddingBlock: '0.5rem',
                borderBottom: '1px solid rgba(148, 163, 184, 0.2)',
              }}
            >
              <span>{log.reason} — {log.details ?? log.actorType}</span>
              <span dir="ltr" style={{ color: 'var(--color-text-muted)' }}>
                {new Date(log.timestampUtc).toISOString().slice(0, 16)}
              </span>
            </div>
          ))}
        </div>
      </GlassCard>
    </div>
  );
};

