import React, { useState } from 'react';
import { GlassCard, GlassButton, GlassInput, AlertBanner } from '@clinic/ui';
import { apiClient } from '@clinic/api-client';
import { KeyRound, Mail, ArrowRight } from 'lucide-react';

export interface ForgotPasswordScreenProps {
  onCodeSent?: (email: string) => void;
  onNavigateToLogin?: () => void;
}

export const ForgotPasswordScreen: React.FC<ForgotPasswordScreenProps> = ({
  onCodeSent,
  onNavigateToLogin,
}) => {
  const [email, setEmail] = useState('');
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [successSubmitted, setSuccessSubmitted] = useState(false);
  const [isLoading, setIsLoading] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMessage(null);

    if (!email) {
      setErrorMessage('يرجى إدخال البريد الإلكتروني');
      return;
    }

    setIsLoading(true);
    try {
      await apiClient.post('/api/v1/auth/forgot-password', { email });
      setSuccessSubmitted(true);
      onCodeSent?.(email);
    } catch (err: unknown) {
      const problem = err as { detail?: string; title?: string };
      setErrorMessage(problem.detail || problem.title || 'حدث خطأ غير متوقع، يرجى المحاولة لاحقاً');
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div style={{ maxWidth: '440px', marginInline: 'auto', width: '100%' }}>
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
            <KeyRound size={36} />
          </div>
          <h2 style={{ fontSize: '1.5rem', fontWeight: 700, color: 'var(--color-text-primary)' }}>
            استعادة كلمة المرور
          </h2>
          <p style={{ fontSize: '0.875rem', color: 'var(--color-text-secondary)', marginTop: '0.375rem' }}>
            أدخل بريدك الإلكتروني المسجل لإرسال رمز التحقق
          </p>
        </div>

        {errorMessage && (
          <div style={{ marginBottom: '1.25rem' }}>
            <AlertBanner variant="error" message={errorMessage} onDismiss={() => setErrorMessage(null)} />
          </div>
        )}

        {successSubmitted ? (
          <div style={{ display: 'flex', flexDirection: 'column', gap: '1.25rem' }}>
            <AlertBanner
              variant="success"
              title="تم استلام الطلب"
              message="إذا كان البريد الإلكتروني مسجلاً لدينا في النظام، فستصلك رسالة تحتوي على رمز التحقق المكون من 6 أرقام لاستعادة كلمة المرور."
            />

            <GlassButton
              variant="prominent"
              onClick={() => onCodeSent?.(email)}
              style={{ width: '100%', minHeight: '2.875rem' }}
            >
              متابعة وإدخال رمز التحقق
            </GlassButton>

            {onNavigateToLogin && (
              <div style={{ textAlign: 'center' }}>
                <button
                  type="button"
                  onClick={onNavigateToLogin}
                  style={{
                    background: 'none',
                    border: 'none',
                    color: 'var(--color-accent-teal)',
                    cursor: 'pointer',
                    fontSize: '0.875rem',
                  }}
                >
                  العودة لتسجيل الدخول
                </button>
              </div>
            )}
          </div>
        ) : (
          <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: '1.25rem' }}>
            <GlassInput
              label="البريد الإلكتروني المسجل"
              type="email"
              required
              autoComplete="email"
              placeholder="user@clinic.com"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              icon={<Mail size={18} />}
            />

            <GlassButton
              type="submit"
              variant="prominent"
              disabled={isLoading}
              style={{ width: '100%', marginTop: '0.5rem', minHeight: '2.875rem' }}
            >
              {isLoading ? 'جاري التحقق...' : 'إرسال رمز الاستعادة'}
            </GlassButton>

            {onNavigateToLogin && (
              <div style={{ textAlign: 'center', marginTop: '0.25rem' }}>
                <button
                  type="button"
                  onClick={onNavigateToLogin}
                  style={{
                    background: 'none',
                    border: 'none',
                    color: 'var(--color-text-secondary)',
                    cursor: 'pointer',
                    fontSize: '0.875rem',
                    display: 'inline-flex',
                    alignItems: 'center',
                    gap: '0.375rem',
                  }}
                >
                  <ArrowRight size={16} />
                  العودة لصفحة تسجيل الدخول
                </button>
              </div>
            )}
          </form>
        )}
      </GlassCard>
    </div>
  );
};

