import React from 'react';
import { LayoutDashboard, Building2, Layers, ShieldCheck, Moon, Sun, LogOut } from 'lucide-react';
import { SUPER_ADMIN_CREDENTIALS } from '../config/authConfig';

export type AdminTab = 'dashboard' | 'owners' | 'modules';

export interface AdminSidebarProps {
  activeTab: AdminTab;
  onTabChange: (tab: AdminTab) => void;
  adminEmail: string;
  isDarkMode: boolean;
  onToggleDarkMode: () => void;
  onLogout: () => void;
}

export const AdminSidebar: React.FC<AdminSidebarProps> = ({
  activeTab,
  onTabChange,
  adminEmail,
  isDarkMode,
  onToggleDarkMode,
  onLogout,
}) => {
  const navItems = [
    {
      id: 'dashboard' as AdminTab,
      label: 'نظرة عامة والمنظومة',
      icon: <LayoutDashboard size={19} />,
      badge: 'مباشر',
    },
    {
      id: 'owners' as AdminTab,
      label: 'دليل الملاك والعيادات',
      icon: <Building2 size={19} />,
      badge: '28 عيادة',
    },
    {
      id: 'modules' as AdminTab,
      label: 'مصفوفة الوحدات والميزات',
      icon: <Layers size={19} />,
      badge: '15 وحدة',
    },
  ];

  return (
    <aside className="admin-sidebar">
      {/* Header & Logo */}
      <div
        style={{
          padding: '1.25rem 1.5rem',
          borderBottom: '1px solid var(--admin-border-subtle)',
          display: 'flex',
          alignItems: 'center',
          gap: '0.75rem',
        }}
      >
        <div
          style={{
            width: '2.5rem',
            height: '2.5rem',
            borderRadius: 'var(--admin-radius-card)',
            backgroundColor: 'var(--admin-accent-soft)',
            color: 'var(--admin-accent-primary)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            flexShrink: 0,
          }}
        >
          <ShieldCheck size={22} />
        </div>

        <div>
          <h2 style={{ fontSize: '0.9375rem', fontWeight: 700, color: 'var(--admin-text-primary)' }}>
            منظومة الرقابة المركزية
          </h2>
          <span style={{ fontSize: '0.6875rem', color: 'var(--admin-text-muted)' }}>
            Super Admin Platform
          </span>
        </div>
      </div>

      {/* Sovereign Node Chip */}
      <div style={{ padding: '0.875rem 1.25rem 0.5rem' }}>
        <div
          style={{
            padding: '0.375rem 0.625rem',
            backgroundColor: 'var(--admin-bg-subtle)',
            borderRadius: 'var(--admin-radius-input)',
            border: '1px solid var(--admin-border-subtle)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            fontSize: '0.6875rem',
            fontFamily: 'monospace',
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.375rem' }}>
            <span
              style={{
                width: '6px',
                height: '6px',
                borderRadius: '50%',
                backgroundColor: 'var(--admin-status-success)',
              }}
            />
            <span>{SUPER_ADMIN_CREDENTIALS.sovereignNode}</span>
          </div>
          <span style={{ color: 'var(--admin-accent-primary)', fontWeight: 600 }}>نطاق معزول 404</span>
        </div>
      </div>

      {/* Navigation Links */}
      <nav style={{ flex: 1, padding: '0.75rem 1rem', display: 'flex', flexDirection: 'column', gap: '0.375rem' }}>
        <div style={{ fontSize: '0.6875rem', fontWeight: 700, color: 'var(--admin-text-muted)', marginBottom: '0.375rem', paddingRight: '0.5rem' }}>
          القائمة الرئيسية
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
              <span style={{ display: 'flex', alignItems: 'center', color: isActive ? 'var(--admin-accent-primary)' : 'inherit' }}>
                {item.icon}
              </span>
              <span style={{ flex: 1 }}>{item.label}</span>
              {item.badge && (
                <span
                  style={{
                    fontSize: '0.6875rem',
                    padding: '0.125rem 0.375rem',
                    borderRadius: 'var(--admin-radius-pill)',
                    backgroundColor: isActive ? 'var(--admin-accent-primary)' : 'var(--admin-bg-subtle)',
                    color: isActive ? '#ffffff' : 'var(--admin-text-secondary)',
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

      {/* Footer Controls: Theme Toggle & Admin Profile */}
      <div
        style={{
          padding: '1rem',
          borderTop: '1px solid var(--admin-border-subtle)',
          display: 'flex',
          flexDirection: 'column',
          gap: '0.75rem',
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
            borderRadius: 'var(--admin-radius-input)',
            backgroundColor: 'var(--admin-bg-subtle)',
            border: '1px solid var(--admin-border-subtle)',
            color: 'var(--admin-text-primary)',
            fontSize: '0.75rem',
            fontWeight: 600,
            cursor: 'pointer',
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
            {isDarkMode ? <Sun size={16} color="#f59e0b" /> : <Moon size={16} color="var(--admin-accent-primary)" />}
            <span>{isDarkMode ? 'الوضع النهاري (Light)' : 'الوضع الليلي (Dark Mode)'}</span>
          </div>
          <span
            style={{
              fontSize: '0.6875rem',
              padding: '0.125rem 0.375rem',
              borderRadius: '4px',
              backgroundColor: isDarkMode ? '#1e293b' : '#ffffff',
            }}
          >
            {isDarkMode ? 'داكن' : 'أبيض'}
          </span>
        </button>

        {/* Profile Card */}
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            padding: '0.625rem 0.75rem',
            borderRadius: 'var(--admin-radius-input)',
            backgroundColor: 'var(--admin-bg-subtle)',
          }}
        >
          <div style={{ minWidth: 0 }}>
            <div style={{ fontSize: '0.8125rem', fontWeight: 700, color: 'var(--admin-text-primary)', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
              {SUPER_ADMIN_CREDENTIALS.fullName}
            </div>
            <div style={{ fontSize: '0.6875rem', color: 'var(--admin-text-muted)', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }} dir="ltr">
              {adminEmail}
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
              color: 'var(--admin-status-danger)',
              cursor: 'pointer',
              borderRadius: 'var(--admin-radius-input)',
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
