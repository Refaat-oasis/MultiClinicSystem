import React, { useState } from 'react';
import { Receipt, CreditCard, Banknote, Printer, Check } from 'lucide-react';
import { StatusChip } from '../components/StatusChip';

interface ChargeItem {
  id: string;
  name: string;
  category: string;
  price: number;
}

export const BillingScreen: React.FC = () => {
  const [patientName] = useState('سارة عبد الله أحمد (ملف: P-2026-0042)');
  const [receiptNumber] = useState('REC-2026-0814'); // Allocated from NumberBlock
  const [items] = useState<ChargeItem[]>([
    { id: 'item-1', name: 'كشف واستشارة طبية تخصصية', category: 'كشوفات', price: 250 },
    { id: 'item-2', name: 'تنظيف قنوات العصب (جلسة أولى)', category: 'علاج عصب', price: 400 },
    { id: 'item-3', name: 'أشعة سينية موضعية (Periapical X-Ray)', category: 'أشعة', price: 100 },
  ]);
  const [discount, setDiscount] = useState(50);
  const [cashAmount, setCashAmount] = useState(400);
  const [cardAmount, setCardAmount] = useState(300);
  const [isPaid, setIsPaid] = useState(false);

  const subTotal = items.reduce((sum, item) => sum + item.price, 0);
  const netTotal = Math.max(0, subTotal - discount);
  const totalPaid = cashAmount + cardAmount;
  const remaining = netTotal - totalPaid;

  const handleIssueReceipt = (e: React.FormEvent) => {
    e.preventDefault();
    if (remaining !== 0) {
      alert(`تنبيه: المبلغ المسدد (${totalPaid} ج.م) لا يساوي الصافي المطلوب (${netTotal} ج.م)!`);
      return;
    }
    setIsPaid(true);
  };

  return (
    <div style={{ display: 'grid', gap: '1.5rem' }}>
      <div>
        <h2 style={{ fontSize: '1.25rem', fontWeight: 700, color: 'var(--clinic-text-primary)' }}>
          الفوترة وإصدار سندات القبض (Billing & Receipts)
        </h2>
        <p style={{ fontSize: '0.875rem', color: 'var(--clinic-text-secondary)' }}>
          احتساب الرسوم الطبية، المدفوعات المجزأة، وإصدار إيصالات بأرقام معتمدة (Number Blocks)
        </p>
      </div>

      {isPaid && (
        <div
          style={{
            padding: '1rem 1.25rem',
            backgroundColor: 'var(--clinic-status-success-bg)',
            color: 'var(--clinic-status-success)',
            borderRadius: 'var(--clinic-radius-input)',
            fontWeight: 600,
            fontSize: '0.875rem',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
            <Check size={20} />
            <span>تم إصدار سند القبض رقم <strong>{receiptNumber}</strong> وقيد المبلغ في دفتر اليومية!</span>
          </div>
          <button
            onClick={() => window.print()}
            className="clinic-btn clinic-btn-secondary"
            style={{ minHeight: '32px', padding: '0.25rem 0.625rem', fontSize: '0.75rem' }}
          >
            <Printer size={14} />
            <span>طباعة الإيصال</span>
          </button>
        </div>
      )}

      {/* Invoice Card */}
      <div className="clinic-card">
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1.25rem' }}>
          <div>
            <span style={{ fontSize: '0.75rem', color: 'var(--clinic-text-muted)' }}>المريض المسجل:</span>
            <div style={{ fontSize: '1rem', fontWeight: 700 }}>{patientName}</div>
          </div>
          <div style={{ textAlign: 'end' }}>
            <span style={{ fontSize: '0.75rem', color: 'var(--clinic-text-muted)' }}>رقم السند المخصص:</span>
            <StatusChip status="info" label={receiptNumber} showDot={false} />
          </div>
        </div>

        {/* Line Items Table */}
        <table className="clinic-table" style={{ marginBottom: '1.25rem' }}>
          <thead>
            <tr>
              <th>البند / الإجراء الطبي</th>
              <th>التصنيف</th>
              <th style={{ textAlign: 'left' }}>القيمة</th>
            </tr>
          </thead>
          <tbody>
            {items.map((it) => (
              <tr key={it.id}>
                <td style={{ fontWeight: 600 }}>{it.name}</td>
                <td style={{ color: 'var(--clinic-text-secondary)' }}>{it.category}</td>
                <td className="num-tabular" style={{ textAlign: 'left', fontWeight: 600 }}>
                  {it.price} ج.م
                </td>
              </tr>
            ))}
          </tbody>
        </table>

        {/* Calculation Totals */}
        <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
          <div style={{ width: '280px', display: 'grid', gap: '0.5rem', fontSize: '0.875rem' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between' }}>
              <span style={{ color: 'var(--clinic-text-secondary)' }}>إجمالي الخدمات:</span>
              <span className="num-tabular" style={{ fontWeight: 600 }}>{subTotal} ج.م</span>
            </div>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <span style={{ color: 'var(--clinic-text-secondary)' }}>الخصم المطبق:</span>
              <input
                type="number"
                value={discount}
                onChange={(e) => setDiscount(Number(e.target.value) || 0)}
                className="clinic-input num-tabular"
                style={{ width: '80px', minHeight: '32px', padding: '0.25rem 0.5rem', textAlign: 'center' }}
              />
            </div>
            <div
              style={{
                display: 'flex',
                justifyContent: 'space-between',
                paddingTop: '0.5rem',
                borderTop: '2px solid var(--clinic-border-strong)',
                fontSize: '1.125rem',
                fontWeight: 700,
                color: 'var(--clinic-accent-primary)',
              }}
            >
              <span>الصافي المطلوب:</span>
              <span className="num-tabular">{netTotal} ج.م</span>
            </div>
          </div>
        </div>
      </div>

      {/* Split Payment Card */}
      <form onSubmit={handleIssueReceipt} className="clinic-card">
        <h3 style={{ fontSize: '1rem', fontWeight: 700, marginBottom: '1rem' }}>
          طريقة السداد وتجزئة الدفعات (Split Payment)
        </h3>

        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '1rem', marginBottom: '1.25rem' }}>
          <div>
            <label style={{ display: 'flex', alignItems: 'center', gap: '0.375rem', fontSize: '0.8125rem', fontWeight: 600, marginBottom: '0.375rem' }}>
              <Banknote size={16} color="var(--clinic-status-success)" />
              <span>المدفوع نقداً (Cash)</span>
            </label>
            <input
              type="number"
              value={cashAmount}
              onChange={(e) => setCashAmount(Number(e.target.value) || 0)}
              className="clinic-input num-tabular"
              min={0}
              required
            />
          </div>

          <div>
            <label style={{ display: 'flex', alignItems: 'center', gap: '0.375rem', fontSize: '0.8125rem', fontWeight: 600, marginBottom: '0.375rem' }}>
              <CreditCard size={16} color="var(--clinic-status-info)" />
              <span>المدفوع بالبطاقة / شبكة (Card / POS)</span>
            </label>
            <input
              type="number"
              value={cardAmount}
              onChange={(e) => setCardAmount(Number(e.target.value) || 0)}
              className="clinic-input num-tabular"
              min={0}
              required
            />
          </div>
        </div>

        <div
          style={{
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center',
            padding: '0.75rem 1rem',
            backgroundColor: remaining === 0 ? 'var(--clinic-status-success-bg)' : '#fffbeb',
            borderRadius: 'var(--clinic-radius-input)',
            marginBottom: '1.25rem',
          }}
        >
          <span style={{ fontSize: '0.875rem', fontWeight: 600 }}>
            إجمالي المسدد: <span className="num-tabular">{totalPaid} ج.م</span>
          </span>
          <span
            className="num-tabular"
            style={{
              fontSize: '0.875rem',
              fontWeight: 700,
              color: remaining === 0 ? 'var(--clinic-status-success)' : 'var(--clinic-status-warning)',
            }}
          >
            {remaining === 0 ? 'المبلغ مطابق تماماً' : `المتبقي: ${remaining} ج.م`}
          </span>
        </div>

        <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
          <button
            type="submit"
            disabled={remaining !== 0 || isPaid}
            className="clinic-btn clinic-btn-primary"
            style={{ minWidth: '200px' }}
          >
            <Receipt size={18} />
            <span>إصدار سند القبض وترحيل السجل</span>
          </button>
        </div>
      </form>
    </div>
  );
};
