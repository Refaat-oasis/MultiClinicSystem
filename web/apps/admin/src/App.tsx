import React, { useState } from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { GlassCard, GlassButton, ThemeToggle, LanguageSwitcher, StatusChip } from '@clinic/ui';
import { ShieldAlert, Server, KeyRound, LayoutDashboard, Users, Building2, Eye, FileSearch, LogOut } from 'lucide-react';
import { AdminLoginScreen, PlatformLoginResponse } from './AdminLoginScreen';
import { AdminEnrollmentScreen, TotpEnrollmentCompletedResponse } from './AdminEnrollmentScreen';
import { PlatformDashboardScreen } from './PlatformDashboardScreen';
import { OwnersManagementScreen } from './OwnersManagementScreen';
import { ClinicDirectoryScreen } from './ClinicDirectoryScreen';
import { InspectClinicShell } from './InspectClinicShell';
import { PlatformAuditScreen } from './PlatformAuditScreen';

type AdminTab = 'overview' | 'login' | 'enroll' | 'dashboard' | 'owners' | 'clinics' | 'inspect' | 'audit';

const adminQueryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: false,
      refetchOnWindowFocus: false,
    },
  },
});

export const App: React.FC = () => {
  const [activeTab, setActiveTab] = useState<AdminTab>('overview');
  const [adminEmail, setAdminEmail] = useState<string | null>(null);
  const [setupKey, setSetupKey] = useState<string>('DEMO3600HOURLYTOTPSECRET');
  const [otpAuthUri, setOtpAuthUri] = useState<string | null>(null);
  const [inspectClinicId, setInspectClinicId] = useState<string | null>(null);

  const handleLoginSuccess = (response: PlatformLoginResponse) => {
    setAdminEmail(response.email);
    if (response.requiresEnrollment && response.manualSetupKey) {
      setSetupKey(response.manualSetupKey);
      setOtpAuthUri(response.otpAuthUri ?? null);
      setActiveTab('enroll');
    } else {
      setActiveTab('dashboard');
    }
  };

  const handleEnrollmentComplete = (response: TotpEnrollmentCompletedResponse) => {
    setAdminEmail(response.email);
    setActiveTab('dashboard');
  };

  const handleInspectClinic = (clinicId: string) => {
    setInspectClinicId(clinicId);
    setActiveTab('inspect');
  };

  const navItems: { id: AdminTab; label: string; icon: React.ReactNode }[] = [
    { id: 'overview', label: 'نظرة عامة', icon: <Server size={16} /> },
    { id: 'login', label: 'تسجيل الدخول (2FA)', icon: <KeyRound size={16} /> },
    { id: 'enroll', label: 'الإعداد الإلزامي', icon: <ShieldAlert size={16} /> },
    { id: 'dashboard', label: 'لوحة المنصة', icon: <LayoutDashboard size={16} /> },
    { id: 'owners', label: 'الملاك', icon: <Users size={16} /> },
    { id: 'clinics', label: 'العيادات', icon: <Building2 size={16} /> },
    { id: 'inspect', label: 'وضع الفحص', icon: <Eye size={16} /> },
    { id: 'audit', label: 'سجل التدقيق', icon: <FileSearch size={16} /> },
  ];

  return (
    <QueryClientProvider client={adminQueryClient}>
      <div style={{ maxWidth: '1200px', marginInline: 'auto', padding: '1.5rem 1rem' }}>
        {/* Stitch Top Sovereign Node Bar */}
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            padding: '0.5rem 1rem',
            borderRadius: 'var(--radius-input)',
            backgroundColor: 'var(--color-surface)',
            border: '1px solid var(--color-border)',
            marginBottom: '1rem',
            fontSize: '0.75rem',
            color: 'var(--color-text-secondary)',
            flexWrap: 'wrap',
            gap: '0.5rem',
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
            <span style={{ width: '8px', height: '8px', borderRadius: '50%', backgroundColor: 'var(--color-accent-teal)', display: 'inline-block' }} />
            <span style={{ fontFamily: 'monospace', fontWeight: 600 }}>Sovereign Cloud Node // KSA-SEC-01</span>
            <span>•</span>
            <span>ISO-27001 & SOC2 Type II Certified</span>
          </div>
          <StatusChip status="info" label="نطاق المشرفين المعزول [Platform Host]" showDot={true} />
        </div>

        <header
          className="glass-card"
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            marginBottom: '1.5rem',
            padding: '1rem 1.5rem',
            flexWrap: 'wrap',
            gap: '1rem',
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.875rem' }}>
            <div
              style={{
                width: '2.75rem',
                height: '2.75rem',
                borderRadius: 'var(--radius-card)',
                backgroundColor: 'var(--color-accent-teal-soft)',
                color: 'var(--color-accent-teal)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                flexShrink: 0,
              }}
            >
              <ShieldAlert size={28} />
            </div>
            <div>
              <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                <h1 style={{ fontSize: '1.25rem', fontWeight: 700, margin: 0, color: 'var(--color-text-primary)' }}>
                  إدارة المنصة المركزية (Platform Admin)
                </h1>
                <span
                  style={{
                    fontSize: '0.6875rem',
                    fontFamily: 'monospace',
                    fontWeight: 700,
                    paddingInline: '0.4rem',
                    paddingBlock: '0.1rem',
                    borderRadius: 'var(--radius-pill)',
                    backgroundColor: 'var(--color-accent-teal)',
                    color: '#ffffff',
                  }}
                >
                  Super Admin
                </span>
              </div>
              <p style={{ fontSize: '0.8125rem', color: 'var(--color-text-secondary)', margin: '0.25rem 0 0' }}>
                {adminEmail ? `متصل: ${adminEmail}` : 'Super Admin Control Plane — المعزولة أمنياً عن العيادات والمرضى'}
              </p>
            </div>
          </div>

          <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem', flexWrap: 'wrap' }}>
            {adminEmail && (
              <GlassButton onClick={() => { setAdminEmail(null); setActiveTab('login'); }} variant="danger">
                <LogOut size={16} />
                <span>خروج</span>
              </GlassButton>
            )}
            <ThemeToggle />
            <LanguageSwitcher />
          </div>
        </header>

        <nav
          aria-label="Platform Admin Navigation"
          style={{
            display: 'flex',
            gap: '0.5rem',
            flexWrap: 'wrap',
            marginBottom: '1.5rem',
          }}
        >
          {navItems.map((item) => (
            <GlassButton
              key={item.id}
              variant={activeTab === item.id ? 'prominent' : 'default'}
              onClick={() => setActiveTab(item.id)}
            >
              {item.icon}
              <span>{item.label}</span>
            </GlassButton>
          ))}
        </nav>

        <main style={{ display: 'grid', gap: '1.5rem' }}>
          {activeTab === 'overview' && (
            <GlassCard>
              <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem', marginBottom: '1rem' }}>
                <Server size={24} color="var(--color-accent-teal)" />
                <h2 style={{ fontSize: '1.125rem', fontWeight: 600 }}>نطاق المضيف (Host Isolation)</h2>
              </div>
              <p style={{ color: 'var(--color-text-secondary)', marginBottom: '1.5rem', lineHeight: 1.6 }}>
                لوحة التحكم هذه متاحة حصرياً على مضيف المشرفين (مثال: <code>admin.clinic.local</code>). كافة مسارات{' '}
                <code>/platform/**</code> غير مرئية تماماً لمضيفي العيادات والمرضى وتُرجع <code>404</code>.
              </p>

              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))', gap: '1rem' }}>
                <div
                  style={{
                    padding: '1rem',
                    borderRadius: '0.75rem',
                    backgroundColor: 'var(--glass-surface)',
                    border: '1px solid rgba(255, 255, 255, 0.2)',
                  }}
                >
                  <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.5rem' }}>
                    <KeyRound size={18} color="var(--color-accent-teal)" />
                    <strong>التحقق الآمن للمشرفين (2FA)</strong>
                  </div>
                  <p style={{ fontSize: '0.875rem', color: 'var(--color-text-muted)' }}>
                    مصادقة ثلاثية برمز ساعي كل 3600 ثانية + مفاتيح استرداد لمرة واحدة + التحقق الإضافي (Step-Up).
                  </p>
                </div>
              </div>
            </GlassCard>
          )}

          {activeTab === 'login' && <AdminLoginScreen onLoginSuccess={handleLoginSuccess} />}

          {activeTab === 'enroll' && (
            <AdminEnrollmentScreen
              manualSetupKey={setupKey}
              otpAuthUri={otpAuthUri}
              onEnrollmentComplete={handleEnrollmentComplete}
            />
          )}

          {activeTab === 'dashboard' && <PlatformDashboardScreen />}

          {activeTab === 'owners' && <OwnersManagementScreen />}

          {activeTab === 'clinics' && <ClinicDirectoryScreen onInspectClinic={handleInspectClinic} />}

          {activeTab === 'inspect' && <InspectClinicShell clinicId={inspectClinicId} />}

          {activeTab === 'audit' && <PlatformAuditScreen />}
        </main>
      </div>
    </QueryClientProvider>
  );
};
