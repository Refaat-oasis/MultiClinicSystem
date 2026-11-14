import React, { useState } from 'react';
import { Lock, ShieldCheck, DollarSign, CreditCard, Banknote, AlertTriangle, Check } from 'lucide-react';
import { StatCard } from '../components/StatCard';
import { StatusChip } from '../components/StatusChip';

export const DailyClosingScreen: React.FC = () => {
  const [expectedCash] = useState(5200);
  const [expectedCard] = useState(3250);
  const [actualCash, setActualCash] = useState(5200);
  const [actualCard, setActualCard] = useState(3250);
  const [closingNotes, setClosingNotes] = useState('تمت مراجعة الخزينة وتطابق الصندوق مع إيصالات اليومية بالكامل.');
  const [isClosed, setIsClosed] = useState(false);

  const cashDiff = actualCash - expectedCash;
  const cardDiff = actualCard - expectedCard;
  const totalDiff = cashDiff + cardDiff;

  const handleCloseDay = (e: React.FormEvent) => {
    e.preventDefault();
    if (totalDiff !== 0) {
      if (!window.confirm(`يوجد فرق في الصندوق مقداره (${totalDiff} ج.م). هل أنت متأكد من ترحيل الإغلاق مع توثيق الفارق في سجل التدقيق؟`)) {
        return;
      }
    }
    setIsClosed(true);
  };

  return (
    <div style={{ display: 'grid', gap: '1.5rem' }}>
      <div>
        <h2 style={{ fontSize: '1.25rem', fontWeight: 700, color: 'var(--clinic-text-primary)' }}>
          شاشة الإغلاق المالي اليومي وتسوية الصندوق (Daily Financial Closing)
        </h2>
        <p style={{ fontSize: '0.875rem', color: 'var(--clinic-text-secondary)' }}>
          مطابقة النقدية الفعلية مع السجل المحاسبي، قفل الوردية، وتوثيق الفروقات في سجل التدقيق
        </p>
      </div>

      {isClosed && (
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
            gap: '0.5rem',
          }}
        >
          <ShieldCheck size={20} />
          <span>تم إغلاق وقفل اليوم المالي بنجاح! لا يمكن تعديل أو حذف أي قيد مالي لهذا اليوم دون إذن استثنائي موثق.</span>
        </div>
      )}

      {/* Summary Cards */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '1rem' }}>
        <StatCard
          title="المتوقع بالخزينة (نقدي)"
          value={`${expectedCash} ج.م`}
          icon={<Banknote size={20} />}
          subtitle="حسب إيصالات القبض المسجلة"
        />
        <StatCard
          title="المتوقع بالشبكة (بطاقات)"
          value={`${expectedCard} ج.م`}
          icon={<CreditCard size={20} />}
          subtitle="سندات الدفع الإلكتروني"
        />
        <StatCard
          title="إجمالي الإيرادات"
          value={`${expectedCash + expectedCard} ج.م`}
          icon={<DollarSign size={20} />}
          subtitle="صافي حركة اليوم"
        />
        <div className="clinic-card">
          <span style={{ fontSize: '0.875rem', fontWeight: 600, color: 'var(--clinic-text-secondary)' }}>
            حالة الإغلاق اليومي
          </span>
          <div style={{ marginTop: '0.5rem' }}>
            {isClosed ? (
              <StatusChip status="success" label="مقفل ومحمي (Locked)" />
            ) : (
              <StatusChip status="warning" label="قيد المراجعة (Open)" />
            )}
          </div>
        </div>
      </div>

      {/* Reconciliation Form */}
      <form onSubmit={handleCloseDay} className="clinic-card">
        <h3 style={{ fontSize: '1rem', fontWeight: 700, marginBottom: '1.25rem' }}>
          تسوية مبالغ الصندوق الفعلي مقابل المحاسبي
        </h3>

        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(260px, 1fr))', gap: '1.25rem', marginBottom: '1.5rem' }}>
          <div>
            <label style={{ display: 'block', fontSize: '0.8125rem', fontWeight: 600, marginBottom: '0.375rem' }}>
              النقدية الفعلية بالدرج (Actual Cash Count)
            </label>
            <input
              type="number"
              value={actualCash}
              onChange={(e) => setActualCash(Number(e.target.value) || 0)}
              disabled={isClosed}
              className="clinic-input num-tabular"
              required
            />
            <div style={{ fontSize: '0.75rem', color: cashDiff === 0 ? 'var(--clinic-status-success)' : 'var(--clinic-status-danger)', marginTop: '0.25rem' }}>
              الفارق النقدي: <span className="num-tabular">{cashDiff >= 0 ? `+${cashDiff}` : cashDiff} ج.م</span>
            </div>
          </div>

          <div>
            <label style={{ display: 'block', fontSize: '0.8125rem', fontWeight: 600, marginBottom: '0.375rem' }}>
              إجمالي مبيعات الشبكة الفعلية من الماكينة (POS Batch)
            </label>
            <input
              type="number"
              value={actualCard}
              onChange={(e) => setActualCard(Number(e.target.value) || 0)}
              disabled={isClosed}
              className="clinic-input num-tabular"
              required
            />
            <div style={{ fontSize: '0.75rem', color: cardDiff === 0 ? 'var(--clinic-status-success)' : 'var(--clinic-status-danger)', marginTop: '0.25rem' }}>
              فارق الشبكة: <span className="num-tabular">{cardDiff >= 0 ? `+${cardDiff}` : cardDiff} ج.م</span>
            </div>
          </div>
        </div>

        <div style={{ marginBottom: '1.5rem' }}>
          <label style={{ display: 'block', fontSize: '0.8125rem', fontWeight: 600, marginBottom: '0.375rem' }}>
            ملاحظات وتبريرات الإغلاق المالي
          </label>
          <textarea
            value={closingNotes}
            onChange={(e) => setClosingNotes(e.target.value)}
            disabled={isClosed}
            className="clinic-input"
            rows={3}
            style={{ minHeight: '75px', resize: 'vertical' }}
          />
        </div>

        {totalDiff !== 0 && (
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', padding: '0.75rem 1rem', borderRadius: 'var(--clinic-radius-input)', backgroundColor: '#fffbeb', border: '1px solid #d97706', marginBottom: '1.25rem', fontSize: '0.8125rem', color: '#92400e' }}>
            <AlertTriangle size={18} />
            <span>تنبيه: يوجد فرق إجمالي قدره {totalDiff} ج.م، سيتم تسجيل هذا الفارق تلقائياً في تقرير تسوية اليومية.</span>
          </div>
        )}

        <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
          <button
            type="submit"
            disabled={isClosed}
            className="clinic-btn clinic-btn-primary"
            style={{ minWidth: '220px' }}
          >
            {isClosed ? <Check size={18} /> : <Lock size={18} />}
            <span>{isClosed ? 'اليوم مقفل بالفعل' : 'ترحيل وإقفال الصندوق اليومي'}</span>
          </button>
        </div>
      </form>
    </div>
  );
};
