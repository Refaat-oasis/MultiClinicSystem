import React, { useState, useEffect } from 'react';
import { Stethoscope, ShieldCheck } from 'lucide-react';
import { StaffLoginScreen } from './screens/StaffLoginScreen';
import { ClinicDashboardScreen } from './screens/ClinicDashboardScreen';
import { ReceptionQueueScreen } from './screens/ReceptionQueueScreen';
import { PatientEhrScreen } from './screens/PatientEhrScreen';
import { DoctorClinicalSessionScreen } from './screens/DoctorClinicalSessionScreen';
import { DoctorReportsDashboardScreen } from './screens/DoctorReportsDashboardScreen';
import { CostManagementScreen } from './screens/CostManagementScreen';
import { BillingScreen } from './screens/BillingScreen';
import { DailyClosingScreen } from './screens/DailyClosingScreen';
import { OnboardingWizardScreen } from './screens/OnboardingWizardScreen';
import { PatientPortalScreen } from './screens/PatientPortalScreen';
import { ClinicSidebar, ClinicTab } from './components/ClinicSidebar';
import { StatusChip } from './components/StatusChip';
import './styles/clinic-stitch.css';

export const App: React.FC = () => {
  const [staffEmail, setStaffEmail] = useState<string | null>(null);
  const [clinicName, setClinicName] = useState<string>('عيادات الصفوة التخصصية');
  const [activeTab, setActiveTab] = useState<ClinicTab>('dashboard');
  const [isDarkMode, setIsDarkMode] = useState<boolean>(() => {
    return localStorage.getItem('clinic_theme') === 'dark';
  });

  useEffect(() => {
    if (isDarkMode) {
      document.documentElement.setAttribute('data-theme', 'dark');
      localStorage.setItem('clinic_theme', 'dark');
    } else {
      document.documentElement.removeAttribute('data-theme');
      localStorage.setItem('clinic_theme', 'light');
    }
  }, [isDarkMode]);

  const toggleDarkMode = () => {
    setIsDarkMode((prev) => !prev);
  };

  const handleLoginSuccess = (email: string, clinic: string) => {
    setStaffEmail(email);
    setClinicName(clinic);
    setActiveTab('dashboard');
  };

  const handleLogout = () => {
    setStaffEmail(null);
  };

  // If not authenticated, STRICTLY render the Login Screen with ZERO navbar/sidebar!
  if (!staffEmail) {
    return <StaffLoginScreen onLoginSuccess={handleLoginSuccess} />;
  }

  return (
    <div className="clinic-shell">
      {/* Right Sidebar: STRICTLY on the right side of the page (RTL First) */}
      <ClinicSidebar
        activeTab={activeTab}
        onTabChange={setActiveTab}
        staffName={staffEmail}
        clinicName={clinicName}
        isDarkMode={isDarkMode}
        onToggleDarkMode={toggleDarkMode}
        onLogout={handleLogout}
      />

      {/* Main Content Area (Flows to the left of the right sidebar) */}
      <div className="clinic-content-area">
        {/* Top Header Card */}
        <header
          className="clinic-card"
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            marginBottom: '1.25rem',
            padding: '1rem 1.25rem',
            flexWrap: 'wrap',
            gap: '1rem',
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.875rem' }}>
            <div
              style={{
                width: '2.5rem',
                height: '2.5rem',
                borderRadius: 'var(--clinic-radius-card)',
                backgroundColor: 'var(--clinic-accent-soft)',
                color: 'var(--clinic-accent-primary)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                flexShrink: 0,
              }}
            >
              <Stethoscope size={22} />
            </div>

            <div>
              <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                <h1 style={{ fontSize: '1.125rem', fontWeight: 700, color: 'var(--clinic-text-primary)' }}>
                  {clinicName}
                </h1>
                <StatusChip status="success" label="نشطة ومنتظمة" />
              </div>
              <span style={{ fontSize: '0.75rem', color: 'var(--clinic-text-secondary)' }}>
                منظومة الرعاية الصحية والملفات السريرية — تصميم Stitch Clinical Calm
              </span>
            </div>
          </div>

          <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
            <div
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: '0.375rem',
                padding: '0.375rem 0.625rem',
                backgroundColor: 'var(--clinic-bg-subtle)',
                borderRadius: 'var(--clinic-radius-pill)',
                border: '1px solid var(--clinic-border-subtle)',
                fontSize: '0.6875rem',
                fontWeight: 600,
              }}
            >
              <ShieldCheck size={14} color="var(--clinic-accent-primary)" />
              <span>نطاق العيادة المعزول (/alsafwa-specialty)</span>
            </div>
          </div>
        </header>

        {/* Active Tab Screen */}
        <main>
          {activeTab === 'dashboard' && <ClinicDashboardScreen />}
          {activeTab === 'reception' && <ReceptionQueueScreen />}
          {activeTab === 'patients' && <PatientEhrScreen />}
          {activeTab === 'clinical' && <DoctorClinicalSessionScreen />}
          {activeTab === 'doctor-reports' && (
            <DoctorReportsDashboardScreen onOpenCostManagement={() => setActiveTab('costs')} />
          )}
          {activeTab === 'costs' && <CostManagementScreen />}
          {activeTab === 'billing' && <BillingScreen />}
          {activeTab === 'closing' && <DailyClosingScreen />}
          {activeTab === 'onboarding' && <OnboardingWizardScreen />}
          {activeTab === 'portal' && <PatientPortalScreen />}
        </main>
      </div>
    </div>
  );
};
