import React, { useState } from 'react';
import { GlassCard, GlassButton, GlassInput, AlertBanner } from '@clinic/ui';
import { apiClient } from '@clinic/api-client';
import { useAuthStore } from './useAuthStore';
import { LogIn, Mail, Lock, Stethoscope } from 'lucide-react';

export interface LoginScreenProps {
  onSuccess?: () => void;
  onNavigateToForgot?: () => void;
  onNavigateToActivate?: () => void;
}

interface LoginResponse {
  accessToken: string;
  refreshToken: string;
  user: {
    id: string;
    email: string;
    fullName: string;
    role: string;
    clinicId?: string;
  };
}

export const LoginScreen: React.FC<LoginScreenProps> = ({
  onSuccess,
  onNavigateToForgot,
  onNavigateToActivate,
}) => {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const setAuth = useAuthStore((s) => s.setAuth);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMessage(null);

    if (!email || !password) {
      setErrorMessage('يرجى إدخال البريد الإلكتروني وكلمة المرور');
      return;
    }

    setIsLoading(true);
    try {
      const response = await apiClient.post<LoginResponse>('/api/v1/auth/login', {
        email,
        password,
        deviceId: `web-${window.navigator.userAgent.replace(/[^a-zA-Z0-9]/g, '').slice(0, 16)}`,
        deviceName: 'المتصفح الرئيسي',
      });

      setAuth(response.user, response.accessToken);
      onSuccess?.();
    } catch (err: unknown) {
      const problem = err as { detail?: string; code?: string; title?: string };
      if (problem.code === 'ACCOUNT_NOT_ACTIVATED') {
        setErrorMessage('الحساب غير مفعل بعد. يرجى تفعيل حسابك أولاً باستخدام الرمز المرسل.');
      } else {
        setErrorMessage(problem.detail || problem.title || 'بيانات الدخول غير صحيحة، يرجى المحاولة ثانية');
      }
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div style={{ maxWidth: '460px', marginInline: 'auto', width: '100%' }}>
      <GlassCard style={{ padding: '2rem' }}>
        <div style={{ textAlign: 'center', marginBottom: '1.75rem' }}>
          <div
            style={{
              display: 'inline-flex',
              padding: '0.875rem',
              borderRadius: 'var(--radius-card)',
              backgroundColor: 'var(--color-accent-teal-soft)',
              color: 'var(--color-accent-teal)',
              marginBottom: '1rem',
              boxShadow: 'var(--glass-shadow)',
            }}
          >
            <Stethoscope size={36} />
          </div>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', gap: '0.5rem', marginBottom: '0.375rem' }}>
            <span
              style={{
                fontSize: '0.6875rem',
                fontWeight: 600,
                paddingInline: '0.5rem',
                paddingBlock: '0.125rem',
                borderRadius: 'var(--radius-pill)',
                backgroundColor: 'var(--color-accent-teal-soft)',
                color: 'var(--color-accent-teal-text)',
              }}
            >
              بوابة الكادر الطبي الموحدة
            </span>
          </div>
          <h2 style={{ fontSize: '1.5rem', fontWeight: 700, color: 'var(--color-text-primary)' }}>
            تسجيل الدخول إلى العيادة
          </h2>
          <p style={{ fontSize: '0.875rem', color: 'var(--color-text-secondary)', marginTop: '0.375rem' }}>
            أدخل بريدك الإلكتروني المعتمد للدخول إلى لوحة التحكم
          </p>
        </div>

        {errorMessage && (
          <div style={{ marginBottom: '1.25rem' }}>
            <AlertBanner variant="error" message={errorMessage} onDismiss={() => setErrorMessage(null)} />
          </div>
        )}

        <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: '1.25rem' }}>
          <GlassInput
            label="البريد الإلكتروني"
            type="email"
            required
            autoComplete="email"
            placeholder="doctor@clinic.com"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            icon={<Mail size={18} />}
          />

          <GlassInput
            label="كلمة المرور"
            type="password"
            required
            autoComplete="current-password"
            placeholder="••••••••••••"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            icon={<Lock size={18} />}
          />

          <div
            style={{
              display: 'flex',
              justifyContent: 'space-between',
              alignItems: 'center',
              fontSize: '0.8125rem',
            }}
          >
            {onNavigateToForgot && (
              <button
                type="button"
                onClick={onNavigateToForgot}
                style={{
                  background: 'none',
                  border: 'none',
                  color: 'var(--color-accent-teal)',
                  cursor: 'pointer',
                  padding: 0,
                  fontWeight: 600,
                }}
              >
                نسيت كلمة المرور؟
              </button>
            )}

            {onNavigateToActivate && (
              <button
                type="button"
                onClick={onNavigateToActivate}
                style={{
                  background: 'none',
                  border: 'none',
                  color: 'var(--color-text-secondary)',
                  cursor: 'pointer',
                  padding: 0,
                }}
              >
                تفعيل حساب جديد
              </button>
            )}
          </div>

          <GlassButton
            type="submit"
            variant="primary"
            disabled={isLoading}
            style={{ width: '100%', marginTop: '0.5rem', minHeight: '2.875rem' }}
          >
            <LogIn size={18} />
            <span>{isLoading ? 'جاري التحقق...' : 'تسجيل الدخول'}</span>
          </GlassButton>
        </form>

        {/* Stitch Compliance Footnote */}
        <div
          style={{
            marginTop: '1.25rem',
            paddingTop: '0.75rem',
            borderTop: '1px solid var(--color-border)',
            textAlign: 'center',
            fontSize: '0.6875rem',
            color: 'var(--color-text-muted)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            gap: '0.5rem',
          }}
        >
          <span>تشفير طبي فائق (TLS 1.3)</span>
          <span>•</span>
          <span>متوافق مع HIPAA و NCA</span>
        </div>
      </GlassCard>
    </div>
  );
};

