import React from 'react';
import { GlassCard, GlassButton, ThemeToggle, LanguageSwitcher } from '@clinic/ui';
import { ShieldAlert, Server, Layers, Lock } from 'lucide-react';

export const App: React.FC = () => {
  return (
    <div style={{ maxWidth: '1200px', marginInline: 'auto', padding: '2rem 1rem' }}>
      {/* Header */}
      <header
        className="glass-card"
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          marginBottom: '2rem',
          padding: '1rem 1.5rem',
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
          <ShieldAlert size={28} color="var(--color-accent-teal)" />
          <div>
            <h1 style={{ fontSize: '1.25rem', fontWeight: 700 }}>إدارة المنصة المركزية (Platform Admin)</h1>
            <p style={{ fontSize: '0.8125rem', color: 'var(--color-text-muted)' }}>
              الموقع الإداري المستقل | مسار الحماية /platform/** فقط
            </p>
          </div>
        </div>

        <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
          <LanguageSwitcher />
          <ThemeToggle />
        </div>
      </header>

      {/* Admin Modules Grid */}
      <main style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(320px, 1fr))', gap: '1.5rem' }}>
        <GlassCard>
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '1rem' }}>
            <Server size={22} color="var(--color-accent-teal)" />
            <h2 style={{ fontSize: '1.125rem', fontWeight: 600 }}>نطاق المضيف (Host Isolation)</h2>
          </div>
          <p style={{ fontSize: '0.9375rem', color: 'var(--color-text-secondary)', marginBottom: '1rem' }}>
            يتم توجيه كافة مسارات المنصة الإشرافية حصراً إلى مضيف الإدارة، وتُرجع رمز 404 تلقائياً على مواقع العيادات.
          </p>
          <div
            style={{
              padding: '0.75rem 1rem',
              borderRadius: '0.5rem',
              backgroundColor: 'rgba(20, 184, 166, 0.1)',
              border: '1px solid rgba(20, 184, 166, 0.2)',
            }}
          >
            <strong>المضيف المعتمد: </strong> admin.clinic.local
          </div>
        </GlassCard>

        <GlassCard>
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '1rem' }}>
            <Lock size={22} color="var(--color-accent-teal)" />
            <h2 style={{ fontSize: '1.125rem', fontWeight: 600 }}>التحقق الآمن للمشرفين (2FA)</h2>
          </div>
          <p style={{ fontSize: '0.9375rem', color: 'var(--color-text-secondary)', marginBottom: '1rem' }}>
            تسجيل دخول المشرف الفائق مقيد بكلمة المرور + رمز رقمي متغير كل ساعة (3600 ثانية)، بدون كلمات مرور افتراضية.
          </p>
          <GlassButton variant="prominent">فحص إعدادات الأمان</GlassButton>
        </GlassCard>

        <GlassCard>
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '1rem' }}>
            <Layers size={22} color="var(--color-accent-teal)" />
            <h2 style={{ fontSize: '1.125rem', fontWeight: 600 }}>إدارة المستأجرين والحصص</h2>
          </div>
          <p style={{ fontSize: '0.9375rem', color: 'var(--color-text-secondary)', marginBottom: '1rem' }}>
            تفعيل الوحدات الوظيفية، تحديد حصص العيادات لكل مالك، وسجل تتبع الدخول المباشر للمرضى.
          </p>
          <GlassButton>عرض قائمة العيادات</GlassButton>
        </GlassCard>
      </main>
    </div>
  );
};

