import React, { useState, useEffect } from 'react';
import { SuperAdminLoginScreen } from './screens/SuperAdminLoginScreen';
import { PlatformDashboardScreen } from './screens/PlatformDashboardScreen';
import { ClinicModuleMatrixScreen } from './screens/ClinicModuleMatrixScreen';
import { ClinicOwnersDirectoryScreen } from './screens/ClinicOwnersDirectoryScreen';
import { AdminSidebar, AdminTab } from './components/AdminSidebar';
import './styles/admin-stitch.css';

export const App: React.FC = () => {
  const [adminEmail, setAdminEmail] = useState<string | null>(null);
  const [activeTab, setActiveTab] = useState<AdminTab>('dashboard');
  const [isDarkMode, setIsDarkMode] = useState<boolean>(() => {
    return localStorage.getItem('admin_theme') === 'dark';
  });

  useEffect(() => {
    if (isDarkMode) {
      document.documentElement.setAttribute('data-theme', 'dark');
      localStorage.setItem('admin_theme', 'dark');
    } else {
      document.documentElement.removeAttribute('data-theme');
      localStorage.setItem('admin_theme', 'light');
    }
  }, [isDarkMode]);

  const toggleDarkMode = () => {
    setIsDarkMode((prev) => !prev);
  };

  const handleLoginSuccess = (email: string) => {
    setAdminEmail(email);
    setActiveTab('dashboard');
  };

  const handleLogout = () => {
    setAdminEmail(null);
  };

  // If not logged in, render Login Screen with NO sidebar
  if (!adminEmail) {
    return <SuperAdminLoginScreen onLoginSuccess={handleLoginSuccess} />;
  }

  return (
    <div className="admin-shell">
      {/* Right-Side Navigation Sidebar */}
      <AdminSidebar
        activeTab={activeTab}
        onTabChange={setActiveTab}
        adminEmail={adminEmail}
        isDarkMode={isDarkMode}
        onToggleDarkMode={toggleDarkMode}
        onLogout={handleLogout}
      />

      {/* Main Content Area (Flows left of the right sidebar) */}
      <main className="admin-content-area">
        {activeTab === 'dashboard' && <PlatformDashboardScreen />}
        {activeTab === 'modules' && <ClinicModuleMatrixScreen />}
        {activeTab === 'owners' && <ClinicOwnersDirectoryScreen />}
      </main>
    </div>
  );
};
