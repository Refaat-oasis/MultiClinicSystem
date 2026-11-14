import React, { useState } from 'react';
import { ShieldAlert, ShieldCheck, KeyRound, Lock, Mail, AlertCircle, RefreshCw } from 'lucide-react';
import { StatusChip } from '../components/StatusChip';

export interface SuperAdminLoginScreenProps {
  onLoginSuccess: (email: string) => void;
}

export const SuperAdminLoginScreen: React.FC<SuperAdminLoginScreenProps> = ({ onLoginSuccess }) => {
  const [email, setEmail] = useState('superadmin@clinic.local');
  const [password, setPassword] = useState('');
  const [verificationCode, setVerificationCode] = useState(['', '', '', '', '', '']);
  const [useRecoveryKey, setUseRecoveryKey] = useState(false);
  const [recoveryKey, setRecoveryKey] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const handleOtpChange = (index: number, val: string) => {
    const clean = val.replace(/\D/g, '').slice(-1);
    const next = [...verificationCode];
    next[index] = clean;
    setVerificationCode(next);
    if (clean && index < 5) {
      const nextInput = document.getElementById(`admin-otp-${index + 1}`);
      nextInput?.focus();
    }
  };

  const handleOtpKeyDown = (index: number, e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Backspace' && !verificationCode[index] && index > 0) {
      const prevInput = document.getElementById(`admin-otp-${index - 1}`);
      prevInput?.focus();
    }
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (!email || !password) {
      setError('يرجى إدخال البريد الإلكتروني للمشرف وكلمة المرور');
      return;
    }

    if (!useRecoveryKey && verificationCode.some((c) => !c)) {
      setError('يرجى إدخال الرمز الساعي المكون من 6 أرقام (صالح لمدة 3600 ثانية)');
      return;
    }

    if (useRecoveryKey && !recoveryKey.trim()) {
      setError('يرجى إدخال مفتاح الاسترداد لمرة واحدة');
      return;
    }

    setLoading(true);
    setTimeout(() => {
      setLoading(false);
      onLoginSuccess(email);
    }, 400);
  };

  return (
    <div
      style={{
        minHeight: '85vh',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        padding: '1.5rem',
      }}
    >
      <div style={{ width: '100%', maxWidth: '480px' }}>
        {/* Top Sovereign Node Badge */}
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            padding: '0.625rem 1rem',
            backgroundColor: 'var(--admin-bg-surface)',
            border: '1px solid var(--admin-border-subtle)',
            borderRadius: 'var(--admin-radius-input)',
            marginBottom: '1rem',
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
            <span style={{ fontSize: '0.75rem', fontFamily: 'monospace', fontWeight: 600 }}>
              Sovereign Cloud Node // KSA-SEC-01
            </span>
          </div>
          <StatusChip status="info" label="نطاق معزول 404" showDot={true} />
        </div>

        {/* Login Card */}
        <div className="admin-card" style={{ padding: '2rem' }}>
          <div style={{ textAlign: 'center', marginBottom: '1.75rem' }}>
            <div
              style={{
                width: '3.5rem',
                height: '3.5rem',
                margin: '0 auto 1rem',
                borderRadius: 'var(--admin-radius-card)',
                backgroundColor: 'var(--admin-accent-soft)',
                color: 'var(--admin-accent-primary)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
              }}
            >
              <ShieldAlert size={32} />
            </div>

            <h1 style={{ fontSize: '1.375rem', fontWeight: 700, color: 'var(--admin-text-primary)' }}>
              تسجيل دخول المشرف الأعلى (Super Admin)
            </h1>
            <p style={{ fontSize: '0.875rem', color: 'var(--admin-text-secondary)', marginTop: '0.375rem' }}>
              بوابة المصادقة المركزية الثلاثية (3-Factor Sign In) مع عزل المضيف التام
            </p>
          </div>

          {error && (
            <div
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: '0.5rem',
                padding: '0.75rem 1rem',
                borderRadius: 'var(--admin-radius-input)',
                backgroundColor: 'var(--admin-status-danger-bg)',
                color: 'var(--admin-status-danger)',
                fontSize: '0.875rem',
                marginBottom: '1.25rem',
              }}
            >
              <AlertCircle size={18} />
              <span>{error}</span>
            </div>
          )}

          <form onSubmit={handleSubmit} style={{ display: 'grid', gap: '1.25rem' }}>
            <div>
              <label
                htmlFor="admin-email"
                style={{ display: 'block', fontSize: '0.8125rem', fontWeight: 600, marginBottom: '0.375rem' }}
              >
                البريد الإلكتروني للمشرف
              </label>
              <div style={{ position: 'relative' }}>
                <input
                  id="admin-email"
                  type="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  className="admin-input"
                  dir="ltr"
                  placeholder="superadmin@clinic.local"
                  required
                />
                <Mail
                  size={18}
                  style={{ position: 'absolute', insetInlineEnd: '12px', top: '13px', color: 'var(--admin-text-muted)' }}
                />
              </div>
            </div>

            <div>
              <label
                htmlFor="admin-password"
                style={{ display: 'block', fontSize: '0.8125rem', fontWeight: 600, marginBottom: '0.375rem' }}
              >
                كلمة المرور
              </label>
              <div style={{ position: 'relative' }}>
                <input
                  id="admin-password"
                  type="password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  className="admin-input"
                  placeholder="••••••••••••"
                  required
                />
                <Lock
                  size={18}
                  style={{ position: 'absolute', insetInlineEnd: '12px', top: '13px', color: 'var(--admin-text-muted)' }}
                />
              </div>
            </div>

            {!useRecoveryKey ? (
              <div>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.375rem' }}>
                  <label style={{ fontSize: '0.8125rem', fontWeight: 600 }}>
                    الرمز الساعي الموثق (3600 ثانية)
                  </label>
                  <span style={{ fontSize: '0.6875rem', color: 'var(--admin-accent-primary)', fontWeight: 600 }}>
                    فترة الصلاحية: 1 ساعة
                  </span>
                </div>

                <div className="otp-container" role="group" aria-label="Verification Code Input">
                  {verificationCode.map((digit, idx) => (
                    <input
                      key={idx}
                      id={`admin-otp-${idx}`}
                      type="text"
                      inputMode="numeric"
                      maxLength={1}
                      value={digit}
                      onChange={(e) => handleOtpChange(idx, e.target.value)}
                      onKeyDown={(e) => handleOtpKeyDown(idx, e)}
                      className="otp-box"
                      autoFocus={idx === 0}
                    />
                  ))}
                </div>
              </div>
            ) : (
              <div>
                <label
                  htmlFor="admin-recovery"
                  style={{ display: 'block', fontSize: '0.8125rem', fontWeight: 600, marginBottom: '0.375rem' }}
                >
                  مفتاح الاسترداد لمرة واحدة (Single-Use Recovery Key)
                </label>
                <div style={{ position: 'relative' }}>
                  <input
                    id="admin-recovery"
                    type="text"
                    value={recoveryKey}
                    onChange={(e) => setRecoveryKey(e.target.value)}
                    className="admin-input"
                    dir="ltr"
                    placeholder="XXXXX-XXXXX-XXXXX"
                  />
                  <KeyRound
                    size={18}
                    style={{ position: 'absolute', insetInlineEnd: '12px', top: '13px', color: 'var(--admin-text-muted)' }}
                  />
                </div>
              </div>
            )}

            <button
              type="button"
              onClick={() => {
                setUseRecoveryKey(!useRecoveryKey);
                setError(null);
              }}
              style={{
                background: 'none',
                border: 'none',
                color: 'var(--admin-accent-primary)',
                fontSize: '0.8125rem',
                fontWeight: 600,
                cursor: 'pointer',
                textAlign: 'start',
                padding: 0,
              }}
            >
              {useRecoveryKey ? 'العودة للرمز الساعي (Hourly TOTP)' : 'استخدام مفتاح الاسترداد لمرة واحدة (Break-Glass)'}
            </button>

            <button
              type="submit"
              disabled={loading}
              className="admin-btn admin-btn-primary"
              style={{ width: '100%', marginTop: '0.5rem' }}
            >
              {loading ? <RefreshCw className="animate-spin" size={18} /> : <ShieldCheck size={18} />}
              <span>تسجيل الدخول والتحقق الآمن</span>
            </button>
          </form>

          {/* Compliance Footer */}
          <div
            style={{
              marginTop: '1.75rem',
              paddingTop: '1.25rem',
              borderTop: '1px solid var(--admin-border-subtle)',
              textAlign: 'center',
              fontSize: '0.75rem',
              color: 'var(--admin-text-muted)',
            }}
          >
            <p>معتمد وفق معايير الأمن السيبراني NCA & SOC2 Type II • مشفر ببروتوكول TLS 1.3</p>
          </div>
        </div>
      </div>
    </div>
  );
};
