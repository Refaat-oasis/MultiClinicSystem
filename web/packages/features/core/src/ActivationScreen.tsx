import React, { useState } from 'react';
import { GlassCard, GlassButton, GlassInput, CodeInput, AlertBanner } from '@clinic/ui';
import { apiClient } from '@clinic/api-client';
import { useAuthStore } from './useAuthStore';
import { ShieldCheck, Mail, Lock, RefreshCw, KeyRound } from 'lucide-react';

export interface ActivationScreenProps {
  initialEmail?: string;
  onSuccess?: () => void;
  onNavigateToLogin?: () => void;
}

interface ActivationResponse {
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

export const ActivationScreen: React.FC<ActivationScreenProps> = ({
  initialEmail = '',
  onSuccess,
  onNavigateToLogin,
}) => {
  const [email, setEmail] = useState(initialEmail);
  const [code, setCode] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [isResending, setIsResending] = useState(false);
  const setAuth = useAuthStore((s) => s.setAuth);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMessage(null);
    setSuccessMessage(null);

    if (!email) {
      setErrorMessage('يرجى إدخال البريد الإلكتروني');
      return;
    }
    if (code.length !== 6) {
      setErrorMessage('يرجى إدخال رمز التحقق كاملاً المكون من 6 أرقام');
      return;
    }
    if (password.length < 8) {
      setErrorMessage('يجب أن تحتوي كلمة المرور على 8 أحرف على الأقل، حرف كبير، ورقم، ورمز خاص');
      return;
    }
    if (password !== confirmPassword) {
      setErrorMessage('كلمتا المرور غير متطابقتين');
      return;
    }

    setIsLoading(true);
    try {
      const response = await apiClient.post<ActivationResponse>('/api/v1/auth/activate', {
        email,
        code,
        password,
        deviceId: `web-${window.navigator.userAgent.replace(/[^a-zA-Z0-9]/g, '').slice(0, 16)}`,
        deviceName: 'المتصفح الرئيسي',
      });

      setAuth(response.user, response.accessToken);
      onSuccess?.();
    } catch (err: unknown) {
      const problem = err as { detail?: string; code?: string; title?: string };
      setErrorMessage(problem.detail || problem.title || 'رمز التحقق غير صحيح أو منتهي الصلاحية');
    } finally {
      setIsLoading(false);
    }
  };

  const handleResendCode = async () => {
    if (!email) {
      setErrorMessage('يرجى كتابة البريد الإلكتروني لإعادة إرسال الرمز');
      return;
    }

    setIsResending(true);
    setErrorMessage(null);
    setSuccessMessage(null);

    try {
      await apiClient.post('/api/v1/auth/resend-code', {
        email,
        purpose: 'Invitation',
      });
      setSuccessMessage('تم إرسال رمز تحقق جديد إلى بريدك الإلكتروني بنجاح');
      setCode('');
    } catch (err: unknown) {
      const problem = err as { detail?: string; title?: string };
      setErrorMessage(problem.detail || problem.title || 'فشل إرسال الرمز، يرجى المحاولة لاحقاً');
    } finally {
      setIsResending(false);
    }
  };

  return (
    <div style={{ maxWidth: '480px', marginInline: 'auto', width: '100%' }}>
      <GlassCard>
        <div style={{ textAlign: 'center', marginBottom: '1.75rem' }}>
          <div
            style={{
              display: 'inline-flex',
              padding: '0.875rem',
              borderRadius: '1rem',
              backgroundColor: 'rgba(13, 148, 136, 0.12)',
              color: 'var(--color-accent-teal)',
              marginBottom: '1rem',
            }}
          >
            <ShieldCheck size={36} />
          </div>
          <h2 style={{ fontSize: '1.5rem', fontWeight: 700, color: 'var(--color-text-primary)' }}>
            تفعيل حساب العيادة
          </h2>
          <p style={{ fontSize: '0.875rem', color: 'var(--color-text-secondary)', marginTop: '0.375rem' }}>
            أدخل رمز التحقق المكون من 6 أرقام وقم بتعيين كلمة المرور لحسابك
          </p>
        </div>

        {errorMessage && (
          <div style={{ marginBottom: '1.25rem' }}>
            <AlertBanner variant="error" message={errorMessage} onDismiss={() => setErrorMessage(null)} />
          </div>
        )}

        {successMessage && (
          <div style={{ marginBottom: '1.25rem' }}>
            <AlertBanner variant="success" message={successMessage} onDismiss={() => setSuccessMessage(null)} />
          </div>
        )}

        <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: '1.25rem' }}>
          <GlassInput
            label="البريد الإلكتروني المدعو"
            type="email"
            required
            autoComplete="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            icon={<Mail size={18} />}
          />

          <div>
            <label
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: '0.375rem',
                fontSize: '0.875rem',
                fontWeight: 500,
                color: 'var(--color-text-secondary)',
                marginBottom: '0.5rem',
              }}
            >
              <KeyRound size={16} color="var(--color-accent-teal)" />
              رمز التحقق الأمني (6 أرقام)
            </label>
            <CodeInput value={code} onChange={setCode} length={6} />
            <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: '0.375rem' }}>
              <button
                type="button"
                onClick={handleResendCode}
                disabled={isResending}
                style={{
                  background: 'none',
                  border: 'none',
                  color: 'var(--color-accent-teal)',
                  cursor: 'pointer',
                  fontSize: '0.8125rem',
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: '0.25rem',
                }}
              >
                <RefreshCw size={13} className={isResending ? 'spin' : ''} />
                {isResending ? 'جاري الإرسال...' : 'إعادة إرسال الرمز'}
              </button>
            </div>
          </div>

          <GlassInput
            label="تعيين كلمة المرور الجديدة"
            type="password"
            required
            autoComplete="new-password"
            placeholder="8 أحرف كحد أدنى، رمز ورقم"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            icon={<Lock size={18} />}
          />

          <GlassInput
            label="تأكيد كلمة المرور"
            type="password"
            required
            autoComplete="new-password"
            placeholder="أعد إدخال كلمة المرور"
            value={confirmPassword}
            onChange={(e) => setConfirmPassword(e.target.value)}
            icon={<Lock size={18} />}
          />

          <GlassButton
            type="submit"
            variant="prominent"
            disabled={isLoading}
            style={{ width: '100%', marginTop: '0.5rem', minHeight: '2.875rem' }}
          >
            {isLoading ? 'جاري الحفظ والتفعيل...' : 'تفعيل الحساب والدخول'}
          </GlassButton>

          {onNavigateToLogin && (
            <div style={{ textAlign: 'center', marginTop: '0.5rem' }}>
              <button
                type="button"
                onClick={onNavigateToLogin}
                style={{
                  background: 'none',
                  border: 'none',
                  color: 'var(--color-text-secondary)',
                  cursor: 'pointer',
                  fontSize: '0.875rem',
                }}
              >
                لديك حساب مفعل بالفعل؟ تسجيل الدخول
              </button>
            </div>
          )}
        </form>
      </GlassCard>
    </div>
  );
};

