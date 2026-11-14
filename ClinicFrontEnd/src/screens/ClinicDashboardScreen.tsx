import React from 'react';
import { Calendar, Users, DollarSign, AlertTriangle, Plus, Clock } from 'lucide-react';
import { StatCard } from '../components/StatCard';
import { StatusChip } from '../components/StatusChip';
import { ClinicalTable, Column } from '../components/ClinicalTable';

interface AppointmentRow {
  time: string;
  patientName: string;
  doctorName: string;
  category: string;
  status: 'Waiting' | 'InSession' | 'Completed';
  hasAllergy: boolean;
}

const UPCOMING_APPOINTMENTS: AppointmentRow[] = [
  { time: '09:30 AM', patientName: 'سارة عبد الله', doctorName: 'د. أحمد الرفاعي', category: 'كشف واستشارة', status: 'InSession', hasAllergy: true },
  { time: '10:00 AM', patientName: 'محمد عثمان الشريف', doctorName: 'د. سارة محمود', category: 'علاج عصب وجلسة ثانية', status: 'Waiting', hasAllergy: false },
  { time: '10:30 AM', patientName: 'ليلى إبراهيم فؤاد', doctorName: 'د. أحمد الرفاعي', category: 'تنظيف وتبييض', status: 'Waiting', hasAllergy: true },
  { time: '11:00 AM', patientName: 'يوسف مصطفى النجار', doctorName: 'د. كريم عبد العزيز', category: 'متابعة وفحص ضغط', status: 'Waiting', hasAllergy: false },
];

export const ClinicDashboardScreen: React.FC = () => {
  const columns: Column<AppointmentRow>[] = [
    {
      key: 'time',
      header: 'الوقت',
      render: (a) => (
        <span className="num-tabular" style={{ fontWeight: 600, color: 'var(--clinic-text-primary)' }}>
          {a.time}
        </span>
      ),
    },
    {
      key: 'patientName',
      header: 'اسم المريض',
      render: (a) => (
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
          <span style={{ fontWeight: 600 }}>{a.patientName}</span>
          {a.hasAllergy && (
            <span
              style={{
                fontSize: '0.6875rem',
                backgroundColor: 'var(--clinic-status-danger-bg)',
                color: 'var(--clinic-status-danger)',
                fontWeight: 700,
                padding: '0.125rem 0.375rem',
                borderRadius: '4px',
              }}
            >
              حساسية!
            </span>
          )}
        </div>
      ),
    },
    {
      key: 'doctorName',
      header: 'الطبيب المعالج',
      render: (a) => <span>{a.doctorName}</span>,
    },
    {
      key: 'category',
      header: 'نوع الإجراء',
      render: (a) => <span style={{ color: 'var(--clinic-text-secondary)' }}>{a.category}</span>,
    },
    {
      key: 'status',
      header: 'حالة الحضور',
      render: (a) => {
        if (a.status === 'InSession') return <StatusChip status="info" label="داخل العيادة" />;
        if (a.status === 'Waiting') return <StatusChip status="warning" label="في غرفة الانتظار" />;
        return <StatusChip status="success" label="مكتمل" />;
      },
    },
  ];

  return (
    <div style={{ display: 'grid', gap: '1.5rem' }}>
      {/* Top Header & Actions */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '1rem' }}>
        <div>
          <h2 style={{ fontSize: '1.25rem', fontWeight: 700, color: 'var(--clinic-text-primary)' }}>
            لوحة قيادة العيادة (Clinical Calm Dashboard)
          </h2>
          <p style={{ fontSize: '0.875rem', color: 'var(--clinic-text-secondary)' }}>
            ملخص حركة المرضى اليومية، طابور المعاينة المباشر، والسيولة النقدية
          </p>
        </div>

        <div style={{ display: 'flex', gap: '0.5rem' }}>
          <button className="clinic-btn clinic-btn-primary">
            <Plus size={18} />
            <span>حجز موعد جديد</span>
          </button>
        </div>
      </div>

      {/* 4 StatCards */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '1rem' }}>
        <StatCard
          title="مواعيد اليوم المسجلة"
          value="24 موعداً"
          trend={{ direction: 'up', value: '+14%' }}
          icon={<Calendar size={20} />}
          subtitle="منها 6 مواعيد مؤكدة عبر البوابة الذاتية"
        />
        <StatCard
          title="المرضى في صالة الانتظار"
          value="3 مرضى"
          icon={<Users size={20} />}
          subtitle="متوسط وقت الانتظار 12 دقيقة"
        />
        <StatCard
          title="الإيرادات المحصلة اليوم"
          value="8,450 ج.م"
          trend={{ direction: 'up', value: '+18% مقارنة بالأمس' }}
          icon={<DollarSign size={20} />}
          subtitle="5,200 نقداً • 3,250 شبكة/تحويل"
        />
        <StatCard
          title="تنبيهات الحساسية الحرجة"
          value="2 حالة"
          icon={<AlertTriangle size={20} />}
          subtitle="تم التدقيق والفحص التلقائي"
        />
      </div>

      {/* Live Schedule Table */}
      <div className="clinic-card">
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1.25rem' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
            <Clock size={20} color="var(--clinic-accent-primary)" />
            <h3 style={{ fontSize: '1rem', fontWeight: 700 }}>جدول المعاينة والانتظار لليوم</h3>
          </div>
          <span style={{ fontSize: '0.75rem', color: 'var(--clinic-text-muted)' }}>
            يتم التحديث المباشر فور تسجيل الوصول
          </span>
        </div>

        <ClinicalTable
          columns={columns}
          data={UPCOMING_APPOINTMENTS}
          keyExtractor={(a) => a.time + a.patientName}
        />
      </div>
    </div>
  );
};
