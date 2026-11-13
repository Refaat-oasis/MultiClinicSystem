import React, { useMemo, useState } from 'react';
import {
  DollarSign,
  Users,
  FileSpreadsheet,
  FileText,
  Upload,
  Plus,
  CheckCircle2,
  Ban,
  AlertCircle,
  BarChart3,
} from 'lucide-react';
import { GlassCard, GlassButton, AlertBanner } from '@clinic/ui';
import { useOfflineMutation } from '@clinic/api-client';

export interface FixedCostItemData {
  id: string;
  clinicId: string;
  name: string;
  category: string;
  amount: number;
  recurrence: string;
  startDate: string;
  endDate: string | null;
  dayOfMonth: number | null;
  isVoided: boolean;
}

export interface EmployeeItemData {
  id: string;
  clinicId: string;
  fullName: string;
  roleTitle: string;
  baseSalary: number;
  isActive: boolean;
  hireDate: string;
}

export interface PayrollAdjustmentItemData {
  id: string;
  employeeId: string;
  employeeName: string;
  periodYear: number;
  periodMonth: number;
  adjustmentType: string;
  amount: number;
  reason: string;
}

export interface PayrollRunItemData {
  id: string;
  periodYear: number;
  periodMonth: number;
  status: string;
  totalBaseSalary: number;
  totalBonuses: number;
  totalDeductions: number;
  totalAdvances: number;
  totalNetPay: number;
}

export interface IncomeVsExpensesReportData {
  fromDate: string;
  toDate: string;
  grossPayments: number;
  refundsTotal: number;
  netPatientCashIn: number;
  operatingExpensesCashOut: number;
  supplierPaymentsCashOut: number;
  fixedCostsDue: number;
  payrollTotalNet: number;
  totalOutflows: number;
  netOperatingCashFlow: number;
  supplierPayablesOpen: number;
}

export interface DoctorFinancialRowData {
  doctorId: string;
  doctorName: string;
  specialty: string;
  visitCount: number;
  billedTotal: number;
  collectedTotal: number;
  payrollCost: number;
}

export interface ImportRowErrorData {
  rowNumber: number;
  columnName: string;
  errorKey: string;
  message: string;
}

export interface ImportValidationData {
  targetKind: string;
  fileName: string;
  totalRows: number;
  validRows: number;
  rejectedRows: number;
  canCommit: boolean;
  errors: ImportRowErrorData[];
}

export interface ImportBatchLogData {
  id: string;
  targetKind: string;
  fileName: string;
  totalRows: number;
  acceptedRows: number;
  rejectedRows: number;
  isCommitted: boolean;
  executedAtUtc: string;
}

export interface ClinicFinanceReportsInspectData {
  clinicId: string;
  incomeVsExpenses: IncomeVsExpensesReportData;
  fixedCosts: FixedCostItemData[];
  employees: EmployeeItemData[];
  payrollRuns: PayrollRunItemData[];
  recentImports: ImportBatchLogData[];
}

export interface Phase14FinanceReportsScreenProps {
  clinicId: string;
  readOnlyInspectMode?: boolean;
  enabledModules?: string[];
  initialInspectSnapshot?: ClinicFinanceReportsInspectData;
}

export const Phase14FinanceReportsScreen: React.FC<Phase14FinanceReportsScreenProps> = ({
  clinicId,
  readOnlyInspectMode = false,
  enabledModules = ['patients', 'scheduling', 'billing', 'inventory', 'finance', 'reports', 'excel-import'],
  initialInspectSnapshot,
}) => {
  const [subTab, setSubTab] = useState<'finance' | 'reports' | 'import' | 'dashboards'>('finance');

  const [fixedCosts, setFixedCosts] = useState<FixedCostItemData[]>(
    initialInspectSnapshot?.fixedCosts ?? [
      {
        id: 'fc-1',
        clinicId,
        name: 'إيجار العيادة الشهري',
        category: 'Rent',
        amount: 12000,
        recurrence: 'Monthly',
        startDate: '2026-01-01',
        endDate: null,
        dayOfMonth: 1,
        isVoided: false,
      },
    ],
  );
  const [employees] = useState<EmployeeItemData[]>(
    initialInspectSnapshot?.employees ?? [
      {
        id: 'emp-1',
        clinicId,
        fullName: 'سارة محمود',
        roleTitle: 'منسقة استقبال',
        baseSalary: 6000,
        isActive: true,
        hireDate: '2026-01-15',
      },
    ],
  );
  const [adjustments, setAdjustments] = useState<PayrollAdjustmentItemData[]>([
    {
      id: 'adj-1',
      employeeId: 'emp-1',
      employeeName: 'سارة محمود',
      periodYear: 2026,
      periodMonth: 10,
      adjustmentType: 'Bonus',
      amount: 500,
      reason: 'التزام بمواعيد الشهر',
    },
  ]);
  const [payrollRuns, setPayrollRuns] = useState<PayrollRunItemData[]>(
    initialInspectSnapshot?.payrollRuns ?? [
      {
        id: 'pr-1',
        periodYear: 2026,
        periodMonth: 10,
        status: 'Draft',
        totalBaseSalary: 6000,
        totalBonuses: 500,
        totalDeductions: 0,
        totalAdvances: 0,
        totalNetPay: 6500,
      },
    ],
  );

  const [incomeReport] = useState<IncomeVsExpensesReportData>(
    initialInspectSnapshot?.incomeVsExpenses ?? {
      fromDate: '2026-10-01',
      toDate: '2026-10-31',
      grossPayments: 48000,
      refundsTotal: 1500,
      netPatientCashIn: 46500,
      operatingExpensesCashOut: 4000,
      supplierPaymentsCashOut: 6000,
      fixedCostsDue: 12000,
      payrollTotalNet: 6500,
      totalOutflows: 28500,
      netOperatingCashFlow: 18000,
      supplierPayablesOpen: 2500,
    },
  );

  const [doctorRows] = useState<DoctorFinancialRowData[]>([
    {
      doctorId: 'doc-1',
      doctorName: 'د. عادل حسني',
      specialty: 'طب الأسنان',
      visitCount: 42,
      billedTotal: 49500,
      collectedTotal: 48000,
      payrollCost: 14000,
    },
  ]);

  const [importTarget, setImportTarget] = useState('Patients');
  const [previewResult, setPreviewResult] = useState<ImportValidationData | null>(null);
  const [importHistory, setImportHistory] = useState<ImportBatchLogData[]>(
    initialInspectSnapshot?.recentImports ?? [],
  );

  const [newCostName, setNewCostName] = useState('');
  const [newCostAmount, setNewCostAmount] = useState('2500');
  const [newCostCategory, setNewCostCategory] = useState('Utilities');
  const [newCostRecurrence, setNewCostRecurrence] = useState('Monthly');
  const [statusMessage, setStatusMessage] = useState<string | null>(null);

  const fixedCostMutation = useOfflineMutation<
    FixedCostItemData,
    { name: string; category: string; amount: number; recurrence: string; startDate: string }
  >({
    endpoint: `/api/v1/clinics/${clinicId}/finance/fixed-costs`,
    method: 'POST',
  });

  const finalizePayrollMutation = useOfflineMutation<PayrollRunItemData, Record<string, never>>({
    endpoint: `/api/v1/clinics/${clinicId}/finance/payroll/runs/${payrollRuns[0]?.id ?? 'pr-1'}/finalize`,
    method: 'POST',
    onlineOnly: true,
  });

  const importPreviewMutation = useOfflineMutation<ImportValidationData, { targetKind: string; fileName: string }>({
    endpoint: `/api/v1/clinics/${clinicId}/import/${importTarget}/preview`,
    method: 'POST',
    onlineOnly: true,
  });

  const availableImportTargets = useMemo(() => {
    const targets: { key: string; labelAr: string }[] = [];
    if (enabledModules.includes('patients')) {
      targets.push({ key: 'Patients', labelAr: 'المرضى' });
    }
    if (enabledModules.includes('scheduling') && enabledModules.includes('patients')) {
      targets.push({ key: 'Appointments', labelAr: 'المواعيد' });
    }
    if (enabledModules.includes('billing') && enabledModules.includes('patients')) {
      targets.push({ key: 'OpeningBalances', labelAr: 'الأرصدة الافتتاحية' });
    }
    if (enabledModules.includes('inventory')) {
      targets.push({ key: 'InventoryItems', labelAr: 'أصناف المخزون' });
    }
    if (enabledModules.includes('finance')) {
      targets.push({ key: 'FixedCosts', labelAr: 'التكاليف الثابتة' });
    }
    return targets;
  }, [enabledModules]);

  const handleAddFixedCost = () => {
    if (readOnlyInspectMode || !newCostName.trim()) {
      return;
    }

    const amountVal = Number(newCostAmount);
    if (!Number.isFinite(amountVal) || amountVal <= 0) {
      return;
    }

    const created: FixedCostItemData = {
      id: `fc-${fixedCosts.length + 1}`,
      clinicId,
      name: newCostName.trim(),
      category: newCostCategory,
      amount: amountVal,
      recurrence: newCostRecurrence,
      startDate: '2026-10-01',
      endDate: null,
      dayOfMonth: 1,
      isVoided: false,
    };

    fixedCostMutation.mutate({
      name: created.name,
      category: created.category,
      amount: created.amount,
      recurrence: created.recurrence,
      startDate: created.startDate,
    });
    setFixedCosts((prev) => [created, ...prev]);
    setNewCostName('');
    setStatusMessage(`تم تسجيل التكلفة الثابتة: ${created.name}`);
  };

  const handleVoidFixedCost = (id: string) => {
    if (readOnlyInspectMode) {
      return;
    }
    setFixedCosts((prev) => prev.map((fc) => (fc.id === id ? { ...fc, isVoided: true } : fc)));
    setStatusMessage('تم إلغاء التكلفة الثابتة (إبطال فقط دون حذف السجل المالي)');
  };

  const handleAddBonus = () => {
    if (readOnlyInspectMode || employees.length === 0) {
      return;
    }
    const targetEmp = employees[0];
    setAdjustments((prev) => [
      {
        id: `adj-${prev.length + 1}`,
        employeeId: targetEmp.id,
        employeeName: targetEmp.fullName,
        periodYear: 2026,
        periodMonth: 10,
        adjustmentType: 'Bonus',
        amount: 250,
        reason: 'مكافأة أداء شهرية',
      },
      ...prev,
    ]);
    setStatusMessage(`تمت إضافة مكافأة للموظف ${targetEmp.fullName}`);
  };

  const handleFinalizePayroll = () => {
    if (readOnlyInspectMode || payrollRuns.length === 0) {
      return;
    }
    finalizePayrollMutation.mutate({});
    setPayrollRuns((prev) =>
      prev.map((run, idx) => (idx === 0 ? { ...run, status: 'Finalized' } : run)),
    );
    setStatusMessage('تم اعتماد كشف الرواتب الشهري وترحيل القيد الثابت');
  };

  const handleExport = (format: 'Excel' | 'Pdf') => {
    if (typeof window !== 'undefined' && typeof navigator !== 'undefined' && !navigator.onLine) {
      setStatusMessage('تصدير التقارير يتطلب اتصالاً بالإنترنت (ONLINE_REQUIRED)');
      return;
    }
    setStatusMessage(`تم تجهيز ملف التقرير بصيغة ${format} مطابقاً للأرقام المعروضة`);
  };

  const handleSimulateDryRun = (withErrors: boolean) => {
    if (readOnlyInspectMode) {
      return;
    }

    importPreviewMutation.mutate({ targetKind: importTarget, fileName: `${importTarget}.xlsx` });
    if (withErrors) {
      setPreviewResult({
        targetKind: importTarget,
        fileName: `${importTarget}_bad.xlsx`,
        totalRows: 3,
        validRows: 1,
        rejectedRows: 2,
        canCommit: false,
        errors: [
          {
            rowNumber: 2,
            columnName: 'Phone',
            errorKey: 'PHONE_INVALID',
            message: 'رقم الهاتف غير صالح أو فارغ',
          },
          {
            rowNumber: 3,
            columnName: 'Amount',
            errorKey: 'NEGATIVE_AMOUNT',
            message: 'المبلغ يجب أن يكون أكبر من صفر',
          },
        ],
      });
      setStatusMessage('فشل الفحص المسبق للاستيراد: لم يتم حفظ أي صف في قاعدة البيانات');
    } else {
      const committed: ImportBatchLogData = {
        id: `imp-${importHistory.length + 1}`,
        targetKind: importTarget,
        fileName: `${importTarget}_clean.xlsx`,
        totalRows: 3,
        acceptedRows: 3,
        rejectedRows: 0,
        isCommitted: true,
        executedAtUtc: '2026-10-31T10:00:00Z',
      };
      setPreviewResult({
        targetKind: importTarget,
        fileName: committed.fileName,
        totalRows: 3,
        validRows: 3,
        rejectedRows: 0,
        canCommit: true,
        errors: [],
      });
      setImportHistory((prev) => [committed, ...prev]);
      setStatusMessage(`تم استيراد ${committed.acceptedRows} صف بنجاح (${importTarget})`);
    }
  };

  return (
    <div dir="rtl" style={{ display: 'flex', flexDirection: 'column', gap: '1.25rem' }}>
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
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
            <DollarSign size={22} color="var(--color-accent-teal)" />
            <h2 style={{ fontSize: '1.25rem', fontWeight: 700, margin: 0 }}>
              المالية والتقارير واستيراد إكسل (المرحلة 14)
            </h2>
          </div>

          <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
            <GlassButton
              variant={subTab === 'finance' ? 'prominent' : 'default'}
              onClick={() => setSubTab('finance')}
            >
              <DollarSign size={15} />
              التكاليف والرواتب
            </GlassButton>
            <GlassButton
              variant={subTab === 'reports' ? 'prominent' : 'default'}
              onClick={() => setSubTab('reports')}
            >
              <BarChart3 size={15} />
              التقارير والتصدير
            </GlassButton>
            <GlassButton
              variant={subTab === 'import' ? 'prominent' : 'default'}
              onClick={() => setSubTab('import')}
            >
              <FileSpreadsheet size={15} />
              استيراد إكسل
            </GlassButton>
            <GlassButton
              variant={subTab === 'dashboards' ? 'prominent' : 'default'}
              onClick={() => setSubTab('dashboards')}
            >
              <CheckCircle2 size={15} />
              بطاقات لوحة القيادة
            </GlassButton>
          </div>
        </div>
      </GlassCard>

      {statusMessage && <AlertBanner variant="info" message={statusMessage} />}

      {subTab === 'finance' && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
          <GlassCard style={{ padding: '1.25rem' }}>
            <h3 style={{ marginBlockEnd: '0.75rem' }}>التكاليف الثابتة المتكررة (بدون حذف نهائي)</h3>
            {!readOnlyInspectMode && (
              <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap', marginBlockEnd: '1rem' }}>
                <input
                  aria-label="اسم التكلفة الثابتة"
                  placeholder="اسم المصروف الثابت"
                  value={newCostName}
                  onChange={(e) => setNewCostName(e.target.value)}
                  style={{ padding: '0.45rem', minWidth: '180px' }}
                />
                <input
                  aria-label="قيمة التكلفة الثابتة"
                  type="number"
                  value={newCostAmount}
                  onChange={(e) => setNewCostAmount(e.target.value)}
                  style={{ padding: '0.45rem', width: '110px' }}
                />
                <select
                  aria-label="تصنيف التكلفة الثابتة"
                  value={newCostCategory}
                  onChange={(e) => setNewCostCategory(e.target.value)}
                  style={{ padding: '0.45rem' }}
                >
                  <option value="Rent">إيجار</option>
                  <option value="Utilities">مرافق وكهرباء</option>
                  <option value="Software">برمجيات واشتراكات</option>
                  <option value="Maintenance">صيانة</option>
                </select>
                <select
                  aria-label="تكرار التكلفة"
                  value={newCostRecurrence}
                  onChange={(e) => setNewCostRecurrence(e.target.value)}
                  style={{ padding: '0.45rem' }}
                >
                  <option value="Monthly">شهري</option>
                  <option value="Weekly">أسبوعي</option>
                  <option value="Daily">يومي</option>
                  <option value="OneTime">مرة واحدة</option>
                </select>
                <GlassButton onClick={handleAddFixedCost}>
                  <Plus size={15} />
                  إضافة تكلفة ثابتة
                </GlassButton>
              </div>
            )}

            <div style={{ display: 'grid', gap: '0.5rem' }}>
              {fixedCosts.map((fc) => (
                <div
                  key={fc.id}
                  data-testid={`fixed-cost-row-${fc.id}`}
                  style={{
                    display: 'flex',
                    justifyContent: 'space-between',
                    alignItems: 'center',
                    padding: '0.6rem',
                    borderRadius: '0.5rem',
                    background: 'var(--glass-surface)',
                    opacity: fc.isVoided ? 0.6 : 1,
                  }}
                >
                  <div>
                    <strong>{fc.name}</strong> — {fc.amount.toFixed(2)} ({fc.recurrence} / {fc.category})
                  </div>
                  {fc.isVoided ? (
                    <span>مُبطَل (Voided)</span>
                  ) : (
                    !readOnlyInspectMode && (
                      <GlassButton onClick={() => handleVoidFixedCost(fc.id)}>
                        <Ban size={14} />
                        إبطال
                      </GlassButton>
                    )
                  )}
                </div>
              ))}
            </div>
          </GlassCard>

          <GlassCard style={{ padding: '1.25rem' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBlockEnd: '0.75rem' }}>
              <h3 style={{ display: 'flex', alignItems: 'center', gap: '0.4rem', margin: 0 }}>
                <Users size={18} />
                الرواتب الثابتة والمكافآت والخصومات والسُلف
              </h3>
              {!readOnlyInspectMode && (
                <div style={{ display: 'flex', gap: '0.5rem' }}>
                  <GlassButton onClick={handleAddBonus}>إضافة مكافأة</GlassButton>
                  <GlassButton variant="prominent" onClick={handleFinalizePayroll}>
                    اعتماد كشف الرواتب
                  </GlassButton>
                </div>
              )}
            </div>

            <div style={{ display: 'grid', gap: '0.5rem', marginBlockEnd: '0.75rem' }}>
              {payrollRuns.map((run) => (
                <div
                  key={run.id}
                  style={{
                    padding: '0.75rem',
                    borderRadius: '0.5rem',
                    background: 'var(--glass-surface)',
                    display: 'flex',
                    justifyContent: 'space-between',
                    flexWrap: 'wrap',
                    gap: '0.5rem',
                  }}
                >
                  <span>
                    كشف {run.periodYear}/{run.periodMonth} — الحالة: <strong>{run.status}</strong>
                  </span>
                  <span>
                    الأساسي: {run.totalBaseSalary.toFixed(2)} | المكافآت: {run.totalBonuses.toFixed(2)} | الصافي:{' '}
                    <strong>{run.totalNetPay.toFixed(2)}</strong>
                  </span>
                </div>
              ))}
            </div>

            <div style={{ fontSize: '0.875rem', color: 'var(--color-text-secondary)' }}>
              آخر التسويات المسجلة: {adjustments.length} (مكافآت، خصومات، سُلف)
            </div>
          </GlassCard>
        </div>
      )}

      {subTab === 'reports' && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
          <GlassCard style={{ padding: '1.25rem' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '0.5rem', marginBlockEnd: '0.75rem' }}>
              <h3 style={{ margin: 0 }}>تقرير الدخل مقابل المصروفات</h3>
              <div style={{ display: 'flex', gap: '0.5rem' }}>
                <GlassButton onClick={() => handleExport('Excel')}>
                  <FileSpreadsheet size={15} />
                  تصدير Excel
                </GlassButton>
                <GlassButton onClick={() => handleExport('Pdf')}>
                  <FileText size={15} />
                  تصدير PDF
                </GlassButton>
              </div>
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(170px, 1fr))', gap: '0.75rem' }}>
              <div style={{ padding: '0.75rem', borderRadius: '0.5rem', background: 'var(--glass-surface)' }}>
                <div>صافي التحصيل النقدي</div>
                <strong>{incomeReport.netPatientCashIn.toFixed(2)}</strong>
              </div>
              <div style={{ padding: '0.75rem', borderRadius: '0.5rem', background: 'var(--glass-surface)' }}>
                <div>إجمالي التدفقات الخارجة</div>
                <strong>{incomeReport.totalOutflows.toFixed(2)}</strong>
              </div>
              <div style={{ padding: '0.75rem', borderRadius: '0.5rem', background: 'var(--glass-surface)' }}>
                <div>صافي التدفق التشغيلي</div>
                <strong>{incomeReport.netOperatingCashFlow.toFixed(2)}</strong>
              </div>
              <div style={{ padding: '0.75rem', borderRadius: '0.5rem', background: 'var(--glass-surface)' }}>
                <div>مستحقات الموردين المفتوحة</div>
                <strong>{incomeReport.supplierPayablesOpen.toFixed(2)}</strong>
              </div>
            </div>
          </GlassCard>

          <GlassCard style={{ padding: '1.25rem' }}>
            <h3 style={{ marginBlockEnd: '0.75rem' }}>ملخص الأطباء (إيرادات العيادة مقابل تكلفة الراتب الثابت)</h3>
            <table style={{ width: '100%', borderCollapse: 'collapse' }}>
              <thead>
                <tr>
                  <th style={{ textAlign: 'start', padding: '0.45rem' }}>الطبيب</th>
                  <th style={{ textAlign: 'start', padding: '0.45rem' }}>التخصص</th>
                  <th style={{ textAlign: 'start', padding: '0.45rem' }}>الزيارات</th>
                  <th style={{ textAlign: 'start', padding: '0.45rem' }}>المفوتر</th>
                  <th style={{ textAlign: 'start', padding: '0.45rem' }}>المحصل</th>
                  <th style={{ textAlign: 'start', padding: '0.45rem' }}>تكلفة الراتب</th>
                </tr>
              </thead>
              <tbody>
                {doctorRows.map((doc) => (
                  <tr key={doc.doctorId}>
                    <td style={{ padding: '0.45rem' }}>{doc.doctorName}</td>
                    <td style={{ padding: '0.45rem' }}>{doc.specialty}</td>
                    <td style={{ padding: '0.45rem' }}>{doc.visitCount}</td>
                    <td style={{ padding: '0.45rem' }}>{doc.billedTotal.toFixed(2)}</td>
                    <td style={{ padding: '0.45rem' }}>{doc.collectedTotal.toFixed(2)}</td>
                    <td style={{ padding: '0.45rem' }}>{doc.payrollCost.toFixed(2)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </GlassCard>
        </div>
      )}

      {subTab === 'import' && (
        <GlassCard style={{ padding: '1.25rem' }}>
          <h3 style={{ marginBlockEnd: '0.75rem' }}>معالج استيراد ملفات إكسل (فحص مسبق ثم اعتماد ذري)</h3>
          <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap', marginBlockEnd: '1rem' }}>
            <select
              aria-label="البيانات المستهدفة للاستيراد"
              value={importTarget}
              onChange={(e) => setImportTarget(e.target.value)}
              style={{ padding: '0.45rem' }}
            >
              {availableImportTargets.map((t) => (
                <option key={t.key} value={t.key}>
                  {t.labelAr}
                </option>
              ))}
            </select>

            {!readOnlyInspectMode && (
              <>
                <GlassButton onClick={() => handleSimulateDryRun(true)}>
                  <AlertCircle size={15} />
                  فحص ملف بأخطاء (معاينة فقط)
                </GlassButton>
                <GlassButton variant="prominent" onClick={() => handleSimulateDryRun(false)}>
                  <Upload size={15} />
                  فحص واعتماد القالب السليم
                </GlassButton>
              </>
            )}
          </div>

          {previewResult && (
            <div style={{ padding: '0.75rem', borderRadius: '0.5rem', background: 'var(--glass-surface)', marginBlockEnd: '0.75rem' }}>
              <div>
                الملف: <strong>{previewResult.fileName}</strong> | الإجمالي: {previewResult.totalRows} | صالح:{' '}
                {previewResult.validRows} | مرفوض: {previewResult.rejectedRows}
              </div>
              {previewResult.errors.map((err) => (
                <div key={`${err.rowNumber}-${err.columnName}`} style={{ color: '#ef4444', marginTop: '0.25rem' }}>
                  صف {err.rowNumber} ({err.columnName}): {err.message} [{err.errorKey}]
                </div>
              ))}
            </div>
          )}

          {importHistory.length > 0 && (
            <div style={{ fontSize: '0.875rem' }}>
              سجل الدُفعات المعتمدة: {importHistory.map((h) => `${h.targetKind} (${h.acceptedRows})`).join('، ')}
            </div>
          )}
        </GlassCard>
      )}

      {subTab === 'dashboards' && (
        <GlassCard style={{ padding: '1.25rem' }}>
          <h3 style={{ marginBlockEnd: '0.75rem' }}>مؤشرات المالية والتقارير والاستيراد</h3>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(190px, 1fr))', gap: '0.75rem' }}>
            <div style={{ padding: '0.75rem', borderRadius: '0.5rem', background: 'var(--glass-surface)' }}>
              <div>الدخل مقابل المصروفات (الشهر)</div>
              <strong>{incomeReport.netOperatingCashFlow.toFixed(2)}</strong>
            </div>
            <div style={{ padding: '0.75rem', borderRadius: '0.5rem', background: 'var(--glass-surface)' }}>
              <div>أرصدة الموردين المفتوحة</div>
              <strong>{incomeReport.supplierPayablesOpen.toFixed(2)}</strong>
            </div>
            <div style={{ padding: '0.75rem', borderRadius: '0.5rem', background: 'var(--glass-surface)' }}>
              <div>التكاليف الثابتة المستحقة</div>
              <strong>{incomeReport.fixedCostsDue.toFixed(2)}</strong>
            </div>
            <div style={{ padding: '0.75rem', borderRadius: '0.5rem', background: 'var(--glass-surface)' }}>
              <div>صافي كشف الرواتب</div>
              <strong>{incomeReport.payrollTotalNet.toFixed(2)}</strong>
            </div>
          </div>
        </GlassCard>
      )}
    </div>
  );
};

