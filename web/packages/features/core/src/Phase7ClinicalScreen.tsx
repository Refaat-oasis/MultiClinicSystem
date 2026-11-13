import React, { useMemo, useState } from 'react';
import { GlassCard, GlassButton, GlassInput, AlertBanner } from '@clinic/ui';
import {
  Stethoscope,
  Clock,
  AlertTriangle,
  Pill,
  Printer,
  CheckCircle2,
  FlaskConical,
  FileSpreadsheet,
  Eye,
  Lock,
} from 'lucide-react';

export interface ClinicalAllergyBannerItem {
  id: string;
  allergen: string;
  severity: string;
  reaction?: string | null;
}

export interface ClinicalVitalEntry {
  id: string;
  bloodPressureSystolic?: number | null;
  bloodPressureDiastolic?: number | null;
  pulseBpm?: number | null;
  temperatureCelsius?: number | null;
  weightKg?: number | null;
  heightCm?: number | null;
  bmi?: number | null;
  spO2Percent?: number | null;
  glucoseMgDl?: number | null;
  recordedAtUtc: string;
}

export interface ClinicalDiagnosisEntry {
  id: string;
  icd10Code: string;
  titleAr: string;
  titleEn: string;
  isPrimary: boolean;
  notes?: string | null;
}

export interface ClinicalProcedureEntry {
  id: string;
  procedureCode: string;
  nameAr: string;
  nameEn: string;
  toothOrSite?: string | null;
}

export interface ClinicalPrescriptionItem {
  id: string;
  drugName: string;
  activeIngredient?: string | null;
  dosage: string;
  frequency: string;
  duration: string;
  triggeredAllergyWarning: boolean;
}

export interface ClinicalPrescriptionEntry {
  id: string;
  notes?: string | null;
  hasAllergyWarning: boolean;
  allergyWarningDetails?: string | null;
  items: ClinicalPrescriptionItem[];
  createdAtUtc: string;
}

export interface ClinicalToothStateEntry {
  fdiToothNumber: string;
  universalToothCode: string;
  isPrimaryTooth: boolean;
  condition: string;
  surfaces: string[];
  notes?: string | null;
}

export interface ClinicalSpecialtyFormDefinition {
  id: string;
  specialtyCode: string;
  titleAr: string;
  titleEn: string;
  jsonSchema: string;
}

export interface ClinicalVisitFormSubmission {
  id: string;
  clinicalFormId: string;
  specialtyCode: string;
  answersJson: string;
  savedAtUtc: string;
}

export interface ClinicalVisitSessionData {
  id: string;
  clinicId: string;
  patientId: string;
  patientName: string;
  doctorId: string;
  doctorName: string;
  specialtyCode: string;
  chiefComplaint?: string | null;
  diagnosisSummary?: string | null;
  advice?: string | null;
  privateDoctorNotes?: string | null;
  startedAtUtc: string;
  endedAtUtc?: string | null;
  plannedDurationMinutes: number;
  elapsedMinutes: number;
  overtimeMinutes: number;
  isOvertimeExcused: boolean;
  overtimeExcuseReason?: string | null;
  status: string;
  isVisibleToPatient: boolean;
  supportsDentalChart: boolean;
  patientAllergies: ClinicalAllergyBannerItem[];
  vitals: ClinicalVitalEntry[];
  diagnoses: ClinicalDiagnosisEntry[];
  procedures: ClinicalProcedureEntry[];
  prescriptions: ClinicalPrescriptionEntry[];
  dentalChart: ClinicalToothStateEntry[];
  formSubmissions: ClinicalVisitFormSubmission[];
}

export interface Phase7ClinicalScreenProps {
  clinicId: string;
  initialVisit?: ClinicalVisitSessionData;
  specialtyForms?: ClinicalSpecialtyFormDefinition[];
  readOnlyInspectMode?: boolean;
  onRecordVitals?: (payload: {
    bloodPressureSystolic?: number;
    bloodPressureDiastolic?: number;
    pulseBpm?: number;
    weightKg?: number;
    heightCm?: number;
  }) => void;
  onAddDiagnosis?: (payload: { icd10Code: string; titleAr: string; titleEn: string }) => void;
  onCreatePrescription?: (payload: {
    drugName: string;
    activeIngredient?: string;
    dosage: string;
    frequency: string;
    duration: string;
    acknowledgeAllergyWarning: boolean;
  }) => void;
  onDownloadPrescriptionPdf?: (prescriptionId: string) => void;
  onRecordToothState?: (payload: {
    toothCode: string;
    numberingSystem: 'FDI' | 'Universal';
    condition: string;
  }) => void;
  onSaveSpecialtyForm?: (payload: { clinicalFormId: string; answersJson: string }) => void;
  onCompleteVisit?: (payload: { isOvertimeExcused: boolean; overtimeExcuseReason?: string }) => void;
}

const DEFAULT_SAMPLE_VISIT: ClinicalVisitSessionData = {
  id: 'visit-sample-1',
  clinicId: 'clinic-1',
  patientId: 'pat-1',
  patientName: 'فاطمة علي',
  doctorId: 'doc-1',
  doctorName: 'د. أحمد محمود',
  specialtyCode: 'dental',
  chiefComplaint: 'ألم بالضرس العلوي الأيمن',
  diagnosisSummary: 'تسوس عميق بالضرس 16',
  advice: 'تجنب المشروبات الباردة لمدة 48 ساعة',
  privateDoctorNotes: 'ملاحظة سرية للطبيب فقط',
  startedAtUtc: '2026-06-08T09:00:00Z',
  plannedDurationMinutes: 20,
  elapsedMinutes: 25,
  overtimeMinutes: 5,
  isOvertimeExcused: false,
  status: 'InProgress',
  isVisibleToPatient: true,
  supportsDentalChart: true,
  patientAllergies: [
    { id: 'alg-1', allergen: 'Penicillin', severity: 'Severe', reaction: 'Anaphylaxis' },
  ],
  vitals: [
    {
      id: 'vit-1',
      bloodPressureSystolic: 120,
      bloodPressureDiastolic: 80,
      pulseBpm: 74,
      weightKg: 72,
      heightCm: 160,
      bmi: 28.13,
      recordedAtUtc: '2026-06-08T09:05:00Z',
    },
  ],
  diagnoses: [
    {
      id: 'diag-1',
      icd10Code: 'K02.9',
      titleAr: 'تسوس الأسنان غير محدد',
      titleEn: 'Dental caries, unspecified',
      isPrimary: true,
    },
  ],
  procedures: [
    {
      id: 'proc-1',
      procedureCode: 'D2392',
      nameAr: 'حشو تجميلي ضوئي',
      nameEn: 'Composite Filling',
      toothOrSite: '16',
    },
  ],
  prescriptions: [],
  dentalChart: [
    {
      fdiToothNumber: '16',
      universalToothCode: '3',
      isPrimaryTooth: false,
      condition: 'Caries',
      surfaces: ['O', 'M'],
      notes: 'Deep occlusal caries',
    },
  ],
  formSubmissions: [],
};

const FDI_PERMANENT_SAMPLE: ReadonlyArray<{ fdi: string; universal: string }> = [
  { fdi: '16', universal: '3' },
  { fdi: '15', universal: '4' },
  { fdi: '14', universal: '5' },
  { fdi: '11', universal: '8' },
  { fdi: '21', universal: '9' },
  { fdi: '26', universal: '14' },
  { fdi: '36', universal: '19' },
  { fdi: '46', universal: '30' },
];

export const Phase7ClinicalScreen: React.FC<Phase7ClinicalScreenProps> = ({
  initialVisit = DEFAULT_SAMPLE_VISIT,
  specialtyForms = [],
  readOnlyInspectMode = false,
  onRecordVitals,
  onAddDiagnosis,
  onCreatePrescription,
  onDownloadPrescriptionPdf,
  onRecordToothState,
  onSaveSpecialtyForm,
  onCompleteVisit,
}) => {
  const [visit, setVisit] = useState<ClinicalVisitSessionData>(initialVisit);
  const [notation, setNotation] = useState<'FDI' | 'Universal'>('FDI');
  const [selectedTooth, setSelectedTooth] = useState<string>('16');
  const [toothCondition, setToothCondition] = useState<string>('Filled');

  const [weightKg, setWeightKg] = useState<string>('72');
  const [heightCm, setHeightCm] = useState<string>('160');
  const [bpSys, setBpSys] = useState<string>('120');
  const [bpDia, setBpDia] = useState<string>('80');

  const [icdQuery, setIcdQuery] = useState<string>('K02.9');
  const [diagTitleAr, setDiagTitleAr] = useState<string>('تسوس الأسنان');

  const [drugName, setDrugName] = useState<string>('Amoxicillin 500mg');
  const [activeIngredient, setActiveIngredient] = useState<string>('Penicillin');
  const [dosage, setDosage] = useState<string>('500mg');
  const [frequency, setFrequency] = useState<string>('كل 8 ساعات');
  const [duration, setDuration] = useState<string>('7 أيام');
  const [acknowledgeAllergy, setAcknowledgeAllergy] = useState<boolean>(false);
  const [allergyBlockedError, setAllergyBlockedError] = useState<string | null>(null);

  const [selectedFormId, setSelectedFormId] = useState<string>(specialtyForms[0]?.id ?? '');
  const [formValues, setFormValues] = useState<Record<string, string>>({});
  const [formMessage, setFormMessage] = useState<string | null>(null);

  const [excuseOvertime, setExcuseOvertime] = useState<boolean>(false);
  const [excuseReason, setExcuseReason] = useState<string>('');
  const [statusNotice, setStatusNotice] = useState<string | null>(null);

  const computedBmi = useMemo(() => {
    const w = parseFloat(weightKg);
    const h = parseFloat(heightCm);
    if (!Number.isFinite(w) || !Number.isFinite(h) || w <= 0 || h <= 0) {
      return null;
    }
    const hm = h / 100;
    return Number((w / (hm * hm)).toFixed(2));
  }, [weightKg, heightCm]);

  const parsedFormFields = useMemo(() => {
    const form = specialtyForms.find((f) => f.id === selectedFormId);
    if (!form) {
      return [] as Array<{ key: string; type: string; required: boolean; min?: number; max?: number }>;
    }
    try {
      const schema = JSON.parse(form.jsonSchema) as {
        required?: string[];
        properties?: Record<string, { type?: string; minimum?: number; maximum?: number }>;
      };
      const reqSet = new Set(schema.required ?? []);
      return Object.entries(schema.properties ?? {}).map(([key, def]) => ({
        key,
        type: def.type ?? 'string',
        required: reqSet.has(key),
        min: def.minimum,
        max: def.maximum,
      }));
    } catch {
      return [];
    }
  }, [specialtyForms, selectedFormId]);

  const matchingAllergy = useMemo(() => {
    const d = drugName.trim().toLowerCase();
    const ing = activeIngredient.trim().toLowerCase();
    return visit.patientAllergies.find((a) => {
      const al = a.allergen.trim().toLowerCase();
      return (d && (d.includes(al) || al.includes(d))) || (ing && (ing.includes(al) || al.includes(ing)));
    });
  }, [drugName, activeIngredient, visit.patientAllergies]);

  const handleSaveVitals = () => {
    if (readOnlyInspectMode) return;
    const w = parseFloat(weightKg);
    const h = parseFloat(heightCm);
    const sys = parseInt(bpSys, 10);
    const dia = parseInt(bpDia, 10);
    const nextVital: ClinicalVitalEntry = {
      id: `vit-${Date.now()}`,
      bloodPressureSystolic: Number.isFinite(sys) ? sys : undefined,
      bloodPressureDiastolic: Number.isFinite(dia) ? dia : undefined,
      weightKg: Number.isFinite(w) ? w : undefined,
      heightCm: Number.isFinite(h) ? h : undefined,
      bmi: computedBmi,
      recordedAtUtc: new Date().toISOString(),
    };
    setVisit((prev) => ({ ...prev, vitals: [nextVital, ...prev.vitals] }));
    onRecordVitals?.({
      bloodPressureSystolic: nextVital.bloodPressureSystolic ?? undefined,
      bloodPressureDiastolic: nextVital.bloodPressureDiastolic ?? undefined,
      weightKg: nextVital.weightKg ?? undefined,
      heightCm: nextVital.heightCm ?? undefined,
    });
    setStatusNotice('تم حفظ العلامات الحيوية وحساب مؤشر كتلة الجسم');
  };

  const handleAddDiagnosis = () => {
    if (readOnlyInspectMode || !icdQuery.trim()) return;
    const entry: ClinicalDiagnosisEntry = {
      id: `diag-${Date.now()}`,
      icd10Code: icdQuery.trim().toUpperCase(),
      titleAr: diagTitleAr.trim() || icdQuery.trim(),
      titleEn: icdQuery.trim(),
      isPrimary: visit.diagnoses.length === 0,
    };
    setVisit((prev) => ({ ...prev, diagnoses: [entry, ...prev.diagnoses] }));
    onAddDiagnosis?.({ icd10Code: entry.icd10Code, titleAr: entry.titleAr, titleEn: entry.titleEn });
    setStatusNotice(`تمت إضافة التشخيص ${entry.icd10Code}`);
  };

  const handleIssuePrescription = () => {
    if (readOnlyInspectMode) return;
    if (matchingAllergy && !acknowledgeAllergy) {
      setAllergyBlockedError(
        `تنبيه حساسية المريض (${matchingAllergy.allergen} - ${matchingAllergy.severity}): يجب تأكيد التجاوز الطبي قبل إصدار الوصفة.`,
      );
      return;
    }

    setAllergyBlockedError(null);
    const rx: ClinicalPrescriptionEntry = {
      id: `rx-${Date.now()}`,
      hasAllergyWarning: Boolean(matchingAllergy),
      allergyWarningDetails: matchingAllergy ? `${matchingAllergy.allergen} (${matchingAllergy.severity})` : null,
      items: [
        {
          id: `rxi-${Date.now()}`,
          drugName: drugName.trim(),
          activeIngredient: activeIngredient.trim() || null,
          dosage: dosage.trim(),
          frequency: frequency.trim(),
          duration: duration.trim(),
          triggeredAllergyWarning: Boolean(matchingAllergy),
        },
      ],
      createdAtUtc: new Date().toISOString(),
    };

    setVisit((prev) => ({ ...prev, prescriptions: [rx, ...prev.prescriptions] }));
    onCreatePrescription?.({
      drugName: drugName.trim(),
      activeIngredient: activeIngredient.trim() || undefined,
      dosage: dosage.trim(),
      frequency: frequency.trim(),
      duration: duration.trim(),
      acknowledgeAllergyWarning: acknowledgeAllergy,
    });
    setStatusNotice('تم إصدار الوصفة الطبية بنجاح');
  };

  const handleRecordTooth = () => {
    if (readOnlyInspectMode) return;
    const mapping = FDI_PERMANENT_SAMPLE.find(
      (t) => t.fdi === selectedTooth || t.universal === selectedTooth,
    ) ?? { fdi: selectedTooth, universal: selectedTooth };
    const updated: ClinicalToothStateEntry = {
      fdiToothNumber: mapping.fdi,
      universalToothCode: mapping.universal,
      isPrimaryTooth: false,
      condition: toothCondition,
      surfaces: ['O'],
    };
    setVisit((prev) => ({
      ...prev,
      dentalChart: [
        updated,
        ...prev.dentalChart.filter((t) => t.fdiToothNumber !== mapping.fdi),
      ],
    }));
    onRecordToothState?.({
      toothCode: notation === 'FDI' ? mapping.fdi : mapping.universal,
      numberingSystem: notation,
      condition: toothCondition,
    });
    setStatusNotice(`تم تحديث حالة السن ${mapping.fdi} (${toothCondition})`);
  };

  const handleSubmitSpecialtyForm = () => {
    if (readOnlyInspectMode || !selectedFormId) return;
    for (const field of parsedFormFields) {
      const raw = formValues[field.key];
      if (field.required && (!raw || !raw.trim())) {
        setFormMessage(`الحقل "${field.key}" مطلوب حسب نموذج التخصص`);
        return;
      }
      if (field.type === 'number' && raw !== undefined && raw !== '') {
        const num = Number(raw);
        if (
          Number.isNaN(num)
          || (field.min !== undefined && num < field.min)
          || (field.max !== undefined && num > field.max)
        ) {
          setFormMessage(`القيمة في "${field.key}" خارج النطاق المسموح`);
          return;
        }
      }
    }

    const typedAnswers: Record<string, string | number> = {};
    for (const field of parsedFormFields) {
      const raw = formValues[field.key];
      if (raw !== undefined && raw !== '') {
        typedAnswers[field.key] = field.type === 'number' ? Number(raw) : raw;
      }
    }
    const answersJson = JSON.stringify(typedAnswers);
    setVisit((prev) => ({
      ...prev,
      formSubmissions: [
        {
          id: `sub-${Date.now()}`,
          clinicalFormId: selectedFormId,
          specialtyCode: prev.specialtyCode,
          answersJson,
          savedAtUtc: new Date().toISOString(),
        },
        ...prev.formSubmissions,
      ],
    }));
    onSaveSpecialtyForm?.({ clinicalFormId: selectedFormId, answersJson });
    setFormMessage('تم حفظ نموذج التخصص بنجاح');
  };

  const handleCompleteVisit = () => {
    if (readOnlyInspectMode) return;
    setVisit((prev) => ({
      ...prev,
      status: 'Completed',
      isOvertimeExcused: excuseOvertime,
      overtimeExcuseReason: excuseReason.trim() || null,
      endedAtUtc: new Date().toISOString(),
    }));
    onCompleteVisit?.({
      isOvertimeExcused: excuseOvertime,
      overtimeExcuseReason: excuseReason.trim() || undefined,
    });
    setStatusNotice('تم إنهاء الجلسة السريرية وإرسال حدث SessionCompleted');
  };

  return (
    <div className="space-y-4" dir="rtl" data-testid="phase7-clinical-screen">
      {readOnlyInspectMode && (
        <AlertBanner
          variant="info"
          title="وضع الفحص للمشرف العام (للقراءة فقط)"
          message="استعراض الجلسات السريرية والنماذج بدون تعديل."
        />
      )}

      {visit.patientAllergies.length > 0 && (
        <div
          className="rounded-xl border border-rose-500/40 bg-rose-500/15 p-3 text-rose-700 dark:text-rose-200 flex items-start gap-3"
          data-testid="clinical-allergy-banner"
        >
          <AlertTriangle className="h-5 w-5 shrink-0 mt-0.5" />
          <div>
            <div className="font-bold text-sm">تنبيه حساسية دائم — ملف المريض: {visit.patientName}</div>
            <ul className="text-xs mt-1 space-y-0.5">
              {visit.patientAllergies.map((a) => (
                <li key={a.id}>
                  • <strong>{a.allergen}</strong> ({a.severity}) {a.reaction ? `— ${a.reaction}` : ''}
                </li>
              ))}
            </ul>
          </div>
        </div>
      )}

      <GlassCard className="p-4 flex flex-wrap items-center justify-between gap-4">
        <div className="flex items-center gap-3">
          <Stethoscope className="h-6 w-6 text-sky-400" />
          <div>
            <h2 className="text-base font-bold text-[var(--glass-text)]">
              جلسة الكشف — {visit.patientName} ({visit.specialtyCode})
            </h2>
            <p className="text-xs text-[var(--glass-muted)]">
              الطبيب: {visit.doctorName} | الحالة: <strong>{visit.status}</strong>
            </p>
          </div>
        </div>

        <div className="flex items-center gap-3">
          <div
            className={`flex items-center gap-2 rounded-lg px-3 py-1.5 text-xs font-semibold ${
              visit.overtimeMinutes > 0
                ? 'bg-amber-500/20 text-amber-700 dark:text-amber-300 border border-amber-500/40'
                : 'bg-emerald-500/15 text-emerald-700 dark:text-emerald-300'
            }`}
            data-testid="visit-server-timer"
          >
            <Clock className="h-4 w-4" />
            <span>
              المنقضي: {visit.elapsedMinutes} د / المخطط: {visit.plannedDurationMinutes} د
              {visit.overtimeMinutes > 0 ? ` (+${visit.overtimeMinutes} د إضافي)` : ''}
            </span>
          </div>

          <button
            type="button"
            disabled={readOnlyInspectMode}
            onClick={() => setVisit((prev) => ({ ...prev, isVisibleToPatient: !prev.isVisibleToPatient }))}
            className="inline-flex items-center gap-1.5 rounded-lg border border-[var(--glass-border)] px-3 py-1.5 text-xs text-[var(--glass-text)]"
          >
            {visit.isVisibleToPatient ? <Eye className="h-3.5 w-3.5 text-emerald-500" /> : <Lock className="h-3.5 w-3.5 text-amber-500" />}
            {visit.isVisibleToPatient ? 'ظاهر ببوابة المريض' : 'مخفي عن البوابة'}
          </button>
        </div>
      </GlassCard>

      {statusNotice && (
        <AlertBanner variant="success" title="تحديث الجلسة" message={statusNotice} />
      )}

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
        <GlassCard className="p-4 space-y-3">
          <h3 className="text-sm font-bold text-[var(--glass-text)]">العلامات الحيوية ومؤشر كتلة الجسم (BMI)</h3>
          <div className="grid grid-cols-2 sm:grid-cols-4 gap-2">
            <GlassInput label="الضغط الانقباضي" value={bpSys} onChange={(e) => setBpSys(e.target.value)} disabled={readOnlyInspectMode} />
            <GlassInput label="الضغط الانبساطي" value={bpDia} onChange={(e) => setBpDia(e.target.value)} disabled={readOnlyInspectMode} />
            <GlassInput label="الوزن (كجم)" value={weightKg} onChange={(e) => setWeightKg(e.target.value)} disabled={readOnlyInspectMode} />
            <GlassInput label="الطول (سم)" value={heightCm} onChange={(e) => setHeightCm(e.target.value)} disabled={readOnlyInspectMode} />
          </div>
          <div className="flex items-center justify-between text-xs">
            <span data-testid="bmi-preview" className="text-[var(--glass-muted)]">
              مؤشر كتلة الجسم المحسوب: <strong>{computedBmi ?? '—'}</strong>
            </span>
            {!readOnlyInspectMode && (
              <GlassButton variant="regular" onClick={handleSaveVitals}>حفظ العلامات</GlassButton>
            )}
          </div>
        </GlassCard>

        <GlassCard className="p-4 space-y-3">
          <h3 className="text-sm font-bold text-[var(--glass-text)]">التشخيصات الطبية (ICD-10)</h3>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
            <GlassInput label="كود ICD-10" value={icdQuery} onChange={(e) => setIcdQuery(e.target.value)} disabled={readOnlyInspectMode} />
            <GlassInput label="وصف التشخيص" value={diagTitleAr} onChange={(e) => setDiagTitleAr(e.target.value)} disabled={readOnlyInspectMode} />
          </div>
          {!readOnlyInspectMode && (
            <GlassButton variant="regular" onClick={handleAddDiagnosis}>إضافة تشخيص</GlassButton>
          )}
          <div className="space-y-1 text-xs">
            {visit.diagnoses.map((d) => (
              <div key={d.id} className="rounded bg-white/5 px-2.5 py-1.5 flex justify-between">
                <span>{d.titleAr}</span>
                <span className="font-mono text-sky-400" dir="ltr">{d.icd10Code}</span>
              </div>
            ))}
          </div>
        </GlassCard>
      </div>

      <GlassCard className="p-4 space-y-3">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <Pill className="h-4 w-4 text-sky-400" />
            <h3 className="text-sm font-bold text-[var(--glass-text)]">الوصفة الطبية وفحص تعارض الحساسية</h3>
          </div>
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-5 gap-2">
          <GlassInput label="الدواء" value={drugName} onChange={(e) => setDrugName(e.target.value)} disabled={readOnlyInspectMode} />
          <GlassInput label="المادة الفعالة" value={activeIngredient} onChange={(e) => setActiveIngredient(e.target.value)} disabled={readOnlyInspectMode} />
          <GlassInput label="الجرعة" value={dosage} onChange={(e) => setDosage(e.target.value)} disabled={readOnlyInspectMode} />
          <GlassInput label="التكرار" value={frequency} onChange={(e) => setFrequency(e.target.value)} disabled={readOnlyInspectMode} />
          <GlassInput label="المدة" value={duration} onChange={(e) => setDuration(e.target.value)} disabled={readOnlyInspectMode} />
        </div>

        {matchingAllergy && (
          <label className="flex items-center gap-2 text-xs text-amber-600 dark:text-amber-300">
            <input
              type="checkbox"
              checked={acknowledgeAllergy}
              disabled={readOnlyInspectMode}
              onChange={(e) => setAcknowledgeAllergy(e.target.checked)}
              data-testid="ack-allergy-checkbox"
            />
            أقرّ بمراجعة حساسية المريض ({matchingAllergy.allergen}) والموافقة على الوصف تحت الإشراف الطبي
          </label>
        )}

        {allergyBlockedError && (
          <AlertBanner variant="error" title="تعارض حساسية دوائية" message={allergyBlockedError} />
        )}

        {!readOnlyInspectMode && (
          <GlassButton variant="prominent" onClick={handleIssuePrescription}>إصدار الروشتة</GlassButton>
        )}

        {visit.prescriptions.length > 0 && (
          <div className="space-y-2 pt-2">
            {visit.prescriptions.map((rx) => (
              <div key={rx.id} className="rounded-lg border border-[var(--glass-border)] p-2.5 flex items-center justify-between text-xs">
                <div>
                  {rx.items.map((item) => (
                    <span key={item.id} className="font-semibold">
                      {item.drugName} — {item.dosage} ({item.frequency} / {item.duration})
                    </span>
                  ))}
                  {rx.hasAllergyWarning && (
                    <span className="ms-2 text-amber-500 font-semibold">[تم تجاوز تنبيه الحساسية]</span>
                  )}
                </div>
                <GlassButton variant="default" onClick={() => onDownloadPrescriptionPdf?.(rx.id)}>
                  <Printer className="h-3.5 w-3.5 me-1 inline" /> PDF
                </GlassButton>
              </div>
            ))}
          </div>
        )}
      </GlassCard>

      {visit.supportsDentalChart ? (
        <GlassCard className="p-4 space-y-3" data-testid="dental-chart-panel">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <h3 className="text-sm font-bold text-[var(--glass-text)]">مخطط الأسنان التفاعلي (Dental Chart Plugin)</h3>
            <div className="flex items-center gap-2">
              <GlassButton
                variant={notation === 'FDI' ? 'prominent' : 'default'}
                onClick={() => setNotation('FDI')}
              >
                نظام FDI
              </GlassButton>
              <GlassButton
                variant={notation === 'Universal' ? 'prominent' : 'default'}
                onClick={() => setNotation('Universal')}
              >
                نظام Universal
              </GlassButton>
            </div>
          </div>

          <div className="flex flex-wrap gap-2">
            {FDI_PERMANENT_SAMPLE.map((tooth) => {
              const code = notation === 'FDI' ? tooth.fdi : tooth.universal;
              const recorded = visit.dentalChart.find((t) => t.fdiToothNumber === tooth.fdi);
              const isSelected = selectedTooth === tooth.fdi || selectedTooth === tooth.universal;
              return (
                <button
                  key={tooth.fdi}
                  type="button"
                  onClick={() => setSelectedTooth(code)}
                  className={`rounded-lg border px-3 py-2 text-xs transition ${
                    isSelected
                      ? 'border-sky-400 bg-sky-500/20 font-bold'
                      : 'border-[var(--glass-border)] bg-white/5'
                  }`}
                  data-testid={`tooth-btn-${tooth.fdi}`}
                >
                  <div dir="ltr">{code}</div>
                  <div className="text-[10px] text-[var(--glass-muted)]">
                    {recorded?.condition ?? 'Healthy'}
                  </div>
                </button>
              );
            })}
          </div>

          {!readOnlyInspectMode && (
            <div className="flex flex-wrap items-end gap-2">
              <label className="text-xs space-y-1">
                <span>الحالة السنية للسن ({selectedTooth})</span>
                <select
                  value={toothCondition}
                  onChange={(e) => setToothCondition(e.target.value)}
                  className="block rounded-lg border border-[var(--glass-border)] bg-[var(--glass-surface)] px-3 py-2 text-xs text-[var(--glass-text)]"
                >
                  <option value="Healthy">سليم (Healthy)</option>
                  <option value="Caries">تسوس (Caries)</option>
                  <option value="Filled">حشو (Filled)</option>
                  <option value="Crown">طربوش (Crown)</option>
                  <option value="RootCanal">علاج جذور (RootCanal)</option>
                  <option value="Extracted">مخلوع (Extracted)</option>
                  <option value="Implant">زراعة (Implant)</option>
                </select>
              </label>
              <GlassButton variant="regular" onClick={handleRecordTooth}>تسجيل حالة السن</GlassButton>
            </div>
          )}
        </GlassCard>
      ) : (
        <GlassCard className="p-4 space-y-3" data-testid="specialty-form-panel">
          <div className="flex items-center gap-2">
            <FileSpreadsheet className="h-4 w-4 text-sky-400" />
            <h3 className="text-sm font-bold text-[var(--glass-text)]">
              نموذج التخصص ({visit.specialtyCode}) — JSON Schema
            </h3>
          </div>

          {specialtyForms.length > 0 ? (
            <>
              <select
                value={selectedFormId}
                onChange={(e) => setSelectedFormId(e.target.value)}
                disabled={readOnlyInspectMode}
                className="rounded-lg border border-[var(--glass-border)] bg-[var(--glass-surface)] px-3 py-2 text-xs text-[var(--glass-text)]"
              >
                {specialtyForms.map((f) => (
                  <option key={f.id} value={f.id}>{f.titleAr} ({f.specialtyCode})</option>
                ))}
              </select>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
                {parsedFormFields.map((field) => (
                  <GlassInput
                    key={field.key}
                    label={`${field.key}${field.required ? ' *' : ''}${field.min !== undefined ? ` (${field.min}-${field.max})` : ''}`}
                    value={formValues[field.key] ?? ''}
                    onChange={(e) => setFormValues((prev) => ({ ...prev, [field.key]: e.target.value }))}
                    disabled={readOnlyInspectMode}
                  />
                ))}
              </div>
              {formMessage && <p className="text-xs text-sky-400">{formMessage}</p>}
              {!readOnlyInspectMode && (
                <GlassButton variant="regular" onClick={handleSubmitSpecialtyForm}>حفظ بيانات النموذج</GlassButton>
              )}
            </>
          ) : (
            <p className="text-xs text-[var(--glass-muted)]">لا توجد نماذج JSON Schema معرفة لهذا التخصص بعد.</p>
          )}
        </GlassCard>
      )}

      {!readOnlyInspectMode && visit.status !== 'Completed' && (
        <GlassCard className="p-4 flex flex-wrap items-center justify-between gap-3">
          <div className="flex flex-wrap items-center gap-3">
            <FlaskConical className="h-4 w-4 text-sky-400" />
            {visit.overtimeMinutes > 0 && (
              <label className="flex items-center gap-2 text-xs">
                <input
                  type="checkbox"
                  checked={excuseOvertime}
                  onChange={(e) => setExcuseOvertime(e.target.checked)}
                />
                <span>إعفاء الوقت الإضافي</span>
                <input
                  type="text"
                  placeholder="سبب الوقت الإضافي..."
                  value={excuseReason}
                  onChange={(e) => setExcuseReason(e.target.value)}
                  className="rounded border border-[var(--glass-border)] bg-transparent px-2 py-1 text-xs"
                />
              </label>
            )}
          </div>
          <GlassButton variant="prominent" onClick={handleCompleteVisit}>
            <CheckCircle2 className="h-4 w-4 me-1 inline" /> إنهاء الجلسة السريرية
          </GlassButton>
        </GlassCard>
      )}
    </div>
  );
};

