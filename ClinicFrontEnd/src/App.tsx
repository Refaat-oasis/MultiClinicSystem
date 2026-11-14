import React, { useState } from 'react';
import { Stethoscope, ShieldCheck } from 'lucide-react';
import { StaffLoginScreen } from './screens/StaffLoginScreen';
import { ClinicDashboardScreen } from './screens/ClinicDashboardScreen';
import { ReceptionQueueScreen } from './screens/ReceptionQueueScreen';
import { PatientEhrScreen } from './screens/PatientEhrScreen';
import { DoctorClinicalSessionScreen } from './screens/DoctorClinicalSessionScreen';
import { BillingScreen } from './screens/BillingScreen';
import { DailyClosingScreen } from './screens/DailyClosingScreen';
import { OnboardingWizardScreen } from './screens/OnboardingWizardScreen';
import { PatientPortalScreen } from './screens/PatientPortalScreen';
import { ClinicNavbar, ClinicTab } from './components/ClinicNavbar';
import { StatusChip } from './components/StatusChip';
import './styles/clinic-stitch.css';

export const App: React.FC = () => {
  const [staffEmail, setStaffEmail] = useState<string | null>(null);
  const [clinicName, setClinicName] = useState<string>('عيادات الصفوة التخصصية');
  const [activeTab, setActiveTab] = useState<ClinicTab>('dashboard');

  const handleLoginSuccess = (email: string, clinic: string) => {
    setStaffEmail(email);
    setClinicName(clinic);
    setActiveTab('dashboard');
  };

  const handleLogout = () => {
    setStaffEmail(null);
  };

  // If not authenticated, STRICTLY render the Login Screen with ZERO navbar!
  if (!staffEmail) {
    return <StaffLoginScreen onLoginSuccess={handleLoginSuccess} />;
  }

  return (
    <div style={{ maxWidth: '1280px', margin: '0 auto', padding: '1.5rem 1rem' }}>
      {/* Top Clinic Header */}
      <header
        className="clinic-card"
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          marginBottom: '1.25rem',
          padding: '1.25rem 1.5rem',
          flexWrap: 'wrap',
          gap: '1rem',
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: '1rem' }}>
          <div
            style={{
              width: '3rem',
              height: '3rem',
              borderRadius: 'var(--clinic-radius-card)',
              backgroundColor: 'var(--clinic-accent-soft)',
              color: 'var(--clinic-accent-primary)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              flexShrink: 0,
            }}
          >
            <Stethoscope size={26} />
          </div>

          <div>
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.625rem' }}>
              <h1 style={{ fontSize: '1.25rem', fontWeight: 700, color: 'var(--clinic-text-primary)' }}>
                {clinicName}
              </h1>
              <StatusChip status="success" label="نشطة ومنتظمة" />
            </div>
            <p style={{ fontSize: '0.8125rem', color: 'var(--clinic-text-secondary)', marginTop: '0.25rem' }}>
              منظومة الرعاية الصحية والملفات السريرية — تصميم Stitch Clinical Calm
            </p>
          </div>
        </div>

        <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
          <div
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: '0.5rem',
              padding: '0.375rem 0.75rem',
              backgroundColor: 'var(--clinic-bg-subtle)',
              borderRadius: 'var(--clinic-radius-pill)',
              border: '1px solid var(--clinic-border-subtle)',
              fontSize: '0.75rem',
              fontWeight: 600,
            }}
          >
            <ShieldCheck size={16} color="var(--clinic-accent-primary)" />
            <span>نطاق العيادة المعزول (/alsafwa-specialty)</span>
          </div>
        </div>
      </header>

      {/* Navbar: STRICTLY rendered after login */}
      <ClinicNavbar
        activeTab={activeTab}
        onTabChange={setActiveTab}
        staffName={staffEmail}
        clinicName={clinicName}
        onLogout={handleLogout}
      />

      {/* Active Tab Screen */}
      <main>
        {activeTab === 'dashboard' && <ClinicDashboardScreen />}
        {activeTab === 'reception' && <ReceptionQueueScreen />}
        {activeTab === 'patients' && <PatientEhrScreen />}
        {activeTab === 'clinical' && <DoctorClinicalSessionScreen />}
        {activeTab === 'billing' && <BillingScreen />}
        {activeTab === 'closing' && <DailyClosingScreen />}
        {activeTab === 'onboarding' && <OnboardingWizardScreen />}
        {activeTab === 'portal' && <PatientPortalScreen />}
      </main>
    </div>
  );
};
