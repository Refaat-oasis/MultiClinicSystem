import React, { useState } from 'react';
import { GlassCard, GlassButton, GlassInput, CodeInput, AlertBanner, StatusChip } from '@clinic/ui';
import { apiClient } from '@clinic/api-client';
import { ShieldCheck, KeyRound, Mail, Lock, AlertTriangle, Key } from 'lucide-react';

export interface PlatformLoginResponse {
  adminId: string;
  email: string;
  fullName: string;
  accessToken: string;
  expiresAtUtc: string;
  requiresEnrollment: boolean;
  mustChangePassword: boolean;
  manualSetupKey?: string | null;
  otpAuthUri?: string | null;
}

export interface AdminLoginScreenProps {
  onLoginSuccess: (response: PlatformLoginResponse) => void;
}

export const AdminLoginScreen: React.FC<AdminLoginScreenProps> = ({ onLoginSuccess }) => {
  const [email, setEmail] = useState('ahmed@platform.local');
  const [password, setPassword] = useState('');
  const [hourlyCode, setHourlyCode] = useState('');
  const [recoveryKey, setRecoveryKey] = useState('');
  const [useRecoveryKey, setUseRecoveryKey] = useState(false);
  const [showBreakGlass, setShowBreakGlass] = useState(false);
  const [breakGlassSecret, setBreakGlassSecret] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [infoMessage, setInfoMessage] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setInfoMessage(null);
    setLoading(true);

    try {
      const response = await apiClient.post<PlatformLoginResponse>('/platform/auth/login', {
        email,
        password,
        hourlyCode: useRecoveryKey ? undefined : hourlyCode || undefined,
        recoveryKey: useRecoveryKey ? recoveryKey || undefined : undefined,
      });
      onLoginSuccess(response);
    } catch (err: unknown) {
      const problem = err as { detail?: string; title?: string; message?: string };
      setError(problem?.detail || problem?.title || problem?.message || 'فشل تسجيل الدخول / Sign-in failed');
    } finally {
      setLoading(false);
    }
  };

  const handleBreakGlassReset = async () => {
    setError(null);
    setInfoMessage(null);
    setLoading(true);
    try {
      const res = await apiClient.post<{ message: string }>('/platform/auth/break-glass-reset', {
        email,
        breakGlassSecret,
      });
      setInfoMessage(res.message);
      setBreakGlassSecret('');
    } catch (err: unknown) {
      const problem = err as { detail?: string; title?: string };
      setError(problem?.detail || problem?.title || 'فشل إعادة ضبط الطوارئ / Break-glass reset failed');
    } finally {
      setLoading(false);
    }
  };

  return (
    <GlassCard style={{ maxWidth: '580px', marginInline: 'auto', padding: '2rem', position: 'relative' }}>
      {/* Stitch Top Trust & Compliance Badge Strip */}
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: '0.5rem', flexWrap: 'wrap', marginBottom: '1.25rem' }}>
        <StatusChip status="info" label="Platform Admin • Super Console Gateway" showDot={true} />
        <span
          style={{
            fontSize: '0.6875rem',
            fontWeight: 600,
            paddingInline: '0.5rem',
            paddingBlock: '0.2rem',
            borderRadius: 'var(--radius-pill)',
            backgroundColor: 'var(--color-surface)',
            border: '1px solid var(--color-border)',
            color: 'var(--color-text-secondary)',
          }}
        >
          SOC2 • NCA ECC-1:2018
        </span>
      </div>

      <div style={{ display: 'flex', alignItems: 'center', gap: '0.875rem', marginBottom: '1.25rem' }}>
        <div
          style={{
            width: '3rem',
            height: '3rem',
            borderRadius: 'var(--radius-card)',
            backgroundColor: 'var(--color-accent-teal-soft)',
            color: 'var(--color-accent-teal)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            flexShrink: 0,
          }}
        >
          <ShieldCheck size={28} />
        </div>
        <div>
          <h2 style={{ fontSize: '1.25rem', fontWeight: 700, margin: 0, color: 'var(--color-text-primary)' }}>
            تسجيل دخول المشرف الأعلى (Super Admin Sign In)
          </h2>
          <p style={{ fontSize: '0.8125rem', color: 'var(--color-text-secondary)', margin: '0.25rem 0 0' }}>
            بنية المصادقة الثلاثية الصارمة: البريد المهني + كلمة المرور + رمز TOTP المشفر كل ساعة (3600 ثانية)
          </p>
        </div>
      </div>

      {error && (
        <div style={{ marginBottom: '1rem' }}>
          <AlertBanner variant="error" message={error} onDismiss={() => setError(null)} />
        </div>
      )}

      {infoMessage && (
        <div style={{ marginBottom: '1rem' }}>
          <AlertBanner variant="success" message={infoMessage} onDismiss={() => setInfoMessage(null)} />
        </div>
      )}

      <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: '1.125rem' }}>
        <GlassInput
          label="البريد الإلكتروني للمشرف (Work Email)"
          type="email"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          required
          icon={<Mail size={18} />}
          placeholder="admin@platform.local"
        />

        <GlassInput
          label="كلمة المرور الفائقة (Master Password)"
          type="password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          required
          icon={<Lock size={18} />}
          placeholder="••••••••••••"
        />

        {!useRecoveryKey ? (
          <div
            style={{
              padding: '1rem',
              borderRadius: 'var(--radius-card)',
              backgroundColor: 'var(--color-surface)',
              border: '1px solid var(--color-border)',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '0.5rem' }}>
              <label style={{ fontSize: '0.875rem', fontWeight: 600, color: 'var(--color-text-primary)', display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                <KeyRound size={16} color="var(--color-accent-teal)" />
                رمز الأمان بالساعة (Hourly TOTP Code - 6 Digits)
              </label>
              <span style={{ fontSize: '0.6875rem', color: 'var(--color-text-secondary)', fontFamily: 'monospace' }}>
                3600s Period
              </span>
            </div>
            <CodeInput value={hourlyCode} onChange={setHourlyCode} length={6} disabled={loading} />
          </div>
        ) : (
          <GlassInput
            label="مفتاح الاسترداد لمرة واحدة (Emergency Recovery Key)"
            type="text"
            value={recoveryKey}
            onChange={(e) => setRecoveryKey(e.target.value)}
            required
            icon={<Key size={18} />}
            placeholder="REC-XXXX-XXXX"
          />
        )}

        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '0.5rem' }}>
          <button
            type="button"
            onClick={() => {
              setUseRecoveryKey(!useRecoveryKey);
              setHourlyCode('');
              setRecoveryKey('');
            }}
            style={{
              background: 'none',
              border: 'none',
              color: 'var(--color-accent-teal)',
              fontSize: '0.8125rem',
              fontWeight: 600,
              cursor: 'pointer',
              padding: 0,
              textDecoration: 'underline',
            }}
          >
            {useRecoveryKey ? 'العودة إلى الرمز الساعي (Switch to Hourly Code)' : 'استخدام مفتاح الاسترداد لمرة واحدة (Use Single-Use Recovery Key)'}
          </button>

          <button
            type="button"
            onClick={() => setShowBreakGlass(!showBreakGlass)}
            style={{
              background: 'none',
              border: 'none',
              color: 'var(--color-status-danger)',
              fontSize: '0.8125rem',
              fontWeight: 600,
              cursor: 'pointer',
              padding: 0,
            }}
          >
            {showBreakGlass ? 'إخفاء كسر الزجاج' : 'طوارئ كسر الزجاج (Break-Glass)'}
          </button>
        </div>

        <GlassButton variant="primary" type="submit" disabled={loading}>
          <ShieldCheck size={18} />
          <span>{loading ? 'جاري التحقق الفائق...' : 'التحقق وتسجيل الدخول الآمن'}</span>
        </GlassButton>
      </form>

      {showBreakGlass && (
        <div
          style={{
            marginTop: '1.5rem',
            padding: '1.25rem',
            borderRadius: 'var(--radius-card)',
            border: '1px dashed var(--color-status-danger)',
            backgroundColor: 'var(--color-status-danger-soft)',
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.5rem', color: 'var(--color-status-danger)' }}>
            <AlertTriangle size={18} />
            <strong style={{ fontSize: '0.875rem' }}>إجراء كسر الزجاج للطوارئ القصوى (Emergency Break-Glass Reset)</strong>
          </div>
          <p style={{ fontSize: '0.75rem', color: 'var(--color-text-secondary)', marginBottom: '0.75rem', lineHeight: 1.5 }}>
            يُستخدم حصراً في حال فقدان مفاتيح الاسترداد والرمز الساعي. يتطلب سر النظام البيئي المسبق.
          </p>
          <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
            <div style={{ flex: 1, minWidth: '200px' }}>
              <GlassInput
                placeholder="Secret: PLATFORM_BREAK_GLASS_SECRET"
                type="password"
                value={breakGlassSecret}
                onChange={(e) => setBreakGlassSecret(e.target.value)}
              />
            </div>
            <GlassButton variant="danger" onClick={handleBreakGlassReset} disabled={loading || !breakGlassSecret}>
              تنفيذ إعادة الضبط
            </GlassButton>
          </div>
        </div>
      )}

      {/* Stitch Telemetry Footer */}
      <div
        style={{
          marginTop: '1.5rem',
          paddingTop: '0.75rem',
          borderTop: '1px solid var(--color-border)',
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          fontSize: '0.6875rem',
          color: 'var(--color-text-muted)',
          flexWrap: 'wrap',
          gap: '0.5rem',
        }}
      >
        <span>جلسة مشفرة: E2EE TLS 1.3 • NCA Node KSA-01</span>
        <span>سجل التدقيق: NCA Audit Log Stream Active</span>
      </div>
    </GlassCard>
  );
};
