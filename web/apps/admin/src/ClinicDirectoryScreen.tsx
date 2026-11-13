import React, { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { GlassCard, GlassButton, GlassInput, AlertBanner } from '@clinic/ui';
import { apiClient } from '@clinic/api-client';
import { Building2, Eye, ArrowRightLeft, Blocks } from 'lucide-react';
import { OwnerSummaryItem } from './OwnersManagementScreen';

interface ClinicModuleStateDto {
  moduleKey: string;
  titleAr: string;
  titleEn: string;
  enabled: boolean;
  prerequisites: string[];
  missingPrerequisites: string[];
  enabledDependents: string[];
}

export interface ClinicDirectoryScreenProps {
  onInspectClinic: (clinicId: string) => void;
}

export const ClinicDirectoryScreen: React.FC<ClinicDirectoryScreenProps> = ({ onInspectClinic }) => {
  const queryClient = useQueryClient();
  const [search, setSearch] = useState('');
  const [targetOwnerId, setTargetOwnerId] = useState('');
  const [stepUpPassword, setStepUpPassword] = useState('');
  const [selectedClinicId, setSelectedClinicId] = useState<string | null>(null);
  const [feedback, setFeedback] = useState<{ variant: 'success' | 'error'; message: string } | null>(null);

  const { data: directory = [] } = useQuery<OwnerSummaryItem[]>({
    queryKey: ['platform-clinics', search],
    queryFn: () => apiClient.get<OwnerSummaryItem[]>(`/platform/clinics${search ? `?search=${encodeURIComponent(search)}` : ''}`),
    retry: false,
  });

  const { data: clinicModules = [], refetch: refetchModules } = useQuery<ClinicModuleStateDto[]>({
    queryKey: ['platform-clinic-modules', selectedClinicId],
    enabled: Boolean(selectedClinicId),
    queryFn: () => apiClient.get<ClinicModuleStateDto[]>(`/platform/clinics/${selectedClinicId}/modules`),
    retry: false,
  });

  const handleToggleModule = async (clinicId: string, moduleKey: string, nextEnabled: boolean) => {
    setFeedback(null);
    try {
      await apiClient.request<ClinicModuleStateDto[]>(`/platform/clinics/${clinicId}/modules`, {
        method: 'PUT',
        body: JSON.stringify({
          stepUpPassword,
          modules: [{ moduleKey, enabled: nextEnabled }],
        }),
      });
      setFeedback({ variant: 'success', message: `تم تحديث الوحدة (${moduleKey}) بنجاح.` });
      await refetchModules();
    } catch (err: unknown) {
      const problem = err as { detail?: string; title?: string };
      setFeedback({ variant: 'error', message: problem?.detail || problem?.title || 'فشل تحديث الوحدات' });
    }
  };

  const handleUpdateStatus = async (clinicId: string, status: string) => {
    setFeedback(null);
    try {
      const res = await apiClient.request<{ message: string }>(`/platform/clinics/${clinicId}/status`, {
        method: 'PUT',
        body: JSON.stringify({ status }),
      });
      setFeedback({ variant: 'success', message: res.message });
      await queryClient.invalidateQueries({ queryKey: ['platform-clinics'] });
    } catch (err: unknown) {
      const problem = err as { detail?: string; title?: string };
      setFeedback({ variant: 'error', message: problem?.detail || problem?.title || 'فشل تحديث الحالة' });
    }
  };

  const handleMoveClinic = async (clinicId: string) => {
    setFeedback(null);
    try {
      const res = await apiClient.post<{ message: string }>(`/platform/clinics/${clinicId}/move`, {
        targetOwnerId,
        stepUpPassword,
      });
      setFeedback({ variant: 'success', message: res.message });
      setTargetOwnerId('');
      setStepUpPassword('');
      await queryClient.invalidateQueries({ queryKey: ['platform-clinics'] });
    } catch (err: unknown) {
      const problem = err as { detail?: string; title?: string };
      setFeedback({ variant: 'error', message: problem?.detail || problem?.title || 'فشل نقل العيادة' });
    }
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '1.25rem' }}>
      {feedback && (
        <AlertBanner variant={feedback.variant} message={feedback.message} onDismiss={() => setFeedback(null)} />
      )}

      <GlassCard style={{ padding: '1.25rem', display: 'flex', flexDirection: 'column', gap: '0.75rem' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
          <Building2 size={22} color="var(--color-accent-teal)" />
          <h2 style={{ fontSize: '1.125rem', fontWeight: 700 }}>دليل العيادات ونقل الملكية (Clinics Directory & Transfer)</h2>
        </div>

        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '0.75rem' }}>
          <GlassInput
            label="بحث بالاسم أو الرابط (Search clinic or owner)"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
          <GlassInput
            label="معرف المالك المستهدف للنقل (Target Owner ID for Move)"
            value={targetOwnerId}
            onChange={(e) => setTargetOwnerId(e.target.value)}
          />
          <GlassInput
            label="كلمة مرور التحقق الإضافي (Step-Up Password)"
            type="password"
            value={stepUpPassword}
            onChange={(e) => setStepUpPassword(e.target.value)}
          />
        </div>
      </GlassCard>

      {directory.length === 0 ? (
        <GlassCard style={{ padding: '1.25rem' }}>
          <p style={{ color: 'var(--color-text-muted)', fontSize: '0.875rem' }}>لا توجد عيادات مطابقة حالياً.</p>
        </GlassCard>
      ) : (
        directory.map((owner) => (
          <GlassCard key={owner.ownerId} style={{ padding: '1.25rem' }}>
            <div style={{ fontWeight: 700, marginBottom: '0.75rem' }}>
              المالك: {owner.fullName} ({owner.email}) — {owner.clinicsCount}/{owner.maxClinics ?? '∞'}
            </div>

            <div style={{ display: 'flex', flexDirection: 'column', gap: '0.625rem' }}>
              {owner.clinics.map((clinic) => (
                <div
                  key={clinic.clinicId}
                  style={{
                    display: 'flex',
                    justifyContent: 'space-between',
                    alignItems: 'center',
                    flexWrap: 'wrap',
                    gap: '0.75rem',
                    padding: '0.75rem',
                    borderRadius: '0.625rem',
                    background: 'var(--glass-surface)',
                  }}
                >
                  <div>
                    <div style={{ fontWeight: 600 }}>{clinic.name}</div>
                    <div style={{ fontSize: '0.8125rem', color: 'var(--color-text-secondary)' }}>
                      /{clinic.slug} • الحالة: {clinic.status}
                    </div>
                  </div>
                  <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
                    <GlassButton type="button" variant="prominent" onClick={() => onInspectClinic(clinic.clinicId)}>
                      <Eye size={16} />
                      <span>فحص للقراءة فقط (Inspect)</span>
                    </GlassButton>
                    <GlassButton type="button" onClick={() => setSelectedClinicId(clinic.clinicId)}>
                      <Blocks size={16} />
                      <span>إدارة الوحدات (Modules)</span>
                    </GlassButton>
                    <GlassButton type="button" onClick={() => handleUpdateStatus(clinic.clinicId, clinic.status === 'Suspended' ? 'Active' : 'Suspended')}>
                      {clinic.status === 'Suspended' ? 'تفعيل (Activate)' : 'إيقاف (Suspend)'}
                    </GlassButton>
                    <GlassButton type="button" onClick={() => handleMoveClinic(clinic.clinicId)} disabled={!targetOwnerId}>
                      <ArrowRightLeft size={16} />
                      <span>نقل الملكية (Move)</span>
                    </GlassButton>
                  </div>
                </div>
              ))}
            </div>
          </GlassCard>
        ))
      )}

      {selectedClinicId && (
        <GlassCard style={{ padding: '1.25rem' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.75rem' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
              <Blocks size={20} color="var(--color-accent-teal)" />
              <h3 style={{ fontSize: '1.0625rem', fontWeight: 700 }}>تحكم الوحدات والتبعيات للعيادة (Module Toggles & Dependencies)</h3>
            </div>
            <GlassButton type="button" onClick={() => setSelectedClinicId(null)}>
              إغلاق
            </GlassButton>
          </div>
          {clinicModules.length === 0 ? (
            <p style={{ fontSize: '0.875rem', color: 'var(--color-text-muted)' }}>
              يتم تحميل قائمة الوحدات الـ 16 بعد تسجيل الدخول بصلاحية المشرف الأعلى.
            </p>
          ) : (
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))', gap: '0.625rem' }}>
              {clinicModules.map((mod) => (
                <div
                  key={mod.moduleKey}
                  style={{
                    display: 'flex',
                    justifyContent: 'space-between',
                    alignItems: 'center',
                    gap: '0.5rem',
                    padding: '0.625rem 0.75rem',
                    borderRadius: '0.5rem',
                    background: 'var(--glass-surface)',
                  }}
                >
                  <div>
                    <div style={{ fontWeight: 600, fontSize: '0.875rem' }}>{mod.titleAr}</div>
                    <div style={{ fontSize: '0.75rem', color: 'var(--color-text-muted)' }}>
                      {mod.prerequisites.length > 0 ? `المتطلبات: ${mod.prerequisites.join(', ')}` : 'أساسي مستقل'}
                    </div>
                  </div>
                  <GlassButton
                    type="button"
                    variant={mod.enabled ? 'prominent' : 'default'}
                    onClick={() => handleToggleModule(selectedClinicId, mod.moduleKey, !mod.enabled)}
                  >
                    {mod.enabled ? 'مفعّل' : 'تفعيل'}
                  </GlassButton>
                </div>
              ))}
            </div>
          )}
        </GlassCard>
      )}
    </div>
  );
};

