import React, { useMemo, useState } from 'react';
import { Package, AlertTriangle, Truck, Settings2, History, Layers } from 'lucide-react';
import { GlassCard, GlassButton, GlassInput, AlertBanner } from '@clinic/ui';
import { useOfflineMutation } from '@clinic/api-client';

export interface InventoryItemData {
  id: string;
  clinicId: string;
  itemCode: string;
  nameAr: string;
  nameEn: string;
  unitName: string;
  packSize: number;
  unitCostAmount: number;
  defaultMarkupPercent: number;
  markedUpPatientUnitPrice: number;
  currency: string;
  lowStockThreshold: number;
  quantityOnHand: number;
  isLowStock: boolean;
  specialtyCode: string | null;
  storageLocation: string | null;
  isActive: boolean;
}

export interface InventoryItemBatchData {
  id: string;
  itemId: string;
  batchNumber: string;
  expiryDate: string | null;
  quantityReceived: number;
  quantityRemaining: number;
  unitCostAmount: number;
  receivedAtUtc: string;
}

export interface StockMovementData {
  id: string;
  itemId: string;
  batchId: string | null;
  movementType: string;
  quantityDelta: number;
  quantityAfterItem: number;
  unitCostAmount: number;
  referenceType: string | null;
  reason: string | null;
  occurredAtUtc: string;
}

export interface InventoryAlertData {
  id: string;
  itemId: string;
  itemCode: string;
  itemNameAr: string;
  itemNameEn: string;
  kind: string;
  messageAr: string;
  messageEn: string;
  isResolved: boolean;
  createdAtUtc: string;
}

export interface ClinicInventorySettingData {
  clinicId: string;
  negativeStockPolicy: 'Block' | 'AllowWithWarning' | string;
  nearExpiryWarningDays: number;
}

export interface PurchaseInvoiceData {
  id: string;
  supplierId: string | null;
  supplierName: string;
  invoiceNumber: string;
  invoiceDate: string;
  totalAmount: number;
  paidAmount: number;
  remainingAmount: number;
  currency: string;
}

export interface SupplierInventoryStatementData {
  supplierId: string;
  supplierName: string;
  totalPurchasedAmount: number;
  totalPaidAmount: number;
  balanceDueAmount: number;
  financeLedgerAccruedAmount: number;
  financeLedgerPaidAmount: number;
  financeLedgerBalanceDueAmount: number;
  financeModuleEnabled: boolean;
  currency: string;
  invoices: PurchaseInvoiceData[];
}

export interface ClinicInventoryInspectData {
  clinicId: string;
  settings: ClinicInventorySettingData;
  items: InventoryItemData[];
  batches: InventoryItemBatchData[];
  alerts: InventoryAlertData[];
  recentInvoices: PurchaseInvoiceData[];
}

export interface ReceivePurchaseInvoicePayload {
  clinicId: string;
  supplierId: string | null;
  supplierName: string;
  invoiceNumber: string;
  invoiceDate: string;
  paidAmount: number;
  paymentMethod: string | null;
  lines: Array<{
    itemId: string;
    packSize: number;
    packCount: number;
    packPriceAmount: number;
    batchNumber: string;
    expiryDate: string | null;
  }>;
}

export interface AdjustStockPayload {
  clinicId: string;
  itemId: string;
  quantityDelta: number;
  reason: string;
}

export interface UpdateInventorySettingPayload {
  clinicId: string;
  negativeStockPolicy: string;
  nearExpiryWarningDays: number;
}

export interface ResolveAlertPayload {
  clinicId: string;
  alertId: string;
}

export interface Phase12InventoryScreenProps {
  clinicId: string;
  readOnlyInspectMode?: boolean;
  initialInspectSnapshot?: ClinicInventoryInspectData | null;
  initialItems?: InventoryItemData[];
  initialBatches?: InventoryItemBatchData[];
  initialMovements?: StockMovementData[];
  initialAlerts?: InventoryAlertData[];
  initialSettings?: ClinicInventorySettingData | null;
  initialStatement?: SupplierInventoryStatementData | null;
  onReceivePurchaseInvoice?: (payload: ReceivePurchaseInvoicePayload) => void;
  onAdjustStock?: (payload: AdjustStockPayload) => void;
  onResolveAlert?: (payload: ResolveAlertPayload) => void;
  onUpdateSettings?: (payload: UpdateInventorySettingPayload) => void;
}

const DEFAULT_ITEMS: InventoryItemData[] = [
  {
    id: 'inv-item-1',
    clinicId: 'clinic-1',
    itemCode: 'GLV-01',
    nameAr: 'قفازات فحص لاتكس',
    nameEn: 'Latex Exam Gloves',
    unitName: 'piece',
    packSize: 30,
    unitCostAmount: 10,
    defaultMarkupPercent: 20,
    markedUpPatientUnitPrice: 12,
    currency: 'EGP',
    lowStockThreshold: 25,
    quantityOnHand: 18,
    isLowStock: true,
    specialtyCode: 'dental',
    storageLocation: 'خزانة أ',
    isActive: true,
  },
];

const DEFAULT_BATCHES: InventoryItemBatchData[] = [
  {
    id: 'batch-1',
    itemId: 'inv-item-1',
    batchNumber: 'LOT-2026-09',
    expiryDate: '2026-10-15',
    quantityReceived: 30,
    quantityRemaining: 18,
    unitCostAmount: 10,
    receivedAtUtc: '2026-09-01T09:00:00Z',
  },
];

const DEFAULT_MOVEMENTS: StockMovementData[] = [
  {
    id: 'mov-1',
    itemId: 'inv-item-1',
    batchId: 'batch-1',
    movementType: 'PurchaseReceived',
    quantityDelta: 30,
    quantityAfterItem: 30,
    unitCostAmount: 10,
    referenceType: 'PurchaseInvoice',
    reason: 'فاتورة شراء INV-100',
    occurredAtUtc: '2026-09-01T09:00:00Z',
  },
  {
    id: 'mov-2',
    itemId: 'inv-item-1',
    batchId: 'batch-1',
    movementType: 'SessionUsed',
    quantityDelta: -12,
    quantityAfterItem: 18,
    unitCostAmount: 10,
    referenceType: 'Visit',
    reason: 'استهلاك جلسة أسنان',
    occurredAtUtc: '2026-09-02T11:30:00Z',
  },
];

const DEFAULT_ALERTS: InventoryAlertData[] = [
  {
    id: 'alert-1',
    itemId: 'inv-item-1',
    itemCode: 'GLV-01',
    itemNameAr: 'قفازات فحص لاتكس',
    itemNameEn: 'Latex Exam Gloves',
    kind: 'LowStock',
    messageAr: 'الرصيد أقل من الحد الأدنى (18 / 25)',
    messageEn: 'Low stock (18 / 25)',
    isResolved: false,
    createdAtUtc: '2026-09-02T11:30:00Z',
  },
];

const DEFAULT_STATEMENT: SupplierInventoryStatementData = {
  supplierId: 'sup-demo-1',
  supplierName: 'شركة الدلتا الطبية',
  totalPurchasedAmount: 1500,
  totalPaidAmount: 600,
  balanceDueAmount: 900,
  financeLedgerAccruedAmount: 1500,
  financeLedgerPaidAmount: 600,
  financeLedgerBalanceDueAmount: 900,
  financeModuleEnabled: true,
  currency: 'EGP',
  invoices: [
    {
      id: 'pinv-1',
      supplierId: 'sup-demo-1',
      supplierName: 'شركة الدلتا الطبية',
      invoiceNumber: 'INV-100',
      invoiceDate: '2026-09-01',
      totalAmount: 1500,
      paidAmount: 600,
      remainingAmount: 900,
      currency: 'EGP',
    },
  ],
};

export const Phase12InventoryScreen: React.FC<Phase12InventoryScreenProps> = ({
  clinicId,
  readOnlyInspectMode = false,
  initialInspectSnapshot = null,
  initialItems,
  initialBatches,
  initialMovements,
  initialAlerts,
  initialSettings,
  initialStatement,
  onReceivePurchaseInvoice,
  onAdjustStock,
  onResolveAlert,
  onUpdateSettings,
}) => {
  const [items, setItems] = useState<InventoryItemData[]>(
    () => initialInspectSnapshot?.items ?? initialItems ?? DEFAULT_ITEMS,
  );
  const [batches] = useState<InventoryItemBatchData[]>(
    () => initialInspectSnapshot?.batches ?? initialBatches ?? DEFAULT_BATCHES,
  );
  const [movements, setMovements] = useState<StockMovementData[]>(
    () => initialMovements ?? DEFAULT_MOVEMENTS,
  );
  const [alerts, setAlerts] = useState<InventoryAlertData[]>(
    () => initialInspectSnapshot?.alerts ?? initialAlerts ?? DEFAULT_ALERTS,
  );
  const [statement] = useState<SupplierInventoryStatementData>(
    () => initialStatement ?? DEFAULT_STATEMENT,
  );

  // Settings state
  const [negativeStockPolicy, setNegativeStockPolicy] = useState<string>(
    () =>
      initialInspectSnapshot?.settings.negativeStockPolicy ??
      initialSettings?.negativeStockPolicy ??
      'Block',
  );
  const [nearExpiryDays, setNearExpiryDays] = useState<number>(
    () =>
      initialInspectSnapshot?.settings.nearExpiryWarningDays ??
      initialSettings?.nearExpiryWarningDays ??
      30,
  );

  // Pack-based purchase invoice form state
  const [selectedItemId, setSelectedItemId] = useState<string>(
    () => (initialInspectSnapshot?.items ?? initialItems ?? DEFAULT_ITEMS)[0]?.id ?? 'inv-item-1',
  );
  const [invoiceNumber, setInvoiceNumber] = useState('INV-2026-002');
  const [packSize, setPackSize] = useState<number>(30);
  const [packCount, setPackCount] = useState<number>(1);
  const [packPrice, setPackPrice] = useState<number>(300);
  const [paidAmount, setPaidAmount] = useState<number>(100);
  const [batchNumber, setBatchNumber] = useState('LOT-2026-10');
  const [expiryDate, setExpiryDate] = useState('2027-03-01');
  const [adjustDelta, setAdjustDelta] = useState<number>(5);
  const [adjustReason, setAdjustReason] = useState('جرد فعلي');
  const [feedback, setFeedback] = useState<string | null>(null);

  const receiveMutation = useOfflineMutation<ReceivePurchaseInvoicePayload, unknown>({
    method: 'POST',
    endpoint: `/clinics/${clinicId}/inventory/purchase-invoices`,
  });
  const adjustMutation = useOfflineMutation<AdjustStockPayload, unknown>({
    method: 'POST',
    endpoint: `/clinics/${clinicId}/inventory/adjustments`,
  });
  const resolveAlertMutation = useOfflineMutation<ResolveAlertPayload, unknown>({
    method: 'POST',
    endpoint: `/clinics/${clinicId}/inventory/alerts/resolve`,
  });
  const settingsMutation = useOfflineMutation<UpdateInventorySettingPayload, unknown>({
    method: 'PUT',
    endpoint: `/clinics/${clinicId}/inventory/settings`,
  });

  const computedTotalUnits = useMemo(
    () => Math.max(0, packSize) * Math.max(0, packCount),
    [packSize, packCount],
  );
  const computedTotalPrice = useMemo(
    () => Math.round(Math.max(0, packCount) * Math.max(0, packPrice) * 100) / 100,
    [packCount, packPrice],
  );
  const computedUnitCost = useMemo(() => {
    if (computedTotalUnits <= 0) {
      return 0;
    }
    return Math.round((computedTotalPrice / computedTotalUnits) * 100) / 100;
  }, [computedTotalPrice, computedTotalUnits]);

  const handleReceivePurchase = () => {
    if (readOnlyInspectMode) {
      return;
    }
    const payload: ReceivePurchaseInvoicePayload = {
      clinicId,
      supplierId: statement.supplierId,
      supplierName: statement.supplierName,
      invoiceNumber,
      invoiceDate: '2026-09-01',
      paidAmount,
      paymentMethod: paidAmount > 0 ? 'Cash' : null,
      lines: [
        {
          itemId: selectedItemId,
          packSize,
          packCount,
          packPriceAmount: packPrice,
          batchNumber,
          expiryDate: expiryDate || null,
        },
      ],
    };

    if (onReceivePurchaseInvoice) {
      onReceivePurchaseInvoice(payload);
    } else {
      receiveMutation.mutate(payload);
    }

    setItems((prev) =>
      prev.map((it) =>
        it.id === selectedItemId
          ? {
              ...it,
              packSize,
              unitCostAmount: computedUnitCost,
              quantityOnHand: it.quantityOnHand + computedTotalUnits,
              isLowStock: it.quantityOnHand + computedTotalUnits <= it.lowStockThreshold,
            }
          : it,
      ),
    );
    setFeedback(`تم استلام فاتورة ${invoiceNumber} (${computedTotalUnits} وحدة)`);
  };

  const handleAdjustStock = () => {
    if (readOnlyInspectMode) {
      return;
    }
    const payload: AdjustStockPayload = {
      clinicId,
      itemId: selectedItemId,
      quantityDelta: adjustDelta,
      reason: adjustReason,
    };
    if (onAdjustStock) {
      onAdjustStock(payload);
    } else {
      adjustMutation.mutate(payload);
    }
    setItems((prev) =>
      prev.map((it) =>
        it.id === selectedItemId
          ? {
              ...it,
              quantityOnHand: it.quantityOnHand + adjustDelta,
              isLowStock: it.quantityOnHand + adjustDelta <= it.lowStockThreshold,
            }
          : it,
      ),
    );
    setMovements((prev) => [
      {
        id: `mov-adj-${prev.length + 1}`,
        itemId: selectedItemId,
        batchId: null,
        movementType: 'ManualAdjustment',
        quantityDelta: adjustDelta,
        quantityAfterItem:
          (items.find((i) => i.id === selectedItemId)?.quantityOnHand ?? 0) + adjustDelta,
        unitCostAmount: items.find((i) => i.id === selectedItemId)?.unitCostAmount ?? 0,
        referenceType: 'Manual',
        reason: adjustReason,
        occurredAtUtc: new Date().toISOString(),
      },
      ...prev,
    ]);
    setFeedback('تم تسجيل التسوية اليدوية للمخزون');
  };

  const handleResolveAlert = (alertId: string) => {
    if (readOnlyInspectMode) {
      return;
    }
    const payload: ResolveAlertPayload = { clinicId, alertId };
    if (onResolveAlert) {
      onResolveAlert(payload);
    } else {
      resolveAlertMutation.mutate(payload);
    }
    setAlerts((prev) =>
      prev.map((a) => (a.id === alertId ? { ...a, isResolved: true } : a)),
    );
  };

  const handleSaveSettings = () => {
    if (readOnlyInspectMode) {
      return;
    }
    const payload: UpdateInventorySettingPayload = {
      clinicId,
      negativeStockPolicy,
      nearExpiryWarningDays: nearExpiryDays,
    };
    if (onUpdateSettings) {
      onUpdateSettings(payload);
    } else {
      settingsMutation.mutate(payload);
    }
    setFeedback('تم حفظ إعدادات المخزون والرصيد السالب');
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }} dir="rtl">
      {readOnlyInspectMode && (
        <AlertBanner
          variant="warning"
          title="وضع المراجعة فقط — المخزون"
          message="بيانات المخزون وفواتير الموردين للعرض فقط ولا يمكن تعديلها من بوابة المشرف العام."
        />
      )}
      {feedback && <AlertBanner variant="info" title="إشعار المخزون" message={feedback} />}

      {/* Items Catalog & Balances */}
      <GlassCard style={{ padding: '1.25rem' }}>
        <h2 style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBlockEnd: '0.75rem' }}>
          <Package size={18} />
          أصناف المخزون والأرصدة الحالية (FEFO)
        </h2>
        <div style={{ display: 'grid', gap: '0.5rem' }}>
          {items.map((item) => (
            <div
              key={item.id}
              data-testid={`inventory-item-${item.id}`}
              style={{
                display: 'flex',
                justifyContent: 'space-between',
                alignItems: 'center',
                paddingBlock: '0.5rem',
                paddingInline: '0.75rem',
                borderRadius: '0.5rem',
                background: 'var(--glass-surface-subtle, rgba(255,255,255,0.06))',
              }}
            >
              <div>
                <strong>
                  {item.itemCode} — {item.nameAr}
                </strong>
                <div style={{ fontSize: '0.85rem', opacity: 0.8 }}>
                  العبوة: {item.packSize} {item.unitName} | تكلفة الوحدة: {item.unitCostAmount} | سعر المريض (+{item.defaultMarkupPercent}%): {item.markedUpPatientUnitPrice} {item.currency}
                </div>
              </div>
              <div style={{ textAlign: 'end' }}>
                <span data-testid={`inventory-item-qty-${item.id}`} style={{ fontWeight: 700 }}>
                  الرصيد: {item.quantityOnHand} {item.unitName}
                </span>
                {item.isLowStock && (
                  <div style={{ color: '#f59e0b', fontSize: '0.8rem' }}>تحت الحد الأدنى ({item.lowStockThreshold})</div>
                )}
              </div>
            </div>
          ))}
        </div>
      </GlassCard>

      {/* Pack-based Purchase Invoice Form */}
      <GlassCard style={{ padding: '1.25rem' }}>
        <h3 style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBlockEnd: '0.75rem' }}>
          <Truck size={18} />
          فاتورة شراء بالعبوات (حساب تكلفة الوحدة تلقائياً)
        </h3>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(170px, 1fr))', gap: '0.75rem' }}>
          <label>
            الصنف
            <select
              aria-label="صنف الفاتورة"
              value={selectedItemId}
              disabled={readOnlyInspectMode}
              onChange={(e) => setSelectedItemId(e.target.value)}
              style={{ width: '100%', padding: '0.45rem' }}
            >
              {items.map((it) => (
                <option key={it.id} value={it.id}>
                  {it.itemCode} — {it.nameAr}
                </option>
              ))}
            </select>
          </label>
          <GlassInput
            label="رقم الفاتورة"
            value={invoiceNumber}
            disabled={readOnlyInspectMode}
            onChange={(e) => setInvoiceNumber(e.target.value)}
          />
          <label>
            حجم العبوة (وحدة/عبوة)
            <input
              aria-label="حجم العبوة"
              type="number"
              min={1}
              value={packSize}
              disabled={readOnlyInspectMode}
              onChange={(e) => setPackSize(Number(e.target.value) || 0)}
              style={{ width: '100%', padding: '0.45rem' }}
            />
          </label>
          <label>
            عدد العبوات
            <input
              aria-label="عدد العبوات"
              type="number"
              min={1}
              value={packCount}
              disabled={readOnlyInspectMode}
              onChange={(e) => setPackCount(Number(e.target.value) || 0)}
              style={{ width: '100%', padding: '0.45rem' }}
            />
          </label>
          <label>
            سعر العبوة
            <input
              aria-label="سعر العبوة"
              type="number"
              min={0}
              step="0.01"
              value={packPrice}
              disabled={readOnlyInspectMode}
              onChange={(e) => setPackPrice(Number(e.target.value) || 0)}
              style={{ width: '100%', padding: '0.45rem' }}
            />
          </label>
          <label>
            المدفوع للمورد
            <input
              aria-label="المدفوع للمورد"
              type="number"
              min={0}
              step="0.01"
              value={paidAmount}
              disabled={readOnlyInspectMode}
              onChange={(e) => setPaidAmount(Number(e.target.value) || 0)}
              style={{ width: '100%', padding: '0.45rem' }}
            />
          </label>
          <GlassInput
            label="رقم التشغيلة"
            value={batchNumber}
            disabled={readOnlyInspectMode}
            onChange={(e) => setBatchNumber(e.target.value)}
          />
          <GlassInput
            label="تاريخ الصلاحية"
            value={expiryDate}
            disabled={readOnlyInspectMode}
            onChange={(e) => setExpiryDate(e.target.value)}
          />
        </div>
        <div
          style={{
            marginBlockStart: '0.75rem',
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center',
            flexWrap: 'wrap',
            gap: '0.5rem',
          }}
        >
          <div data-testid="purchase-unit-cost-preview">
            إجمالي الوحدات: <strong>{computedTotalUnits}</strong> | إجمالي الفاتورة:{' '}
            <strong>{computedTotalPrice}</strong> | تكلفة الوحدة المحسوبة:{' '}
            <strong data-testid="computed-unit-cost">{computedUnitCost}</strong>
          </div>
          <GlassButton disabled={readOnlyInspectMode} onClick={handleReceivePurchase}>
            استلام فاتورة الشراء
          </GlassButton>
        </div>
      </GlassCard>

      {/* Batches & Stock Adjustment */}
      <GlassCard style={{ padding: '1.25rem' }}>
        <h3 style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBlockEnd: '0.75rem' }}>
          <Layers size={18} />
          التشغيلات والصلاحيات والتسوية اليدوية
        </h3>
        <div style={{ display: 'grid', gap: '0.4rem', marginBlockEnd: '0.75rem' }}>
          {batches.map((b) => (
            <div key={b.id} style={{ display: 'flex', justifyContent: 'space-between', fontSize: '0.9rem' }}>
              <span>
                تشغيلة {b.batchNumber} (انتهاء: {b.expiryDate ?? 'بدون'})
              </span>
              <span>
                المتبقي: {b.quantityRemaining} / {b.quantityReceived} (تكلفة {b.unitCostAmount})
              </span>
            </div>
          ))}
        </div>
        <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap', alignItems: 'flex-end' }}>
          <label>
            الكمية (+/-)
            <input
              aria-label="تعديل الكمية"
              type="number"
              value={adjustDelta}
              disabled={readOnlyInspectMode}
              onChange={(e) => setAdjustDelta(Number(e.target.value) || 0)}
              style={{ width: '110px', padding: '0.45rem', marginInlineStart: '0.35rem' }}
            />
          </label>
          <GlassInput
            label="سبب التسوية"
            value={adjustReason}
            disabled={readOnlyInspectMode}
            onChange={(e) => setAdjustReason(e.target.value)}
          />
          <GlassButton disabled={readOnlyInspectMode} onClick={handleAdjustStock}>
            تنفيذ التسوية اليدوية
          </GlassButton>
        </div>
      </GlassCard>

      {/* Alerts & Negative Stock Policy Settings */}
      <GlassCard style={{ padding: '1.25rem' }}>
        <h3 style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBlockEnd: '0.75rem' }}>
          <AlertTriangle size={18} />
          تنبيهات النقص والصلاحية وسياسة الرصيد السالب
        </h3>
        <div style={{ display: 'grid', gap: '0.5rem', marginBlockEnd: '0.75rem' }}>
          {alerts.map((al) => (
            <div
              key={al.id}
              data-testid={`inventory-alert-${al.id}`}
              style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}
            >
              <span>
                [{al.kind}] {al.itemNameAr}: {al.messageAr}{' '}
                {al.isResolved ? '(تمت المعالجة)' : '(نشط)'}
              </span>
              {!al.isResolved && (
                <GlassButton disabled={readOnlyInspectMode} onClick={() => handleResolveAlert(al.id)}>
                  تأكيد المعالجة
                </GlassButton>
              )}
            </div>
          ))}
        </div>
        <div style={{ display: 'flex', gap: '0.75rem', alignItems: 'center', flexWrap: 'wrap' }}>
          <Settings2 size={16} />
          <label>
            سياسة الرصيد السالب
            <select
              aria-label="سياسة الرصيد السالب"
              value={negativeStockPolicy}
              disabled={readOnlyInspectMode}
              onChange={(e) => setNegativeStockPolicy(e.target.value)}
              style={{ marginInlineStart: '0.4rem', padding: '0.4rem' }}
            >
              <option value="Block">منع السحب السالب (Block)</option>
              <option value="AllowWithWarning">السماح مع التنبيه (AllowWithWarning)</option>
            </select>
          </label>
          <label>
            أيام تنبيه الصلاحية
            <input
              aria-label="أيام تنبيه الصلاحية"
              type="number"
              min={1}
              value={nearExpiryDays}
              disabled={readOnlyInspectMode}
              onChange={(e) => setNearExpiryDays(Number(e.target.value) || 30)}
              style={{ width: '90px', marginInlineStart: '0.4rem', padding: '0.4rem' }}
            />
          </label>
          <GlassButton disabled={readOnlyInspectMode} onClick={handleSaveSettings}>
            حفظ إعدادات المخزون
          </GlassButton>
        </div>
      </GlassCard>

      {/* Movements & Supplier Statement */}
      <GlassCard style={{ padding: '1.25rem' }}>
        <h3 style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBlockEnd: '0.75rem' }}>
          <History size={18} />
          حركة المخزون وكشف حساب المورد ({statement.supplierName})
        </h3>
        <div data-testid="supplier-statement-summary" style={{ marginBlockEnd: '0.75rem' }}>
          المشتريات: <strong>{statement.totalPurchasedAmount} {statement.currency}</strong> | المدفوع:{' '}
          <strong>{statement.totalPaidAmount} {statement.currency}</strong> | المتبقي:{' '}
          <strong>{statement.balanceDueAmount} {statement.currency}</strong>
        </div>
        <div style={{ display: 'grid', gap: '0.35rem', fontSize: '0.88rem' }}>
          {movements.map((mv) => (
            <div key={mv.id} style={{ display: 'flex', justifyContent: 'space-between' }}>
              <span>
                {mv.movementType} — {mv.reason ?? ''}
              </span>
              <span>
                التغير: {mv.quantityDelta > 0 ? `+${mv.quantityDelta}` : mv.quantityDelta} (بعد:{' '}
                {mv.quantityAfterItem})
              </span>
            </div>
          ))}
        </div>
      </GlassCard>
    </div>
  );
};
