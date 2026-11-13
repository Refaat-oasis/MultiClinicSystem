import React from 'react';
import { Building2, CheckCircle2, PlusCircle, Clock } from 'lucide-react';
import { GlassCard, GlassButton } from '@clinic/ui';

export interface MyClinicCard {
  clinicId: string;
  name: string;
  slug: string;
  status: string;
  onboardingStatus: string;
  timeZone: string;
  currency: string;
  roundingRule: string;
  modulesEnabled: string[];
  completedSteps: string[];
}

export interface ClinicSwitcherProps {
  clinics: MyClinicCard[];
  activeClinicId: string | null;
  onSwitch: (clinicId: string) => void;
}

export const ClinicSwitcher: React.FC<ClinicSwitcherProps> = ({
  clinics,
  activeClinicId,
  onSwitch,
}) => {
  if (clinics.length === 0) {
    return null;
  }

  return (
    <div
      style={{
        display: 'flex',
        alignItems: 'center',
        gap: '0.5rem',
      }}
    >
      <Building2 size={16} color="var(--color-accent-teal)" />
      <label htmlFor="clinic-switcher-select" style={{ fontSize: '0.8125rem', fontWeight: 600 }}>
        العيادة النشطة:
      </label>
      <select
        id="clinic-switcher-select"
        aria-label="تبديل العيادة النشطة"
        value={activeClinicId ?? clinics[0].clinicId}
        onChange={(e) => onSwitch(e.target.value)}
        style={{
          padding: '0.375rem 0.625rem',
          borderRadius: '0.5rem',
          border: '1px solid var(--color-border-subtle, rgba(148, 163, 184, 0.35))',
          backgroundColor: 'var(--color-surface-card, rgba(255, 255, 255, 0.7))',
          color: 'var(--color-text-primary)',
          fontSize: '0.875rem',
        }}
      >
        {clinics.map((c) => (
          <option key={c.clinicId} value={c.clinicId}>
            {c.name} ({c.onboardingStatus === 'Completed' ? 'مكتملة' : 'قيد التأسيس'})
          </option>
        ))}
      </select>
    </div>
  );
};

export interface MyClinicsScreenProps {
  clinics: MyClinicCard[];
  activeClinicId: string | null;
  quotaLimit?: number | null;
  onCreateClinic: (name: string) => void;
  onSelectClinic: (clinicId: string) => void;
  onResumeOnboarding: (clinicId: string) => void;
  onOpenSettings: (clinicId: string) => void;
  errorMessage?: string | null;
}

export const MyClinicsScreen: React.FC<MyClinicsScreenProps> = ({
  clinics,
  activeClinicId,
  quotaLimit,
  onCreateClinic,
  onSelectClinic,
  onResumeOnboarding,
  onOpenSettings,
  errorMessage,
}) => {
  const [newClinicName, setNewClinicName] = React.useState('');
  const quotaReached =
    typeof quotaLimit === 'number' && quotaLimit > 0 && clinics.length >= quotaLimit;

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!newClinicName.trim() || quotaReached) return;
    onCreateClinic(newClinicName.trim());
    setNewClinicName('');
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '1.5rem' }}>
      <GlassCard>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '1rem' }}>
          <div>
            <h2 style={{ fontSize: '1.25rem', fontWeight: 700 }}>عياداتي</h2>
            <p style={{ fontSize: '0.875rem', color: 'var(--color-text-muted)' }}>
              إدارة العيادات المملوكة لك، متابعة التأسيس، والتبديل بين الفروع.
            </p>
          </div>
          {typeof quotaLimit === 'number' && (
            <span style={{ fontSize: '0.875rem', fontWeight: 600 }}>
              الحد المسموح: {clinics.length} / {quotaLimit}
            </span>
          )}
        </div>

        <form onSubmit={handleSubmit} style={{ display: 'flex', gap: '0.75rem', marginTop: '1rem', flexWrap: 'wrap' }}>
          <input
            aria-label="اسم العيادة الجديدة"
            placeholder="أدخل اسم العيادة الجديدة..."
            value={newClinicName}
            onChange={(e) => setNewClinicName(e.target.value)}
            style={{
              flex: 1,
              minWidth: '220px',
              padding: '0.625rem 0.875rem',
              borderRadius: '0.5rem',
              border: '1px solid rgba(148, 163, 184, 0.35)',
            }}
          />
          <GlassButton type="submit" variant="prominent" disabled={quotaReached}>
            <PlusCircle size={16} />
            إضافة عيادة
          </GlassButton>
        </form>

        {(quotaReached || errorMessage) && (
          <p role="alert" style={{ color: '#ef4444', marginTop: '0.75rem', fontSize: '0.875rem' }}>
            {errorMessage || 'تم الوصول إلى الحد الأقصى لعدد العيادات المسموح به في حسابك (CLINIC_QUOTA_REACHED).'}
          </p>
        )}
      </GlassCard>

      {clinics.length === 0 ? (
        <GlassCard>
          <p style={{ textAlign: 'center', padding: '1rem' }}>
            لا توجد عيادات مسجلة بعد. أضف عيادتك الأولى للبدء في معالج التأسيس.
          </p>
        </GlassCard>
      ) : (
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))', gap: '1rem' }}>
          {clinics.map((clinic) => {
            const isSelected = clinic.clinicId === activeClinicId;
            const isCompleted = clinic.onboardingStatus === 'Completed';
            return (
              <GlassCard key={clinic.clinicId}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: '0.75rem' }}>
                  <div>
                    <h3 style={{ fontSize: '1.0625rem', fontWeight: 700 }}>{clinic.name}</h3>
                    <div style={{ fontSize: '0.8125rem', color: 'var(--color-text-muted)', direction: 'ltr', textAlign: 'start' }}>
                      /c/{clinic.slug}
                    </div>
                  </div>
                  {isCompleted ? (
                    <span style={{ display: 'inline-flex', alignItems: 'center', gap: '0.25rem', fontSize: '0.75rem', color: '#0d9488' }}>
                      <CheckCircle2 size={14} /> مكتملة
                    </span>
                  ) : (
                    <span style={{ display: 'inline-flex', alignItems: 'center', gap: '0.25rem', fontSize: '0.75rem', color: '#d97706' }}>
                      <Clock size={14} /> تأسيس غير مكتمل
                    </span>
                  )}
                </div>

                <p style={{ fontSize: '0.8125rem', color: 'var(--color-text-secondary)', marginBottom: '1rem' }}>
                  الوحدات المفعلة:{' '}
                  {clinic.modulesEnabled.length > 0
                    ? clinic.modulesEnabled.join('، ')
                    : 'بانتظار التفعيل من مشرف المنصة'}
                </p>

                <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
                  <GlassButton
                    variant={isSelected ? 'prominent' : 'default'}
                    onClick={() => onSelectClinic(clinic.clinicId)}
                  >
                    {isSelected ? 'العيادة النشطة' : 'تفعيل العيادة'}
                  </GlassButton>
                  {!isCompleted ? (
                    <GlassButton onClick={() => onResumeOnboarding(clinic.clinicId)}>
                      استكمال التأسيس
                    </GlassButton>
                  ) : (
                    <GlassButton onClick={() => onOpenSettings(clinic.clinicId)}>
                      إعدادات العيادة
                    </GlassButton>
                  )}
                </div>
              </GlassCard>
            );
          })}
        </div>
      )}
    </div>
  );
};

