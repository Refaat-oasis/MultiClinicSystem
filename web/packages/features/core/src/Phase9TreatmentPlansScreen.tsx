import React, { useMemo, useState } from 'react';
import { GlassCard, GlassButton, GlassInput, AlertBanner } from '@clinic/ui';
import {
  ClipboardList,
  CalendarClock,
  CheckCircle2,
  CreditCard,
  RotateCcw,
  Eye,
  EyeOff,
  PlusCircle,
} from 'lucide-react';

export type TreatmentPlanMode = 'FixedTotal' | 'PerSession';

export interface TreatmentPlanSessionData {
  id: string;
  sequenceNumber: number;
  title: string;
  intervalDaysFromPrevious: number;
  windowMinusDays: number;
  windowPlusDays: number;
  targetDate: string;
  earliestAllowedDate: string;
  latestAllowedDate: string;
  actualOrSlippedDate?: string | null;
  sessionPriceAmount: number;
  materialsCostAmount: number;
  linkedAppointmentId?: string | null;
  linkedVisitId?: string | null;
  linkedChargeId?: string | null;
  status: string;
  notes?: string | null;
}

export interface TreatmentPlanInstallmentData {
  id: string;
  installmentNumber: number;
  isUpfront: boolean;
  dueDate: string;
  amount: number;
  paidAmount: number;
  remainingAmount: number;
  currency: string;
  status: string;
  linkedPaymentId?: string | null;
  paidAtUtc?: string | null;
}

export interface TreatmentPlanData {
  id: string;
  clinicId: string;
  patientId: string;
  doctorId: string;
  name: string;
  mode: TreatmentPlanMode | string;
  fixedTotalAmount: number;
  includedMaterialsCostAmount: number;
  upfrontAmount: number;
  paidAmount: number;
  refundedAmount: number;
  totalBilledOrContractedAmount: number;
  remainingBalanceAmount: number;
  currency: string;
  status: string;
  totalSessionsCount: number;
  completedSessionsCount: number;
  remainingSessionsCount: number;
  completionPercentage: number;
  showCostToPatient: boolean;
  notes?: string | null;
  createdAtUtc: string;
  sessions: TreatmentPlanSessionData[];
  installments: TreatmentPlanInstallmentData[];
}

export interface CreateTreatmentPlanPayload {
  clinicId: string;
  patientId: string;
  doctorId: string;
  name: string;
  mode: TreatmentPlanMode;
  fixedTotalAmount: number;
  includedMaterialsCostAmount: number;
  upfrontAmount: number;
  startDate: string;
  showCostToPatient: boolean;
  sessions: Array<{
    sequenceNumber: number;
    title: string;
    intervalDaysFromPrevious: number;
    windowMinusDays: number;
    windowPlusDays: number;
    sessionPriceAmount: number;
    materialsCostAmount: number;
  }>;
  installments: Array<{
    installmentNumber: number;
    isUpfront: boolean;
    dueDate: string;
    amount: number;
  }>;
}

export interface SlipSessionPayload {
  clinicId: string;
  planId: string;
  sessionId: string;
  actualOrSlippedDate: string;
  reason: string;
}

export interface CompletePlanSessionPayload {
  clinicId: string;
  planId: string;
  sessionId: string;
  completedDate: string;
  additionalMaterialsCostAmount: number;
}

export interface PayPlanInstallmentPayload {
  clinicId: string;
  planId: string;
  installmentId: string;
  method: string;
  amount: number;
}

export interface RefundPlanInstallmentPayload {
  clinicId: string;
  planId: string;
  installmentId: string;
  amount: number;
  reason: string;
}

export interface Phase9TreatmentPlansScreenProps {
  clinicId: string;
  readOnlyInspectMode?: boolean;
  initialPlans?: TreatmentPlanData[];
  onCreatePlan?: (payload: CreateTreatmentPlanPayload) => void;
  onRecordSessionSlip?: (payload: SlipSessionPayload) => void;
  onCompleteSession?: (payload: CompletePlanSessionPayload) => void;
  onPayInstallment?: (payload: PayPlanInstallmentPayload) => void;
  onRefundInstallment?: (payload: RefundPlanInstallmentPayload) => void;
  onTogglePortalCostVisibility?: (payload: { clinicId: string; planId: string; showCostToPatient: boolean }) => void;
}

function addDaysIso(isoDate: string, days: number): string {
  const parsed = new Date(`${isoDate}T00:00:00Z`);
  if (Number.isNaN(parsed.getTime())) {
    return isoDate;
  }
  parsed.setUTCDate(parsed.getUTCDate() + days);
  return parsed.toISOString().slice(0, 10);
}

const DEFAULT_PLANS: TreatmentPlanData[] = [
  {
    id: 'plan-demo-1',
    clinicId: 'clinic-1',
    patientId: 'pat-1',
    doctorId: 'doc-1',
    name: 'تقويم أسنان شامل — المرحلة الأولى',
    mode: 'FixedTotal',
    fixedTotalAmount: 12000,
    includedMaterialsCostAmount: 2400,
    upfrontAmount: 3000,
    paidAmount: 3000,
    refundedAmount: 0,
    totalBilledOrContractedAmount: 12000,
    remainingBalanceAmount: 9000,
    currency: 'EGP',
    status: 'Active',
    totalSessionsCount: 3,
    completedSessionsCount: 0,
    remainingSessionsCount: 3,
    completionPercentage: 0,
    showCostToPatient: true,
    createdAtUtc: '2026-06-01T09:00:00Z',
    sessions: [
      {
        id: 'sess-1',
        sequenceNumber: 1,
        title: 'تركيب الحاصرات والبدء',
        intervalDaysFromPrevious: 0,
        windowMinusDays: 0,
        windowPlusDays: 2,
        targetDate: '2026-06-10',
        earliestAllowedDate: '2026-06-10',
        latestAllowedDate: '2026-06-12',
        sessionPriceAmount: 0,
        materialsCostAmount: 1500,
        status: 'Planned',
      },
      {
        id: 'sess-2',
        sequenceNumber: 2,
        title: 'شد وتعديل السلك الأول',
        intervalDaysFromPrevious: 21,
        windowMinusDays: 2,
        windowPlusDays: 3,
        targetDate: '2026-07-01',
        earliestAllowedDate: '2026-06-29',
        latestAllowedDate: '2026-07-04',
        sessionPriceAmount: 0,
        materialsCostAmount: 450,
        status: 'Planned',
      },
      {
        id: 'sess-3',
        sequenceNumber: 3,
        title: 'متابعة التثبيت والشد الثاني',
        intervalDaysFromPrevious: 21,
        windowMinusDays: 2,
        windowPlusDays: 3,
        targetDate: '2026-07-22',
        earliestAllowedDate: '2026-07-20',
        latestAllowedDate: '2026-07-25',
        sessionPriceAmount: 0,
        materialsCostAmount: 450,
        status: 'Planned',
      },
    ],
    installments: [
      {
        id: 'inst-1',
        installmentNumber: 1,
        isUpfront: true,
        dueDate: '2026-06-10',
        amount: 3000,
        paidAmount: 3000,
        remainingAmount: 0,
        currency: 'EGP',
        status: 'Paid',
      },
      {
        id: 'inst-2',
        installmentNumber: 2,
        isUpfront: false,
        dueDate: '2026-07-10',
        amount: 4500,
        paidAmount: 0,
        remainingAmount: 4500,
        currency: 'EGP',
        status: 'Pending',
      },
      {
        id: 'inst-3',
        installmentNumber: 3,
        isUpfront: false,
        dueDate: '2026-08-10',
        amount: 4500,
        paidAmount: 0,
        remainingAmount: 4500,
        currency: 'EGP',
        status: 'Pending',
      },
    ],
  },
];

export const Phase9TreatmentPlansScreen: React.FC<Phase9TreatmentPlansScreenProps> = ({
  clinicId,
  readOnlyInspectMode = false,
  initialPlans,
  onCreatePlan,
  onRecordSessionSlip,
  onCompleteSession,
  onPayInstallment,
  onRefundInstallment,
  onTogglePortalCostVisibility,
}) => {
  const [plans, setPlans] = useState<TreatmentPlanData[]>(initialPlans ?? DEFAULT_PLANS);
  const [selectedPlanId, setSelectedPlanId] = useState<string>((initialPlans ?? DEFAULT_PLANS)[0]?.id ?? '');
  const [activeSubTab, setActiveSubTab] = useState<'overview' | 'builder'>('overview');

  // Plan builder state
  const [planName, setPlanName] = useState<string>('خطة علاج متعددة الجلسات');
  const [mode, setMode] = useState<TreatmentPlanMode>('FixedTotal');
  const [startDate, setStartDate] = useState<string>('2026-06-15');
  const [fixedTotalAmount, setFixedTotalAmount] = useState<string>('6000');
  const [upfrontAmount, setUpfrontAmount] = useState<string>('2000');
  const [includedMaterialsCost, setIncludedMaterialsCost] = useState<string>('1000');
  const [sessionCount, setSessionCount] = useState<string>('3');
  const [intervalDays, setIntervalDays] = useState<string>('14');
  const [windowMinusDays, setWindowMinusDays] = useState<string>('2');
  const [windowPlusDays, setWindowPlusDays] = useState<string>('3');
  const [perSessionPrice, setPerSessionPrice] = useState<string>('800');
  const [showCostToPatient, setShowCostToPatient] = useState<boolean>(true);
  const [feedbackError, setFeedbackError] = useState<string | null>(null);
  const [feedbackSuccess, setFeedbackSuccess] = useState<string | null>(null);

  // Slip & installment action states
  const [slipDateBySession, setSlipDateBySession] = useState<Record<string, string>>({});
  const [slipReasonBySession, setSlipReasonBySession] = useState<Record<string, string>>({});
  const [payAmountByInstallment, setPayAmountByInstallment] = useState<Record<string, string>>({});
  const [refundReasonByInstallment, setRefundReasonByInstallment] = useState<Record<string, string>>({});

  const selectedPlan = useMemo(
    () => plans.find((p) => p.id === selectedPlanId) ?? plans[0] ?? null,
    [plans, selectedPlanId],
  );

  const builderTotalPreview = useMemo(() => {
    const count = Math.max(1, Number(sessionCount) || 1);
    if (mode === 'FixedTotal') {
      return Number(fixedTotalAmount) || 0;
    }
    return count * (Number(perSessionPrice) || 0);
  }, [mode, fixedTotalAmount, sessionCount, perSessionPrice]);

  const handleCreatePlan = (e: React.FormEvent) => {
    e.preventDefault();
    if (readOnlyInspectMode) return;

    setFeedbackError(null);
    setFeedbackSuccess(null);

    const trimmedName = planName.trim();
    const count = Math.max(1, Number(sessionCount) || 0);
    const interval = Math.max(0, Number(intervalDays) || 0);
    const minus = Math.max(0, Number(windowMinusDays) || 0);
    const plus = Math.max(0, Number(windowPlusDays) || 0);
    const fixedTotal = Math.max(0, Number(fixedTotalAmount) || 0);
    const upfront = Math.max(0, Number(upfrontAmount) || 0);
    const materials = Math.max(0, Number(includedMaterialsCost) || 0);
    const unitPrice = Math.max(0, Number(perSessionPrice) || 0);

    if (!trimmedName) {
      setFeedbackError('اسم الخطة العلاجية مطلوب.');
      return;
    }

    if (mode === 'FixedTotal' && fixedTotal <= 0) {
      setFeedbackError('إجمالي العقد الثابت يجب أن يكون أكبر من صفر.');
      return;
    }

    if (mode === 'PerSession' && unitPrice <= 0) {
      setFeedbackError('سعر الجلسة يجب أن يكون أكبر من صفر.');
      return;
    }

    const sessionInputs = Array.from({ length: count }, (_, idx) => {
      const seq = idx + 1;
      return {
        sequenceNumber: seq,
        title: `الجلسة رقم ${seq}`,
        intervalDaysFromPrevious: idx === 0 ? 0 : interval,
        windowMinusDays: minus,
        windowPlusDays: plus,
        sessionPriceAmount: mode === 'PerSession' ? unitPrice : 0,
        materialsCostAmount: mode === 'PerSession' ? Math.round((materials / count) * 100) / 100 : 0,
      };
    });

    const installments =
      mode === 'FixedTotal'
        ? [
            ...(upfront > 0
              ? [{ installmentNumber: 1, isUpfront: true, dueDate: startDate, amount: upfront }]
              : []),
            ...(fixedTotal - upfront > 0
              ? [
                  {
                    installmentNumber: upfront > 0 ? 2 : 1,
                    isUpfront: false,
                    dueDate: addDaysIso(startDate, interval),
                    amount: fixedTotal - upfront,
                  },
                ]
              : []),
          ]
        : [];

    onCreatePlan?.({
      clinicId,
      patientId: 'pat-1',
      doctorId: 'doc-1',
      name: trimmedName,
      mode,
      fixedTotalAmount: fixedTotal,
      includedMaterialsCostAmount: materials,
      upfrontAmount: upfront,
      startDate,
      showCostToPatient,
      sessions: sessionInputs,
      installments,
    });

    let anchorDate = startDate;
    const generatedSessions: TreatmentPlanSessionData[] = sessionInputs.map((s, idx) => {
      anchorDate = idx === 0 ? startDate : addDaysIso(anchorDate, s.intervalDaysFromPrevious);
      return {
        id: `sess-${Date.now()}-${s.sequenceNumber}`,
        sequenceNumber: s.sequenceNumber,
        title: s.title,
        intervalDaysFromPrevious: s.intervalDaysFromPrevious,
        windowMinusDays: s.windowMinusDays,
        windowPlusDays: s.windowPlusDays,
        targetDate: anchorDate,
        earliestAllowedDate: addDaysIso(anchorDate, -s.windowMinusDays),
        latestAllowedDate: addDaysIso(anchorDate, s.windowPlusDays),
        sessionPriceAmount: s.sessionPriceAmount,
        materialsCostAmount: s.materialsCostAmount,
        status: 'Planned',
      };
    });

    const newPlanId = `plan-${Date.now()}`;
    const totalAmount = mode === 'FixedTotal' ? fixedTotal : count * unitPrice;
    const generatedInstallments: TreatmentPlanInstallmentData[] = installments.map((inst, idx) => ({
      id: `inst-${Date.now()}-${idx + 1}`,
      installmentNumber: inst.installmentNumber,
      isUpfront: inst.isUpfront,
      dueDate: inst.dueDate,
      amount: inst.amount,
      paidAmount: 0,
      remainingAmount: inst.amount,
      currency: 'EGP',
      status: 'Pending',
    }));

    const newPlan: TreatmentPlanData = {
      id: newPlanId,
      clinicId,
      patientId: 'pat-1',
      doctorId: 'doc-1',
      name: trimmedName,
      mode,
      fixedTotalAmount: fixedTotal,
      includedMaterialsCostAmount: materials,
      upfrontAmount: upfront,
      paidAmount: 0,
      refundedAmount: 0,
      totalBilledOrContractedAmount: totalAmount,
      remainingBalanceAmount: totalAmount,
      currency: 'EGP',
      status: 'Active',
      totalSessionsCount: generatedSessions.length,
      completedSessionsCount: 0,
      remainingSessionsCount: generatedSessions.length,
      completionPercentage: 0,
      showCostToPatient,
      createdAtUtc: new Date().toISOString(),
      sessions: generatedSessions,
      installments: generatedInstallments,
    };

    setPlans((prev) => [newPlan, ...prev]);
    setSelectedPlanId(newPlanId);
    setActiveSubTab('overview');
    setFeedbackSuccess(`تم إنشاء خطة العلاج "${trimmedName}" بـ ${generatedSessions.length} جلسات.`);
  };

  const handleRecordSlip = (plan: TreatmentPlanData, session: TreatmentPlanSessionData) => {
    if (readOnlyInspectMode) return;
    setFeedbackError(null);
    const newDate = (slipDateBySession[session.id] ?? addDaysIso(session.targetDate, 5)).trim();
    const reason = (slipReasonBySession[session.id] ?? '').trim();
    if (!reason) {
      setFeedbackError('سبب تأجيل/انزلاق الجلسة مطلوب.');
      return;
    }

    onRecordSessionSlip?.({
      clinicId,
      planId: plan.id,
      sessionId: session.id,
      actualOrSlippedDate: newDate,
      reason,
    });

    setPlans((prev) =>
      prev.map((p) => {
        if (p.id !== plan.id) return p;
        const sorted = [...p.sessions].sort((a, b) => a.sequenceNumber - b.sequenceNumber);
        let anchor = '';
        const updatedSessions = sorted.map((s) => {
          if (s.sequenceNumber < session.sequenceNumber) {
            return s;
          }
          if (s.id === session.id) {
            anchor = newDate;
            return {
              ...s,
              actualOrSlippedDate: newDate,
              targetDate: newDate,
              earliestAllowedDate: addDaysIso(newDate, -s.windowMinusDays),
              latestAllowedDate: addDaysIso(newDate, s.windowPlusDays),
              status: 'Delayed',
            };
          }
          const nextTarget = addDaysIso(anchor, s.intervalDaysFromPrevious);
          anchor = nextTarget;
          return {
            ...s,
            targetDate: nextTarget,
            earliestAllowedDate: addDaysIso(nextTarget, -s.windowMinusDays),
            latestAllowedDate: addDaysIso(nextTarget, s.windowPlusDays),
          };
        });
        return { ...p, sessions: updatedSessions };
      }),
    );
    setFeedbackSuccess(`تم إعادة حساب نوافذ الجلسات اللاحقة بعد انزلاق "${session.title}".`);
  };

  const handleCompleteSession = (plan: TreatmentPlanData, session: TreatmentPlanSessionData) => {
    if (readOnlyInspectMode) return;
    setFeedbackError(null);
    const completedDate = session.actualOrSlippedDate ?? session.targetDate;

    onCompleteSession?.({
      clinicId,
      planId: plan.id,
      sessionId: session.id,
      completedDate,
      additionalMaterialsCostAmount: 0,
    });

    setPlans((prev) =>
      prev.map((p) => {
        if (p.id !== plan.id) return p;
        const updatedSessions = p.sessions.map((s) =>
          s.id === session.id ? { ...s, status: 'Completed', actualOrSlippedDate: completedDate } : s,
        );
        const completedCount = updatedSessions.filter((s) => s.status === 'Completed').length;
        const total = updatedSessions.length;
        return {
          ...p,
          sessions: updatedSessions,
          completedSessionsCount: completedCount,
          remainingSessionsCount: total - completedCount,
          completionPercentage: Math.round((completedCount / total) * 10000) / 100,
          status: completedCount === total ? 'Completed' : 'Active',
        };
      }),
    );
    setFeedbackSuccess(`تم إتمام "${session.title}".`);
  };

  const handlePayInstallment = (plan: TreatmentPlanData, inst: TreatmentPlanInstallmentData) => {
    if (readOnlyInspectMode) return;
    setFeedbackError(null);
    const payAmount = Number(payAmountByInstallment[inst.id] ?? inst.remainingAmount) || 0;
    if (payAmount <= 0 || payAmount > inst.remainingAmount) {
      setFeedbackError('مبلغ التحصيل غير صالح أو يتجاوز المتبقي من القسط.');
      return;
    }

    onPayInstallment?.({
      clinicId,
      planId: plan.id,
      installmentId: inst.id,
      method: 'Cash',
      amount: payAmount,
    });

    setPlans((prev) =>
      prev.map((p) => {
        if (p.id !== plan.id) return p;
        const updatedInst = p.installments.map((item) => {
          if (item.id !== inst.id) return item;
          const nextPaid = item.paidAmount + payAmount;
          const nextRemaining = Math.max(0, item.amount - nextPaid);
          return {
            ...item,
            paidAmount: nextPaid,
            remainingAmount: nextRemaining,
            status: nextRemaining === 0 ? 'Paid' : 'PartiallyPaid',
          };
        });
        const newPaid = p.paidAmount + payAmount;
        return {
          ...p,
          installments: updatedInst,
          paidAmount: newPaid,
          remainingBalanceAmount: Math.max(0, p.totalBilledOrContractedAmount - (newPaid - p.refundedAmount)),
        };
      }),
    );
    setFeedbackSuccess(`تم تحصيل ${payAmount} ${inst.currency} من القسط #${inst.installmentNumber}.`);
  };

  const handleRefundInstallment = (plan: TreatmentPlanData, inst: TreatmentPlanInstallmentData) => {
    if (readOnlyInspectMode) return;
    setFeedbackError(null);
    const reason = (refundReasonByInstallment[inst.id] ?? '').trim();
    if (!reason) {
      setFeedbackError('سبب رد المقدم/الدفعة مطلوب.');
      return;
    }
    const refundable = Math.min(inst.paidAmount, plan.paidAmount - plan.refundedAmount);
    if (refundable <= 0) {
      setFeedbackError('لا يوجد رصيد مدفوع قابل للاسترداد.');
      return;
    }

    onRefundInstallment?.({
      clinicId,
      planId: plan.id,
      installmentId: inst.id,
      amount: refundable,
      reason,
    });

    setPlans((prev) =>
      prev.map((p) => {
        if (p.id !== plan.id) return p;
        const nextRefunded = p.refundedAmount + refundable;
        return {
          ...p,
          refundedAmount: nextRefunded,
          remainingBalanceAmount: Math.max(0, p.totalBilledOrContractedAmount - (p.paidAmount - nextRefunded)),
          installments: p.installments.map((item) =>
            item.id === inst.id ? { ...item, status: 'Refunded', remainingAmount: 0 } : item,
          ),
        };
      }),
    );
    setFeedbackSuccess(`تم رد ${refundable} ${inst.currency} بنجاح.`);
  };

  const handleToggleCostVisibility = (plan: TreatmentPlanData) => {
    if (readOnlyInspectMode) return;
    const nextValue = !plan.showCostToPatient;
    onTogglePortalCostVisibility?.({
      clinicId,
      planId: plan.id,
      showCostToPatient: nextValue,
    });
    setPlans((prev) =>
      prev.map((p) => (p.id === plan.id ? { ...p, showCostToPatient: nextValue } : p)),
    );
  };

  return (
    <div dir="rtl" className="space-y-4">
      {readOnlyInspectMode && (
        <AlertBanner
          variant="info"
          message="وضع الفحص للقراءة فقط (سوبر أدمن) — لا يمكن إنشاء أو تعديل خطط العلاج أو تحصيل الدفعات."
        />
      )}

      {feedbackError && <AlertBanner variant="error" message={feedbackError} />}
      {feedbackSuccess && <AlertBanner variant="success" message={feedbackSuccess} />}

      <GlassCard>
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div className="flex items-center gap-2">
            <ClipboardList size={20} />
            <h2 className="text-lg font-semibold">خطط العلاج متعددة الجلسات والنوافذ الزمنية</h2>
          </div>
          <div className="flex flex-wrap gap-2">
            <GlassButton
              type="button"
              variant={activeSubTab === 'overview' ? 'prominent' : 'default'}
              onClick={() => setActiveSubTab('overview')}
            >
              متابعة الخطط والجلسات
            </GlassButton>
            {!readOnlyInspectMode && (
              <GlassButton
                type="button"
                variant={activeSubTab === 'builder' ? 'prominent' : 'default'}
                onClick={() => setActiveSubTab('builder')}
              >
                <PlusCircle size={16} />
                بناء خطة علاج جديدة
              </GlassButton>
            )}
          </div>
        </div>
      </GlassCard>

      {activeSubTab === 'builder' && !readOnlyInspectMode && (
        <GlassCard>
          <form onSubmit={handleCreatePlan} className="space-y-4">
            <h3 className="text-base font-semibold">منشئ خطة العلاج (FixedTotal / PerSession)</h3>

            <div className="grid grid-cols-1 gap-3 md:grid-cols-3">
              <GlassInput
                label="اسم خطة العلاج"
                value={planName}
                onChange={(e) => setPlanName(e.target.value)}
              />
              <div>
                <label
                  htmlFor="plan-mode-select"
                  className="mb-1 block text-sm font-medium text-[var(--color-text-secondary)]"
                >
                  نظام تسعير الخطة
                </label>
                <select
                  id="plan-mode-select"
                  value={mode}
                  onChange={(e) => setMode(e.target.value as TreatmentPlanMode)}
                  className="glass-input w-full rounded-xl border border-[var(--glass-border)] bg-[var(--glass-surface)] px-3 py-2.5 text-sm"
                >
                  <option value="FixedTotal">إجمالي ثابت مع أقساط (FixedTotal)</option>
                  <option value="PerSession">الدفع حسب الجلسة (PerSession)</option>
                </select>
              </div>
              <GlassInput
                label="تاريخ البدء"
                type="date"
                value={startDate}
                onChange={(e) => setStartDate(e.target.value)}
              />
            </div>

            <div className="grid grid-cols-1 gap-3 md:grid-cols-4">
              {mode === 'FixedTotal' ? (
                <>
                  <GlassInput
                    label="إجمالي العقد الثابت"
                    type="number"
                    value={fixedTotalAmount}
                    onChange={(e) => setFixedTotalAmount(e.target.value)}
                  />
                  <GlassInput
                    label="المقدم (Upfront)"
                    type="number"
                    value={upfrontAmount}
                    onChange={(e) => setUpfrontAmount(e.target.value)}
                  />
                </>
              ) : (
                <GlassInput
                  label="سعر الجلسة الواحدة"
                  type="number"
                  value={perSessionPrice}
                  onChange={(e) => setPerSessionPrice(e.target.value)}
                />
              )}

              <GlassInput
                label="تكلفة الخامات الداخلية (للطاقم فقط)"
                type="number"
                value={includedMaterialsCost}
                onChange={(e) => setIncludedMaterialsCost(e.target.value)}
              />
              <GlassInput
                label="عدد الجلسات"
                type="number"
                value={sessionCount}
                onChange={(e) => setSessionCount(e.target.value)}
              />
            </div>

            <div className="grid grid-cols-1 gap-3 md:grid-cols-3">
              <GlassInput
                label="الفاصل بالأيام بين الجلسات"
                type="number"
                value={intervalDays}
                onChange={(e) => setIntervalDays(e.target.value)}
              />
              <GlassInput
                label="هامش التبكير (أيام)"
                type="number"
                value={windowMinusDays}
                onChange={(e) => setWindowMinusDays(e.target.value)}
              />
              <GlassInput
                label="هامش التأخير (أيام)"
                type="number"
                value={windowPlusDays}
                onChange={(e) => setWindowPlusDays(e.target.value)}
              />
            </div>

            <div className="flex flex-wrap items-center justify-between gap-3 pt-2">
              <label className="flex items-center gap-2 text-sm">
                <input
                  type="checkbox"
                  checked={showCostToPatient}
                  onChange={(e) => setShowCostToPatient(e.target.checked)}
                />
                <span>إظهار تكلفة الخطة للمريض في البوابة</span>
              </label>

              <div className="flex items-center gap-3">
                <span data-testid="plan-builder-total" className="text-sm font-semibold">
                  إجمالي الخطة المتوقع: {builderTotalPreview} EGP
                </span>
                <GlassButton type="submit" variant="prominent">
                  حفظ واعتماد الخطة
                </GlassButton>
              </div>
            </div>
          </form>
        </GlassCard>
      )}

      {activeSubTab === 'overview' && selectedPlan && (
        <div className="space-y-4">
          {plans.length > 1 && (
            <div className="flex flex-wrap gap-2">
              {plans.map((p) => (
                <GlassButton
                  key={p.id}
                  type="button"
                  variant={p.id === selectedPlan.id ? 'prominent' : 'default'}
                  onClick={() => setSelectedPlanId(p.id)}
                >
                  {p.name}
                </GlassButton>
              ))}
            </div>
          )}

          <GlassCard>
            <div className="flex flex-wrap items-center justify-between gap-3">
              <div>
                <h3 className="text-base font-semibold">{selectedPlan.name}</h3>
                <p className="text-xs text-[var(--color-text-secondary)]">
                  النظام: {selectedPlan.mode} | الحالة: {selectedPlan.status}
                </p>
              </div>

              {!readOnlyInspectMode && (
                <GlassButton
                  type="button"
                  variant="default"
                  onClick={() => handleToggleCostVisibility(selectedPlan)}
                >
                  {selectedPlan.showCostToPatient ? <Eye size={16} /> : <EyeOff size={16} />}
                  {selectedPlan.showCostToPatient ? 'التكلفة ظاهرة في البوابة' : 'التكلفة مخفية عن البوابة'}
                </GlassButton>
              )}
            </div>

            <div className="mt-4 grid grid-cols-2 gap-3 md:grid-cols-5">
              <div className="rounded-xl border border-[var(--glass-border)] p-3">
                <div className="text-xs text-[var(--color-text-secondary)]">نسبة الإنجاز</div>
                <div data-testid="plan-completion-pct" className="text-lg font-bold">
                  {selectedPlan.completionPercentage}% ({selectedPlan.completedSessionsCount}/{selectedPlan.totalSessionsCount})
                </div>
              </div>
              <div className="rounded-xl border border-[var(--glass-border)] p-3">
                <div className="text-xs text-[var(--color-text-secondary)]">إجمالي الخطة</div>
                <div className="text-lg font-bold">
                  {selectedPlan.totalBilledOrContractedAmount} {selectedPlan.currency}
                </div>
              </div>
              <div className="rounded-xl border border-[var(--glass-border)] p-3">
                <div className="text-xs text-[var(--color-text-secondary)]">المدفوع الصافي</div>
                <div className="text-lg font-bold">
                  {selectedPlan.paidAmount - selectedPlan.refundedAmount} {selectedPlan.currency}
                </div>
              </div>
              <div className="rounded-xl border border-[var(--glass-border)] p-3">
                <div className="text-xs text-[var(--color-text-secondary)]">الرصيد المتبقي</div>
                <div data-testid="plan-remaining-balance" className="text-lg font-bold">
                  {selectedPlan.remainingBalanceAmount} {selectedPlan.currency}
                </div>
              </div>
              <div className="rounded-xl border border-[var(--glass-border)] p-3">
                <div className="text-xs text-[var(--color-text-secondary)]">تكلفة الخامات (داخلي)</div>
                <div className="text-lg font-bold">
                  {selectedPlan.includedMaterialsCostAmount} {selectedPlan.currency}
                </div>
              </div>
            </div>
          </GlassCard>

          <GlassCard>
            <div className="mb-3 flex items-center gap-2">
              <CalendarClock size={18} />
              <h4 className="text-base font-semibold">جدول الجلسات والنوافذ الزمنية</h4>
            </div>

            <div className="space-y-3">
              {selectedPlan.sessions.map((sess) => (
                <div
                  key={sess.id}
                  data-testid={`plan-session-${sess.sequenceNumber}`}
                  className="rounded-xl border border-[var(--glass-border)] p-3"
                >
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <div>
                      <div className="font-semibold">
                        #{sess.sequenceNumber} — {sess.title}
                      </div>
                      <div className="text-xs text-[var(--color-text-secondary)]">
                        المستهدف: <span data-testid={`session-target-${sess.sequenceNumber}`}>{sess.targetDate}</span> | النافذة المسموحة:{' '}
                        [{sess.earliestAllowedDate} .. {sess.latestAllowedDate}] | الحالة: {sess.status}
                      </div>
                    </div>

                    {!readOnlyInspectMode && sess.status !== 'Completed' && (
                      <GlassButton
                        type="button"
                        variant="prominent"
                        onClick={() => handleCompleteSession(selectedPlan, sess)}
                      >
                        <CheckCircle2 size={15} />
                        إتمام الجلسة #{sess.sequenceNumber}
                      </GlassButton>
                    )}
                  </div>

                  {!readOnlyInspectMode && sess.status !== 'Completed' && (
                    <div className="mt-3 flex flex-wrap items-end gap-2">
                      <GlassInput
                        label={`تاريخ انزلاق الجلسة #${sess.sequenceNumber}`}
                        type="date"
                        value={slipDateBySession[sess.id] ?? addDaysIso(sess.targetDate, 5)}
                        onChange={(e) =>
                          setSlipDateBySession((prev) => ({ ...prev, [sess.id]: e.target.value }))
                        }
                      />
                      <GlassInput
                        label={`سبب التأجيل/الانزلاق #${sess.sequenceNumber}`}
                        value={slipReasonBySession[sess.id] ?? ''}
                        onChange={(e) =>
                          setSlipReasonBySession((prev) => ({ ...prev, [sess.id]: e.target.value }))
                        }
                      />
                      <GlassButton
                        type="button"
                        variant="default"
                        onClick={() => handleRecordSlip(selectedPlan, sess)}
                      >
                        تسجيل انزلاق وإعادة حساب النوافذ #{sess.sequenceNumber}
                      </GlassButton>
                    </div>
                  )}
                </div>
              ))}
            </div>
          </GlassCard>

          <GlassCard>
            <div className="mb-3 flex items-center gap-2">
              <CreditCard size={18} />
              <h4 className="text-base font-semibold">جدول الأقساط والمقدم</h4>
            </div>

            {selectedPlan.installments.length === 0 ? (
              <p className="text-sm text-[var(--color-text-secondary)]">
                خطة بنظام PerSession — يتم التحصيل مع كل جلسة عند إتمامها.
              </p>
            ) : (
              <div className="space-y-3">
                {selectedPlan.installments.map((inst) => (
                  <div
                    key={inst.id}
                    data-testid={`plan-installment-${inst.installmentNumber}`}
                    className="rounded-xl border border-[var(--glass-border)] p-3"
                  >
                    <div className="flex flex-wrap items-center justify-between gap-2">
                      <div>
                        <div className="font-semibold">
                          قسط #{inst.installmentNumber} {inst.isUpfront ? '(مقدم)' : ''} — {inst.amount} {inst.currency}
                        </div>
                        <div className="text-xs text-[var(--color-text-secondary)]">
                          تاريخ الاستحقاق: {inst.dueDate} | المدفوع: {inst.paidAmount} | المتبقي: {inst.remainingAmount} | الحالة: {inst.status}
                        </div>
                      </div>
                    </div>

                    {!readOnlyInspectMode && inst.remainingAmount > 0 && (
                      <div className="mt-2 flex flex-wrap items-end gap-2">
                        <GlassInput
                          label={`مبلغ تحصيل القسط #${inst.installmentNumber}`}
                          type="number"
                          value={payAmountByInstallment[inst.id] ?? String(inst.remainingAmount)}
                          onChange={(e) =>
                            setPayAmountByInstallment((prev) => ({ ...prev, [inst.id]: e.target.value }))
                          }
                        />
                        <GlassButton
                          type="button"
                          variant="prominent"
                          onClick={() => handlePayInstallment(selectedPlan, inst)}
                        >
                          تحصيل القسط #{inst.installmentNumber}
                        </GlassButton>
                      </div>
                    )}

                    {!readOnlyInspectMode && inst.paidAmount > 0 && inst.status !== 'Refunded' && (
                      <div className="mt-2 flex flex-wrap items-end gap-2">
                        <GlassInput
                          label={`سبب رد القسط #${inst.installmentNumber}`}
                          value={refundReasonByInstallment[inst.id] ?? ''}
                          onChange={(e) =>
                            setRefundReasonByInstallment((prev) => ({ ...prev, [inst.id]: e.target.value }))
                          }
                        />
                        <GlassButton
                          type="button"
                          variant="default"
                          onClick={() => handleRefundInstallment(selectedPlan, inst)}
                        >
                          <RotateCcw size={15} />
                          رد المبلغ #{inst.installmentNumber}
                        </GlassButton>
                      </div>
                    )}
                  </div>
                ))}
              </div>
            )}
          </GlassCard>
        </div>
      )}
    </div>
  );
};

