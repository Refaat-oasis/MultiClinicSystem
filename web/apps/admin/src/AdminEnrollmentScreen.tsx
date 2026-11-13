import React, { useState } from 'react';
import { GlassCard, GlassButton, GlassInput, CodeInput, AlertBanner } from '@clinic/ui';
import { apiClient } from '@clinic/api-client';
import { ShieldAlert, KeyRound, CheckCircle2 } from 'lucide-react';

export interface TotpEnrollmentCompletedResponse {
  adminId: string;
  email: string;
  accessToken: string;
  expiresAtUtc: string;
  recoveryKeys: string[];
}

export interface AdminEnrollmentScreenProps {
  manualSetupKey: string;
  otpAuthUri?: string | null;
  onEnrollmentComplete: (response: TotpEnrollmentCompletedResponse) => void;
}

export const AdminEnrollmentScreen: React.FC<AdminEnrollmentScreenProps> = ({
  manualSetupKey,
  otpAuthUri,
  onEnrollmentComplete,
}) => {
  const [newPassword, setNewPassword] = useState('');
  const [hourlyCode, setHourlyCode] = useState('');
  const [recoveryKeys, setRecoveryKeys] = useState<string[] | null>(null);
  const [completedPayload, setCompletedPayload] = useState<TotpEnrollmentCompletedResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const handleEnroll = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setLoading(true);
    try {
      const response = await apiClient.post<TotpEnrollmentCompletedResponse>('/platform/auth/enroll', {
        newPassword,
        hourlyCode,
      });
      setRecoveryKeys(response.recoveryKeys);
      setCompletedPayload(response);
    } catch (err: unknown) {
      const problem = err as { detail?: string; title?: string };
      setError(problem?.detail || problem?.title || 'فشل إتمام التفعيل / Enrollment failed');
    } finally {
      setLoading(false);
    }
  };

  if (recoveryKeys && completedPayload) {
    return (
      <GlassCard style={{ maxWidth: '640px', marginInline: 'auto', padding: '1.75rem' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem', marginBottom: '1rem' }}>
          <CheckCircle2 size={28} color="#10b981" />
          <div>
            <h2 style={{ fontSize: '1.25rem', fontWeight: 700 }}>
              مفاتيح الاسترداد لمرة واحدة (Single-Use Recovery Keys)
            </h2>
            <p style={{ fontSize: '0.875rem', color: 'var(--color-text-secondary)' }}>
              احفظ هذه المفاتيح العشرة في مكان آمن. لن يتم عرضها مرة أخرى.
            </p>
          </div>
        </div>

        <div
          style={{
            display: 'grid',
            gridTemplateColumns: 'repeat(auto-fit, minmax(150px, 1fr))',
            gap: '0.75rem',
            marginBottom: '1.5rem',
            direction: 'ltr',
          }}
        >
          {recoveryKeys.map((key) => (
            <div
              key={key}
              style={{
                padding: '0.625rem 0.75rem',
                borderRadius: '0.5rem',
                background: 'var(--glass-surface)',
                border: '1px solid rgba(255,255,255,0.2)',
                fontFamily: 'monospace',
                fontWeight: 600,
                textAlign: 'center',
              }}
            >
              {key}
            </div>
          ))}
        </div>

        <GlassButton variant="prominent" onClick={() => onEnrollmentComplete(completedPayload)}>
          لقد حفظت مفاتيح الاسترداد — متابعة للوحة التحكم (Continue to Dashboard)
        </GlassButton>
      </GlassCard>
    );
  }

  return (
    <GlassCard style={{ maxWidth: '600px', marginInline: 'auto', padding: '1.75rem' }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem', marginBottom: '1.25rem' }}>
        <ShieldAlert size={28} color="#f59e0b" />
        <div>
          <h2 style={{ fontSize: '1.25rem', fontWeight: 700 }}>
            إعداد الأمان الإلزامي للمشرف الأعلى (Mandatory Super Admin Enrollment)
          </h2>
          <p style={{ fontSize: '0.875rem', color: 'var(--color-text-secondary)' }}>
            تغيير كلمة المرور الافتراضية وتفعيل رمز المصادقة الساعي (Period: 3600s)
          </p>
        </div>
      </div>

      {error && (
        <div style={{ marginBottom: '1rem' }}>
          <AlertBanner variant="error" message={error} onDismiss={() => setError(null)} />
        </div>
      )}

      <div
        style={{
          padding: '1rem',
          borderRadius: '0.75rem',
          background: 'var(--glass-surface)',
          marginBottom: '1.25rem',
          border: '1px solid rgba(255,255,255,0.18)',
        }}
      >
        <div style={{ fontSize: '0.875rem', fontWeight: 600, marginBottom: '0.375rem' }}>
          مفتاح الإعداد اليدوي (Manual Setup Key — 3600s Hourly TOTP)
        </div>
        <code style={{ display: 'block', direction: 'ltr', fontSize: '1rem', fontWeight: 700, wordBreak: 'break-all' }}>
          {manualSetupKey}
        </code>
        {otpAuthUri && (
          <div style={{ marginTop: '0.5rem', fontSize: '0.75rem', color: 'var(--color-text-muted)', direction: 'ltr', wordBreak: 'break-all' }}>
            {otpAuthUri}
          </div>
        )}
      </div>

      <form onSubmit={handleEnroll} style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
        <GlassInput
          label="كلمة المرور الجديدة (New Strong Password — 12+ chars)"
          type="password"
          value={newPassword}
          onChange={(e) => setNewPassword(e.target.value)}
          icon={<KeyRound size={18} />}
          required
        />

        <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem' }}>
          <label style={{ fontSize: '0.875rem', fontWeight: 500, color: 'var(--color-text-secondary)' }}>
            أدخل الرمز الساعي الحالي للتأكيد (Verify Current 6-Digit Hourly Code)
          </label>
          <CodeInput value={hourlyCode} onChange={setHourlyCode} length={6} disabled={loading} />
        </div>

        <GlassButton type="submit" variant="prominent" disabled={loading}>
          {loading ? 'جارٍ التفعيل...' : 'إكمال التفعيل وإصدار مفاتيح الاسترداد (Complete Enrollment)'}
        </GlassButton>
      </form>
    </GlassCard>
  );
};

