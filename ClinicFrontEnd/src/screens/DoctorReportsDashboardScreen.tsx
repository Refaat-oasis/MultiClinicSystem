import React, { useState, useEffect } from 'react';
import { Users, TrendingUp, DollarSign, PieChart, Calendar, ArrowUpRight, BarChart3, Plus } from 'lucide-react';
import { StatCard } from '../components/StatCard';
import { clinicApi, DoctorReportMetrics } from '../services/api/clinicApiClient';

export interface DoctorReportsDashboardScreenProps {
  onOpenCostManagement?: () => void;
}

export const DoctorReportsDashboardScreen: React.FC<DoctorReportsDashboardScreenProps> = ({ onOpenCostManagement }) => {
  const [metrics, setMetrics] = useState<DoctorReportMetrics | null>(null);
  const [timeFilter, setTimeFilter] = useState<'month' | 'quarter' | 'year'>('month');
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    clinicApi.getDoctorReportMetrics().then((data) => {
      setMetrics(data);
      setLoading(false);
    });
  }, []);

  if (loading || !metrics) {
    return (
      <div style={{ padding: '3rem', textAlign: 'center', color: 'var(--clinic-text-muted)' }}>
        جاري تحميل تقارير ومؤشرات الطبيب...
      </div>
    );
  }

  const maxIncome = Math.max(...metrics.monthlyIncomeExpenses.map((m) => m.income));

  return (
    <div style={{ display: 'grid', gap: '1.5rem' }}>
      {/* Dashboard Top Header */}
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
              لوحة تحليلات وتقارير أداء الطبيب (Doctor Analytics)
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
              وحدة مفعلة
            </span>
          </div>
          <p style={{ fontSize: '0.8125rem', color: 'var(--clinic-text-secondary)', marginTop: '0.25rem' }}>
            رصد الجلسات السريرية، أعداد المرضى، والإيرادات مقابل المصروفات والتكاليف
          </p>
        </div>

        {/* Time Filters & Actions */}
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem', flexWrap: 'wrap' }}>
          <div style={{ display: 'flex', backgroundColor: 'var(--clinic-bg-subtle)', padding: '0.25rem', borderRadius: 'var(--clinic-radius-input)' }}>
            {(['month', 'quarter', 'year'] as const).map((period) => (
              <button
                key={period}
                type="button"
                onClick={() => setTimeFilter(period)}
                style={{
                  padding: '0.375rem 0.75rem',
                  borderRadius: '6px',
                  border: 'none',
                  background: timeFilter === period ? 'var(--clinic-bg-surface)' : 'transparent',
                  fontWeight: timeFilter === period ? 700 : 500,
                  color: timeFilter === period ? 'var(--clinic-accent-primary)' : 'var(--clinic-text-secondary)',
                  cursor: 'pointer',
                  fontSize: '0.75rem',
                  boxShadow: timeFilter === period ? 'var(--clinic-shadow-sm)' : 'none',
                }}
              >
                {period === 'month' ? 'هذا الشهر' : period === 'quarter' ? 'الربع الحالي' : 'هذا العام'}
              </button>
            ))}
          </div>

          {onOpenCostManagement && (
            <button
              type="button"
              onClick={onOpenCostManagement}
              className="clinic-btn clinic-btn-primary"
              style={{ display: 'flex', alignItems: 'center', gap: '0.375rem', fontSize: '0.8125rem' }}
            >
              <Plus size={16} />
              <span>إدارة التكاليف الثابتة والمتغيرة</span>
            </button>
          )}
        </div>
      </div>

      {/* KPI Stat Cards Grid */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '1rem' }}>
        <StatCard
          title="المرضى المعاينون"
          value={metrics.totalPatientsTreated.toLocaleString('ar-SA')}
          subtitle="إجمالي المرضى الفريدين الذين تمت معاينتهم"
          trend={{ direction: 'up', value: `+${metrics.weeklyGrowthRate}% هذا الأسبوع` }}
          icon={<Users size={20} />}
        />
        <StatCard
          title="الجلسات المنفذة"
          value={metrics.totalSessionsCompleted.toLocaleString('ar-SA')}
          subtitle="إجمالي جلسات الكشف والعلاج الطبي"
          trend={{ direction: 'up', value: '+8% معدل الإنجاز' }}
          icon={<TrendingUp size={20} />}
        />
        <StatCard
          title="إجمالي الإيرادات"
          value={`${metrics.totalRevenueCollected.toLocaleString('ar-SA')} ر.س`}
          subtitle="عائدات الكشوفات والإجراءات الجراحية"
          trend={{ direction: 'up', value: '+18% نمو شهري' }}
          icon={<DollarSign size={20} />}
        />
        <StatCard
          title="صافي الربح التشغيلي"
          value={`${metrics.netMargin.toLocaleString('ar-SA')} ر.س`}
          subtitle="بعد استقطاع التكاليف الثابتة والمتغيرة"
          trend={{ direction: 'up', value: 'هامش ربح 63.8%' }}
          icon={<PieChart size={20} />}
        />
      </div>

      {/* Main Charts Section */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(340px, 1fr))', gap: '1.25rem' }}>
        {/* Chart 1: Income vs Expenses Interactive Bar Chart */}
        <div className="clinic-card" style={{ padding: '1.5rem', display: 'flex', flexDirection: 'column', gap: '1.25rem' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
              <BarChart3 size={18} color="var(--clinic-accent-primary)" />
              <h3 style={{ fontSize: '1rem', fontWeight: 700 }}>الإيرادات مقابل المصروفات (Income vs Expenses)</h3>
            </div>
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem', fontSize: '0.6875rem', fontWeight: 600 }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: '0.25rem' }}>
                <span style={{ width: '8px', height: '8px', borderRadius: '2px', backgroundColor: '#0f766e' }} />
                <span>الإيرادات</span>
              </div>
              <div style={{ display: 'flex', alignItems: 'center', gap: '0.25rem' }}>
                <span style={{ width: '8px', height: '8px', borderRadius: '2px', backgroundColor: '#ef4444' }} />
                <span>التكاليف والمصروفات</span>
              </div>
            </div>
          </div>

          {/* SVG Responsive Bar Visualization */}
          <div style={{ width: '100%', height: '220px', display: 'flex', alignItems: 'flex-end', gap: '1.25rem', paddingBottom: '1rem', borderBottom: '1px solid var(--clinic-border-subtle)' }}>
            {metrics.monthlyIncomeExpenses.map((item, idx) => {
              const incomeHeight = Math.round((item.income / maxIncome) * 160);
              const expenseHeight = Math.round((item.expense / maxIncome) * 160);

              return (
                <div key={idx} style={{ flex: 1, display: 'flex', flexDirection: 'column', alignItems: 'center', gap: '0.375rem', height: '100%', justifyContent: 'flex-end' }}>
                  <div style={{ display: 'flex', alignItems: 'flex-end', gap: '0.25rem', width: '100%', justifyContent: 'center' }}>
                    {/* Income Bar */}
                    <div
                      title={`الإيراد: ${item.income.toLocaleString()} ر.س`}
                      style={{
                        width: '18px',
                        height: `${incomeHeight}px`,
                        backgroundColor: 'var(--clinic-accent-primary)',
                        borderRadius: '4px 4px 0 0',
                        transition: 'height 0.4s ease',
                      }}
                    />
                    {/* Expense Bar */}
                    <div
                      title={`المصروفات: ${item.expense.toLocaleString()} ر.س`}
                      style={{
                        width: '18px',
                        height: `${expenseHeight}px`,
                        backgroundColor: '#ef4444',
                        borderRadius: '4px 4px 0 0',
                        opacity: 0.85,
                        transition: 'height 0.4s ease',
                      }}
                    />
                  </div>
                  <span style={{ fontSize: '0.6875rem', color: 'var(--clinic-text-secondary)', fontWeight: 600 }}>
                    {item.month}
                  </span>
                </div>
              );
            })}
          </div>

          <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '0.75rem', color: 'var(--clinic-text-secondary)' }}>
            <span>إجمالي الإيرادات المسجلة: <strong style={{ color: 'var(--clinic-accent-primary)' }}>{metrics.totalRevenueCollected.toLocaleString()} ر.س</strong></span>
            <span>إجمالي التكاليف: <strong style={{ color: '#ef4444' }}>{metrics.totalOperationalCosts.toLocaleString()} ر.س</strong></span>
          </div>
        </div>

        {/* Chart 2: Patient Sessions Distribution by Day of Week */}
        <div className="clinic-card" style={{ padding: '1.5rem', display: 'flex', flexDirection: 'column', gap: '1.25rem' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
              <Calendar size={18} color="var(--clinic-accent-primary)" />
              <h3 style={{ fontSize: '1rem', fontWeight: 700 }}>أعداد الجلسات والمرضى خلال أيام الأسبوع</h3>
            </div>
            <span style={{ fontSize: '0.6875rem', color: 'var(--clinic-text-muted)' }}>متوسط 28 جلسة / يوم</span>
          </div>

          <div style={{ width: '100%', height: '220px', display: 'flex', alignItems: 'flex-end', gap: '1rem', paddingBottom: '1rem', borderBottom: '1px solid var(--clinic-border-subtle)' }}>
            {metrics.sessionsByDay.map((d, i) => {
              const maxCount = 40;
              const h = Math.round((d.count / maxCount) * 160);
              const isPeak = d.count >= 32;

              return (
                <div key={i} style={{ flex: 1, display: 'flex', flexDirection: 'column', alignItems: 'center', gap: '0.375rem', height: '100%', justifyContent: 'flex-end' }}>
                  <div
                    title={`${d.count} جلسة`}
                    style={{
                      width: '28px',
                      height: `${h}px`,
                      backgroundColor: isPeak ? '#f59e0b' : 'var(--clinic-accent-soft)',
                      border: `1px solid ${isPeak ? '#d97706' : 'var(--clinic-accent-primary)'}`,
                      borderRadius: '6px 6px 0 0',
                      display: 'flex',
                      alignItems: 'flex-start',
                      justifyContent: 'center',
                      paddingTop: '4px',
                      transition: 'height 0.3s ease',
                    }}
                  >
                    <span style={{ fontSize: '0.625rem', fontWeight: 700, color: isPeak ? '#78350f' : 'var(--clinic-accent-primary)' }}>
                      {d.count}
                    </span>
                  </div>
                  <span style={{ fontSize: '0.6875rem', color: 'var(--clinic-text-secondary)', fontWeight: 600 }}>
                    {d.day}
                  </span>
                </div>
              );
            })}
          </div>

          <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', fontSize: '0.75rem', color: 'var(--clinic-text-secondary)' }}>
            <ArrowUpRight size={16} color="#f59e0b" />
            <span>ذروة الإقبال تسجل يومي الأحد والثلاثاء مع اكتمال 100% من السعة المجدولة.</span>
          </div>
        </div>
      </div>
    </div>
  );
};
