import React from 'react';
import { Users, Phone, Mail, Building, Plus, Shield } from 'lucide-react';
import { StatusChip } from '../components/StatusChip';
import { ClinicalTable, Column } from '../components/ClinicalTable';

interface OwnerRow {
  id: string;
  name: string;
  email: string;
  phone: string;
  clinicsCount: number;
  quota: number;
  status: 'Active' | 'Suspended';
  registeredDate: string;
}

const SAMPLE_OWNERS: OwnerRow[] = [
  {
    id: 'owner-01',
    name: 'د. أحمد الرفاعي',
    email: 'ahmed.refaai@oasis-med.com',
    phone: '+966 50 123 4567',
    clinicsCount: 3,
    quota: 5,
    status: 'Active',
    registeredDate: '2025-10-12',
  },
  {
    id: 'owner-02',
    name: 'د. سارة محمود',
    email: 'sara.m@elnokhba.com',
    phone: '+966 55 987 6543',
    clinicsCount: 1,
    quota: 2,
    status: 'Active',
    registeredDate: '2026-02-01',
  },
  {
    id: 'owner-03',
    name: 'د. كريم عبد العزيز',
    email: 'kareem.aziz@alshifa.com',
    phone: '+966 54 333 2211',
    clinicsCount: 1,
    quota: 1,
    status: 'Active',
    registeredDate: '2026-08-15',
  },
];

export const ClinicOwnersDirectoryScreen: React.FC = () => {
  const columns: Column<OwnerRow>[] = [
    {
      key: 'name',
      header: 'اسم المالك',
      render: (o) => (
        <div>
          <div style={{ fontWeight: 600, color: 'var(--admin-text-primary)' }}>{o.name}</div>
          <div style={{ fontSize: '0.75rem', color: 'var(--admin-text-muted)' }}>ID: {o.id}</div>
        </div>
      ),
    },
    {
      key: 'contact',
      header: 'بيانات التواصل',
      render: (o) => (
        <div style={{ fontSize: '0.8125rem' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.375rem', color: 'var(--admin-text-secondary)' }}>
            <Mail size={13} />
            <span dir="ltr">{o.email}</span>
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.375rem', color: 'var(--admin-text-muted)', marginTop: '0.125rem' }}>
            <Phone size={13} />
            <span dir="ltr">{o.phone}</span>
          </div>
        </div>
      ),
    },
    {
      key: 'clinicsCount',
      header: 'العيادات / الحصة المصرح بها',
      render: (o) => (
        <div>
          <span className="num-tabular" style={{ fontWeight: 600 }}>
            {o.clinicsCount} من {o.quota} عيادة
          </span>
          <div
            style={{
              width: '100px',
              height: '4px',
              backgroundColor: 'var(--admin-bg-hover)',
              borderRadius: '2px',
              marginTop: '4px',
              overflow: 'hidden',
            }}
          >
            <div
              style={{
                width: `${(o.clinicsCount / o.quota) * 100}%`,
                height: '100%',
                backgroundColor: 'var(--admin-accent-primary)',
              }}
            />
          </div>
        </div>
      ),
    },
    {
      key: 'status',
      header: 'حالة الحساب',
      render: (o) => {
        return o.status === 'Active' ? (
          <StatusChip status="success" label="نشط ومعتمد" />
        ) : (
          <StatusChip status="danger" label="موقوف" />
        );
      },
    },
    {
      key: 'registeredDate',
      header: 'تاريخ الانضمام',
      render: (o) => <span className="num-tabular">{o.registeredDate}</span>,
    },
    {
      key: 'actions',
      header: 'الإجراءات',
      render: () => (
        <button className="admin-btn admin-btn-secondary" style={{ minHeight: '32px', padding: '0.25rem 0.625rem', fontSize: '0.75rem' }}>
          <Building size={14} />
          <span>المحفظة</span>
        </button>
      ),
      align: 'center',
    },
  ];

  return (
    <div style={{ display: 'grid', gap: '1.5rem' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '1rem' }}>
        <div>
          <h2 style={{ fontSize: '1.25rem', fontWeight: 700, color: 'var(--admin-text-primary)' }}>
            دليل ملاك العيادات والمحافظ (Owners Directory)
          </h2>
          <p style={{ fontSize: '0.875rem', color: 'var(--admin-text-secondary)' }}>
            إدارة حسابات المستثمرين وتخصيص حصص إنشاء العيادات ومتابعة المحافظ
          </p>
        </div>

        <button className="admin-btn admin-btn-primary">
          <Plus size={18} />
          <span>تسجيل مالك جديد</span>
        </button>
      </div>

      <div className="admin-card">
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '1.25rem' }}>
          <Users size={20} color="var(--admin-accent-primary)" />
          <h3 style={{ fontSize: '1rem', fontWeight: 700 }}>سجل الملاك وحصص التراخيص</h3>
        </div>

        <ClinicalTable
          columns={columns}
          data={SAMPLE_OWNERS}
          keyExtractor={(o) => o.id}
          emptyMessage="لا يوجد ملاك مسجلون حالياً"
        />
      </div>

      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          gap: '0.75rem',
          padding: '1rem',
          backgroundColor: 'var(--admin-bg-subtle)',
          borderRadius: 'var(--admin-radius-card)',
          border: '1px solid var(--admin-border-subtle)',
          fontSize: '0.8125rem',
          color: 'var(--admin-text-secondary)',
        }}
      >
        <Shield size={20} color="var(--admin-accent-primary)" />
        <span>
          يتم تجميع تقارير محفظة المالك عبر واجهة <code>IOwnerPortfolio</code> المعتمدة، وتعمل استعلامات كل عيادة ضمن سياق الأمان الخاص بها حصراً.
        </span>
      </div>
    </div>
  );
};
