import React, { useState } from 'react';
import { Clock, SlidersHorizontal, RefreshCw, ShieldCheck, CheckCircle2, AlertCircle } from 'lucide-react';
import { GlassCard, GlassButton, AlertBanner } from '@clinic/ui';
import { useOfflineMutation } from '@clinic/api-client';

export interface ClinicTimeEngineSettingData {
  clinicId: string;
  minSampleSize: number;
  trimFraction: number;
  maxStepChangePercent: number;
  updatedAtUtc: string;
}

export interface DoctorTimeEngineSettingData {
  clinicId: string;
  doctorId: string;
  doctorName: string;
  isEngineEnabled: boolean;
  updatedAtUtc: string;
}

export interface CategoryTimeStatData {
  id: string;
  clinicId: string;
  doctorId: string;
  doctorName: string;
  categoryId: string;
  categoryNameAr: string;
  categoryNameEn: string;
  sampleCount: number;
  excusedCount: number;
  trimmedAverageMinutes: number;
  suggestedDurationMinutes: number;
  appliedDurationMinutes: number;
  defaultDurationMinutes: number;
  isDoctorEngineEnabled: boolean;
  isAdopted: boolean;
  lastUpdatedAtUtc: string;
}

export interface SessionDurationData {
  id: string;
  clinicId: string;
  visitId: string;
  doctorId: string;
  categoryId: string;
  rawMinutes: number;
  plannedMinutes: number;
  isExcused: boolean;
  excuseReason: string | null;
  occurredAtUtc: string;
}

export interface ClinicTimeEngineInspectData {
  clinicId: string;
  settings: ClinicTimeEngineSettingData;
  doctorSettings: DoctorTimeEngineSettingData[];
  stats: CategoryTimeStatData[];
  recentDurations: SessionDurationData[];
}

export interface Phase13TimeEngineScreenProps {
  clinicId: string;
  readOnlyInspectMode?: boolean;
  initialInspectSnapshot?: ClinicTimeEngineInspectData;
}

export const Phase13TimeEngineScreen: React.FC<Phase13TimeEngineScreenProps> = ({
  clinicId,
  readOnlyInspectMode = false,
  initialInspectSnapshot,
}) => {
  const [settings, setSettings] = useState<ClinicTimeEngineSettingData>(
    initialInspectSnapshot?.settings ?? {
      clinicId,
      minSampleSize: 5,
      trimFraction: 0.1,
      maxStepChangePercent: 20,
      updatedAtUtc: '2026-09-14T09:00:00Z',
    },
  );
  const [doctors, setDoctors] = useState<DoctorTimeEngineSettingData[]>(
    initialInspectSnapshot?.doctorSettings ?? [
      {
        clinicId,
        doctorId: 'doc-1',
        doctorName: 'د. عادل حسني',
        isEngineEnabled: true,
        updatedAtUtc: '2026-09-14T09:00:00Z',
      },
    ],
  );
  const [stats, setStats] = useState<CategoryTimeStatData[]>(
    initialInspectSnapshot?.stats ?? [
      {
        id: 'stat-1',
        clinicId,
        doctorId: 'doc-1',
        doctorName: 'د. عادل حسني',
        categoryId: 'cat-1',
        categoryNameAr: 'علاج جذور',
        categoryNameEn: 'Root Canal',
        sampleCount: 6,
        excusedCount: 1,
        trimmedAverageMinutes: 45,
        suggestedDurationMinutes: 36,
        appliedDurationMinutes: 36,
        defaultDurationMinutes: 30,
        isDoctorEngineEnabled: true,
        isAdopted: true,
        lastUpdatedAtUtc: '2026-09-14T09:00:00Z',
      },
    ],
  );
  const [durations, setDurations] = useState<SessionDurationData[]>(
    initialInspectSnapshot?.recentDurations ?? [
      {
        id: 'dur-1',
        clinicId,
        visitId: 'visit-1',
        doctorId: 'doc-1',
        categoryId: 'cat-1',
        rawMinutes: 45,
        plannedMinutes: 30,
        isExcused: false,
        excuseReason: null,
        occurredAtUtc: '2026-09-14T09:45:00Z',
      },
    ],
  );
  const [excuseDraftReason, setExcuseDraftReason] = useState('حالة طارئة مطولة');
  const [statusMessage, setStatusMessage] = useState<string | null>(null);

  const settingsMutation = useOfflineMutation<
    ClinicTimeEngineSettingData,
    { minSampleSize: number; trimFraction: number; maxStepChangePercent: number }
  >({
    endpoint: `/api/v1/clinics/${clinicId}/time-engine/settings`,
    method: 'PUT',
  });

  const recalcMutation = useOfflineMutation<CategoryTimeStatData[], Record<string, never>>({
    endpoint: `/api/v1/clinics/${clinicId}/time-engine/recalculate`,
    method: 'POST',
  });

  const handleSaveSettings = () => {
    if (readOnlyInspectMode) {
      return;
    }

    settingsMutation.mutate({
      minSampleSize: settings.minSampleSize,
      trimFraction: settings.trimFraction,
      maxStepChangePercent: settings.maxStepChangePercent,
    });
    setStatusMessage(`تم حفظ إعدادات محرك الزمن (الحد الأدنى ${settings.minSampleSize} جلسات)`);
  };

  const handleToggleDoctor = (doctorId: string) => {
    if (readOnlyInspectMode) {
      return;
    }

    setDoctors((prev) =>
      prev.map((d) => (d.doctorId === doctorId ? { ...d, isEngineEnabled: !d.isEngineEnabled } : d)),
    );
    setStats((prev) =>
      prev.map((s) =>
        s.doctorId === doctorId
          ? {
              ...s,
              isDoctorEngineEnabled: !s.isDoctorEngineEnabled,
              appliedDurationMinutes: !s.isDoctorEngineEnabled
                ? s.suggestedDurationMinutes
                : s.defaultDurationMinutes,
              isAdopted: !s.isDoctorEngineEnabled && s.suggestedDurationMinutes !== s.defaultDurationMinutes,
            }
          : s,
      ),
    );
    setStatusMessage('تم تحديث تفعيل محرك الزمن للطبيب');
  };

  const handleToggleExcuse = (visitId: string) => {
    if (readOnlyInspectMode) {
      return;
    }

    setDurations((prev) =>
      prev.map((d) =>
        d.visitId === visitId
          ? {
              ...d,
              isExcused: !d.isExcused,
              excuseReason: !d.isExcused ? excuseDraftReason : null,
            }
          : d,
      ),
    );
    setStatusMessage('تم تحديث حالة استثناء الجلسة وإعادة حساب المتوسط');
  };

  const handleRecalculate = () => {
    if (readOnlyInspectMode) {
      return;
    }

    recalcMutation.mutate({});
    setStatusMessage('تمت إعادة حساب مؤشرات محرك الزمن للعيادة');
  };

  return (
    <div dir="rtl" style={{ display: 'grid', gap: '1.25rem' }}>
      <GlassCard style={{ padding: '1.25rem' }}>
        <div
          style={{
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center',
            flexWrap: 'wrap',
            gap: '0.75rem',
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.6rem' }}>
            <Clock size={22} />
            <div>
              <h2 style={{ margin: 0 }}>محرك متوسط الزمن الذكي والتأخير</h2>
              <p style={{ margin: 0, opacity: 0.78, fontSize: '0.9rem' }}>
                تعلم مدة الكشف الفعلية لكل طبيب وفئة موعد مع استبعاد القيم الشاذة وحدود التغيير التدريجي
              </p>
            </div>
          </div>
          {readOnlyInspectMode ? (
            <span data-testid="time-engine-readonly-badge" style={{ display: 'inline-flex', alignItems: 'center', gap: '0.35rem' }}>
              <ShieldCheck size={16} />
              وضع الفحص للقراءة فقط
            </span>
          ) : (
            <GlassButton onClick={handleRecalculate}>
              <RefreshCw size={16} />
              إعادة الحساب الآن
            </GlassButton>
          )}
        </div>

        {statusMessage && (
          <div style={{ marginBlockStart: '0.75rem' }}>
            <AlertBanner variant="info" title="تحديث" message={statusMessage} />
          </div>
        )}
      </GlassCard>

      <GlassCard style={{ padding: '1.25rem' }}>
        <h3 style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBlockEnd: '0.75rem' }}>
          <SlidersHorizontal size={18} />
          إعدادات العيادة الإحصائية
        </h3>
        <div style={{ display: 'flex', gap: '1rem', flexWrap: 'wrap', alignItems: 'flex-end' }}>
          <label>
            الحد الأدنى للعينات
            <input
              aria-label="الحد الأدنى للعينات"
              type="number"
              min={2}
              max={100}
              disabled={readOnlyInspectMode}
              value={settings.minSampleSize}
              onChange={(e) =>
                setSettings((prev) => ({ ...prev, minSampleSize: Math.max(2, Number(e.target.value) || 5) }))
              }
              style={{ display: 'block', marginBlockStart: '0.25rem', padding: '0.45rem', width: '110px' }}
            />
          </label>
          <label>
            نسبة قص القيم الشاذة
            <input
              aria-label="نسبة قص القيم الشاذة"
              type="number"
              step="0.05"
              min={0}
              max={0.4}
              disabled={readOnlyInspectMode}
              value={settings.trimFraction}
              onChange={(e) =>
                setSettings((prev) => ({ ...prev, trimFraction: Math.min(0.4, Math.max(0, Number(e.target.value) || 0)) }))
              }
              style={{ display: 'block', marginBlockStart: '0.25rem', padding: '0.45rem', width: '110px' }}
            />
          </label>
          <label>
            حد التغيير الأقصى (%)
            <input
              aria-label="حد التغيير الأقصى (%)"
              type="number"
              min={1}
              max={100}
              disabled={readOnlyInspectMode}
              value={settings.maxStepChangePercent}
              onChange={(e) =>
                setSettings((prev) => ({
                  ...prev,
                  maxStepChangePercent: Math.min(100, Math.max(1, Number(e.target.value) || 20)),
                }))
              }
              style={{ display: 'block', marginBlockStart: '0.25rem', padding: '0.45rem', width: '120px' }}
            />
          </label>
          <GlassButton disabled={readOnlyInspectMode} onClick={handleSaveSettings}>
            حفظ إعدادات المحرك
          </GlassButton>
        </div>
      </GlassCard>

      <GlassCard style={{ padding: '1.25rem' }}>
        <h3 style={{ marginBlockEnd: '0.75rem' }}>تفعيل المحرك لكل طبيب</h3>
        <div style={{ display: 'grid', gap: '0.6rem' }}>
          {doctors.map((doc) => (
            <div
              key={doc.doctorId}
              data-testid={`doctor-engine-row-${doc.doctorId}`}
              style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}
            >
              <span>{doc.doctorName}</span>
              <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
                <span>{doc.isEngineEnabled ? 'مفعّل' : 'متوقف (الافتراضي)'}</span>
                <GlassButton disabled={readOnlyInspectMode} onClick={() => handleToggleDoctor(doc.doctorId)}>
                  {doc.isEngineEnabled ? 'إيقاف للطبيب' : 'تفعيل للطبيب'}
                </GlassButton>
              </div>
            </div>
          ))}
        </div>
      </GlassCard>

      <GlassCard style={{ padding: '1.25rem' }}>
        <h3 style={{ marginBlockEnd: '0.75rem' }}>مقارنة المدة المقترحة مقابل المطبقة</h3>
        <div style={{ overflowX: 'auto' }}>
          <table style={{ width: '100%', borderCollapse: 'collapse' }}>
            <thead>
              <tr>
                <th style={{ textAlign: 'start', padding: '0.5rem' }}>الطبيب</th>
                <th style={{ textAlign: 'start', padding: '0.5rem' }}>الفئة</th>
                <th style={{ textAlign: 'start', padding: '0.5rem' }}>العينات / المستثنى</th>
                <th style={{ textAlign: 'start', padding: '0.5rem' }}>المتوسط المشذب</th>
                <th style={{ textAlign: 'start', padding: '0.5rem' }}>الافتراضي</th>
                <th style={{ textAlign: 'start', padding: '0.5rem' }}>المقترح</th>
                <th style={{ textAlign: 'start', padding: '0.5rem' }}>المطبق بالحجز</th>
              </tr>
            </thead>
            <tbody>
              {stats.map((row) => (
                <tr key={row.id} data-testid={`stat-row-${row.id}`}>
                  <td style={{ padding: '0.5rem' }}>{row.doctorName}</td>
                  <td style={{ padding: '0.5rem' }}>{row.categoryNameAr}</td>
                  <td style={{ padding: '0.5rem' }}>
                    {row.sampleCount} / {row.excusedCount}
                  </td>
                  <td style={{ padding: '0.5rem' }}>{row.trimmedAverageMinutes} د</td>
                  <td style={{ padding: '0.5rem' }}>{row.defaultDurationMinutes} د</td>
                  <td style={{ padding: '0.5rem' }}>{row.suggestedDurationMinutes} د</td>
                  <td style={{ padding: '0.5rem', fontWeight: 700 }}>
                    {row.appliedDurationMinutes} د{' '}
                    {row.isAdopted ? <CheckCircle2 size={14} style={{ verticalAlign: 'middle' }} /> : null}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </GlassCard>

      <GlassCard style={{ padding: '1.25rem' }}>
        <h3 style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBlockEnd: '0.75rem' }}>
          <AlertCircle size={18} />
          سجل أزمنة الجلسات واستثناءات التأخير
        </h3>
        <div style={{ marginBlockEnd: '0.75rem' }}>
          <label>
            سبب الاستثناء الافتراضي
            <input
              aria-label="سبب الاستثناء الافتراضي"
              type="text"
              disabled={readOnlyInspectMode}
              value={excuseDraftReason}
              onChange={(e) => setExcuseDraftReason(e.target.value)}
              style={{ marginInlineStart: '0.5rem', padding: '0.4rem', minWidth: '220px' }}
            />
          </label>
        </div>
        <div style={{ display: 'grid', gap: '0.5rem' }}>
          {durations.map((dur) => (
            <div
              key={dur.id}
              data-testid={`duration-row-${dur.visitId}`}
              style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap' }}
            >
              <span>
                المدة الفعلية: <strong>{dur.rawMinutes} د</strong> (المخطط: {dur.plannedMinutes} د)
                {dur.isExcused && dur.excuseReason ? ` — مستثنى: ${dur.excuseReason}` : ''}
              </span>
              <GlassButton disabled={readOnlyInspectMode} onClick={() => handleToggleExcuse(dur.visitId)}>
                {dur.isExcused ? 'إلغاء الاستثناء' : 'استثناء من الحساب'}
              </GlassButton>
            </div>
          ))}
        </div>
      </GlassCard>
    </div>
  );
};
