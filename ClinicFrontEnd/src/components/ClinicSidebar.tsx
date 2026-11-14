import React from 'react';
import {
  LayoutDashboard,
  Users2,
  FolderHeart,
  Stethoscope,
  BarChart3,
  Receipt,
  Lock,
  Compass,
  Globe,
  Sun,
  Moon,
  LogOut,
  Wallet,
} from 'lucide-react';

export type ClinicTab =
  | 'dashboard'
  | 'reception'
  | 'patients'
  | 'clinical'
  | 'doctor-reports'
  | 'costs'
  | 'billing'
  | 'closing'
  | 'onboarding'
  | 'portal';

export interface ClinicSidebarProps {
  activeTab: ClinicTab;
  onTabChange: (tab: ClinicTab) => void;
  staffName: string;
  clinicName: string;
  isDarkMode: boolean;
  onToggleDarkMode: () => void;
  onLogout: () => void;
}

export const ClinicSidebar: React.FC<ClinicSidebarProps> = ({
  activeTab,
  onTabChange,
  staffName,
  clinicName,
  isDarkMode,
  onToggleDarkMode,
  onLogout,
}) => {
  const navItems = [
    {
      id: 'dashboard' as ClinicTab,
      label: 'لوحة التحكم',
      icon: <LayoutDashboard size={18} />,
    },
    {
      id: 'reception' as ClinicTab,
      label: 'الاستقبال والانتظار',
      icon: <Users2 size={18} />,
      badge: '4 منتظرين',
    },
    {
      id: 'patients' as ClinicTab,
      label: 'سجلات المرضى (EHR)',
      icon: <FolderHeart size={18} />,
    },
    {
      id: 'clinical' as ClinicTab,
      label: 'جلسة المعاينة ومخطط 3D',
      icon: <Stethoscope size={18} />,
      badge: 'جلسة نشطة',
    },
    {
      id: 'doctor-reports' as ClinicTab,
      label: 'تقارير وتحليلات الطبيب',
      icon: <BarChart3 size={18} />,
      badge: 'جديد',
    },
    {
      id: 'costs' as ClinicTab,
      label: 'التكاليف الثابتة والمتغيرة',
      icon: <Wallet size={18} />,
      badge: 'جديد',
    },
    {
      id: 'billing' as ClinicTab,
      label: 'الفوترة والتحصيل',
      icon: <Receipt size={18} />,
    },
    {
      id: 'closing' as ClinicTab,
      label: 'الإغلاق المالي',
      icon: <Lock size={18} />,
    },
    {
      id: 'onboarding' as ClinicTab,
      label: 'معالج التهيئة',
      icon: <Compass size={18} />,
    },
    {
      id: 'portal' as ClinicTab,
      label: 'بوابة المريض',
      icon: <Globe size={18} />,
    },
  ];

  return (
    <aside className="clinic-sidebar">
      {/* Header & Clinic Brand */}
      <div
        style={{
          padding: '1.25rem 1.25rem 1rem',
          borderBottom: '1px solid var(--clinic-border-subtle)',
          display: 'flex',
          alignItems: 'center',
          gap: '0.75rem',
        }}
      >
        <div
          style={{
            width: '2.5rem',
            height: '2.5rem',
            borderRadius: 'var(--clinic-radius-card)',
            backgroundColor: 'var(--clinic-accent-soft)',
            color: 'var(--clinic-accent-primary)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            flexShrink: 0,
          }}
        >
          <Stethoscope size={22} />
        </div>

        <div style={{ minWidth: 0 }}>
          <h2
            style={{
              fontSize: '0.9375rem',
              fontWeight: 700,
              color: 'var(--clinic-text-primary)',
              whiteSpace: 'nowrap',
              overflow: 'hidden',
              textOverflow: 'ellipsis',
            }}
          >
            {clinicName}
          </h2>
          <span style={{ fontSize: '0.6875rem', color: 'var(--clinic-text-muted)' }}>
            عيادة نشطة • نطاق معزول
          </span>
        </div>
      </div>

      {/* Navigation Menu */}
      <nav
        style={{
          flex: 1,
          padding: '0.75rem 0.875rem',
          display: 'flex',
          flexDirection: 'column',
          gap: '0.25rem',
          overflowY: 'auto',
        }}
      >
        <div
          style={{
            fontSize: '0.6875rem',
            fontWeight: 700,
            color: 'var(--clinic-text-muted)',
            marginBottom: '0.375rem',
            paddingRight: '0.5rem',
          }}
        >
          أقسام العيادة والخدمات
        </div>

        {navItems.map((item) => {
          const isActive = activeTab === item.id;
          return (
            <button
              key={item.id}
              type="button"
              onClick={() => onTabChange(item.id)}
              className={`sidebar-nav-item ${isActive ? 'active' : ''}`}
            >
              <span style={{ display: 'flex', alignItems: 'center', color: isActive ? 'var(--clinic-accent-primary)' : 'inherit' }}>
                {item.icon}
              </span>
              <span style={{ flex: 1, textAlign: 'start' }}>{item.label}</span>
              {item.badge && (
                <span
                  style={{
                    fontSize: '0.625rem',
                    padding: '0.125rem 0.375rem',
                    borderRadius: 'var(--clinic-radius-pill)',
                    backgroundColor: isActive ? 'var(--clinic-accent-primary)' : 'var(--clinic-bg-subtle)',
                    color: isActive ? '#ffffff' : 'var(--clinic-text-secondary)',
                    fontWeight: 600,
                  }}
                >
                  {item.badge}
                </span>
              )}
            </button>
          );
        })}
      </nav>

      {/* Footer Controls: Dark Mode Switcher & Staff Profile */}
      <div
        style={{
          padding: '1rem',
          borderTop: '1px solid var(--clinic-border-subtle)',
          display: 'flex',
          flexDirection: 'column',
          gap: '0.625rem',
        }}
      >
        {/* Dark Mode Switcher */}
        <button
          type="button"
          onClick={onToggleDarkMode}
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            padding: '0.5rem 0.75rem',
            borderRadius: 'var(--clinic-radius-input)',
            backgroundColor: 'var(--clinic-bg-subtle)',
            border: '1px solid var(--clinic-border-subtle)',
            color: 'var(--clinic-text-primary)',
            fontSize: '0.75rem',
            fontWeight: 600,
            cursor: 'pointer',
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
            {isDarkMode ? <Sun size={16} color="#f59e0b" /> : <Moon size={16} color="var(--clinic-accent-primary)" />}
            <span>{isDarkMode ? 'الوضع النهاري' : 'الوضع الليلي (Dark)'}</span>
          </div>
          <span
            style={{
              fontSize: '0.6875rem',
              padding: '0.125rem 0.375rem',
              borderRadius: '4px',
              backgroundColor: isDarkMode ? '#1e293b' : '#ffffff',
            }}
          >
            {isDarkMode ? 'داكن' : 'فاتح'}
          </span>
        </button>

        {/* Staff Profile & Logout */}
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            padding: '0.5rem 0.75rem',
            borderRadius: 'var(--clinic-radius-input)',
            backgroundColor: 'var(--clinic-bg-subtle)',
          }}
        >
          <div style={{ minWidth: 0 }}>
            <div
              style={{
                fontSize: '0.8125rem',
                fontWeight: 700,
                color: 'var(--clinic-text-primary)',
                whiteSpace: 'nowrap',
                overflow: 'hidden',
                textOverflow: 'ellipsis',
              }}
            >
              طاقم العيادة
            </div>
            <div
              style={{
                fontSize: '0.6875rem',
                color: 'var(--clinic-text-muted)',
                whiteSpace: 'nowrap',
                overflow: 'hidden',
                textOverflow: 'ellipsis',
              }}
              dir="ltr"
            >
              {staffName}
            </div>
          </div>

          <button
            type="button"
            onClick={onLogout}
            title="تسجيل الخروج"
            style={{
              background: 'none',
              border: 'none',
              padding: '0.375rem',
              color: 'var(--clinic-status-danger)',
              cursor: 'pointer',
              borderRadius: 'var(--clinic-radius-input)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
            }}
          >
            <LogOut size={16} />
          </button>
        </div>
      </div>
    </aside>
  );
};
