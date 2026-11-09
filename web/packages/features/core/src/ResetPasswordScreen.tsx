import React, { useState } from 'react';
import { GlassCard, GlassButton, GlassInput, CodeInput, AlertBanner } from '@clinic/ui';
import { apiClient } from '@clinic/api-client';
import { KeyRound, Lock, Mail, CheckCircle } from 'lucide-react';

export interface ResetPasswordScreenProps {
  initialEmail?: string;
  onSuccess?: () => void;
  onNavigateToLogin?: () => void;
}

export const ResetPasswordScreen: React.FC<ResetPasswordScreenProps> = ({
  initialEmail = '',
  onSuccess,
  onNavigateToLogin,
}) => {
  const [email, setEmail] = useState(initialEmail);
  const [code, setCode] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [isSuccess, setIsSuccess] = useState(false);
  const [isLoading, setIsLoading] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMessage(null);

    if (!email) {
      setErrorMessage('يرجى إدخال البريد الإلكتروني');
      return;
    }
    if (code.length !== 6) {
      setErrorMessage('يرجى إدخال رمز التحقق كاملاً (6 أرقام)');
      return;
    }
    if (newPassword.length < 8) {
      setErrorMessage('يجب أن تحتوي كلمة المرور على 8 أحرف على الأقل، حرف كبير، رقم، ورمز');
      return;
    }
    if (newPassword !== confirmPassword) {
      setErrorMessage('كلمتا المرور غير متطابقتين');
      return;
    }

    setIsLoading(true);
    try {
      await apiClient.post('/api/v1/auth/reset-password', {
        email,
        code,
        newPassword,
      });

      setIsSuccess(true);
      onSuccess?.();
    } catch (err: unknown) {
      const problem = err as { detail?: string; title?: string };
      setErrorMessage(problem.detail || problem.title || 'فشل إعادة تعيين كلمة المرور. تأكد من صحة الرمز.');
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div style={{ maxWidth: '460px', marginInline: 'auto', width: '100%' }}>
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
            تعيين كلمة المرور الجديدة
          </h2>
          <p style={{ fontSize: '0.875rem', color: 'var(--color-text-secondary)', marginTop: '0.375rem' }}>
            أدخل الرمز المكون من 6 أرقام الذي استلمته في بريدك
          </p>
        </div>

        {errorMessage && (
          <div style={{ marginBottom: '1.25rem' }}>
            <AlertBanner variant="error" message={errorMessage} onDismiss={() => setErrorMessage(null)} />
          </div>
        )}

        {isSuccess ? (
          <div style={{ display: 'flex', flexDirection: 'column', gap: '1.25rem', textAlign: 'center' }}>
            <div style={{ display: 'flex', justifyContent: 'center', marginBlock: '0.5rem' }}>
              <CheckCircle size={56} color="#10b981" />
            </div>
            <AlertBanner
              variant="success"
              title="تمت إعادة التعيين بنجاح"
              message="تم تغيير كلمة المرور الخاصة بحسابك وإلغاء جميع الجلسات القديمة بنجاح لأسباب أمنية."
            />
            {onNavigateToLogin && (
              <GlassButton
                variant="prominent"
                onClick={onNavigateToLogin}
                style={{ width: '100%', minHeight: '2.875rem' }}
              >
                تسجيل الدخول بكلمة المرور الجديدة
              </GlassButton>
            )}
          </div>
        ) : (
          <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: '1.25rem' }}>
            <GlassInput
              label="البريد الإلكتروني"
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
                  display: 'block',
                  fontSize: '0.875rem',
                  fontWeight: 500,
                  color: 'var(--color-text-secondary)',
                  marginBottom: '0.5rem',
                }}
              >
                رمز التحقق (6 أرقام)
              </label>
              <CodeInput value={code} onChange={setCode} length={6} />
            </div>

            <GlassInput
              label="كلمة المرور الجديدة"
              type="password"
              required
              autoComplete="new-password"
              value={newPassword}
              onChange={(e) => setNewPassword(e.target.value)}
              icon={<Lock size={18} />}
            />

            <GlassInput
              label="تأكيد كلمة المرور الجديدة"
              type="password"
              required
              autoComplete="new-password"
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
              {isLoading ? 'جاري الحفظ...' : 'تأكيد كلمة المرور الجديدة'}
            </GlassButton>
          </form>
        )}
      </GlassCard>
    </div>
  );
};
