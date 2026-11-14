import React, { useState } from 'react';
import { Shield, ShieldAlert, Server } from 'lucide-react';
import { SuperAdminLoginScreen } from './screens/SuperAdminLoginScreen';
import { PlatformDashboardScreen } from './screens/PlatformDashboardScreen';
import { ClinicModuleMatrixScreen } from './screens/ClinicModuleMatrixScreen';
import { ClinicOwnersDirectoryScreen } from './screens/ClinicOwnersDirectoryScreen';
import { AdminNavbar, AdminTab } from './components/AdminNavbar';
import { StatusChip } from './components/StatusChip';
import './styles/admin-stitch.css';

export const App: React.FC = () => {
  const [adminEmail, setAdminEmail] = useState<string | null>(null);
  const [activeTab, setActiveTab] = useState<AdminTab>('dashboard');

  const handleLoginSuccess = (email: string) => {
    setAdminEmail(email);
    setActiveTab('dashboard');
  };

  const handleLogout = () => {
    setAdminEmail(null);
  };

  // If not logged in, STRICTLY render Login Screen with NO navbar!
  if (!adminEmail) {
    return <SuperAdminLoginScreen onLoginSuccess={handleLoginSuccess} />;
  }

  return (
    <div style={{ maxWidth: '1280px', margin: '0 auto', padding: '1.5rem 1rem' }}>
      {/* Top Sovereign Node Bar */}
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          padding: '0.5rem 1rem',
          borderRadius: 'var(--admin-radius-input)',
          backgroundColor: 'var(--admin-bg-surface)',
          border: '1px solid var(--admin-border-subtle)',
          marginBottom: '1rem',
          fontSize: '0.75rem',
          color: 'var(--admin-text-secondary)',
          flexWrap: 'wrap',
          gap: '0.5rem',
          boxShadow: 'var(--admin-shadow-sm)',
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
          <span
            style={{
              width: '8px',
              height: '8px',
              borderRadius: '50%',
              backgroundColor: 'var(--admin-accent-primary)',
              display: 'inline-block',
            }}
          />
          <span style={{ fontFamily: 'monospace', fontWeight: 600 }}>Sovereign Cloud Node // KSA-SEC-01</span>
          <span>•</span>
          <span>ISO-27001 & SOC2 Type II Certified</span>
        </div>
        <StatusChip status="info" label="نطاق المشرفين المعزول [Platform Host]" showDot={true} />
      </div>

      {/* Main Admin Header */}
      <header
        className="admin-card"
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
              borderRadius: 'var(--admin-radius-card)',
              backgroundColor: 'var(--admin-accent-soft)',
              color: 'var(--admin-accent-primary)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              flexShrink: 0,
            }}
          >
            <ShieldAlert size={28} />
          </div>

          <div>
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.625rem' }}>
              <h1 style={{ fontSize: '1.25rem', fontWeight: 700, color: 'var(--admin-text-primary)' }}>
                إدارة المنصة المركزية (Platform Admin)
              </h1>
              <span
                style={{
                  fontSize: '0.6875rem',
                  fontFamily: 'monospace',
                  fontWeight: 700,
                  paddingInline: '0.5rem',
                  paddingBlock: '0.125rem',
                  borderRadius: 'var(--admin-radius-pill)',
                  backgroundColor: 'var(--admin-accent-primary)',
                  color: '#ffffff',
                }}
              >
                Super Admin
              </span>
            </div>
            <p style={{ fontSize: '0.8125rem', color: 'var(--admin-text-secondary)', marginTop: '0.25rem' }}>
              لوحة التحكم المركزية المشفرة — معزولة أمنياً عن نطاقات العيادات والمرضى
            </p>
          </div>
        </div>
      </header>

      {/* Navbar: STRICTLY rendered after login */}
      <AdminNavbar
        activeTab={activeTab}
        onTabChange={setActiveTab}
        adminEmail={adminEmail}
        onLogout={handleLogout}
      />

      {/* Active Tab Screen */}
      <main>
        {activeTab === 'dashboard' && <PlatformDashboardScreen />}
        {activeTab === 'modules' && <ClinicModuleMatrixScreen />}
        {activeTab === 'owners' && <ClinicOwnersDirectoryScreen />}
        {activeTab === 'security' && (
          <div className="admin-card">
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem', marginBottom: '1rem' }}>
              <Server size={24} color="var(--admin-accent-primary)" />
              <h2 style={{ fontSize: '1.125rem', fontWeight: 700 }}>عزل نطاق المضيف (Host Isolation Architecture)</h2>
            </div>
            <p style={{ color: 'var(--admin-text-secondary)', lineHeight: 1.7, marginBottom: '1.5rem' }}>
              تعمل لوحة التحكم هذه حصرياً على مضيف المشرفين (مثل: <code>admin.clinic.local</code>). كافة مسارات{' '}
              <code>/platform/**</code> غير مرئية لنطاقات العيادات وبوابة المرضى وتُرجع <code>404 Not Found</code>{' '}
              تلقائياً عند محاولة الوصول إليها من أي مضيف آخر.
            </p>

            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))', gap: '1rem' }}>
              <div
                style={{
                  padding: '1.25rem',
                  borderRadius: 'var(--admin-radius-input)',
                  backgroundColor: 'var(--admin-bg-subtle)',
                  border: '1px solid var(--admin-border-subtle)',
                }}
              >
                <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.5rem' }}>
                  <Shield size={18} color="var(--admin-accent-primary)" />
                  <strong style={{ fontSize: '0.9375rem' }}>المصادقة الثلاثية (3FA Security)</strong>
                </div>
                <p style={{ fontSize: '0.8125rem', color: 'var(--admin-text-secondary)' }}>
                  رمز ساعي مدته 3600 ثانية + مفاتيح استرداد لمرة واحدة + تحقق إضافي (Step-Up) عند تعديل صلاحيات الموديلات.
                </p>
              </div>

              <div
                style={{
                  padding: '1.25rem',
                  borderRadius: 'var(--admin-radius-input)',
                  backgroundColor: 'var(--admin-bg-subtle)',
                  border: '1px solid var(--admin-border-subtle)',
                }}
              >
                <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.5rem' }}>
                  <Shield size={18} color="var(--admin-status-success)" />
                  <strong style={{ fontSize: '0.9375rem' }}>سياسات RLS و Audit Logging</strong>
                </div>
                <p style={{ fontSize: '0.8125rem', color: 'var(--admin-text-secondary)' }}>
                  أي وصول استثنائي لبيانات المرضى من المشرف الأعلى يمر حصراً عبر <code>IPlatformAccess</code> ويوثق في سجل التدقيق.
                </p>
              </div>
            </div>
          </div>
        )}
      </main>
    </div>
  );
};
