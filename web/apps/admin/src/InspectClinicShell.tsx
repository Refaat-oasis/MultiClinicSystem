import React from 'react';
import { useQuery } from '@tanstack/react-query';
import { GlassCard, AlertBanner } from '@clinic/ui';
import { apiClient } from '@clinic/api-client';
import {
  Phase6SchedulingScreen,
  Phase7ClinicalScreen,
  Phase8BillingScreen,
  Phase9TreatmentPlansScreen,
  Phase10PortalScreen,
  Phase11PortalBookingScreen,
  Phase12InventoryScreen,
  Phase13TimeEngineScreen,
  Phase14FinanceReportsScreen,
  type AppointmentItem,
  type QueueEntryItem,
  type WaitingListItem,
  type ClinicalVisitSessionData,
  type ClinicalSpecialtyFormDefinition,
  type BillingChargeData,
  type BillingDailyClosingData,
  type TreatmentPlanData,
  type ClinicPortalInspectData,
  type ClinicPortalBookingInspectData,
  type ClinicInventoryInspectData,
  type ClinicTimeEngineInspectData,
  type ClinicFinanceReportsInspectData,
} from '@clinic/feature-core';

interface InspectClinicScheduleSnapshot {
  clinicId: string;
  appointments: AppointmentItem[];
  queueEntries: QueueEntryItem[];
  waitingList: WaitingListItem[];
}

interface InspectClinicClinicalSnapshot {
  clinicId: string;
  visits: ClinicalVisitSessionData[];
  forms: ClinicalSpecialtyFormDefinition[];
}

interface InspectClinicBillingSnapshot {
  clinicId: string;
  recentCharges: BillingChargeData[];
  recentClosings: BillingDailyClosingData[];
}
interface InspectClinicTreatmentPlansSnapshot {
  clinicId: string;
  totalPlansCount: number;
  activePlansCount: number;
  completedPlansCount: number;
  delayedSessionsCount: number;
  recentPlans: TreatmentPlanData[];
}
import { Eye, Lock } from 'lucide-react';

export interface InspectClinicSnapshot {
  clinicId: string;
  name: string;
  slug: string;
  status: string;
  ownerId?: string | null;
  subscriptionExpiryUtc?: string | null;
  graceDays: number;
  sampleRecordsCount: number;
  sampleRecordsTotalAmount: number;
  currency: string;
  readOnlyMode: boolean;
}

export interface InspectClinicShellProps {
  clinicId?: string | null;
}

export const InspectClinicShell: React.FC<InspectClinicShellProps> = ({ clinicId }) => {
  const { data, isLoading, error } = useQuery<InspectClinicSnapshot>({
    queryKey: ['platform-inspect', clinicId],
    queryFn: () => apiClient.get<InspectClinicSnapshot>(`/platform/clinics/${clinicId}/inspect`),
    enabled: Boolean(clinicId),
    retry: false,
  });

  const { data: scheduleSnapshot } = useQuery<InspectClinicScheduleSnapshot>({
    queryKey: ['platform-inspect-schedule', clinicId],
    queryFn: () => apiClient.get<InspectClinicScheduleSnapshot>(`/platform/clinics/${clinicId}/inspect/schedule`),
    enabled: Boolean(clinicId),
    retry: false,
  });

  const { data: clinicalSnapshot } = useQuery<InspectClinicClinicalSnapshot>({
    queryKey: ['platform-inspect-clinical', clinicId],
    queryFn: () => apiClient.get<InspectClinicClinicalSnapshot>(`/platform/clinics/${clinicId}/inspect/clinical`),
    enabled: Boolean(clinicId),
    retry: false,
  });

  const { data: billingSnapshot } = useQuery<InspectClinicBillingSnapshot>({
    queryKey: ['platform-inspect-billing', clinicId],
    queryFn: () => apiClient.get<InspectClinicBillingSnapshot>(`/platform/clinics/${clinicId}/inspect/billing`),
    enabled: Boolean(clinicId),
    retry: false,
  });

  const { data: treatmentPlansSnapshot } = useQuery<InspectClinicTreatmentPlansSnapshot>({
    queryKey: ['platform-inspect-treatment-plans', clinicId],
    queryFn: () => apiClient.get<InspectClinicTreatmentPlansSnapshot>(`/platform/clinics/${clinicId}/inspect/treatment-plans`),
    enabled: Boolean(clinicId),
    retry: false,
  });

  const { data: portalSnapshot } = useQuery<ClinicPortalInspectData>({
    queryKey: ['platform-inspect-portal', clinicId],
    queryFn: () => apiClient.get<ClinicPortalInspectData>(`/platform/clinics/${clinicId}/inspect/portal`),
    enabled: Boolean(clinicId),
    retry: false,
  });

  const { data: portalBookingSnapshot } = useQuery<ClinicPortalBookingInspectData>({
    queryKey: ['platform-inspect-portal-booking', clinicId],
    queryFn: () => apiClient.get<ClinicPortalBookingInspectData>(`/platform/clinics/${clinicId}/inspect/portal-booking`),
    enabled: Boolean(clinicId),
    retry: false,
  });

  const { data: inventorySnapshot } = useQuery<ClinicInventoryInspectData>({
    queryKey: ['platform-inspect-inventory', clinicId],
    queryFn: () => apiClient.get<ClinicInventoryInspectData>(`/platform/clinics/${clinicId}/inspect/inventory`),
    enabled: Boolean(clinicId),
    retry: false,
  });

  const { data: timeEngineSnapshot } = useQuery<ClinicTimeEngineInspectData>({
    queryKey: ['platform-inspect-time-engine', clinicId],
    queryFn: () => apiClient.get<ClinicTimeEngineInspectData>(`/platform/clinics/${clinicId}/inspect/time-engine`),
    enabled: Boolean(clinicId),
    retry: false,
  });

  const { data: financeReportsSnapshot } = useQuery<ClinicFinanceReportsInspectData>({
    queryKey: ['platform-inspect-finance-reports', clinicId],
    queryFn: () => apiClient.get<ClinicFinanceReportsInspectData>(`/platform/clinics/${clinicId}/inspect/finance-reports`),
    enabled: Boolean(clinicId),
    retry: false,
  });

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '1.25rem' }}>
      <AlertBanner
        variant="warning"
        title="وضع الفحص للقراءة فقط (Read-Only Inspect Mode)"
        message="وضع الفحص للقراءة فقط — يتم تسجيل كل وصول في سجل أمان المريض (PatientAccessLog) وهو ظاهر لمالك العيادة، وجميع عمليات الكتابة محظورة."
      />

      <GlassCard style={{ padding: '1.5rem' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '0.75rem', marginBottom: '1rem' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.625rem' }}>
            <Eye size={22} color="var(--color-accent-teal)" />
            <h2 style={{ fontSize: '1.125rem', fontWeight: 700 }}>
              بيانات العيادة في وضع الفحص (Clinic Inspect Snapshot)
            </h2>
          </div>

          <span
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: '0.375rem',
              padding: '0.375rem 0.75rem',
              borderRadius: '9999px',
              background: 'rgba(245, 158, 11, 0.14)',
              border: '1px solid rgba(245, 158, 11, 0.35)',
              color: '#f59e0b',
              fontSize: '0.8125rem',
              fontWeight: 600,
            }}
          >
            <Lock size={14} />
            الكتابة معطلة في وضع الفحص (Writes Disabled in Inspect Mode)
          </span>
        </div>

        {!clinicId ? (
          <p style={{ color: 'var(--color-text-secondary)', fontSize: '0.9375rem' }}>
            اختر عيادة من دليل العيادات لبدء الفحص الآمن للقراءة فقط. (Select a clinic from the directory to open Read-Only Inspect Mode.)
          </p>
        ) : isLoading ? (
          <p>جارٍ تحميل لقطة الفحص الآمنة...</p>
        ) : error || !data ? (
          <AlertBanner variant="error" message="تعذر تحميل بيانات الفحص للعيادة المحددة." />
        ) : (
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '1rem' }}>
            <div style={{ padding: '0.875rem', borderRadius: '0.625rem', background: 'var(--glass-surface)' }}>
              <div style={{ fontSize: '0.75rem', color: 'var(--color-text-muted)' }}>العيادة / Clinic</div>
              <div style={{ fontWeight: 700 }}>{data.name}</div>
              <div style={{ fontSize: '0.8125rem' }}>/{data.slug}</div>
            </div>
            <div style={{ padding: '0.875rem', borderRadius: '0.625rem', background: 'var(--glass-surface)' }}>
              <div style={{ fontSize: '0.75rem', color: 'var(--color-text-muted)' }}>الحالة / Status</div>
              <div style={{ fontWeight: 700 }}>{data.status}</div>
              <div style={{ fontSize: '0.8125rem' }}>فترة السماح: {data.graceDays} يوم</div>
            </div>
            <div style={{ padding: '0.875rem', borderRadius: '0.625rem', background: 'var(--glass-surface)' }}>
              <div style={{ fontSize: '0.75rem', color: 'var(--color-text-muted)' }}>السجلات المقيدة / Tenant Records</div>
              <div style={{ fontWeight: 700 }}>{data.sampleRecordsCount}</div>
              <div style={{ fontSize: '0.8125rem' }}>
                {data.sampleRecordsTotalAmount} {data.currency}
              </div>
            </div>
          </div>
        )}
      </GlassCard>

      {clinicId && scheduleSnapshot && (
        <Phase6SchedulingScreen
          appointments={scheduleSnapshot.appointments}
          queueEntries={scheduleSnapshot.queueEntries}
          waitingList={scheduleSnapshot.waitingList}
          readOnlyInspectMode={true}
        />
      )}

      {clinicId && clinicalSnapshot && clinicalSnapshot.visits.length > 0 && (
        <Phase7ClinicalScreen
          clinicId={clinicId}
          initialVisit={clinicalSnapshot.visits[0]}
          specialtyForms={clinicalSnapshot.forms}
          readOnlyInspectMode={true}
        />
      )}

      {clinicId && billingSnapshot && (
        <Phase8BillingScreen
          clinicId={clinicId}
          readOnlyInspectMode={true}
          initialSnapshot={{
            charges: billingSnapshot.recentCharges,
            dailyClosing: billingSnapshot.recentClosings[0] ?? null,
          }}
        />
      )}
      {clinicId && treatmentPlansSnapshot && (
        <Phase9TreatmentPlansScreen
          clinicId={clinicId}
          readOnlyInspectMode={true}
          initialPlans={treatmentPlansSnapshot.recentPlans}
        />
      )}
      {clinicId && portalSnapshot && (
        <Phase10PortalScreen
          clinicId={clinicId}
          readOnlyInspectMode={true}
          initialInspectSnapshot={portalSnapshot}
        />
      )}
      {clinicId && portalBookingSnapshot && (
        <Phase11PortalBookingScreen
          clinicId={clinicId}
          readOnlyInspectMode={true}
          initialInspectSnapshot={portalBookingSnapshot}
        />
      )}
      {clinicId && inventorySnapshot && (
        <Phase12InventoryScreen
          clinicId={clinicId}
          readOnlyInspectMode={true}
          initialInspectSnapshot={inventorySnapshot}
        />
      )}
      {clinicId && timeEngineSnapshot && (
        <Phase13TimeEngineScreen
          clinicId={clinicId}
          readOnlyInspectMode={true}
          initialInspectSnapshot={timeEngineSnapshot}
        />
      )}
      {clinicId && financeReportsSnapshot && (
        <Phase14FinanceReportsScreen
          clinicId={clinicId}
          readOnlyInspectMode={true}
          initialInspectSnapshot={financeReportsSnapshot}
        />
      )}
    </div>
  );
};

