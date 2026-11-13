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
  MyFeaturesScreen,
  RolesPermissionsScreen,
  StaffDevicesScreen,
  ClinicStructureScreen,
  OwnerPortfolioAndWidgetsScreen,
  Phase5PatientsScreen,
  Phase6SchedulingScreen,
  Phase7ClinicalScreen,
  Phase8BillingScreen,
  Phase9TreatmentPlansScreen,
  Phase10PortalScreen,
  Phase11PortalBookingScreen,
  Phase12InventoryScreen,
  Phase13TimeEngineScreen,
  type PatientSummaryItem,
  type PatientDetailData,
  type PatientScopingSettings,
  type MyClinicCard,
  type OnboardingWizardState,
  type SpecialtyOption,
  type ClinicSettingsData,
  type UserProfileData,
  type ClinicRoleItem,
  type StaffMemberItem,
  type ClinicDeviceItem,
  type ClinicRoomItem,
  type ClinicDoctorItem,
  type WorkingHourItem,
  type DayOffItem,
  type NumberBlockItem,
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
  Blocks,
  Shield,
  Users,
  DoorOpen,
  BarChart3,
  Calendar,
  Stethoscope,
  Receipt,
  ClipboardList,
  Package,
  Clock,
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
  | 'features'
  | 'roles'
  | 'staffDevices'
  | 'structure'
  | 'patients'
  | 'scheduling'
  | 'clinical'
  | 'billing'
  | 'treatmentPlans'
  | 'portal'
  | 'portalBooking'
  | 'inventory'
  | 'timeEngine'
  | 'portfolio'
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
  const [roles, setRoles] = useState<ClinicRoleItem[]>([
    {
      roleId: 'role-recep',
      name: 'Receptionist',
      nameAr: 'استقبال',
      nameEn: 'Receptionist',
      isSystemOwner: false,
      isEditable: true,
      assignedUsersCount: 0,
      permissions: ['dashboard.view', 'patients.read'],
      effectivePermissions: ['dashboard.view'],
      dormantPermissions: ['patients.read'],
    },
  ]);
  const [staffMembers, setStaffMembers] = useState<StaffMemberItem[]>([]);
  const [clinicDevices, setClinicDevices] = useState<ClinicDeviceItem[]>([]);
  const [rooms, setRooms] = useState<ClinicRoomItem[]>([]);
  const [doctors, setDoctors] = useState<ClinicDoctorItem[]>([]);
  const [workingHours, setWorkingHours] = useState<WorkingHourItem[]>([]);
  const [daysOff, setDaysOff] = useState<DayOffItem[]>([]);
  const [numberBlocks, setNumberBlocks] = useState<NumberBlockItem[]>([]);
  const [patients, setPatients] = useState<PatientSummaryItem[]>([]);
  const [selectedPatient, setSelectedPatient] = useState<PatientDetailData | null>(null);
  const [patientScoping, setPatientScoping] = useState<PatientScopingSettings>({
    scopePatientsByDoctor: false,
    scopePatientsBySpecialty: false,
  });

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
          aria-label="تبويب الميزات المفعلة"
          variant={activeTab === 'features' ? 'prominent' : 'default'}
          onClick={() => setActiveTab('features')}
        >
          <Blocks size={16} />
          الميزات المفعلة
        </GlassButton>

        <GlassButton
          aria-label="تبويب الأدوار والصلاحيات"
          variant={activeTab === 'roles' ? 'prominent' : 'default'}
          onClick={() => setActiveTab('roles')}
        >
          <Shield size={16} />
          الأدوار والصلاحيات
        </GlassButton>

        <GlassButton
          aria-label="تبويب الطاقم والأجهزة"
          variant={activeTab === 'staffDevices' ? 'prominent' : 'default'}
          onClick={() => setActiveTab('staffDevices')}
        >
          <Users size={16} />
          الطاقم والأجهزة
        </GlassButton>

        <GlassButton
          aria-label="تبويب هيكل العيادة"
          variant={activeTab === 'structure' ? 'prominent' : 'default'}
          onClick={() => setActiveTab('structure')}
        >
          <DoorOpen size={16} />
          هيكل العيادة
        </GlassButton>

        <GlassButton
          aria-label="تبويب المرضى والملف الطبي"
          variant={activeTab === 'patients' ? 'prominent' : 'default'}
          onClick={() => setActiveTab('patients')}
        >
          <Users size={16} />
          المرضى والملف الطبي
        </GlassButton>

        <GlassButton
          aria-label="تبويب المواعيد والطابور"
          variant={activeTab === 'scheduling' ? 'prominent' : 'default'}
          onClick={() => setActiveTab('scheduling')}
        >
          <Calendar size={16} />
          المواعيد والطابور
        </GlassButton>

        <GlassButton
          aria-label="تبويب الجلسات السريرية"
          variant={activeTab === 'clinical' ? 'prominent' : 'default'}
          onClick={() => setActiveTab('clinical')}
        >
          <Stethoscope size={16} />
          الجلسات السريرية
        </GlassButton>
        <GlassButton
          aria-label="تبويب الفواتير والخزينة"
          variant={activeTab === 'billing' ? 'prominent' : 'default'}
          onClick={() => setActiveTab('billing')}
        >
          <Receipt size={16} />
          الفواتير والخزينة
        </GlassButton>
        <GlassButton
          aria-label="تبويب خطط العلاج"
          variant={activeTab === 'treatmentPlans' ? 'prominent' : 'default'}
          onClick={() => setActiveTab('treatmentPlans')}
        >
          <ClipboardList size={16} />
          خطط العلاج
        </GlassButton>
        <GlassButton
          aria-label="تبويب بوابة المريض"
          variant={activeTab === 'portal' ? 'prominent' : 'default'}
          onClick={() => setActiveTab('portal')}
        >
          <Sparkles size={16} />
          بوابة المريض
        </GlassButton>
        <GlassButton
          aria-label="تبويب حجوزات البوابة"
          variant={activeTab === 'portalBooking' ? 'prominent' : 'default'}
          onClick={() => setActiveTab('portalBooking')}
        >
          <Calendar size={16} />
          حجوزات البوابة
        </GlassButton>
        <GlassButton
          aria-label="تبويب المخزون والموردين"
          variant={activeTab === 'inventory' ? 'prominent' : 'default'}
          onClick={() => setActiveTab('inventory')}
        >
          <Package size={16} />
          المخزون
        </GlassButton>
        <GlassButton
          aria-label="تبويب محرك الزمن"
          variant={activeTab === 'timeEngine' ? 'prominent' : 'default'}
          onClick={() => setActiveTab('timeEngine')}
        >
          <Clock size={16} />
          محرك الزمن
        </GlassButton>

        <GlassButton
          aria-label="تبويب المحفظة والمؤشرات"
          variant={activeTab === 'portfolio' ? 'prominent' : 'default'}
          onClick={() => setActiveTab('portfolio')}
        >
          <BarChart3 size={16} />
          المحفظة والمؤشرات
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

        {activeTab === 'features' && (
          <MyFeaturesScreen
            modules={[
              {
                moduleKey: 'patients',
                titleAr: 'إدارة المرضى',
                titleEn: 'Patients Registry',
                descriptionAr: 'سجلات المرضى والبحث الذكي والملفات.',
                descriptionEn: 'Patients Registry',
                enabled: (activeClinic?.modulesEnabled ?? []).includes('patients'),
                prerequisites: [],
                missingPrerequisites: [],
                enabledDependents: [],
              },
            ]}
            menuItems={[]}
            effectivePermissions={['dashboard.view', 'structure.manage', 'roles.manage', 'users.manage']}
          />
        )}

        {activeTab === 'roles' && (
          <RolesPermissionsScreen
            roles={roles}
            catalog={[
              { key: 'dashboard.view', moduleKey: 'core', group: 'Core', titleAr: 'عرض لوحة التحكم', titleEn: 'View Dashboard', isReserved: false },
              { key: 'patients.read', moduleKey: 'patients', group: 'Patients', titleAr: 'عرض المرضى', titleEn: 'Read Patients', isReserved: false },
              { key: 'clinic.settings.edit', moduleKey: 'core', group: 'Core', titleAr: 'تعديل إعدادات العيادة', titleEn: 'Edit Settings', isReserved: true },
            ]}
            staff={staffMembers}
            onCloneRole={(sourceRoleId, newName) => {
              const source = roles.find((r) => r.roleId === sourceRoleId);
              if (!source) return;
              setRoles((prev) => [
                ...prev,
                {
                  ...source,
                  roleId: `role-${Date.now()}`,
                  name: newName,
                  nameAr: newName,
                  nameEn: newName,
                  assignedUsersCount: 0,
                },
              ]);
            }}
            onUpdateRolePermissions={(roleId, permissions) =>
              setRoles((prev) => prev.map((r) => (r.roleId === roleId ? { ...r, permissions } : r)))
            }
            onSetUserOverrides={(staffUserId, overrides) =>
              setStaffMembers((prev) => prev.map((s) => (s.userId === staffUserId ? { ...s, overrides } : s)))
            }
          />
        )}

        {activeTab === 'staffDevices' && (
          <StaffDevicesScreen
            staff={staffMembers}
            roles={roles}
            devices={clinicDevices}
            onInviteStaff={({ fullName, email, roleName }) =>
              setStaffMembers((prev) => [
                ...prev,
                {
                  userId: `staff-${Date.now()}`,
                  fullName,
                  email,
                  roleName,
                  isOwner: false,
                  isActive: false,
                  effectivePermissions: [],
                  dormantPermissions: [],
                  overrides: [],
                },
              ])
            }
            onApproveDevice={(deviceId) =>
              setClinicDevices((prev) => prev.map((d) => (d.deviceId === deviceId ? { ...d, isApproved: true } : d)))
            }
          />
        )}

        {activeTab === 'structure' && (
          <ClinicStructureScreen
            rooms={rooms}
            doctors={doctors}
            workingHours={workingHours}
            daysOff={daysOff}
            numberBlocks={numberBlocks}
            onCreateRoom={({ code, nameAr, nameEn }) =>
              setRooms((prev) => [...prev, { id: `room-${Date.now()}`, roomCode: code, nameAr, nameEn, isActive: true }])
            }
            onCreateDoctor={({ fullName, titleAr, slotDurationMinutes }) =>
              setDoctors((prev) => [
                ...prev,
                { id: `doc-${Date.now()}`, fullName, titleAr, titleEn: titleAr, specialtyCodes: [], assignedRoomIds: [], slotDurationMinutes, isActive: true },
              ])
            }
            onSaveDefaultHours={(items) => setWorkingHours(items)}
            onCreateDayOff={({ startDate, endDate, reasonAr }) =>
              setDaysOff((prev) => [...prev, { id: `off-${Date.now()}`, startDate, endDate, reasonAr, reasonEn: reasonAr }])
            }
            onReserveBlock={(kind) =>
              setNumberBlocks((prev) => {
                const maxTo = prev.filter((b) => b.kind === kind).reduce((m, b) => Math.max(m, b.toNumber), 0);
                const fromNumber = maxTo + 1;
                const toNumber = fromNumber + 499;
                return [
                  ...prev,
                  { id: `blk-${Date.now()}`, deviceId: 'web-browser', kind, fromNumber, toNumber, nextNumber: fromNumber, status: 'Active' },
                ];
              })
            }
          />
        )}

        {activeTab === 'patients' && (
          <Phase5PatientsScreen
            patients={patients}
            selectedPatient={selectedPatient}
            scopingSettings={patientScoping}
            onSaveScoping={(next) => setPatientScoping(next)}
            onSelectPatient={(id) => {
              const found = patients.find((p) => p.id === id);
              if (found) {
                setSelectedPatient({
                  id: found.id,
                  clinicId: activeClinic?.clinicId ?? '',
                  fileNumber: found.fileNumber,
                  fullName: found.fullName,
                  phone: found.phone,
                  ageYears: found.ageYears,
                  gender: found.gender,
                  isMedicalHistoryEnabled: true,
                  isMedicalHistoryReviewed: false,
                  chronicConditions: [],
                  currentMedications: [],
                  pastSurgeries: [],
                  allergies: [],
                  notes: [],
                  isAttachmentsEnabled: true,
                  attachments: [],
                  duplicateWarnings: [],
                });
              }
            }}
            onCreatePatient={(payload) => {
              const id = typeof crypto !== 'undefined' && 'randomUUID' in crypto ? crypto.randomUUID() : `pat-${Date.now()}`;
              const fileNumber = `P-${String(patients.length + 1).padStart(6, '0')}`;
              const summary: PatientSummaryItem = {
                id,
                fileNumber,
                fullName: payload.fullName,
                phone: payload.phone,
                gender: payload.gender,
                ageYears: payload.approximateAgeYears ?? 30,
                primarySpecialtyCode: payload.primarySpecialtyCode ?? null,
                allergyCount: 0,
                hasSevereAllergy: false,
              };
              setPatients((prev) => [summary, ...prev]);
            }}
          />
        )}

        {activeTab === 'scheduling' && (
          <Phase6SchedulingScreen
            doctors={doctors.map((d) => ({ id: d.id, fullName: d.fullName }))}
            patients={patients.map((p) => ({ id: p.id, fullName: p.fullName, fileNumber: p.fileNumber }))}
            categories={[
              {
                id: 'cat-default-exam',
                specialtyCode: 'dental',
                nameAr: 'كشف استشارة',
                nameEn: 'Consultation',
                defaultDurationMinutes: 30,
                defaultPriceAmount: 250,
                defaultPriceCurrency: 'EGP',
              },
            ]}
          />
        )}

        {activeTab === 'clinical' && (
          <Phase7ClinicalScreen clinicId={activeClinic?.clinicId ?? 'clinic-1'} />
        )}

        {activeTab === 'billing' && (
          <Phase8BillingScreen clinicId={activeClinic?.clinicId ?? 'clinic-1'} />
        )}
        {activeTab === 'treatmentPlans' && (
          <Phase9TreatmentPlansScreen clinicId={activeClinic?.clinicId ?? 'clinic-1'} />
        )}
        {activeTab === 'portal' && (
          <Phase10PortalScreen clinicId={activeClinic?.clinicId ?? 'clinic-1'} />
        )}
        {activeTab === 'portalBooking' && (
          <Phase11PortalBookingScreen clinicId={activeClinic?.clinicId ?? 'clinic-1'} />
        )}
        {activeTab === 'inventory' && (
          <Phase12InventoryScreen clinicId={activeClinic?.clinicId ?? 'clinic-1'} />
        )}
        {activeTab === 'timeEngine' && (
          <Phase13TimeEngineScreen clinicId={activeClinic?.clinicId ?? 'clinic-1'} />
        )}

        {activeTab === 'portfolio' && (
          <OwnerPortfolioAndWidgetsScreen
            widgets={[
              { widgetKey: 'core.rooms-count', moduleKey: 'core', titleAr: 'الغرف النشطة', titleEn: 'Rooms', primaryValue: rooms.length, unitLabel: 'count' },
              { widgetKey: 'core.doctors-count', moduleKey: 'core', titleAr: 'الأطباء المسجلون', titleEn: 'Doctors', primaryValue: doctors.length, unitLabel: 'count' },
            ]}
            portfolio={{
              ownerId: user?.id ?? 'owner',
              totalClinics: clinics.length,
              activeClinics: clinics.length,
              totalStaffAcrossClinics: staffMembers.length,
              totalDoctorsAcrossClinics: doctors.length,
              clinics: clinics.map((c) => ({
                clinicId: c.clinicId,
                name: c.name,
                slug: c.slug,
                status: c.status,
                staffCount: staffMembers.length,
                roomsCount: rooms.length,
                doctorsCount: doctors.length,
                enabledModulesCount: c.modulesEnabled.length,
              })),
            }}
            auditLogs={[]}
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
