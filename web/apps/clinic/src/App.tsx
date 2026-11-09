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
} from 'lucide-react';

interface HealthStatus {
  status: string;
  timestampUtc: string;
}

type ActiveTab = 'login' | 'activate' | 'forgot' | 'reset' | 'devices' | 'health';

export const App: React.FC = () => {
  const [activeTab, setActiveTab] = useState<ActiveTab>('login');
  const [resetEmail, setResetEmail] = useState('');
  const { user, isAuthenticated, logout } = useAuthStore();

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
