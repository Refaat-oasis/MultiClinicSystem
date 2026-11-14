import React from 'react';
import { LayoutDashboard, Layers, Users, ShieldCheck, LogOut } from 'lucide-react';

export type AdminTab = 'dashboard' | 'modules' | 'owners' | 'security';

export interface AdminNavbarProps {
  activeTab: AdminTab;
  onTabChange: (tab: AdminTab) => void;
  adminEmail: string;
  onLogout: () => void;
}

export const AdminNavbar: React.FC<AdminNavbarProps> = ({
  activeTab,
  onTabChange,
  adminEmail,
  onLogout,
}) => {
  const tabs: { id: AdminTab; label: string; icon: React.ReactNode }[] = [
    { id: 'dashboard', label: 'لوحة قيادة المنصة', icon: <LayoutDashboard size={18} /> },
    { id: 'modules', label: 'مصفوفة تفعيل الموديلات', icon: <Layers size={18} /> },
    { id: 'owners', label: 'دليل ملاك العيادات', icon: <Users size={18} /> },
    { id: 'security', label: 'الأمان وعزل المضيف', icon: <ShieldCheck size={18} /> },
  ];

  return (
    <nav
      style={{
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        padding: '0.75rem 1.5rem',
        backgroundColor: 'var(--admin-bg-surface)',
        border: '1px solid var(--admin-border-subtle)',
        borderRadius: 'var(--admin-radius-card)',
        marginBottom: '1.5rem',
        boxShadow: 'var(--admin-shadow-sm)',
        flexWrap: 'wrap',
        gap: '1rem',
      }}
    >
      <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
        {tabs.map((tab) => {
          const isActive = activeTab === tab.id;
          return (
            <button
              key={tab.id}
              onClick={() => onTabChange(tab.id)}
              className={`admin-btn ${isActive ? 'admin-btn-primary' : 'admin-btn-secondary'}`}
              style={{ minHeight: '40px', padding: '0.5rem 1rem' }}
            >
              {tab.icon}
              <span>{tab.label}</span>
            </button>
          );
        })}
      </div>

      <div style={{ display: 'flex', alignItems: 'center', gap: '1rem' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
          <span
            style={{
              width: '8px',
              height: '8px',
              borderRadius: '50%',
              backgroundColor: 'var(--admin-status-success)',
              display: 'inline-block',
            }}
          />
          <span style={{ fontSize: '0.8125rem', fontWeight: 600, color: 'var(--admin-text-secondary)' }}>
            {adminEmail}
          </span>
        </div>

        <button
          onClick={onLogout}
          className="admin-btn admin-btn-danger"
          style={{ minHeight: '36px', padding: '0.375rem 0.875rem', fontSize: '0.8125rem' }}
        >
          <LogOut size={16} />
          <span>تسجيل الخروج</span>
        </button>
      </div>
    </nav>
  );
};
