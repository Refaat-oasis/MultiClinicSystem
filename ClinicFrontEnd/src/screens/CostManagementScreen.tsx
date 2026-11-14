import React, { useState, useEffect } from 'react';
import { Plus, Check, X } from 'lucide-react';
import { StatusChip } from '../components/StatusChip';
import { clinicApi, CostEntry } from '../services/api/clinicApiClient';

export const CostManagementScreen: React.FC = () => {
  const [costs, setCosts] = useState<CostEntry[]>([]);
  const [filterType, setFilterType] = useState<'all' | 'fixed' | 'variable'>('all');
  const [showAddModal, setShowAddModal] = useState(false);
  const [feedbackMessage, setFeedbackMessage] = useState<string | null>(null);

  // New Cost Form State
  const [name, setName] = useState('');
  const [type, setType] = useState<'fixed' | 'variable'>('variable');
  const [category, setCategory] = useState('مستلزمات طبية');
  const [amount, setAmount] = useState('');
  const [dueDate, setDueDate] = useState('2026-06-30');
  const [recurrence, setRecurrence] = useState('شهري');

  useEffect(() => {
    clinicApi.getCosts().then(setCosts);
  }, []);

  const totalFixed = costs.filter((c) => c.type === 'fixed').reduce((acc, c) => acc + c.amount, 0);
  const totalVariable = costs.filter((c) => c.type === 'variable').reduce((acc, c) => acc + c.amount, 0);
  const totalCosts = totalFixed + totalVariable;

  const filteredCosts = costs.filter((c) => {
    if (filterType === 'all') return true;
    return c.type === filterType;
  });

  const handleAddSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const parsedAmount = parseFloat(amount);
    if (!name || isNaN(parsedAmount) || parsedAmount <= 0) {
      alert('يرجى إدخال اسم البند والمبلغ بشكل صحيح');
      return;
    }

    const newCost = await clinicApi.addCost({
      name,
      type,
      category,
      amount: parsedAmount,
      dueDate,
      recurrence,
      status: 'pending',
    });

    setCosts((prev) => [newCost, ...prev]);
    setShowAddModal(false);
    setName('');
    setAmount('');
    setFeedbackMessage(`تمت إضافة بند التكلفة "${newCost.name}" بقيمة ${parsedAmount.toLocaleString()} ر.س بنجاح!`);
    setTimeout(() => setFeedbackMessage(null), 4000);
  };

  return (
    <div style={{ display: 'grid', gap: '1.5rem' }}>
      {/* Header Bar */}
      <div
        className="clinic-card"
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          flexWrap: 'wrap',
          gap: '1rem',
          padding: '1.25rem 1.5rem',
        }}
      >
        <div>
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
            <h2 style={{ fontSize: '1.25rem', fontWeight: 700, color: 'var(--clinic-text-primary)' }}>
              إدارة التكاليف الثابتة والمتغيرة (Cost Management)
            </h2>
            <span
              style={{
                fontSize: '0.6875rem',
                backgroundColor: 'var(--clinic-accent-soft)',
                color: 'var(--clinic-accent-primary)',
                padding: '0.125rem 0.5rem',
                borderRadius: 'var(--clinic-radius-pill)',
                fontWeight: 700,
              }}
            >
              وحدة نمطية
            </span>
          </div>
          <p style={{ fontSize: '0.8125rem', color: 'var(--clinic-text-secondary)', marginTop: '0.25rem' }}>
            تسجيل ومتابعة التكاليف التشغيلية، الإيجارات، رواتب الكادر، وفواتير معامل ومستلزمات الأسنان
          </p>
        </div>

        <button
          type="button"
          onClick={() => setShowAddModal(true)}
          className="clinic-btn clinic-btn-primary"
          style={{ display: 'flex', alignItems: 'center', gap: '0.375rem', fontSize: '0.875rem' }}
        >
          <Plus size={18} />
          <span>إضافة بند تكلفة جديد</span>
        </button>
      </div>

      {feedbackMessage && (
        <div
          style={{
            padding: '0.875rem 1.25rem',
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
          <Check size={18} />
          <span>{feedbackMessage}</span>
        </div>
      )}

      {/* Summary Cards */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '1rem' }}>
        <div className="clinic-card" style={{ padding: '1.25rem' }}>
          <span style={{ fontSize: '0.8125rem', color: 'var(--clinic-text-secondary)', fontWeight: 600 }}>
            إجمالي التكاليف الثابتة (Fixed)
          </span>
          <div style={{ fontSize: '1.5rem', fontWeight: 800, color: 'var(--clinic-accent-primary)', marginTop: '0.375rem' }}>
            {totalFixed.toLocaleString('ar-SA')} <span style={{ fontSize: '0.875rem', fontWeight: 500 }}>ر.س</span>
          </div>
          <span style={{ fontSize: '0.75rem', color: 'var(--clinic-text-muted)' }}>إيجار، رواتب، واشتراكات دورية</span>
        </div>

        <div className="clinic-card" style={{ padding: '1.25rem' }}>
          <span style={{ fontSize: '0.8125rem', color: 'var(--clinic-text-secondary)', fontWeight: 600 }}>
            إجمالي التكاليف المتغيرة (Variable)
          </span>
          <div style={{ fontSize: '1.5rem', fontWeight: 800, color: '#f59e0b', marginTop: '0.375rem' }}>
            {totalVariable.toLocaleString('ar-SA')} <span style={{ fontSize: '0.875rem', fontWeight: 500 }}>ر.س</span>
          </div>
          <span style={{ fontSize: '0.75rem', color: 'var(--clinic-text-muted)' }}>معامل أسنان، مستلزمات ومستهلكات</span>
        </div>

        <div className="clinic-card" style={{ padding: '1.25rem' }}>
          <span style={{ fontSize: '0.8125rem', color: 'var(--clinic-text-secondary)', fontWeight: 600 }}>
            إجمالي المصروفات الشهرية
          </span>
          <div style={{ fontSize: '1.5rem', fontWeight: 800, color: '#ef4444', marginTop: '0.375rem' }}>
            {totalCosts.toLocaleString('ar-SA')} <span style={{ fontSize: '0.875rem', fontWeight: 500 }}>ر.س</span>
          </div>
          <span style={{ fontSize: '0.75rem', color: 'var(--clinic-text-muted)' }}>معدل الصرف التشغيلي (Burn Rate)</span>
        </div>
      </div>

      {/* Costs Data Table with Filter Tabs */}
      <div className="clinic-card" style={{ padding: '1.5rem', display: 'grid', gap: '1rem' }}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: '0.75rem' }}>
          <div style={{ display: 'flex', backgroundColor: 'var(--clinic-bg-subtle)', padding: '0.25rem', borderRadius: 'var(--clinic-radius-input)' }}>
            <button
              type="button"
              onClick={() => setFilterType('all')}
              style={{
                padding: '0.375rem 0.75rem',
                borderRadius: '6px',
                border: 'none',
                background: filterType === 'all' ? 'var(--clinic-bg-surface)' : 'transparent',
                fontWeight: filterType === 'all' ? 700 : 500,
                color: filterType === 'all' ? 'var(--clinic-accent-primary)' : 'var(--clinic-text-secondary)',
                cursor: 'pointer',
                fontSize: '0.75rem',
                boxShadow: filterType === 'all' ? 'var(--clinic-shadow-sm)' : 'none',
              }}
            >
              جميع التكاليف ({costs.length})
            </button>
            <button
              type="button"
              onClick={() => setFilterType('fixed')}
              style={{
                padding: '0.375rem 0.75rem',
                borderRadius: '6px',
                border: 'none',
                background: filterType === 'fixed' ? 'var(--clinic-bg-surface)' : 'transparent',
                fontWeight: filterType === 'fixed' ? 700 : 500,
                color: filterType === 'fixed' ? 'var(--clinic-accent-primary)' : 'var(--clinic-text-secondary)',
                cursor: 'pointer',
                fontSize: '0.75rem',
                boxShadow: filterType === 'fixed' ? 'var(--clinic-shadow-sm)' : 'none',
              }}
            >
              التكاليف الثابتة ({costs.filter((c) => c.type === 'fixed').length})
            </button>
            <button
              type="button"
              onClick={() => setFilterType('variable')}
              style={{
                padding: '0.375rem 0.75rem',
                borderRadius: '6px',
                border: 'none',
                background: filterType === 'variable' ? 'var(--clinic-bg-surface)' : 'transparent',
                fontWeight: filterType === 'variable' ? 700 : 500,
                color: filterType === 'variable' ? 'var(--clinic-accent-primary)' : 'var(--clinic-text-secondary)',
                cursor: 'pointer',
                fontSize: '0.75rem',
                boxShadow: filterType === 'variable' ? 'var(--clinic-shadow-sm)' : 'none',
              }}
            >
              التكاليف المتغيرة ({costs.filter((c) => c.type === 'variable').length})
            </button>
          </div>

          <span style={{ fontSize: '0.75rem', color: 'var(--clinic-text-muted)' }}>
            مجموع المعروض: <strong style={{ color: 'var(--clinic-text-primary)' }}>{filteredCosts.reduce((s, c) => s + c.amount, 0).toLocaleString()} ر.س</strong>
          </span>
        </div>

        <div style={{ overflowX: 'auto' }}>
          <table className="clinic-table">
            <thead>
              <tr>
                <th>بند التكلفة والمصروف</th>
                <th>النوع</th>
                <th>التصنيف</th>
                <th>المبلغ</th>
                <th>تاريخ الاستحقاق</th>
                <th>دورية التكرار</th>
                <th>الحالة</th>
              </tr>
            </thead>
            <tbody>
              {filteredCosts.map((c) => (
                <tr key={c.id}>
                  <td>
                    <div style={{ fontWeight: 600 }}>{c.name}</div>
                  </td>
                  <td>
                    <span
                      style={{
                        fontSize: '0.6875rem',
                        fontWeight: 700,
                        padding: '0.2rem 0.5rem',
                        borderRadius: 'var(--clinic-radius-pill)',
                        backgroundColor: c.type === 'fixed' ? 'var(--clinic-accent-soft)' : '#fef3c7',
                        color: c.type === 'fixed' ? 'var(--clinic-accent-primary)' : '#b45309',
                      }}
                    >
                      {c.type === 'fixed' ? 'تكلفة ثابتة' : 'تكلفة متغيرة'}
                    </span>
                  </td>
                  <td>
                    <span style={{ fontSize: '0.8125rem', color: 'var(--clinic-text-secondary)' }}>{c.category}</span>
                  </td>
                  <td>
                    <strong style={{ fontSize: '0.875rem' }} className="num-tabular">
                      {c.amount.toLocaleString()} ر.س
                    </strong>
                  </td>
                  <td>
                    <span style={{ fontSize: '0.8125rem' }} className="num-tabular">
                      {c.dueDate}
                    </span>
                  </td>
                  <td>
                    <span style={{ fontSize: '0.75rem', color: 'var(--clinic-text-secondary)' }}>{c.recurrence}</span>
                  </td>
                  <td>
                    <StatusChip
                      status={c.status === 'paid' ? 'success' : 'warning'}
                      label={c.status === 'paid' ? 'مسدد' : 'بانتظار السداد'}
                    />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {/* Add New Cost Modal */}
      {showAddModal && (
        <div
          style={{
            position: 'fixed',
            inset: 0,
            backgroundColor: 'rgba(0,0,0,0.5)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            zIndex: 100,
            padding: '1rem',
          }}
        >
          <div className="clinic-card" style={{ width: '100%', maxWidth: '520px', padding: '1.75rem' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1.25rem' }}>
              <h3 style={{ fontSize: '1.125rem', fontWeight: 700 }}>تسجيل بند تكلفة تشغيلية جديد</h3>
              <button
                type="button"
                onClick={() => setShowAddModal(false)}
                style={{ background: 'none', border: 'none', cursor: 'pointer', color: 'var(--clinic-text-muted)' }}
              >
                <X size={20} />
              </button>
            </div>

            <form onSubmit={handleAddSubmit} style={{ display: 'grid', gap: '1rem' }}>
              <div>
                <label style={{ display: 'block', fontSize: '0.8125rem', fontWeight: 600, marginBottom: '0.375rem' }}>
                  اسم البند أو التكلفة
                </label>
                <input
                  type="text"
                  value={name}
                  onChange={(e) => setName(e.target.value)}
                  className="clinic-input"
                  placeholder="مثال: فاتورة معمل الأسنان لشهر يونيو أو إيجار العيادة"
                  required
                />
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.75rem' }}>
                <div>
                  <label style={{ display: 'block', fontSize: '0.8125rem', fontWeight: 600, marginBottom: '0.375rem' }}>
                    نوع التكلفة
                  </label>
                  <select
                    value={type}
                    onChange={(e) => setType(e.target.value as 'fixed' | 'variable')}
                    className="clinic-input"
                  >
                    <option value="fixed">تكلفة ثابتة (Fixed)</option>
                    <option value="variable">تكلفة متغيرة (Variable)</option>
                  </select>
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '0.8125rem', fontWeight: 600, marginBottom: '0.375rem' }}>
                    التصنيف
                  </label>
                  <select
                    value={category}
                    onChange={(e) => setCategory(e.target.value)}
                    className="clinic-input"
                  >
                    <option value="إيجار ومرافق">إيجار ومرافق</option>
                    <option value="رواتب">رواتب وكادر طبي</option>
                    <option value="معامل الأسنان">معامل الأسنان (Dental Lab)</option>
                    <option value="مستلزمات طبية">مستلزمات ومستهلكات طبية</option>
                    <option value="برمجيات وتقنية">برمجيات وسحابة</option>
                    <option value="صيانة دورية">صيانة دورية</option>
                    <option value="صيانة طارئة">صيانة طارئة</option>
                  </select>
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.75rem' }}>
                <div>
                  <label style={{ display: 'block', fontSize: '0.8125rem', fontWeight: 600, marginBottom: '0.375rem' }}>
                    المبلغ (ر.س)
                  </label>
                  <input
                    type="number"
                    step="0.01"
                    value={amount}
                    onChange={(e) => setAmount(e.target.value)}
                    className="clinic-input"
                    placeholder="مثال: 4500"
                    required
                  />
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '0.8125rem', fontWeight: 600, marginBottom: '0.375rem' }}>
                    تاريخ الاستحقاق
                  </label>
                  <input
                    type="date"
                    value={dueDate}
                    onChange={(e) => setDueDate(e.target.value)}
                    className="clinic-input"
                  />
                </div>
              </div>

              <div>
                <label style={{ display: 'block', fontSize: '0.8125rem', fontWeight: 600, marginBottom: '0.375rem' }}>
                  دورية التكرار
                </label>
                <select
                  value={recurrence}
                  onChange={(e) => setRecurrence(e.target.value)}
                  className="clinic-input"
                >
                  <option value="شهري">شهري منتظم</option>
                  <option value="ربع سنوي">ربع سنوي</option>
                  <option value="سنوي">سنوي</option>
                  <option value="حسب الطلب">حسب الطلب / عند التوريد</option>
                  <option value="مرة واحدة">دفعة لمرة واحدة</option>
                </select>
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.75rem', marginTop: '0.5rem' }}>
                <button
                  type="button"
                  onClick={() => setShowAddModal(false)}
                  className="clinic-btn"
                  style={{ backgroundColor: 'var(--clinic-bg-subtle)' }}
                >
                  إلغاء
                </button>
                <button type="submit" className="clinic-btn clinic-btn-primary">
                  حفظ بند التكلفة
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};
