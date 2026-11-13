import React, { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { GlassCard, GlassInput, GlassButton } from '@clinic/ui';
import { apiClient } from '@clinic/api-client';
import { FileSearch } from 'lucide-react';
import { AuditLogItem } from './PlatformDashboardScreen';

export const PlatformAuditScreen: React.FC = () => {
  const [clinicIdFilter, setClinicIdFilter] = useState('');
  const [actionFilter, setActionFilter] = useState('');

  const queryParams = new URLSearchParams();
  if (clinicIdFilter.trim()) queryParams.set('clinicId', clinicIdFilter.trim());
  if (actionFilter.trim()) queryParams.set('action', actionFilter.trim());
  const suffix = queryParams.toString() ? `?${queryParams.toString()}` : '';

  const { data: logs = [], refetch, isLoading } = useQuery<AuditLogItem[]>({
    queryKey: ['platform-audit', clinicIdFilter, actionFilter],
    queryFn: () => apiClient.get<AuditLogItem[]>(`/platform/audit${suffix}`),
    retry: false,
  });

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '1.25rem' }}>
      <GlassCard style={{ padding: '1.25rem' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '1rem' }}>
          <FileSearch size={22} color="var(--color-accent-teal)" />
          <h2 style={{ fontSize: '1.125rem', fontWeight: 700 }}>سجل تدقيق المنصة (Platform Audit Viewer)</h2>
        </div>

        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '0.75rem', alignItems: 'end' }}>
          <GlassInput
            label="تصفية حسب العيادة (Clinic ID)"
            value={clinicIdFilter}
            onChange={(e) => setClinicIdFilter(e.target.value)}
          />
          <GlassInput
            label="تصفية حسب الإجراء (Action)"
            value={actionFilter}
            onChange={(e) => setActionFilter(e.target.value)}
          />
          <GlassButton type="button" onClick={() => refetch()} disabled={isLoading}>
            تحديث السجل (Refresh Logs)
          </GlassButton>
        </div>
      </GlassCard>

      <GlassCard style={{ padding: '1.25rem' }}>
        {logs.length === 0 ? (
          <p style={{ color: 'var(--color-text-muted)', fontSize: '0.875rem' }}>
            لا توجد إدخالات مطابقة في سجل التدقيق.
          </p>
        ) : (
          <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem' }}>
            {logs.map((entry) => (
              <div
                key={entry.id}
                style={{
                  padding: '0.75rem',
                  borderRadius: '0.5rem',
                  background: 'var(--glass-surface)',
                  display: 'flex',
                  justifyContent: 'space-between',
                  flexWrap: 'wrap',
                  gap: '0.5rem',
                  fontSize: '0.875rem',
                }}
              >
                <div>
                  <strong>{entry.action}</strong> — {entry.entityName} ({entry.entityId ?? '-'})
                </div>
                <div style={{ color: 'var(--color-text-muted)', direction: 'ltr' }}>
                  {entry.timestampUtc} • IP: {entry.ipAddress ?? 'n/a'}
                </div>
              </div>
            ))}
          </div>
        )}
      </GlassCard>
    </div>
  );
};

