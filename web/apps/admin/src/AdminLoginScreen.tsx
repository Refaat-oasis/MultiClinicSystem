import React, { useState } from 'react';
import { GlassCard, GlassButton, GlassInput, CodeInput, AlertBanner } from '@clinic/ui';
import { apiClient } from '@clinic/api-client';
import { ShieldCheck, KeyRound, Mail, Lock, AlertTriangle } from 'lucide-react';

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
    <GlassCard style={{ maxWidth: '520px', marginInline: 'auto', padding: '1.75rem' }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem', marginBottom: '1.25rem' }}>
        <ShieldCheck size={28} color="var(--color-accent-teal)" />
        <div>
          <h2 style={{ fontSize: '1.25rem', fontWeight: 700 }}>تسجيل دخول المشرف الأعلى (Super Admin Sign In)</h2>
          <p style={{ fontSize: '0.875rem', color: 'var(--color-text-secondary)' }}>
            مصادقة ثلاثية: البريد الإلكتروني + كلمة المرور + الرمز الزمني كل ساعة (3600 ثانية)
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

      <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
        <GlassInput
          label="البريد الإلكتروني للمشرف (Admin Email)"
          type="email"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          icon={<Mail size={18} />}
          required
        />

        <GlassInput
          label="كلمة المرور (Password)"
          type="password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          icon={<Lock size={18} />}
          required
        />

        {!useRecoveryKey ? (
          <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem' }}>
            <label style={{ fontSize: '0.875rem', fontWeight: 500, color: 'var(--color-text-secondary)' }}>
              الرمز الزمني الساعي — 6 أرقام (Hourly TOTP Code — 3600s)
            </label>
            <CodeInput value={hourlyCode} onChange={setHourlyCode} length={6} disabled={loading} />
          </div>
        ) : (
          <GlassInput
            label="مفتاح الاسترداد لمرة واحدة (Single-Use Recovery Key)"
            type="text"
            placeholder="XXXX-XXXX-XXXX"
            value={recoveryKey}
            onChange={(e) => setRecoveryKey(e.target.value)}
            icon={<KeyRound size={18} />}
            style={{ direction: 'ltr', textAlign: 'left' }}
          />
        )}

        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '0.5rem' }}>
          <button
            type="button"
            onClick={() => setUseRecoveryKey((prev) => !prev)}
            style={{
              background: 'none',
              border: 'none',
              color: 'var(--color-accent-teal)',
              cursor: 'pointer',
              fontSize: '0.875rem',
              textDecoration: 'underline',
            }}
          >
            {useRecoveryKey
              ? 'العودة إلى الرمز الزمني الساعي (Use Hourly TOTP Code)'
              : 'استخدام مفتاح الاسترداد لمرة واحدة (Use Recovery Key)'}
          </button>

          <button
            type="button"
            onClick={() => setShowBreakGlass((prev) => !prev)}
            style={{
              background: 'none',
              border: 'none',
              color: '#f59e0b',
              cursor: 'pointer',
              fontSize: '0.875rem',
            }}
          >
            إعادة ضبط الطوارئ (Break-Glass 2FA Reset)
          </button>
        </div>

        <GlassButton type="submit" variant="prominent" disabled={loading}>
          {loading ? 'جارٍ التحقق...' : 'دخول آمن (Secure Sign In)'}
        </GlassButton>
      </form>

      {showBreakGlass && (
        <div
          style={{
            marginTop: '1.25rem',
            paddingTop: '1rem',
            borderTop: '1px solid rgba(255,255,255,0.18)',
            display: 'flex',
            flexDirection: 'column',
            gap: '0.75rem',
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', color: '#f59e0b', fontSize: '0.875rem' }}>
            <AlertTriangle size={16} />
            <span>إعادة ضبط المصادقة الثنائية عبر مفتاح الطوارئ بالخادم</span>
          </div>
          <GlassInput
            label="مفتاح الطوارئ (Break-Glass Secret)"
            type="password"
            value={breakGlassSecret}
            onChange={(e) => setBreakGlassSecret(e.target.value)}
          />
          <GlassButton type="button" onClick={handleBreakGlassReset} disabled={loading || !breakGlassSecret}>
            تأكيد إعادة ضبط الطوارئ (Execute Break-Glass Reset)
          </GlassButton>
        </div>
      )}
    </GlassCard>
  );
};

