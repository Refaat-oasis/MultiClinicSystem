import React, { useMemo, useState } from 'react';
import { GlassCard, GlassButton, GlassInput, AlertBanner } from '@clinic/ui';
import {
  Receipt,
  CreditCard,
  Calculator,
  BookOpen,
  FlaskConical,
  Truck,
  Lock,
  Unlock,
  Printer,
  RotateCcw,
  PlusCircle,
} from 'lucide-react';

export interface BillingPriceListItemData {
  id: string;
  priceCode: string;
  nameAr: string;
  nameEn: string;
  kind: string;
  unitCostAmount: number;
  defaultPriceAmount: number;
  currency: string;
  isActive: boolean;
}

export interface BillingChargeLineData {
  id: string;
  kind: string;
  description: string;
  quantity: number;
  unitCostAmount: number;
  unitChargedPriceAmount: number;
  lineCostAmount: number;
  lineTotalAmount: number;
  markupAmount: number;
}

export interface BillingChargeData {
  id: string;
  patientId: string;
  patientName?: string;
  status: string;
  sessionPriceAmount: number;
  isFreeSession: boolean;
  freeSessionReason?: string | null;
  extraChargeAmount: number;
  extraChargeReason?: string | null;
  grossSubtotalAmount: number;
  discountAmount: number;
  discountReason?: string | null;
  netTotalAmount: number;
  paidAmount: number;
  refundedAmount: number;
  remainingDueAmount: number;
  currency: string;
  roundingRuleApplied: string;
  createdAtUtc: string;
  lines: BillingChargeLineData[];
}

export interface BillingPaymentData {
  id: string;
  receiptNumber: string;
  method: 'Cash' | 'Visa' | 'InstaPay' | 'Wallet' | string;
  amount: number;
  currency: string;
  isDeposit: boolean;
  paidAtUtc: string;
  notes?: string | null;
}

export interface BillingRefundData {
  id: string;
  originalPaymentId: string;
  method: string;
  amount: number;
  currency: string;
  reason: string;
  receiptNumber?: string | null;
  refundedAtUtc: string;
}

export interface BillingLedgerEntryData {
  id: string;
  entryType: 'Charge' | 'Payment' | 'Refund' | string;
  signedAmount: number;
  currency: string;
  method?: string | null;
  description: string;
  occurredAtUtc: string;
}

export interface BillingLabWorkData {
  id: string;
  labName: string;
  workDescription: string;
  toothReference?: string | null;
  labCostAmount: number;
  patientPriceAmount: number;
  currency: string;
  createdAtUtc: string;
}

export interface BillingSupplierData {
  id: string;
  name: string;
  phone?: string | null;
  notes?: string | null;
  totalBilled?: number;
  totalPaid?: number;
  balanceDue?: number;
}

export interface BillingExpenseData {
  id: string;
  category: string;
  description: string;
  method?: string | null;
  amount: number;
  currency: string;
  countsAsCashOut: boolean;
  occurredAtUtc: string;
}

export interface BillingClosingMethodData {
  method: string;
  paymentsTotal: number;
  refundsTotal: number;
  expensesTotal: number;
  expectedTotal: number;
  countedTotal: number;
  varianceAmount: number;
  currency: string;
}

export interface BillingDailyClosingData {
  closingDate: string;
  status: 'Closed' | 'Reopened' | string;
  financeExpensesExcluded: boolean;
  totalExpectedAmount: number;
  totalCountedAmount: number;
  totalVarianceAmount: number;
  currency: string;
  closeNotes?: string | null;
  reopenReason?: string | null;
  methods: BillingClosingMethodData[];
}

export interface BillingInitialSnapshot {
  currency?: string;
  priceList?: BillingPriceListItemData[];
  charges?: BillingChargeData[];
  payments?: BillingPaymentData[];
  refunds?: BillingRefundData[];
  ledgerEntries?: BillingLedgerEntryData[];
  labWorkOrders?: BillingLabWorkData[];
  suppliers?: BillingSupplierData[];
  expenses?: BillingExpenseData[];
  dailyClosing?: BillingDailyClosingData | null;
}

export interface Phase8BillingScreenProps {
  clinicId: string;
  readOnlyInspectMode?: boolean;
  initialSnapshot?: BillingInitialSnapshot;
  onCreateCharge?: (payload: {
    clinicId: string;
    sessionPriceAmount: number;
    isFreeSession: boolean;
    freeSessionReason?: string;
    toolDescription?: string;
    toolUnitCost: number;
    toolChargedPrice: number;
    extraChargeAmount: number;
    extraChargeReason?: string;
    discountAmount: number;
    discountReason?: string;
    roundingRule: string;
    netTotalAmount: number;
  }) => void;
  onRecordPayment?: (payload: {
    clinicId: string;
    chargeId?: string;
    isDeposit: boolean;
    splits: Array<{ method: string; amount: number }>;
  }) => void;
  onDownloadReceiptPdf?: (paymentId: string) => void;
  onRecordSkippedReceipt?: (payload: { clinicId: string; receiptNumber: string; reason: string }) => void;
  onIssueRefund?: (payload: { clinicId: string; originalPaymentId: string; amount: number; reason: string }) => void;
  onCreateLabWork?: (payload: {
    clinicId: string;
    labName: string;
    workDescription: string;
    labCostAmount: number;
    patientPriceAmount: number;
  }) => void;
  onCreateSupplier?: (payload: { clinicId: string; name: string; phone?: string }) => void;
  onCreateExpense?: (payload: {
    clinicId: string;
    category: string;
    description: string;
    method: string;
    amount: number;
    countsAsCashOut: boolean;
  }) => void;
  onCloseDay?: (payload: {
    clinicId: string;
    closingDate: string;
    countedByMethod: Record<string, number>;
    closeNotes?: string;
  }) => void;
  onReopenDay?: (payload: { clinicId: string; closingDate: string; reason: string }) => void;
  onSavePriceItem?: (payload: {
    clinicId: string;
    priceCode: string;
    nameAr: string;
    defaultPriceAmount: number;
    unitCostAmount: number;
  }) => void;
}

const DEFAULT_SNAPSHOT: Required<BillingInitialSnapshot> = {
  currency: 'EGP',
  priceList: [
    {
      id: 'pl-1',
      priceCode: 'CONS-01',
      nameAr: 'كشف واستشارة',
      nameEn: 'Consultation',
      kind: 'Service',
      unitCostAmount: 0,
      defaultPriceAmount: 300,
      currency: 'EGP',
      isActive: true,
    },
    {
      id: 'pl-2',
      priceCode: 'COMP-02',
      nameAr: 'حشو تجميلي مركب',
      nameEn: 'Composite Filling',
      kind: 'ToolMaterial',
      unitCostAmount: 80,
      defaultPriceAmount: 650,
      currency: 'EGP',
      isActive: true,
    },
  ],
  charges: [
    {
      id: 'chg-1',
      patientId: 'pat-1',
      patientName: 'أحمد محمود',
      status: 'PartiallyPaid',
      sessionPriceAmount: 300,
      isFreeSession: false,
      extraChargeAmount: 0,
      grossSubtotalAmount: 950,
      discountAmount: 0,
      netTotalAmount: 950,
      paidAmount: 500,
      refundedAmount: 0,
      remainingDueAmount: 450,
      currency: 'EGP',
      roundingRuleApplied: 'None',
      createdAtUtc: '2026-06-08T09:15:00Z',
      lines: [
        {
          id: 'ln-1',
          kind: 'ToolMaterial',
          description: 'Composite Resin Kit',
          quantity: 1,
          unitCostAmount: 80,
          unitChargedPriceAmount: 650,
          lineCostAmount: 80,
          lineTotalAmount: 650,
          markupAmount: 570,
        },
      ],
    },
  ],
  payments: [
    {
      id: 'pay-1',
      receiptNumber: 'R-000001',
      method: 'Cash',
      amount: 300,
      currency: 'EGP',
      isDeposit: false,
      paidAtUtc: '2026-06-08T09:30:00Z',
      notes: 'دفعة أولى نقداً',
    },
    {
      id: 'pay-2',
      receiptNumber: 'R-000002',
      method: 'Visa',
      amount: 200,
      currency: 'EGP',
      isDeposit: false,
      paidAtUtc: '2026-06-08T09:30:00Z',
      notes: 'استكمال فيزا',
    },
  ],
  refunds: [],
  ledgerEntries: [
    {
      id: 'led-1',
      entryType: 'Charge',
      signedAmount: 950,
      currency: 'EGP',
      method: null,
      description: 'فاتورة جلسة علاجية',
      occurredAtUtc: '2026-06-08T09:15:00Z',
    },
    {
      id: 'led-2',
      entryType: 'Payment',
      signedAmount: -300,
      currency: 'EGP',
      method: 'Cash',
      description: 'Receipt R-000001 (Cash)',
      occurredAtUtc: '2026-06-08T09:30:00Z',
    },
    {
      id: 'led-3',
      entryType: 'Payment',
      signedAmount: -200,
      currency: 'EGP',
      method: 'Visa',
      description: 'Receipt R-000002 (Visa)',
      occurredAtUtc: '2026-06-08T09:30:00Z',
    },
  ],
  labWorkOrders: [
    {
      id: 'lab-1',
      labName: 'معمل النيل للتركيبات',
      workDescription: 'طربوش زيركون سن 16',
      toothReference: '16',
      labCostAmount: 450,
      patientPriceAmount: 1400,
      currency: 'EGP',
      createdAtUtc: '2026-06-08T10:00:00Z',
    },
  ],
  suppliers: [
    {
      id: 'sup-1',
      name: 'شركة النخبة للمستلزمات الطبية',
      phone: '01001234567',
      notes: 'توريد خامات أسنان',
      totalBilled: 1200,
      totalPaid: 700,
      balanceDue: 500,
    },
  ],
  expenses: [
    {
      id: 'exp-1',
      category: 'Utilities',
      description: 'مستلزمات تعقيم يومية',
      method: 'Cash',
      amount: 50,
      currency: 'EGP',
      countsAsCashOut: true,
      occurredAtUtc: '2026-06-08T11:00:00Z',
    },
  ],
  dailyClosing: {
    closingDate: '2026-06-08',
    status: 'Reopened',
    financeExpensesExcluded: false,
    totalExpectedAmount: 450,
    totalCountedAmount: 450,
    totalVarianceAmount: 0,
    currency: 'EGP',
    methods: [
      { method: 'Cash', paymentsTotal: 300, refundsTotal: 0, expensesTotal: 50, expectedTotal: 250, countedTotal: 250, varianceAmount: 0, currency: 'EGP' },
      { method: 'Visa', paymentsTotal: 200, refundsTotal: 0, expensesTotal: 0, expectedTotal: 200, countedTotal: 200, varianceAmount: 0, currency: 'EGP' },
      { method: 'InstaPay', paymentsTotal: 0, refundsTotal: 0, expensesTotal: 0, expectedTotal: 0, countedTotal: 0, varianceAmount: 0, currency: 'EGP' },
      { method: 'Wallet', paymentsTotal: 0, refundsTotal: 0, expensesTotal: 0, expectedTotal: 0, countedTotal: 0, varianceAmount: 0, currency: 'EGP' },
    ],
  },
};

function applyRounding(amount: number, rule: string): number {
  const safe = Math.max(0, amount);
  if (rule === 'NearestInteger') {
    return Math.round(safe);
  }
  if (rule === 'NearestFive') {
    return Math.round(safe / 5) * 5;
  }
  if (rule === 'NearestTen') {
    return Math.round(safe / 10) * 10;
  }
  return Number(safe.toFixed(2));
}

export const Phase8BillingScreen: React.FC<Phase8BillingScreenProps> = ({
  clinicId,
  readOnlyInspectMode = false,
  initialSnapshot,
  onCreateCharge,
  onRecordPayment,
  onDownloadReceiptPdf,
  onRecordSkippedReceipt,
  onIssueRefund,
  onCreateLabWork,
  onCreateSupplier,
  onCreateExpense,
  onCloseDay,
  onReopenDay,
  onSavePriceItem,
}) => {
  const currency = initialSnapshot?.currency ?? DEFAULT_SNAPSHOT.currency;

  const [priceList, setPriceList] = useState<BillingPriceListItemData[]>(
    initialSnapshot?.priceList ?? DEFAULT_SNAPSHOT.priceList,
  );
  const [charges, setCharges] = useState<BillingChargeData[]>(
    initialSnapshot?.charges ?? DEFAULT_SNAPSHOT.charges,
  );
  const [payments, setPayments] = useState<BillingPaymentData[]>(
    initialSnapshot?.payments ?? DEFAULT_SNAPSHOT.payments,
  );
  const [refunds, setRefunds] = useState<BillingRefundData[]>(
    initialSnapshot?.refunds ?? DEFAULT_SNAPSHOT.refunds,
  );
  const [ledgerEntries, setLedgerEntries] = useState<BillingLedgerEntryData[]>(
    initialSnapshot?.ledgerEntries ?? DEFAULT_SNAPSHOT.ledgerEntries,
  );
  const [labWorkOrders, setLabWorkOrders] = useState<BillingLabWorkData[]>(
    initialSnapshot?.labWorkOrders ?? DEFAULT_SNAPSHOT.labWorkOrders,
  );
  const [suppliers, setSuppliers] = useState<BillingSupplierData[]>(
    initialSnapshot?.suppliers ?? DEFAULT_SNAPSHOT.suppliers,
  );
  const [expenses, setExpenses] = useState<BillingExpenseData[]>(
    initialSnapshot?.expenses ?? DEFAULT_SNAPSHOT.expenses,
  );
  const [dailyClosing, setDailyClosing] = useState<BillingDailyClosingData>(
    initialSnapshot?.dailyClosing ?? DEFAULT_SNAPSHOT.dailyClosing!,
  );

  const [billingSection, setBillingSection] = useState<'charges' | 'payments' | 'ledger' | 'finance'>('charges');
  const [errorBanner, setErrorBanner] = useState<string | null>(null);
  const [noticeBanner, setNoticeBanner] = useState<string | null>(null);

  // Charge calculator form state
  const [sessionPrice, setSessionPrice] = useState<string>('300');
  const [isFreeSession, setIsFreeSession] = useState<boolean>(false);
  const [freeSessionReason, setFreeSessionReason] = useState<string>('');
  const [toolDescription, setToolDescription] = useState<string>('مادة حشو تجميلي');
  const [toolUnitCost, setToolUnitCost] = useState<string>('80');
  const [toolChargedPrice, setToolChargedPrice] = useState<string>('400');
  const [extraChargeAmount, setExtraChargeAmount] = useState<string>('0');
  const [extraChargeReason, setExtraChargeReason] = useState<string>('');
  const [discountAmount, setDiscountAmount] = useState<string>('0');
  const [discountReason, setDiscountReason] = useState<string>('');
  const [roundingRule, setRoundingRule] = useState<string>('None');

  // Price list form state
  const [newPriceCode, setNewPriceCode] = useState<string>('');
  const [newPriceNameAr, setNewPriceNameAr] = useState<string>('');
  const [newPriceUnitCost, setNewPriceUnitCost] = useState<string>('0');
  const [newPriceDefault, setNewPriceDefault] = useState<string>('250');

  // Split payment form state
  const [cashSplit, setCashSplit] = useState<string>('250');
  const [visaSplit, setVisaSplit] = useState<string>('0');
  const [isDeposit, setIsDeposit] = useState<boolean>(false);
  const [skippedNumber, setSkippedNumber] = useState<string>('');
  const [skippedReason, setSkippedReason] = useState<string>('');

  // Refund form state (onlineOnly)
  const [refundPaymentId, setRefundPaymentId] = useState<string>(payments[0]?.id ?? '');
  const [refundAmount, setRefundAmount] = useState<string>('50');
  const [refundReason, setRefundReason] = useState<string>('');

  // Lab work form state
  const [labName, setLabName] = useState<string>('معمل النيل للتركيبات');
  const [labWorkDesc, setLabWorkDesc] = useState<string>('');
  const [labCost, setLabCost] = useState<string>('350');
  const [labPatientPrice, setLabPatientPrice] = useState<string>('1100');

  // Finance supplier & expense form state
  const [supplierName, setSupplierName] = useState<string>('');
  const [supplierPhone, setSupplierPhone] = useState<string>('');
  const [expenseDesc, setExpenseDesc] = useState<string>('');
  const [expenseMethod, setExpenseMethod] = useState<string>('Cash');
  const [expenseAmount, setExpenseAmount] = useState<string>('100');

  // Closing & Reopen state (onlineOnly)
  const [closeNotes, setCloseNotes] = useState<string>('مطابقة درج الخزينة نهاية اليوم');
  const [reopenReason, setReopenReason] = useState<string>('');

  const calculatorPreview = useMemo(() => {
    const sess = isFreeSession ? 0 : Math.max(0, parseFloat(sessionPrice) || 0);
    const tCost = Math.max(0, parseFloat(toolUnitCost) || 0);
    const tPrice = Math.max(0, parseFloat(toolChargedPrice) || 0);
    const extra = Math.max(0, parseFloat(extraChargeAmount) || 0);
    const disc = Math.max(0, parseFloat(discountAmount) || 0);
    const gross = sess + tPrice + extra;
    const unroundedNet = Math.max(0, gross - disc);
    const net = applyRounding(unroundedNet, roundingRule);
    return {
      sess,
      tCost,
      tPrice,
      markup: Number((tPrice - tCost).toFixed(2)),
      gross: Number(gross.toFixed(2)),
      net,
    };
  }, [sessionPrice, isFreeSession, toolUnitCost, toolChargedPrice, extraChargeAmount, discountAmount, roundingRule]);

  const runningBalance = useMemo(
    () => Number(ledgerEntries.reduce((acc, item) => acc + item.signedAmount, 0).toFixed(2)),
    [ledgerEntries],
  );

  const ensureOnline = (actionLabel: string): boolean => {
    if (typeof navigator !== 'undefined' && !navigator.onLine) {
      setErrorBanner(`العملية "${actionLabel}" تتطلب اتصالاً مباشراً بالإنترنت ولا يمكن تنفيذها أوفلاين (ONLINE_REQUIRED).`);
      return false;
    }
    return true;
  };

  const handleSavePriceItem = () => {
    if (readOnlyInspectMode) return;
    if (!ensureOnline('تحديث قائمة الأسعار')) return;
    if (!newPriceCode.trim() || !newPriceNameAr.trim()) {
      setErrorBanner('كود البند والاسم العربي مطلوبان.');
      return;
    }
    setErrorBanner(null);
    const item: BillingPriceListItemData = {
      id: `pl-${Date.now()}`,
      priceCode: newPriceCode.trim().toUpperCase(),
      nameAr: newPriceNameAr.trim(),
      nameEn: newPriceCode.trim().toUpperCase(),
      kind: 'Service',
      unitCostAmount: Math.max(0, parseFloat(newPriceUnitCost) || 0),
      defaultPriceAmount: Math.max(0, parseFloat(newPriceDefault) || 0),
      currency,
      isActive: true,
    };
    setPriceList((prev) => [item, ...prev]);
    onSavePriceItem?.({
      clinicId,
      priceCode: item.priceCode,
      nameAr: item.nameAr,
      unitCostAmount: item.unitCostAmount,
      defaultPriceAmount: item.defaultPriceAmount,
    });
    setNewPriceCode('');
    setNewPriceNameAr('');
    setNoticeBanner('تم تحديث قائمة الأسعار بنجاح.');
  };

  const handleCreateCharge = () => {
    if (readOnlyInspectMode) return;
    if (dailyClosing.status === 'Closed') {
      setErrorBanner('اليومية مغلقة ومقفلة (DAY_CLOSED_LOCKED). أعد الفتح بصلاحية المالك أولاً.');
      return;
    }
    if (isFreeSession && !freeSessionReason.trim()) {
      setErrorBanner('الجلسة المجانية تتطلب إدخال سبب صريح.');
      return;
    }
    const extra = parseFloat(extraChargeAmount) || 0;
    if (extra > 0 && !extraChargeReason.trim()) {
      setErrorBanner('الرسوم الإضافية تتطلب إدخال سبب صريح.');
      return;
    }
    const disc = parseFloat(discountAmount) || 0;
    if (disc > 0 && !discountReason.trim()) {
      setErrorBanner('الخصم يتطلب إدخال سبب صريح.');
      return;
    }
    setErrorBanner(null);

    const newCharge: BillingChargeData = {
      id: `chg-${Date.now()}`,
      patientId: 'pat-1',
      patientName: 'أحمد محمود',
      status: 'Posted',
      sessionPriceAmount: calculatorPreview.sess,
      isFreeSession,
      freeSessionReason: isFreeSession ? freeSessionReason.trim() : null,
      extraChargeAmount: extra,
      extraChargeReason: extra > 0 ? extraChargeReason.trim() : null,
      grossSubtotalAmount: calculatorPreview.gross,
      discountAmount: disc,
      discountReason: disc > 0 ? discountReason.trim() : null,
      netTotalAmount: calculatorPreview.net,
      paidAmount: 0,
      refundedAmount: 0,
      remainingDueAmount: calculatorPreview.net,
      currency,
      roundingRuleApplied: roundingRule,
      createdAtUtc: new Date().toISOString(),
      lines: calculatorPreview.tPrice > 0 || calculatorPreview.tCost > 0
        ? [
            {
              id: `ln-${Date.now()}`,
              kind: 'ToolMaterial',
              description: toolDescription || 'مستلزمات جلسة',
              quantity: 1,
              unitCostAmount: calculatorPreview.tCost,
              unitChargedPriceAmount: calculatorPreview.tPrice,
              lineCostAmount: calculatorPreview.tCost,
              lineTotalAmount: calculatorPreview.tPrice,
              markupAmount: calculatorPreview.markup,
            },
          ]
        : [],
    };
    setCharges((prev) => [newCharge, ...prev]);
    setLedgerEntries((prev) => [
      ...prev,
      {
        id: `led-${Date.now()}`,
        entryType: 'Charge',
        signedAmount: newCharge.netTotalAmount,
        currency,
        method: null,
        description: `فاتورة جديدة (${newCharge.roundingRuleApplied})`,
        occurredAtUtc: newCharge.createdAtUtc,
      },
    ]);
    onCreateCharge?.({
      clinicId,
      sessionPriceAmount: calculatorPreview.sess,
      isFreeSession,
      freeSessionReason: isFreeSession ? freeSessionReason.trim() : undefined,
      toolDescription,
      toolUnitCost: calculatorPreview.tCost,
      toolChargedPrice: calculatorPreview.tPrice,
      extraChargeAmount: extra,
      extraChargeReason: extra > 0 ? extraChargeReason.trim() : undefined,
      discountAmount: disc,
      discountReason: disc > 0 ? discountReason.trim() : undefined,
      roundingRule,
      netTotalAmount: calculatorPreview.net,
    });
    setNoticeBanner('تم إنشاء الفاتورة وإضافتها إلى كشف الحساب (جاهز للمزامنة).');
  };

  const handleRecordSplitPayment = () => {
    if (readOnlyInspectMode) return;
    if (dailyClosing.status === 'Closed') {
      setErrorBanner('اليومية مغلقة ومقفلة (DAY_CLOSED_LOCKED). لا يمكن تحصيل دفعات جديدة.');
      return;
    }
    const splits: Array<{ method: string; amount: number }> = [];
    const cashVal = parseFloat(cashSplit) || 0;
    const visaVal = parseFloat(visaSplit) || 0;
    if (cashVal > 0) splits.push({ method: 'Cash', amount: cashVal });
    if (visaVal > 0) splits.push({ method: 'Visa', amount: visaVal });
    if (splits.length === 0) {
      setErrorBanner('أدخل مبلغاً موجباً لوسيلة دفع واحدة على الأقل.');
      return;
    }
    setErrorBanner(null);

    const startIdx = payments.length + 1;
    const now = new Date().toISOString();
    const addedPayments: BillingPaymentData[] = splits.map((s, idx) => ({
      id: `pay-${Date.now()}-${idx}`,
      receiptNumber: `R-${String(startIdx + idx).padStart(6, '0')}`,
      method: s.method,
      amount: s.amount,
      currency,
      isDeposit,
      paidAtUtc: now,
    }));

    setPayments((prev) => [...addedPayments, ...prev]);
    setLedgerEntries((prev) => [
      ...prev,
      ...addedPayments.map((p) => ({
        id: `led-${p.id}`,
        entryType: 'Payment',
        signedAmount: -p.amount,
        currency,
        method: p.method,
        description: `Receipt ${p.receiptNumber} (${p.method})`,
        occurredAtUtc: now,
      })),
    ]);
    onRecordPayment?.({ clinicId, chargeId: charges[0]?.id, isDeposit, splits });
    setNoticeBanner(`تم إصدار الإيصال رقم ${addedPayments.map((p) => p.receiptNumber).join(' ، ')} بنجاح.`);
  };

  const handleSkippedReceipt = () => {
    if (readOnlyInspectMode) return;
    if (!skippedNumber.trim() || !skippedReason.trim()) {
      setErrorBanner('رقم الإيصال المُلغى والسبب مطلوبان.');
      return;
    }
    setErrorBanner(null);
    onRecordSkippedReceipt?.({ clinicId, receiptNumber: skippedNumber.trim().toUpperCase(), reason: skippedReason.trim() });
    setNoticeBanner(`تم توثيق رقم الإيصال المتخطّى ${skippedNumber.trim().toUpperCase()}.`);
    setSkippedNumber('');
    setSkippedReason('');
  };

  const handleIssueRefund = () => {
    if (readOnlyInspectMode) return;
    if (!ensureOnline('رد مبلغ (Refund)')) return;
    if (dailyClosing.status === 'Closed') {
      setErrorBanner('اليومية مغلقة ومقفلة (DAY_CLOSED_LOCKED).');
      return;
    }
    const amt = parseFloat(refundAmount) || 0;
    if (!refundPaymentId || amt <= 0 || !refundReason.trim()) {
      setErrorBanner('اختر الإيصال الأصلي وأدخل المبلغ وسبب الاسترداد.');
      return;
    }
    setErrorBanner(null);
    const targetPay = payments.find((p) => p.id === refundPaymentId) ?? payments[0];
    const now = new Date().toISOString();
    const newRefund: BillingRefundData = {
      id: `ref-${Date.now()}`,
      originalPaymentId: targetPay?.id ?? refundPaymentId,
      method: targetPay?.method ?? 'Cash',
      amount: amt,
      currency,
      reason: refundReason.trim(),
      receiptNumber: targetPay?.receiptNumber,
      refundedAtUtc: now,
    };
    setRefunds((prev) => [newRefund, ...prev]);
    setLedgerEntries((prev) => [
      ...prev,
      {
        id: `led-${newRefund.id}`,
        entryType: 'Refund',
        signedAmount: amt,
        currency,
        method: newRefund.method,
        description: `Refund: ${newRefund.reason}`,
        occurredAtUtc: now,
      },
    ]);
    onIssueRefund?.({ clinicId, originalPaymentId: newRefund.originalPaymentId, amount: amt, reason: newRefund.reason });
    setRefundReason('');
    setNoticeBanner('تم تسجيل المرتجع في دفتر الحسابات بنجاح (قيد عكسي بدون حذف).');
  };

  const handleCreateLabWork = () => {
    if (readOnlyInspectMode) return;
    const cost = Math.max(0, parseFloat(labCost) || 0);
    const price = Math.max(0, parseFloat(labPatientPrice) || 0);
    if (!labName.trim() || !labWorkDesc.trim()) {
      setErrorBanner('اسم المعمل ووصف العمل مطلوبان.');
      return;
    }
    setErrorBanner(null);
    const now = new Date().toISOString();
    const item: BillingLabWorkData = {
      id: `lab-${Date.now()}`,
      labName: labName.trim(),
      workDescription: labWorkDesc.trim(),
      labCostAmount: cost,
      patientPriceAmount: price,
      currency,
      createdAtUtc: now,
    };
    setLabWorkOrders((prev) => [item, ...prev]);
    onCreateLabWork?.({ clinicId, labName: item.labName, workDescription: item.workDescription, labCostAmount: cost, patientPriceAmount: price });
    setLabWorkDesc('');
    setNoticeBanner('تم تسجيل طلب المعمل وفصله إلى تكلفة معمل وسعر مريض.');
  };

  const handleCreateSupplier = () => {
    if (readOnlyInspectMode) return;
    if (!supplierName.trim()) {
      setErrorBanner('اسم المورد مطلوب.');
      return;
    }
    setErrorBanner(null);
    const sup: BillingSupplierData = {
      id: `sup-${Date.now()}`,
      name: supplierName.trim(),
      phone: supplierPhone.trim() || null,
      totalBilled: 0,
      totalPaid: 0,
      balanceDue: 0,
    };
    setSuppliers((prev) => [sup, ...prev]);
    onCreateSupplier?.({ clinicId, name: sup.name, phone: sup.phone ?? undefined });
    setSupplierName('');
    setSupplierPhone('');
    setNoticeBanner('تم إضافة المورد بنجاح.');
  };

  const handleCreateExpense = () => {
    if (readOnlyInspectMode) return;
    if (dailyClosing.status === 'Closed') {
      setErrorBanner('اليومية مغلقة ومقفلة (DAY_CLOSED_LOCKED).');
      return;
    }
    const amt = parseFloat(expenseAmount) || 0;
    if (!expenseDesc.trim() || amt <= 0) {
      setErrorBanner('وصف المصروف ومبلغه الموجب مطلوبان.');
      return;
    }
    setErrorBanner(null);
    const exp: BillingExpenseData = {
      id: `exp-${Date.now()}`,
      category: 'Operations',
      description: expenseDesc.trim(),
      method: expenseMethod,
      amount: amt,
      currency,
      countsAsCashOut: true,
      occurredAtUtc: new Date().toISOString(),
    };
    setExpenses((prev) => [exp, ...prev]);
    onCreateExpense?.({
      clinicId,
      category: exp.category,
      description: exp.description,
      method: expenseMethod,
      amount: amt,
      countsAsCashOut: true,
    });
    setExpenseDesc('');
    setNoticeBanner('تم تسجيل المصروف النقدي في عيادة اليوم.');
  };

  const handleCloseDay = () => {
    if (readOnlyInspectMode) return;
    if (!ensureOnline('إقفال اليومية (Close Day)')) return;
    setErrorBanner(null);
    const countedMap: Record<string, number> = {};
    for (const m of dailyClosing.methods) {
      countedMap[m.method] = m.countedTotal;
    }
    setDailyClosing((prev) => ({
      ...prev,
      status: 'Closed',
      closeNotes,
    }));
    onCloseDay?.({ clinicId, closingDate: dailyClosing.closingDate, countedByMethod: countedMap, closeNotes });
    setNoticeBanner('تم إقفال اليومية وقفل أي تعديلات مالية لهذا التاريخ.');
  };

  const handleReopenDay = () => {
    if (readOnlyInspectMode) return;
    if (!ensureOnline('إعادة فتح اليومية (Reopen Day)')) return;
    if (!reopenReason.trim()) {
      setErrorBanner('إعادة فتح يومية مقفلة يتطلب كتابة السبب بصلاحية المالك.');
      return;
    }
    setErrorBanner(null);
    setDailyClosing((prev) => ({
      ...prev,
      status: 'Reopened',
      reopenReason: reopenReason.trim(),
    }));
    onReopenDay?.({ clinicId, closingDate: dailyClosing.closingDate, reason: reopenReason.trim() });
    setReopenReason('');
    setNoticeBanner('تمت إعادة فتح اليومية وتوثيق السبب في سجل التدقيق.');
  };

  return (
    <div dir="rtl" className="space-y-4 text-start" data-testid="phase8-billing-screen">
      {readOnlyInspectMode && (
        <AlertBanner
          variant="warning"
          title="وضع الفحص للقراءة فقط (Admin Inspect Mode)"
          message="يتم عرض فواتير العيادة، دفتر الأستاذ، وإقفال اليومية للمراجعة فقط دون إتاحة التعديل."
        />
      )}

      {errorBanner && <AlertBanner variant="error" message={errorBanner} />}
      {noticeBanner && <AlertBanner variant="success" message={noticeBanner} />}

      <GlassCard className="p-4 flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center gap-2">
          <Receipt className="h-5 w-5 text-emerald-400" />
          <div>
            <h2 className="text-base font-bold text-[var(--glass-text)]">
              الفواتير والخزينة والموردون
            </h2>
            <p className="text-xs text-[var(--glass-muted)]">
              الرصيد الحالي في دفتر الأستاذ: <strong data-testid="ledger-balance">{runningBalance} {currency}</strong> • حالة اليومية:{' '}
              <strong data-testid="closing-status-badge">{dailyClosing.status === 'Closed' ? 'مقفلة (Closed)' : 'مفتوحة (Open)'}</strong>
            </p>
          </div>
        </div>

        <div className="flex flex-wrap gap-2">
          <GlassButton variant={billingSection === 'charges' ? 'prominent' : 'default'} onClick={() => setBillingSection('charges')}>
            <Calculator size={15} className="me-1 inline" /> الفاتورة وقائمة الأسعار
          </GlassButton>
          <GlassButton variant={billingSection === 'payments' ? 'prominent' : 'default'} onClick={() => setBillingSection('payments')}>
            <CreditCard size={15} className="me-1 inline" /> التحصيل والإيصالات والمرتجعات
          </GlassButton>
          <GlassButton variant={billingSection === 'ledger' ? 'prominent' : 'default'} onClick={() => setBillingSection('ledger')}>
            <BookOpen size={15} className="me-1 inline" /> كشف الحساب والمعمل
          </GlassButton>
          <GlassButton variant={billingSection === 'finance' ? 'prominent' : 'default'} onClick={() => setBillingSection('finance')}>
            <Truck size={15} className="me-1 inline" /> الموردون وإقفال اليومية
          </GlassButton>
        </div>
      </GlassCard>

      {billingSection === 'charges' && (
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
          <GlassCard className="p-4 space-y-3" data-testid="charge-calculator-panel">
            <h3 className="text-sm font-bold text-[var(--glass-text)] flex items-center gap-2">
              <Calculator size={16} className="text-sky-400" /> حاسبة الفاتورة (Calculator)
            </h3>
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
              <GlassInput
                label="سعر الجلسة الأساسي"
                type="number"
                value={sessionPrice}
                disabled={readOnlyInspectMode || isFreeSession}
                onChange={(e) => setSessionPrice(e.target.value)}
              />
              <GlassInput
                label="وصف المستلزم / الأداة"
                value={toolDescription}
                disabled={readOnlyInspectMode}
                onChange={(e) => setToolDescription(e.target.value)}
              />
              <GlassInput
                label="تكلفة الأداة (للعيادة فقط)"
                type="number"
                value={toolUnitCost}
                disabled={readOnlyInspectMode}
                onChange={(e) => setToolUnitCost(e.target.value)}
              />
              <GlassInput
                label="سعر الأداة للمريض"
                type="number"
                value={toolChargedPrice}
                disabled={readOnlyInspectMode}
                onChange={(e) => setToolChargedPrice(e.target.value)}
              />
              <GlassInput
                label="رسوم إضافية"
                type="number"
                value={extraChargeAmount}
                disabled={readOnlyInspectMode}
                onChange={(e) => setExtraChargeAmount(e.target.value)}
              />
              <GlassInput
                label="سبب الرسوم الإضافية"
                value={extraChargeReason}
                disabled={readOnlyInspectMode}
                onChange={(e) => setExtraChargeReason(e.target.value)}
              />
              <GlassInput
                label="مبلغ الخصم"
                type="number"
                value={discountAmount}
                disabled={readOnlyInspectMode}
                onChange={(e) => setDiscountAmount(e.target.value)}
              />
              <GlassInput
                label="سبب الخصم"
                value={discountReason}
                disabled={readOnlyInspectMode}
                onChange={(e) => setDiscountReason(e.target.value)}
              />
            </div>

            <div className="flex flex-wrap items-center gap-4 text-xs">
              <label className="flex items-center gap-2">
                <input
                  type="checkbox"
                  data-testid="free-session-checkbox"
                  checked={isFreeSession}
                  disabled={readOnlyInspectMode}
                  onChange={(e) => setIsFreeSession(e.target.checked)}
                />
                <span>جلسة مجانية (تتطلب سبباً)</span>
              </label>

              {isFreeSession && (
                <GlassInput
                  label="سبب الجلسة المجانية"
                  value={freeSessionReason}
                  disabled={readOnlyInspectMode}
                  onChange={(e) => setFreeSessionReason(e.target.value)}
                />
              )}

              <label className="flex items-center gap-2">
                <span>التقريب:</span>
                <select
                  value={roundingRule}
                  disabled={readOnlyInspectMode}
                  onChange={(e) => setRoundingRule(e.target.value)}
                  className="rounded-lg border border-[var(--glass-border)] bg-[var(--glass-surface)] px-2 py-1 text-xs text-[var(--glass-text)]"
                >
                  <option value="None">بدون تقريب</option>
                  <option value="NearestInteger">أقرب عدد صحيح</option>
                  <option value="NearestFive">أقرب 5</option>
                  <option value="NearestTen">أقرب 10</option>
                </select>
              </label>
            </div>

            <div className="rounded-lg border border-[var(--glass-border)] p-3 text-xs flex flex-wrap justify-between gap-2">
              <span>الإجمالي قبل الخصم: <strong>{calculatorPreview.gross} {currency}</strong></span>
              <span>هامش المستلزمات: <strong>{calculatorPreview.markup} {currency}</strong></span>
              <span>الصافي المستحق: <strong data-testid="charge-net-preview">{calculatorPreview.net} {currency}</strong></span>
            </div>

            {!readOnlyInspectMode && (
              <GlassButton variant="prominent" onClick={handleCreateCharge}>
                <PlusCircle size={15} className="me-1 inline" /> اعتماد الفاتورة
              </GlassButton>
            )}
          </GlassCard>

          <GlassCard className="p-4 space-y-3" data-testid="price-list-panel">
            <h3 className="text-sm font-bold text-[var(--glass-text)]">قائمة الأسعار المعتمدة</h3>
            <ul className="space-y-2 text-xs">
              {priceList.map((item) => (
                <li key={item.id} className="flex items-center justify-between border-b border-[var(--glass-border)] pb-1">
                  <span>{item.nameAr} ({item.priceCode})</span>
                  <span>تكلفة: {item.unitCostAmount} | للمريض: <strong>{item.defaultPriceAmount} {item.currency}</strong></span>
                </li>
              ))}
            </ul>
            {!readOnlyInspectMode && (
              <div className="grid grid-cols-2 gap-2 pt-2">
                <GlassInput label="كود البند" value={newPriceCode} onChange={(e) => setNewPriceCode(e.target.value)} />
                <GlassInput label="الاسم العربي" value={newPriceNameAr} onChange={(e) => setNewPriceNameAr(e.target.value)} />
                <GlassInput label="التكلفة" type="number" value={newPriceUnitCost} onChange={(e) => setNewPriceUnitCost(e.target.value)} />
                <GlassInput label="السعر" type="number" value={newPriceDefault} onChange={(e) => setNewPriceDefault(e.target.value)} />
                <div className="col-span-2">
                  <GlassButton variant="regular" onClick={handleSavePriceItem}>حفظ في قائمة الأسعار (أونلاين فقط)</GlassButton>
                </div>
              </div>
            )}
          </GlassCard>
        </div>
      )}

      {billingSection === 'payments' && (
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
          <GlassCard className="p-4 space-y-3" data-testid="split-payment-panel">
            <h3 className="text-sm font-bold text-[var(--glass-text)]">تحصيل دفعة مقسّمة وإصدار إيصال</h3>
            <div className="grid grid-cols-2 gap-2">
              <GlassInput label="مبلغ نقدي (Cash)" type="number" value={cashSplit} disabled={readOnlyInspectMode} onChange={(e) => setCashSplit(e.target.value)} />
              <GlassInput label="مبلغ بطاقة (Visa)" type="number" value={visaSplit} disabled={readOnlyInspectMode} onChange={(e) => setVisaSplit(e.target.value)} />
            </div>
            <label className="flex items-center gap-2 text-xs">
              <input type="checkbox" checked={isDeposit} disabled={readOnlyInspectMode} onChange={(e) => setIsDeposit(e.target.checked)} />
              <span>تسجيل كدفعة مقدم (Deposit)</span>
            </label>
            {!readOnlyInspectMode && (
              <GlassButton variant="prominent" onClick={handleRecordSplitPayment}>تحصيل وإصدار الإيصالات</GlassButton>
            )}

            <div className="space-y-2 pt-2">
              <h4 className="text-xs font-bold text-[var(--glass-muted)]">الإيصالات الصادرة ({payments.length})</h4>
              {payments.map((p) => (
                <div key={p.id} className="flex items-center justify-between text-xs border-b border-[var(--glass-border)] pb-1">
                  <span>{p.receiptNumber} — {p.method} ({p.amount} {p.currency})</span>
                  <GlassButton variant="default" onClick={() => onDownloadReceiptPdf?.(p.id)}>
                    <Printer size={13} className="me-1 inline" /> PDF
                  </GlassButton>
                </div>
              ))}
            </div>
          </GlassCard>

          <GlassCard className="p-4 space-y-3" data-testid="refund-and-skip-panel">
            <h3 className="text-sm font-bold text-[var(--glass-text)] flex items-center gap-2">
              <RotateCcw size={15} className="text-amber-400" /> المرتجعات (بدون حذف) وتخطي الإيصال
            </h3>
            <div className="space-y-2">
              <select
                aria-label="الإيصال المراد رده"
                value={refundPaymentId}
                disabled={readOnlyInspectMode}
                onChange={(e) => setRefundPaymentId(e.target.value)}
                className="w-full rounded-lg border border-[var(--glass-border)] bg-[var(--glass-surface)] px-2 py-2 text-xs text-[var(--glass-text)]"
              >
                {payments.map((p) => (
                  <option key={p.id} value={p.id}>{p.receiptNumber} — {p.amount} {p.currency} ({p.method})</option>
                ))}
              </select>
              <GlassInput label="مبلغ الاسترداد" type="number" value={refundAmount} disabled={readOnlyInspectMode} onChange={(e) => setRefundAmount(e.target.value)} />
              <GlassInput label="سبب الاسترداد (مطلوب)" value={refundReason} disabled={readOnlyInspectMode} onChange={(e) => setRefundReason(e.target.value)} />
              {!readOnlyInspectMode && (
                <GlassButton variant="regular" onClick={handleIssueRefund}>تسجيل مرتجع (أونلاين فقط)</GlassButton>
              )}
            </div>

            {refunds.length > 0 && (
              <ul className="text-xs space-y-1 pt-2 border-t border-[var(--glass-border)]">
                {refunds.map((r) => (
                  <li key={r.id}>مرتجع {r.amount} {r.currency} — {r.reason}</li>
                ))}
              </ul>
            )}

            {!readOnlyInspectMode && (
              <div className="pt-3 border-t border-[var(--glass-border)] grid grid-cols-2 gap-2">
                <GlassInput label="رقم إيصال متخطى" value={skippedNumber} onChange={(e) => setSkippedNumber(e.target.value)} />
                <GlassInput label="سبب التخطي" value={skippedReason} onChange={(e) => setSkippedReason(e.target.value)} />
                <div className="col-span-2">
                  <GlassButton variant="default" onClick={handleSkippedReceipt}>توثيق رقم إيصال تالف/متخطى</GlassButton>
                </div>
              </div>
            )}
          </GlassCard>
        </div>
      )}

      {billingSection === 'ledger' && (
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
          <GlassCard className="p-4 space-y-3" data-testid="patient-ledger-panel">
            <h3 className="text-sm font-bold text-[var(--glass-text)]">دفتر أستاذ المريض (Append-Only Ledger)</h3>
            <ul className="space-y-2 text-xs">
              {ledgerEntries.map((entry) => (
                <li key={entry.id} className="flex items-center justify-between border-b border-[var(--glass-border)] pb-1">
                  <span>{entry.entryType} — {entry.description}</span>
                  <strong>{entry.signedAmount > 0 ? `+${entry.signedAmount}` : entry.signedAmount} {entry.currency}</strong>
                </li>
              ))}
            </ul>
          </GlassCard>

          <GlassCard className="p-4 space-y-3" data-testid="lab-work-panel">
            <h3 className="text-sm font-bold text-[var(--glass-text)] flex items-center gap-2">
              <FlaskConical size={15} className="text-sky-400" /> أعمال المعامل (التكلفة مقابل سعر المريض)
            </h3>
            <ul className="space-y-2 text-xs">
              {labWorkOrders.map((lw) => (
                <li key={lw.id} className="border-b border-[var(--glass-border)] pb-1 flex justify-between">
                  <span>{lw.labName}: {lw.workDescription}</span>
                  <span>تكلفة: {lw.labCostAmount} | للمريض: <strong>{lw.patientPriceAmount} {lw.currency}</strong></span>
                </li>
              ))}
            </ul>
            {!readOnlyInspectMode && (
              <div className="grid grid-cols-2 gap-2 pt-2">
                <GlassInput label="اسم المعمل" value={labName} onChange={(e) => setLabName(e.target.value)} />
                <GlassInput label="وصف التركيبة" value={labWorkDesc} onChange={(e) => setLabWorkDesc(e.target.value)} />
                <GlassInput label="تكلفة المعمل" type="number" value={labCost} onChange={(e) => setLabCost(e.target.value)} />
                <GlassInput label="سعر المريض" type="number" value={labPatientPrice} onChange={(e) => setLabPatientPrice(e.target.value)} />
                <div className="col-span-2">
                  <GlassButton variant="regular" onClick={handleCreateLabWork}>إضافة طلب معمل</GlassButton>
                </div>
              </div>
            )}
          </GlassCard>
        </div>
      )}

      {billingSection === 'finance' && (
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
          <GlassCard className="p-4 space-y-3" data-testid="suppliers-expenses-panel">
            <h3 className="text-sm font-bold text-[var(--glass-text)]">الموردون والمصروفات اليومية</h3>
            <div className="space-y-1 text-xs">
              {suppliers.map((s) => (
                <div key={s.id} className="flex justify-between border-b border-[var(--glass-border)] pb-1">
                  <span>{s.name}</span>
                  <span>متبقي: <strong>{s.balanceDue ?? 0} {currency}</strong></span>
                </div>
              ))}
            </div>
            {!readOnlyInspectMode && (
              <div className="grid grid-cols-2 gap-2">
                <GlassInput label="اسم مورد جديد" value={supplierName} onChange={(e) => setSupplierName(e.target.value)} />
                <GlassInput label="هاتف المورد" value={supplierPhone} onChange={(e) => setSupplierPhone(e.target.value)} />
                <div className="col-span-2">
                  <GlassButton variant="default" onClick={handleCreateSupplier}>إضافة مورد</GlassButton>
                </div>
              </div>
            )}

            <div className="pt-2 border-t border-[var(--glass-border)] space-y-2 text-xs">
              <h4 className="font-bold text-[var(--glass-muted)]">المصروفات ({expenses.length})</h4>
              {expenses.map((e) => (
                <div key={e.id} className="flex justify-between">
                  <span>{e.description} ({e.method ?? 'غير نقدي'})</span>
                  <strong>{e.amount} {e.currency}</strong>
                </div>
              ))}
              {!readOnlyInspectMode && (
                <div className="grid grid-cols-2 gap-2 pt-2">
                  <GlassInput label="وصف المصروف" value={expenseDesc} onChange={(e) => setExpenseDesc(e.target.value)} />
                  <GlassInput label="المبلغ" type="number" value={expenseAmount} onChange={(e) => setExpenseAmount(e.target.value)} />
                  <select
                    value={expenseMethod}
                    onChange={(e) => setExpenseMethod(e.target.value)}
                    className="rounded-lg border border-[var(--glass-border)] bg-[var(--glass-surface)] px-2 py-2 text-xs"
                  >
                    <option value="Cash">نقداً (Cash)</option>
                    <option value="Visa">فيزا (Visa)</option>
                    <option value="InstaPay">إنستاباي (InstaPay)</option>
                  </select>
                  <GlassButton variant="regular" onClick={handleCreateExpense}>تسجيل مصروف</GlassButton>
                </div>
              )}
            </div>
          </GlassCard>

          <GlassCard className="p-4 space-y-3" data-testid="daily-closing-panel">
            <div className="flex items-center justify-between">
              <h3 className="text-sm font-bold text-[var(--glass-text)]">إقفال اليومية ومطابقة الخزينة ({dailyClosing.closingDate})</h3>
              {dailyClosing.status === 'Closed' ? <Lock size={16} className="text-rose-400" /> : <Unlock size={16} className="text-emerald-400" />}
            </div>

            {dailyClosing.financeExpensesExcluded && (
              <AlertBanner
                variant="info"
                message="وحدة المالية (Finance) غير مفعلة — يتم احتساب الإقفال بناءً على المقبوضات والمرتجعات فقط."
              />
            )}

            <div className="space-y-1 text-xs">
              {dailyClosing.methods.map((m) => (
                <div key={m.method} className="flex justify-between border-b border-[var(--glass-border)] pb-1">
                  <span>{m.method}: وارد {m.paymentsTotal} - مرتجع {m.refundsTotal} - مصروف {m.expensesTotal}</span>
                  <span>متوقع: <strong>{m.expectedTotal}</strong> | فعلي: <strong>{m.countedTotal}</strong></span>
                </div>
              ))}
            </div>

            {!readOnlyInspectMode && dailyClosing.status !== 'Closed' && (
              <div className="space-y-2 pt-2">
                <GlassInput label="ملاحظات الإقفال" value={closeNotes} onChange={(e) => setCloseNotes(e.target.value)} />
                <GlassButton variant="prominent" onClick={handleCloseDay}>إقفال اليومية (أونلاين فقط)</GlassButton>
              </div>
            )}

            {!readOnlyInspectMode && dailyClosing.status === 'Closed' && (
              <div className="space-y-2 pt-2">
                <GlassInput label="سبب إعادة الفتح (يتطلب صلاحية المالك)" value={reopenReason} onChange={(e) => setReopenReason(e.target.value)} />
                <GlassButton variant="regular" onClick={handleReopenDay}>إعادة فتح اليومية (أونلاين فقط)</GlassButton>
              </div>
            )}
          </GlassCard>
        </div>
      )}
    </div>
  );
};

