import React from 'react';
import { useQuery } from '@tanstack/react-query';
import { GlassCard, GlassButton, AlertBanner } from '@clinic/ui';
import { apiClient } from '@clinic/api-client';
import { Activity, Building2, Users, AlertTriangle, ShieldAlert, MailWarning } from 'lucide-react';

export interface AuditLogItem {
  id: string;
  userId?: string | null;
  clinicId?: string | null;
  action: string;
  entityName: string;
  entityId?: string | null;
  timestampUtc: string;
  ipAddress?: string | null;
  requestId?: string | null;
}

export interface PlatformDashboardData {
  totalOwners: number;
  activeOwners: number;
  totalClinics: number;
  activeClinics: number;
  pendingOnboardingClinics: number;
  readOnlyClinics: number;
  suspendedClinics: number;
  clinicsAwaitingModuleSetup: number;
  expiringSubscriptionsCount: number;
  failedLoginsLast24Hours: number;
  pendingOutboxMessages: number;
  recentAuditEntries: AuditLogItem[];
}

const FALLBACK_DASHBOARD: PlatformDashboardData = {
  totalOwners: 0,
  activeOwners: 0,
  totalClinics: 0,
  activeClinics: 0,
  pendingOnboardingClinics: 0,
  readOnlyClinics: 0,
  suspendedClinics: 0,
  clinicsAwaitingModuleSetup: 0,
  expiringSubscriptionsCount: 0,
  failedLoginsLast24Hours: 0,
  pendingOutboxMessages: 0,
  recentAuditEntries: [],
};

export const PlatformDashboardScreen: React.FC = () => {
  const { data, isLoading, error, refetch } = useQuery<PlatformDashboardData>({
    queryKey: ['platform-dashboard'],
    queryFn: () => apiClient.get<PlatformDashboardData>('/platform/dashboard'),
    retry: false,
  });

  const stats = data ?? FALLBACK_DASHBOARD;

  const cards = [
    { label: 'إجمالي الملاك / Total Owners', value: stats.totalOwners, sub: `${stats.activeOwners} نشط`, icon: <Users size={20} color="var(--color-accent-teal)" /> },
    { label: 'إجمالي العيادات / Total Clinics', value: stats.totalClinics, sub: `${stats.activeClinics} نشط`, icon: <Building2 size={20} color="var(--color-accent-teal)" /> },
    { label: 'بانتظار التهيئة / Pending Onboarding', value: stats.pendingOnboardingClinics, sub: `${stats.clinicsAwaitingModuleSetup} بانتظار الوحدات`, icon: <Activity size={20} color="#f59e0b" /> },
    { label: 'قراءة فقط / Read-Only & Suspended', value: `${stats.readOnlyClinics} / ${stats.suspendedClinics}`, sub: `${stats.expiringSubscriptionsCount} اشتراكات تنتهي قريباً`, icon: <AlertTriangle size={20} color="#ef4444" /> },
    { label: 'محاولات دخول فاشلة (24 س) / Failed Logins', value: stats.failedLoginsLast24Hours, sub: 'مراقبة الأمان الفورية', icon: <ShieldAlert size={20} color="#ef4444" /> },
    { label: 'رسائل البريد الصادرة / Outbox Queue', value: stats.pendingOutboxMessages, sub: 'في قائمة المعالجة', icon: <MailWarning size={20} color="#0ea5e9" /> },
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '1.25rem' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '0.75rem' }}>
        <h2 style={{ fontSize: '1.25rem', fontWeight: 700 }}>لوحة مراقبة المنصة (Platform Dashboard)</h2>
        <GlassButton onClick={() => refetch()} disabled={isLoading}>
          تحديث المؤشرات (Refresh)
        </GlassButton>
      </div>

      {error && (
        <AlertBanner
          variant="info"
          message="قم بتسجيل الدخول كمشرف أعلى لعرض البيانات الحية من الخادم (Sign in as Super Admin to load live server metrics)."
        />
      )}

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '1rem' }}>
        {cards.map((card) => (
          <GlassCard key={card.label} style={{ padding: '1.25rem' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.5rem' }}>
              <span style={{ fontSize: '0.8125rem', color: 'var(--color-text-secondary)' }}>{card.label}</span>
              {card.icon}
            </div>
            <div style={{ fontSize: '1.5rem', fontWeight: 700 }}>{card.value}</div>
            <div style={{ fontSize: '0.75rem', color: 'var(--color-text-muted)', marginTop: '0.25rem' }}>{card.sub}</div>
          </GlassCard>
        ))}
      </div>

      <GlassCard style={{ padding: '1.25rem' }}>
        <h3 style={{ fontSize: '1rem', fontWeight: 600, marginBottom: '0.75rem' }}>
          أحدث سجلات الأمان والتدقيق (Recent Audit Activity)
        </h3>
        {stats.recentAuditEntries.length === 0 ? (
          <p style={{ color: 'var(--color-text-muted)', fontSize: '0.875rem' }}>
            لا توجد سجلات تدقيق حديثة معروضة حالياً.
          </p>
        ) : (
          <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem' }}>
            {stats.recentAuditEntries.map((entry) => (
              <div
                key={entry.id}
                style={{
                  display: 'flex',
                  justifyContent: 'space-between',
                  padding: '0.625rem 0.75rem',
                  borderRadius: '0.5rem',
                  background: 'var(--glass-surface)',
                  fontSize: '0.875rem',
                }}
              >
                <span>
                  <strong>{entry.action}</strong> — {entry.entityName}
                </span>
                <span style={{ color: 'var(--color-text-muted)', direction: 'ltr' }}>{entry.timestampUtc}</span>
              </div>
            ))}
          </div>
        )}
      </GlassCard>
    </div>
  );
};

