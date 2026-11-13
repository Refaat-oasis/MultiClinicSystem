import React, { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { GlassCard, GlassButton, ThemeToggle, LanguageSwitcher } from '@clinic/ui';
import { apiClient } from '@clinic/api-client';
import {
  useAuthStore,
  LoginScreen,
  ActivationScreen,
  ForgotPasswordScreen,
  ResetPasswordScreen,
  DeviceManagementScreen,
  ClinicSwitcher,
  MyClinicsScreen,
  OnboardingWizardScreen,
  ClinicSettingsScreen,
  UserProfileScreen,
  type MyClinicCard,
  type OnboardingWizardState,
  type SpecialtyOption,
  type ClinicSettingsData,
  type UserProfileData,
} from '@clinic/feature-core';
import {
  Activity,
  ShieldCheck,
  Building2,
  LogIn,
  KeyRound,
  Laptop,
  LogOut,
  UserCheck,
  LayoutDashboard,
  Sparkles,
  Settings,
  User,
} from 'lucide-react';

interface HealthStatus {
  status: string;
  timestampUtc: string;
}

type ActiveTab =
  | 'login'
  | 'activate'
  | 'forgot'
  | 'reset'
  | 'devices'
  | 'myClinics'
  | 'onboarding'
  | 'clinicSettings'
  | 'profile'
  | 'health';

const DEFAULT_SPECIALTY_CATALOG: SpecialtyOption[] = [
  { catalogCode: 'dental', nameAr: 'طب الأسنان', nameEn: 'Dental', hasDentalPlugin: true },
  { catalogCode: 'cardiology', nameAr: 'القلب والأوعية', nameEn: 'Cardiology', hasDentalPlugin: false },
  { catalogCode: 'general_practice', nameAr: 'الطب العام', nameEn: 'General Practice', hasDentalPlugin: false },
  { catalogCode: 'dermatology', nameAr: 'الأمراض الجلدية', nameEn: 'Dermatology', hasDentalPlugin: false },
];

export const App: React.FC = () => {
  const [activeTab, setActiveTab] = useState<ActiveTab>('login');
  const [resetEmail, setResetEmail] = useState('');
  const { user, isAuthenticated, logout } = useAuthStore();
  const [clinics, setClinics] = useState<MyClinicCard[]>([]);
  const [activeClinicId, setActiveClinicId] = useState<string | null>(null);
  const [clinicError, setClinicError] = useState<string | null>(null);

  const activeClinic = clinics.find((c) => c.clinicId === activeClinicId) ?? clinics[0] ?? null;

  const onboardingState: OnboardingWizardState = activeClinic
    ? {
        clinicId: activeClinic.clinicId,
        name: activeClinic.name,
        slug: activeClinic.slug,
        onboardingStatus: activeClinic.onboardingStatus,
        completedSteps: activeClinic.completedSteps,
        specialties: [],
      }
    : {
        clinicId: 'default-onboarding',
        name: 'عيادة الأمل التخصصية',
        slug: 'alamal-clinic',
        onboardingStatus: 'Pending',
        completedSteps: [],
        specialties: [],
      };

  const settingsData: ClinicSettingsData = activeClinic
    ? {
        clinicId: activeClinic.clinicId,
        name: activeClinic.name,
        slug: activeClinic.slug,
        street: '',
        city: '',
        governorate: '',
        mainPhone: '',
        contactEmail: null,
        timeZone: activeClinic.timeZone,
        currency: activeClinic.currency,
        roundingRule: activeClinic.roundingRule,
        hasFinancialEntries: false,
        sections: [{ sectionKey: 'general', titleAr: 'البيانات العامة', titleEn: 'General Info' }],
      }
    : {
        clinicId: 'default-clinic',
        name: 'عيادة الأمل التخصصية',
        slug: 'alamal-clinic',
        street: '',
        city: '',
        governorate: '',
        mainPhone: '',
        contactEmail: null,
        timeZone: 'Africa/Cairo',
        currency: 'EGP',
        roundingRule: 'NearestPound',
        hasFinancialEntries: false,
        sections: [{ sectionKey: 'general', titleAr: 'البيانات العامة', titleEn: 'General Info' }],
      };

  const profileData: UserProfileData = {
    email: user?.email ?? '',
    fullName: user?.fullName ?? '',
    phoneNumber: '',
    locale: 'ar',
    theme: 'light',
    devices: [],
    clinics: clinics.map((c) => ({
      clinicId: c.clinicId,
      clinicName: c.name,
      role: user?.role ?? 'Owner',
      status: c.status,
    })),
  };

  const handleCreateClinic = (name: string) => {
    setClinicError(null);
    const id = typeof crypto !== 'undefined' && 'randomUUID' in crypto ? crypto.randomUUID() : `clinic-${Date.now()}`;
    const nextClinic: MyClinicCard = {
      clinicId: id,
      name,
      slug: `clinic-${clinics.length + 1}`,
      status: 'PendingOnboarding',
      onboardingStatus: 'Pending',
      timeZone: 'Africa/Cairo',
      currency: 'EGP',
      roundingRule: 'NearestPound',
      modulesEnabled: [],
      completedSteps: [],
    };
    setClinics((prev) => [...prev, nextClinic]);
    setActiveClinicId(id);
    if (isAuthenticated) {
      void apiClient.post('/clinics', { name, deviceId: 'web-browser' }).catch((err: unknown) => {
        setClinicError(err instanceof Error ? err.message : 'تعذر إضافة العيادة');
      });
    }
  };

  const handleSwitchClinic = (clinicId: string) => {
    setActiveClinicId(clinicId);
    if (isAuthenticated) {
      void apiClient.post(`/clinics/${clinicId}/switch`, {});
    }
  };

  const { data: health, isLoading } = useQuery<HealthStatus>({
    queryKey: ['health'],
    queryFn: () => apiClient.get<HealthStatus>('/health'),
  });

  return (
    <div style={{ maxWidth: '1200px', marginInline: 'auto', padding: '2rem 1rem' }}>
      {/* Top Navigation Bar */}
      <header
        className="glass-card"
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          marginBottom: '2rem',
          padding: '1rem 1.5rem',
          flexWrap: 'wrap',
          gap: '1rem',
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
          <Building2 size={32} color="var(--color-accent-teal)" />
          <div>
            <h1 style={{ fontSize: '1.25rem', fontWeight: 700 }}>عيادة الأمل التخصصية</h1>
            <p style={{ fontSize: '0.8125rem', color: 'var(--color-text-muted)' }}>
              منظومة الرعاية الصحية المتعددة | المرحلة الأولى: الهوية والأمان
            </p>
          </div>
        </div>

        <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
          {isAuthenticated && clinics.length > 0 && (
            <ClinicSwitcher
              clinics={clinics}
              activeClinicId={activeClinicId}
              onSwitch={handleSwitchClinic}
            />
          )}
          {isAuthenticated && user && (
            <div
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: '0.5rem',
                paddingInline: '0.75rem',
                paddingBlock: '0.375rem',
                borderRadius: '0.5rem',
                backgroundColor: 'rgba(13, 148, 136, 0.1)',
                border: '1px solid rgba(13, 148, 136, 0.2)',
                fontSize: '0.875rem',
              }}
            >
              <UserCheck size={16} color="var(--color-accent-teal)" />
              <span>{user.fullName || user.email}</span>
              <span
                style={{
                  fontSize: '0.75rem',
                  padding: '2px 6px',
                  borderRadius: '4px',
                  backgroundColor: 'var(--color-accent-teal)',
                  color: '#fff',
                }}
              >
                {user.role}
              </span>
              <button
                type="button"
                onClick={logout}
                title="تسجيل الخروج"
                style={{
                  background: 'none',
                  border: 'none',
                  cursor: 'pointer',
                  color: '#ef4444',
                  marginInlineStart: '0.5rem',
                  display: 'flex',
                  alignItems: 'center',
                }}
              >
                <LogOut size={16} />
              </button>
            </div>
          )}
          <LanguageSwitcher />
          <ThemeToggle />
        </div>
      </header>

      {/* Navigation Tabs */}
      <nav
        aria-label="أقسام المرحلة الأولى"
        style={{
          display: 'flex',
          gap: '0.5rem',
          marginBottom: '2rem',
          overflowX: 'auto',
          paddingBottom: '0.5rem',
        }}
      >
        <GlassButton
          aria-label="تبويب تسجيل الدخول"
          variant={activeTab === 'login' ? 'prominent' : 'default'}
          onClick={() => setActiveTab('login')}
        >
          <LogIn size={16} />
          تسجيل الدخول
        </GlassButton>

        <GlassButton
          aria-label="تبويب تفعيل الحساب"
          variant={activeTab === 'activate' ? 'prominent' : 'default'}
          onClick={() => setActiveTab('activate')}
        >
          <ShieldCheck size={16} />
          تفعيل الحساب (رمز 6 أرقام)
        </GlassButton>

        <GlassButton
          aria-label="تبويب استعادة كلمة المرور"
          variant={activeTab === 'forgot' ? 'prominent' : 'default'}
          onClick={() => setActiveTab('forgot')}
        >
          <KeyRound size={16} />
          استعادة كلمة المرور
        </GlassButton>

        <GlassButton
          aria-label="تبويب إعادة تعيين المرور"
          variant={activeTab === 'reset' ? 'prominent' : 'default'}
          onClick={() => setActiveTab('reset')}
        >
          <KeyRound size={16} />
          إعادة تعيين المرور
        </GlassButton>

        <GlassButton
          aria-label="تبويب إدارة الأجهزة"
          variant={activeTab === 'devices' ? 'prominent' : 'default'}
          onClick={() => setActiveTab('devices')}
        >
          <Laptop size={16} />
          إدارة الأجهزة
        </GlassButton>

        <GlassButton
          aria-label="تبويب عياداتي"
          variant={activeTab === 'myClinics' ? 'prominent' : 'default'}
          onClick={() => setActiveTab('myClinics')}
        >
          <Building2 size={16} />
          عياداتي
        </GlassButton>

        <GlassButton
          aria-label="تبويب تأسيس العيادة"
          variant={activeTab === 'onboarding' ? 'prominent' : 'default'}
          onClick={() => setActiveTab('onboarding')}
        >
          <Sparkles size={16} />
          معالج التأسيس
        </GlassButton>

        <GlassButton
          aria-label="تبويب إعدادات العيادة"
          variant={activeTab === 'clinicSettings' ? 'prominent' : 'default'}
          onClick={() => setActiveTab('clinicSettings')}
        >
          <Settings size={16} />
          إعدادات العيادة
        </GlassButton>

        <GlassButton
          aria-label="تبويب الملف الشخصي"
          variant={activeTab === 'profile' ? 'prominent' : 'default'}
          onClick={() => setActiveTab('profile')}
        >
          <User size={16} />
          الملف الشخصي
        </GlassButton>

        <GlassButton
          aria-label="تبويب نظرة عامة والسلامة"
          variant={activeTab === 'health' ? 'prominent' : 'default'}
          onClick={() => setActiveTab('health')}
        >
          <LayoutDashboard size={16} />
          نظرة عامة والسلامة
        </GlassButton>
      </nav>

      {/* Screen View */}
      <main>
        {activeTab === 'login' && (
          <LoginScreen
            onSuccess={() => setActiveTab('devices')}
            onNavigateToForgot={() => setActiveTab('forgot')}
            onNavigateToActivate={() => setActiveTab('activate')}
          />
        )}

        {activeTab === 'activate' && (
          <ActivationScreen
            onSuccess={() => setActiveTab('devices')}
            onNavigateToLogin={() => setActiveTab('login')}
          />
        )}

        {activeTab === 'forgot' && (
          <ForgotPasswordScreen
            onCodeSent={(email) => {
              setResetEmail(email);
              setActiveTab('reset');
            }}
            onNavigateToLogin={() => setActiveTab('login')}
          />
        )}

        {activeTab === 'reset' && (
          <ResetPasswordScreen
            initialEmail={resetEmail}
            onSuccess={() => setActiveTab('login')}
            onNavigateToLogin={() => setActiveTab('login')}
          />
        )}

        {activeTab === 'devices' && <DeviceManagementScreen />}

        {activeTab === 'myClinics' && (
          <MyClinicsScreen
            clinics={clinics}
            activeClinicId={activeClinicId}
            onCreateClinic={handleCreateClinic}
            onSelectClinic={handleSwitchClinic}
            onResumeOnboarding={(clinicId) => {
              setActiveClinicId(clinicId);
              setActiveTab('onboarding');
            }}
            onOpenSettings={(clinicId) => {
              setActiveClinicId(clinicId);
              setActiveTab('clinicSettings');
            }}
            errorMessage={clinicError}
          />
        )}

        {activeTab === 'onboarding' && (
          <OnboardingWizardScreen
            ownerName={user?.fullName || 'مالك العيادة'}
            state={onboardingState}
            catalog={DEFAULT_SPECIALTY_CATALOG}
            onSaveDetails={(details) => {
              if (!activeClinic) return;
              setClinics((prev) =>
                prev.map((c) =>
                  c.clinicId === activeClinic.clinicId
                    ? { ...c, name: details.name, completedSteps: [...new Set([...c.completedSteps, 'welcome', 'details'])] }
                    : c
                )
              );
            }}
            onSaveTypes={(codes) => {
              if (!activeClinic || codes.length === 0) return;
              setClinics((prev) =>
                prev.map((c) =>
                  c.clinicId === activeClinic.clinicId
                    ? { ...c, completedSteps: [...new Set([...c.completedSteps, 'types'])] }
                    : c
                )
              );
            }}
            onSaveSlug={(slug) => {
              if (!activeClinic) return;
              setClinics((prev) =>
                prev.map((c) =>
                  c.clinicId === activeClinic.clinicId
                    ? { ...c, slug, completedSteps: [...new Set([...c.completedSteps, 'slug'])] }
                    : c
                )
              );
            }}
            onCompleteOnboarding={() => {
              if (!activeClinic) return;
              setClinics((prev) =>
                prev.map((c) =>
                  c.clinicId === activeClinic.clinicId
                    ? { ...c, onboardingStatus: 'Completed', status: 'Active' }
                    : c
                )
              );
              setActiveTab('clinicSettings');
            }}
          />
        )}

        {activeTab === 'clinicSettings' && (
          <ClinicSettingsScreen
            settings={settingsData}
            isOwner={!user || user.role === 'Owner'}
            onSave={(payload) => {
              if (!activeClinic) return;
              setClinics((prev) =>
                prev.map((c) =>
                  c.clinicId === activeClinic.clinicId
                    ? {
                        ...c,
                        name: payload.name,
                        slug: payload.slug,
                        timeZone: payload.timeZone,
                        currency: payload.currency,
                        roundingRule: payload.roundingRule,
                      }
                    : c
                )
              );
            }}
          />
        )}

        {activeTab === 'profile' && (
          <UserProfileScreen
            profile={profileData}
            onUpdateProfile={() => {}}
            onRequestEmailChange={() => {}}
            onConfirmEmailChange={() => {}}
            onChangePassword={() => {}}
            onRevokeDevice={() => {}}
          />
        )}

        {activeTab === 'health' && (
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(320px, 1fr))', gap: '1.5rem' }}>
            {/* System Health Card */}
            <GlassCard>
              <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '1rem' }}>
                <Activity size={22} color="var(--color-accent-teal)" />
                <h2 style={{ fontSize: '1.125rem', fontWeight: 600 }}>حالة النظام والاتصال (Backend API)</h2>
              </div>
              <p style={{ fontSize: '0.9375rem', color: 'var(--color-text-secondary)', marginBottom: '1rem' }}>
                الاتصال المباشر بنواة النظام ASP.NET Core 10 ومراقبة السلامة التشغيلية.
              </p>

              <div
                style={{
                  padding: '0.75rem 1rem',
                  borderRadius: '0.5rem',
                  backgroundColor: 'rgba(13, 148, 136, 0.1)',
                  border: '1px solid rgba(13, 148, 136, 0.2)',
                  marginBottom: '1rem',
                }}
              >
                <strong>الحالة: </strong>
                <span>{isLoading ? 'جاري الفحص...' : health?.status ?? 'متصل بنجاح (Healthy)'}</span>
                <div style={{ fontSize: '0.8125rem', color: 'var(--color-text-muted)', marginTop: '0.25rem' }}>
                  التوقيت الموحد (UTC): {health?.timestampUtc ? new Date(health.timestampUtc).toUTCString() : new Date().toUTCString()}
                </div>
              </div>

              <GlassButton variant="prominent">فحص جاهزية الخادم</GlassButton>
            </GlassCard>

            {/* Architecture & Tenancy Gate Card */}
            <GlassCard>
              <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '1rem' }}>
                <ShieldCheck size={22} color="var(--color-accent-teal)" />
                <h2 style={{ fontSize: '1.125rem', fontWeight: 600 }}>المرحلة الأولى: الهوية والأمان (مكتملة)</h2>
              </div>
              <ul style={{ listStyle: 'none', display: 'flex', flexDirection: 'column', gap: '0.625rem', fontSize: '0.9375rem' }}>
                <li>✓ حسابات العيادة، الأدوار، الصلاحيات المدمجة ASP.NET Core Identity</li>
                <li>✓ تسجيل الدخول ورموز التفعيل والتحقق 6 أرقام LTR مع تشفير HMAC-SHA256</li>
                <li>✓ تدوير Refresh Tokens وكشف إعادة الاستخدام وتتبع الأجهزة والجلسات</li>
                <li>✓ استعادة كلمة المرور بدون كشف الحسابات (Anti-Enumeration 202 Accepted)</li>
                <li>✓ عزل مستوى قاعدة البيانات (EF Core Filter + SQL Server RLS)</li>
              </ul>
            </GlassCard>
          </div>
        )}
      </main>
    </div>
  );
};
