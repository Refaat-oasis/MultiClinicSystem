import React from 'react';
import { useQuery } from '@tanstack/react-query';
import { GlassCard, GlassButton, AlertBanner, StatCard, StatusChip, ClinicalTable } from '@clinic/ui';
import { apiClient } from '@clinic/api-client';
import { Activity, Building2, Users, AlertTriangle, ShieldAlert, MailWarning, RefreshCw, Server } from 'lucide-react';

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

  const auditColumns = [
    {
      key: 'timestampUtc',
      header: 'الوقت (UTC)',
      render: (item: AuditLogItem) => (
        <span className="force-ltr" style={{ fontFamily: 'monospace', fontSize: '0.8125rem' }}>
          {new Date(item.timestampUtc).toLocaleTimeString()}
        </span>
      ),
      width: '120px',
    },
    {
      key: 'action',
      header: 'الإجراء',
      render: (item: AuditLogItem) => <StatusChip status="info" label={item.action} showDot={false} />,
    },
    {
      key: 'entityName',
      header: 'الكيان المتأثر',
      render: (item: AuditLogItem) => (
        <span>
          <strong>{item.entityName}</strong> {item.entityId && <span style={{ opacity: 0.7 }}>({item.entityId.slice(0, 8)}...)</span>}
        </span>
      ),
    },
    {
      key: 'ipAddress',
      header: 'عنوان IP',
      render: (item: AuditLogItem) => (
        <span className="force-ltr" style={{ fontFamily: 'monospace', fontSize: '0.8125rem' }}>
          {item.ipAddress || 'Internal/Node'}
        </span>
      ),
    },
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '1.5rem' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '0.75rem' }}>
        <div>
          <h2 style={{ fontSize: '1.25rem', fontWeight: 700, margin: 0, color: 'var(--color-text-primary)' }}>
            لوحة مراقبة المنصة (Platform Super Admin Dashboard)
          </h2>
          <p style={{ fontSize: '0.8125rem', color: 'var(--color-text-secondary)', margin: '0.25rem 0 0' }}>
            رصد حي لكافة العيادات، التراخيص، الحسابات وسجلات التدقيق السيبرانية
          </p>
        </div>
        <GlassButton onClick={() => refetch()} disabled={isLoading} variant="secondary">
          <RefreshCw size={16} />
          <span>تحديث المؤشرات (Refresh)</span>
        </GlassButton>
      </div>

      {error && (
        <AlertBanner
          variant="info"
          message="قم بتسجيل الدخول كمشرف أعلى لعرض البيانات الحية من الخادم (Sign in as Super Admin to load live server metrics)."
        />
      )}

      {/* Stitch Stat Cards Grid */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '1rem' }}>
        <StatCard
          title="إجمالي الملاك / Owners"
          value={stats.totalOwners}
          subtitle={`${stats.activeOwners} نشط في النظام`}
          icon={<Users size={20} />}
        />
        <StatCard
          title="إجمالي العيادات / Clinics"
          value={stats.totalClinics}
          subtitle={`${stats.activeClinics} نشطة معتمدة`}
          icon={<Building2 size={20} />}
        />
        <StatCard
          title="بانتظار التهيئة / Onboarding"
          value={stats.pendingOnboardingClinics}
          subtitle={`${stats.clinicsAwaitingModuleSetup} بانتظار تفعيل الوحدات`}
          icon={<Activity size={20} />}
          iconBg="var(--color-status-warning-soft)"
        />
        <StatCard
          title="قراءة فقط وموقوفة / Suspended"
          value={`${stats.readOnlyClinics} / ${stats.suspendedClinics}`}
          subtitle={`${stats.expiringSubscriptionsCount} اشتراكات تنتهي قريباً`}
          icon={<AlertTriangle size={20} />}
          iconBg="var(--color-status-danger-soft)"
        />
        <StatCard
          title="محاولات دخول فاشلة (24س)"
          value={stats.failedLoginsLast24Hours}
          subtitle="مراقبة الأمان الفورية"
          icon={<ShieldAlert size={20} />}
          iconBg="var(--color-status-danger-soft)"
        />
        <StatCard
          title="رسائل البريد الصادرة / Outbox"
          value={stats.pendingOutboxMessages}
          subtitle="في قائمة المعالجة الفورية"
          icon={<MailWarning size={20} />}
          iconBg="var(--color-status-info-soft)"
        />
      </div>

      {/* Audit Log Table */}
      <GlassCard style={{ padding: '1.5rem' }}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '1rem' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
            <Server size={18} color="var(--color-accent-teal)" />
            <h3 style={{ fontSize: '1rem', fontWeight: 700, margin: 0 }}>
              أحدث سجلات الأمان والتدقيق (Recent Audit Activity)
            </h3>
          </div>
          <span style={{ fontSize: '0.75rem', color: 'var(--color-text-secondary)' }}>
            سجل غير قابل للتعديل (Append-Only Audit Stream)
          </span>
        </div>

        <ClinicalTable
          columns={auditColumns}
          data={stats.recentAuditEntries}
          keyExtractor={(item) => item.id}
          emptyMessage="لا توجد عمليات تدقيق مسجلة حالياً / No audit entries loaded"
        />
      </GlassCard>
    </div>
  );
};
