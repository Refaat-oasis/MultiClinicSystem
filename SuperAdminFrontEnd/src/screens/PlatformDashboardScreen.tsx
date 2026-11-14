import React from 'react';
import { Building2, Users, Activity, Clock, Shield, Plus, ExternalLink } from 'lucide-react';
import { StatCard } from '../components/StatCard';
import { StatusChip } from '../components/StatusChip';
import { ClinicalTable, Column } from '../components/ClinicalTable';

interface ClinicRow {
  id: string;
  name: string;
  slug: string;
  ownerName: string;
  activeModules: number;
  status: 'Active' | 'PendingOnboarding' | 'Suspended';
  createdAt: string;
}

const SAMPLE_CLINICS: ClinicRow[] = [
  {
    id: 'clinic-01',
    name: 'عيادات الصفوة التخصصية',
    slug: 'alsafwa-specialty',
    ownerName: 'د. أحمد الرفاعي',
    activeModules: 8,
    status: 'Active',
    createdAt: '2026-01-15',
  },
  {
    id: 'clinic-02',
    name: 'مركز النخبة لطب الأسنان',
    slug: 'al-nokhba-dental',
    ownerName: 'د. سارة محمود',
    activeModules: 6,
    status: 'Active',
    createdAt: '2026-03-20',
  },
  {
    id: 'clinic-03',
    name: 'عيادة الشفاء للقلب والباطنية',
    slug: 'al-shifa-clinic',
    ownerName: 'د. كريم عبد العزيز',
    activeModules: 0,
    status: 'PendingOnboarding',
    createdAt: '2026-11-10',
  },
];

export const PlatformDashboardScreen: React.FC = () => {
  const columns: Column<ClinicRow>[] = [
    {
      key: 'name',
      header: 'اسم العيادة',
      render: (c) => (
        <div>
          <div style={{ fontWeight: 600, color: 'var(--admin-text-primary)' }}>{c.name}</div>
          <div style={{ fontSize: '0.75rem', fontFamily: 'monospace', color: 'var(--admin-text-muted)' }}>
            /{c.slug}
          </div>
        </div>
      ),
    },
    {
      key: 'ownerName',
      header: 'مالك العيادة',
      render: (c) => <span>{c.ownerName}</span>,
    },
    {
      key: 'activeModules',
      header: 'الوحدات المفعلة',
      render: (c) => (
        <span className="num-tabular" style={{ fontWeight: 600 }}>
          {c.activeModules} / 8 وحدات
        </span>
      ),
      align: 'center',
    },
    {
      key: 'status',
      header: 'حالة العيادة',
      render: (c) => {
        if (c.status === 'Active') return <StatusChip status="success" label="نشطة ومنتظمة" />;
        if (c.status === 'PendingOnboarding') return <StatusChip status="warning" label="قيد التهيئة (Onboarding)" />;
        return <StatusChip status="danger" label="موقوفة" />;
      },
    },
    {
      key: 'createdAt',
      header: 'تاريخ التسجيل',
      render: (c) => <span className="num-tabular">{c.createdAt}</span>,
    },
    {
      key: 'actions',
      header: 'الإجراءات',
      render: () => (
        <button className="admin-btn admin-btn-secondary" style={{ minHeight: '32px', padding: '0.25rem 0.625rem', fontSize: '0.75rem' }}>
          <ExternalLink size={14} />
          <span>إدارة</span>
        </button>
      ),
      align: 'center',
    },
  ];

  return (
    <div style={{ display: 'grid', gap: '1.5rem' }}>
      {/* Overview Header */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '1rem' }}>
        <div>
          <h2 style={{ fontSize: '1.25rem', fontWeight: 700, color: 'var(--admin-text-primary)' }}>
            نظرة عامة على المنصة السحابية
          </h2>
          <p style={{ fontSize: '0.875rem', color: 'var(--admin-text-secondary)' }}>
            المؤشرات المباشرة للعيادات ومعدلات الأداء ومطابقة سياسات الأمان
          </p>
        </div>

        <button className="admin-btn admin-btn-primary">
          <Plus size={18} />
          <span>إضافة عيادة جديدة</span>
        </button>
      </div>

      {/* 4 StatCards matching Stitch */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '1rem' }}>
        <StatCard
          title="إجمالي العيادات المسجلة"
          value="48 عيادة"
          trend={{ direction: 'up', value: '+12% هذا الشهر' }}
          icon={<Building2 size={20} />}
          subtitle="42 عيادة نشطة بالكامل"
        />
        <StatCard
          title="الزيارات الطبية هذا اليوم"
          value="1,420 زيارة"
          trend={{ direction: 'up', value: '+8.4%' }}
          icon={<Users size={20} />}
          subtitle="في كافة التخصصات والعيادات"
        />
        <StatCard
          title="زمن استجابة الـ API"
          value="18ms"
          trend={{ direction: 'down', value: '-3ms تحسن' }}
          icon={<Activity size={20} />}
          subtitle="عبر شبكة الحوسبة السحابية المحلية"
        />
        <StatCard
          title="نسبة التوفر والاعتمادية"
          value="99.99%"
          icon={<Clock size={20} />}
          subtitle="ISO-27001 SLA Verified"
        />
      </div>

      {/* Clinics Table Section */}
      <div className="admin-card" style={{ padding: '1.5rem' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1.25rem' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
            <Shield size={20} color="var(--admin-accent-primary)" />
            <h3 style={{ fontSize: '1rem', fontWeight: 700 }}>دليل العيادات والمستأجرين (Tenants)</h3>
          </div>
          <span style={{ fontSize: '0.75rem', color: 'var(--admin-text-muted)' }}>
            عزل كامل على مستوى قاعدة البيانات (Row-Level Security)
          </span>
        </div>

        <ClinicalTable
          columns={columns}
          data={SAMPLE_CLINICS}
          keyExtractor={(c) => c.id}
          emptyMessage="لا توجد عيادات مسجلة"
        />
      </div>
    </div>
  );
};
