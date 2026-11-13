import React, { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { GlassCard, GlassButton, GlassInput, AlertBanner } from '@clinic/ui';
import { apiClient } from '@clinic/api-client';
import { UserPlus, ShieldAlert, Mail, CheckCircle2, Ban } from 'lucide-react';

export interface ClinicSummaryItem {
  clinicId: string;
  name: string;
  slug: string;
  status: string;
  isActive: boolean;
}

export interface OwnerSummaryItem {
  ownerId: string;
  userId: string;
  fullName: string;
  email: string;
  phoneNumber?: string | null;
  status: string;
  emailConfirmed: boolean;
  maxClinics?: number | null;
  clinicsCount: number;
  createdAtUtc: string;
  clinics: ClinicSummaryItem[];
}

export const OwnersManagementScreen: React.FC = () => {
  const queryClient = useQueryClient();
  const [fullName, setFullName] = useState('');
  const [email, setEmail] = useState('');
  const [phone, setPhone] = useState('');
  const [maxClinics, setMaxClinics] = useState('3');
  const [selectedOwner, setSelectedOwner] = useState<OwnerSummaryItem | null>(null);
  const [stepUpPassword, setStepUpPassword] = useState('');
  const [quotaDraft, setQuotaDraft] = useState('3');
  const [feedback, setFeedback] = useState<{ variant: 'success' | 'error'; message: string } | null>(null);

  const { data: owners = [] } = useQuery<OwnerSummaryItem[]>({
    queryKey: ['platform-owners'],
    queryFn: () => apiClient.get<OwnerSummaryItem[]>('/platform/owners'),
    retry: false,
  });

  const handleCreateOwner = async (e: React.FormEvent) => {
    e.preventDefault();
    setFeedback(null);
    try {
      const created = await apiClient.post<OwnerSummaryItem>('/platform/owners', {
        fullName,
        email,
        phone: phone || null,
        maxClinics: maxClinics ? Number(maxClinics) : null,
      });
      setFeedback({ variant: 'success', message: `تمت دعوة المالك ${created.fullName} بنجاح / Owner invited successfully` });
      setFullName('');
      setEmail('');
      setPhone('');
      setSelectedOwner(created);
      await queryClient.invalidateQueries({ queryKey: ['platform-owners'] });
    } catch (err: unknown) {
      const problem = err as { detail?: string; title?: string };
      setFeedback({ variant: 'error', message: problem?.detail || problem?.title || 'تعذر إنشاء المالك / Failed to invite owner' });
    }
  };

  const handleDeactivateOwner = async () => {
    if (!selectedOwner) return;
    setFeedback(null);
    try {
      const res = await apiClient.post<{ message: string }>(`/platform/owners/${selectedOwner.ownerId}/deactivate`, {
        stepUpPassword,
      });
      setFeedback({ variant: 'success', message: res.message });
      setStepUpPassword('');
      await queryClient.invalidateQueries({ queryKey: ['platform-owners'] });
    } catch (err: unknown) {
      const problem = err as { detail?: string; title?: string };
      setFeedback({ variant: 'error', message: problem?.detail || problem?.title || 'فشل تعطيل المالك / Deactivation failed' });
    }
  };

  const handleReactivateOwner = async () => {
    if (!selectedOwner) return;
    setFeedback(null);
    try {
      const res = await apiClient.post<{ message: string }>(`/platform/owners/${selectedOwner.ownerId}/reactivate`);
      setFeedback({ variant: 'success', message: res.message });
      await queryClient.invalidateQueries({ queryKey: ['platform-owners'] });
    } catch (err: unknown) {
      const problem = err as { detail?: string; title?: string };
      setFeedback({ variant: 'error', message: problem?.detail || problem?.title || 'فشل إعادة التفعيل / Reactivation failed' });
    }
  };

  const handleUpdateQuota = async () => {
    if (!selectedOwner) return;
    setFeedback(null);
    try {
      const res = await apiClient.request<{ message: string }>(`/platform/owners/${selectedOwner.ownerId}/quota`, {
        method: 'PUT',
        body: JSON.stringify({ maxClinics: Number(quotaDraft), stepUpPassword }),
      });
      setFeedback({ variant: 'success', message: res.message });
      await queryClient.invalidateQueries({ queryKey: ['platform-owners'] });
    } catch (err: unknown) {
      const problem = err as { detail?: string; title?: string };
      setFeedback({ variant: 'error', message: problem?.detail || problem?.title || 'فشل تحديث الحصة / Quota update failed' });
    }
  };

  const handleResendCode = async (ownerId: string) => {
    setFeedback(null);
    try {
      const res = await apiClient.post<{ message: string }>(`/platform/owners/${ownerId}/resend-code`);
      setFeedback({ variant: 'success', message: res.message });
    } catch (err: unknown) {
      const problem = err as { detail?: string; title?: string };
      setFeedback({ variant: 'error', message: problem?.detail || problem?.title || 'تعذر إعادة الإرسال / Resend failed' });
    }
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '1.25rem' }}>
      {feedback && (
        <AlertBanner variant={feedback.variant} message={feedback.message} onDismiss={() => setFeedback(null)} />
      )}

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(320px, 1fr))', gap: '1.25rem' }}>
        <GlassCard style={{ padding: '1.5rem' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '1rem' }}>
            <UserPlus size={22} color="var(--color-accent-teal)" />
            <h2 style={{ fontSize: '1.125rem', fontWeight: 700 }}>دعوة مالك عيادات جديد (Invite Clinic Owner)</h2>
          </div>

          <form onSubmit={handleCreateOwner} style={{ display: 'flex', flexDirection: 'column', gap: '0.875rem' }}>
            <GlassInput label="الاسم الكامل (Full Name)" value={fullName} onChange={(e) => setFullName(e.target.value)} required />
            <GlassInput label="البريد الإلكتروني (Email)" type="email" value={email} onChange={(e) => setEmail(e.target.value)} required />
            <GlassInput label="رقم الهاتف (Phone)" value={phone} onChange={(e) => setPhone(e.target.value)} />
            <GlassInput label="الحد الأقصى للعيادات (Max Clinics Quota)" type="number" min="0" value={maxClinics} onChange={(e) => setMaxClinics(e.target.value)} />
            <GlassButton type="submit" variant="prominent">
              إرسال الدعوة ورمز التفعيل (Send Invitation)
            </GlassButton>
          </form>
        </GlassCard>

        <GlassCard style={{ padding: '1.5rem' }}>
          <h2 style={{ fontSize: '1.125rem', fontWeight: 700, marginBottom: '1rem' }}>
            سجل الملاك وتفاصيل الحصص (Owners Directory)
          </h2>

          {owners.length === 0 ? (
            <p style={{ fontSize: '0.875rem', color: 'var(--color-text-muted)' }}>
              لا يوجد ملاك مسجلون حالياً أو يلزم تسجيل الدخول كمشرف أعلى.
            </p>
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '0.625rem' }}>
              {owners.map((owner) => (
                <div
                  key={owner.ownerId}
                  onClick={() => {
                    setSelectedOwner(owner);
                    setQuotaDraft(String(owner.maxClinics ?? 3));
                  }}
                  style={{
                    padding: '0.75rem',
                    borderRadius: '0.625rem',
                    background: 'var(--glass-surface)',
                    border: selectedOwner?.ownerId === owner.ownerId ? '1px solid var(--color-accent-teal)' : '1px solid rgba(255,255,255,0.12)',
                    cursor: 'pointer',
                    display: 'flex',
                    justifyContent: 'space-between',
                    alignItems: 'center',
                  }}
                >
                  <div>
                    <div style={{ fontWeight: 600 }}>{owner.fullName}</div>
                    <div style={{ fontSize: '0.8125rem', color: 'var(--color-text-secondary)' }}>{owner.email}</div>
                  </div>
                  <span style={{ fontSize: '0.75rem', fontWeight: 600 }}>
                    {owner.status} ({owner.clinicsCount}/{owner.maxClinics ?? '∞'})
                  </span>
                </div>
              ))}
            </div>
          )}

          {selectedOwner && (
            <div style={{ marginTop: '1.25rem', paddingTop: '1rem', borderTop: '1px solid rgba(255,255,255,0.16)', display: 'flex', flexDirection: 'column', gap: '0.75rem' }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', fontWeight: 600 }}>
                <ShieldAlert size={18} color="#f59e0b" />
                <span>إدارة المالك المحدد: {selectedOwner.fullName}</span>
              </div>

              <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
                <GlassButton type="button" onClick={() => handleResendCode(selectedOwner.ownerId)}>
                  <Mail size={16} />
                  <span>إعادة إرسال الرمز (Resend Code)</span>
                </GlassButton>
                <GlassButton type="button" onClick={handleReactivateOwner}>
                  <CheckCircle2 size={16} />
                  <span>إعادة تفعيل (Reactivate)</span>
                </GlassButton>
              </div>

              <GlassInput
                label="تأكيد كلمة مرور المشرف للإجراءات الحساسة (Step-Up Password)"
                type="password"
                value={stepUpPassword}
                onChange={(e) => setStepUpPassword(e.target.value)}
              />

              <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', flexWrap: 'wrap' }}>
                <div style={{ flex: 1 }}>
                  <GlassInput label="حصة العيادات الجديدة (New Quota)" type="number" value={quotaDraft} onChange={(e) => setQuotaDraft(e.target.value)} />
                </div>
                <GlassButton type="button" onClick={handleUpdateQuota}>
                  حفظ الحصة (Update Quota)
                </GlassButton>
                <GlassButton type="button" onClick={handleDeactivateOwner}>
                  <Ban size={16} />
                  <span>تعطيل المالك (Deactivate)</span>
                </GlassButton>
              </div>
            </div>
          )}
        </GlassCard>
      </div>
    </div>
  );
};

