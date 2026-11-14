import React from 'react';
import {
  LayoutDashboard,
  Users,
  UserCheck,
  Stethoscope,
  Receipt,
  Lock,
  Sparkles,
  Globe,
  LogOut,
} from 'lucide-react';

export type ClinicTab =
  | 'dashboard'
  | 'reception'
  | 'patients'
  | 'clinical'
  | 'billing'
  | 'closing'
  | 'onboarding'
  | 'portal';

export interface ClinicNavbarProps {
  activeTab: ClinicTab;
  onTabChange: (tab: ClinicTab) => void;
  staffName: string;
  clinicName: string;
  onLogout: () => void;
}

export const ClinicNavbar: React.FC<ClinicNavbarProps> = ({
  activeTab,
  onTabChange,
  staffName,
  clinicName,
  onLogout,
}) => {
  const tabs: { id: ClinicTab; label: string; icon: React.ReactNode }[] = [
    { id: 'dashboard', label: 'لوحة التحكم', icon: <LayoutDashboard size={18} /> },
    { id: 'reception', label: 'الاستقبال والانتظار', icon: <Users size={18} /> },
    { id: 'patients', label: 'سجلات المرضى', icon: <UserCheck size={18} /> },
    { id: 'clinical', label: 'جلسة المعاينة', icon: <Stethoscope size={18} /> },
    { id: 'billing', label: 'الفوترة والقبض', icon: <Receipt size={18} /> },
    { id: 'closing', label: 'الإغلاق المالي', icon: <Lock size={18} /> },
    { id: 'onboarding', label: 'معالج التهيئة', icon: <Sparkles size={18} /> },
    { id: 'portal', label: 'بوابة المريض', icon: <Globe size={18} /> },
  ];

  return (
    <nav
      style={{
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        padding: '0.75rem 1.25rem',
        backgroundColor: 'var(--clinic-bg-surface)',
        border: '1px solid var(--clinic-border-subtle)',
        borderRadius: 'var(--clinic-radius-card)',
        marginBottom: '1.5rem',
        boxShadow: 'var(--clinic-shadow-sm)',
        flexWrap: 'wrap',
        gap: '1rem',
      }}
    >
      <div style={{ display: 'flex', gap: '0.375rem', flexWrap: 'wrap' }}>
        {tabs.map((tab) => {
          const isActive = activeTab === tab.id;
          return (
            <button
              key={tab.id}
              onClick={() => onTabChange(tab.id)}
              className={`clinic-btn ${isActive ? 'clinic-btn-primary' : 'clinic-btn-secondary'}`}
              style={{ minHeight: '38px', padding: '0.375rem 0.875rem', fontSize: '0.8125rem' }}
            >
              {tab.icon}
              <span>{tab.label}</span>
            </button>
          );
        })}
      </div>

      <div style={{ display: 'flex', alignItems: 'center', gap: '1rem' }}>
        <div style={{ textAlign: 'start' }}>
          <div style={{ fontSize: '0.8125rem', fontWeight: 600, color: 'var(--clinic-text-primary)' }}>
            {staffName}
          </div>
          <div style={{ fontSize: '0.6875rem', color: 'var(--clinic-accent-primary)', fontWeight: 600 }}>
            {clinicName}
          </div>
        </div>

        <button
          onClick={onLogout}
          className="clinic-btn clinic-btn-danger"
          style={{ minHeight: '34px', padding: '0.25rem 0.75rem', fontSize: '0.75rem' }}
        >
          <LogOut size={14} />
          <span>خروج</span>
        </button>
      </div>
    </nav>
  );
};
