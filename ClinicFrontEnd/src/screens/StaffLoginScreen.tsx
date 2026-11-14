import React, { useState } from 'react';
import { Stethoscope, Lock, Mail, AlertCircle, RefreshCw } from 'lucide-react';
import { StatusChip } from '../components/StatusChip';

export interface StaffLoginScreenProps {
  onLoginSuccess: (email: string, clinicName: string) => void;
}

export const StaffLoginScreen: React.FC<StaffLoginScreenProps> = ({ onLoginSuccess }) => {
  const [email, setEmail] = useState('doctor@alsafwa.com');
  const [password, setPassword] = useState('');
  const [rememberDevice, setRememberDevice] = useState(true);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (!email || !password) {
      setError('يرجى إدخال البريد الإلكتروني وكلمة المرور');
      return;
    }

    setLoading(true);
    setTimeout(() => {
      setLoading(false);
      onLoginSuccess(email, 'عيادات الصفوة التخصصية');
    }, 350);
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
      <div style={{ width: '100%', maxWidth: '440px' }}>
        {/* Top Sync & Security Indicator */}
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            padding: '0.5rem 1rem',
            backgroundColor: 'var(--clinic-bg-surface)',
            border: '1px solid var(--clinic-border-subtle)',
            borderRadius: 'var(--clinic-radius-input)',
            marginBottom: '1rem',
            boxShadow: 'var(--clinic-shadow-sm)',
          }}
        >
          <span style={{ fontSize: '0.75rem', fontWeight: 600, color: 'var(--clinic-text-secondary)' }}>
            منظومة السجلات الطبية والفوترة الذاتية
          </span>
          <StatusChip status="success" label="مزامنة دون اتصال نشطة" showDot={true} />
        </div>

        {/* Login Card */}
        <div className="clinic-card" style={{ padding: '2rem' }}>
          <div style={{ textAlign: 'center', marginBottom: '1.75rem' }}>
            <div
              style={{
                width: '3.5rem',
                height: '3.5rem',
                margin: '0 auto 1rem',
                borderRadius: 'var(--clinic-radius-card)',
                backgroundColor: 'var(--clinic-accent-soft)',
                color: 'var(--clinic-accent-primary)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
              }}
            >
              <Stethoscope size={30} />
            </div>

            <h1 style={{ fontSize: '1.375rem', fontWeight: 700, color: 'var(--clinic-text-primary)' }}>
              تسجيل دخول الكادر الطبي
            </h1>
            <p style={{ fontSize: '0.875rem', color: 'var(--clinic-text-secondary)', marginTop: '0.25rem' }}>
              بوابة الأطباء وطاقم التمريض والاستقبال الموحدة
            </p>
          </div>

          {error && (
            <div
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: '0.5rem',
                padding: '0.75rem 1rem',
                borderRadius: 'var(--clinic-radius-input)',
                backgroundColor: 'var(--clinic-status-danger-bg)',
                color: 'var(--clinic-status-danger)',
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
                htmlFor="staff-email"
                style={{ display: 'block', fontSize: '0.8125rem', fontWeight: 600, marginBottom: '0.375rem' }}
              >
                البريد الإلكتروني المهني
              </label>
              <div style={{ position: 'relative' }}>
                <input
                  id="staff-email"
                  type="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  className="clinic-input"
                  dir="ltr"
                  placeholder="doctor@clinic.com"
                  required
                />
                <Mail
                  size={18}
                  style={{ position: 'absolute', insetInlineEnd: '12px', top: '13px', color: 'var(--clinic-text-muted)' }}
                />
              </div>
            </div>

            <div>
              <label
                htmlFor="staff-password"
                style={{ display: 'block', fontSize: '0.8125rem', fontWeight: 600, marginBottom: '0.375rem' }}
              >
                كلمة المرور
              </label>
              <div style={{ position: 'relative' }}>
                <input
                  id="staff-password"
                  type="password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  className="clinic-input"
                  placeholder="••••••••••••"
                  required
                />
                <Lock
                  size={18}
                  style={{ position: 'absolute', insetInlineEnd: '12px', top: '13px', color: 'var(--clinic-text-muted)' }}
                />
              </div>
            </div>

            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <label style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', fontSize: '0.8125rem', cursor: 'pointer' }}>
                <input
                  type="checkbox"
                  checked={rememberDevice}
                  onChange={(e) => setRememberDevice(e.target.checked)}
                />
                <span>تذكر هذا الجهاز الطبي للعمل دون اتصال</span>
              </label>

              <a
                href="#forgot"
                onClick={(e) => { e.preventDefault(); alert('يرجى مراجعة مسؤول النظام لإعادة تعيين كلمة المرور'); }}
                style={{ fontSize: '0.8125rem', color: 'var(--clinic-accent-primary)', textDecoration: 'none' }}
              >
                نسيت كلمة المرور؟
              </a>
            </div>

            <button
              type="submit"
              disabled={loading}
              className="clinic-btn clinic-btn-primary"
              style={{ width: '100%', marginTop: '0.5rem' }}
            >
              {loading ? <RefreshCw className="animate-spin" size={18} /> : <Stethoscope size={18} />}
              <span>تسجيل الدخول ومزامنة البيانات</span>
            </button>
          </form>

          <div
            style={{
              marginTop: '1.75rem',
              paddingTop: '1.25rem',
              borderTop: '1px solid var(--clinic-border-subtle)',
              textAlign: 'center',
              fontSize: '0.75rem',
              color: 'var(--clinic-text-muted)',
            }}
          >
            <p>مشفر بالكامل بمعايير HIPAA و RLS للخصوصية الطبية • إصدار 1.0 Clinical Calm</p>
          </div>
        </div>
      </div>
    </div>
  );
};
