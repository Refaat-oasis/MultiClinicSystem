import React, { useState } from 'react';
import { Layers, ShieldCheck, AlertTriangle, Check, Lock } from 'lucide-react';
import { StatusChip } from '../components/StatusChip';

interface ModuleItem {
  code: string;
  nameAr: string;
  nameEn: string;
  description: string;
  isCore: boolean;
  enabled: boolean;
  dependencies: string[];
}

const INITIAL_MODULES: ModuleItem[] = [
  { code: 'core', nameAr: 'النواة الأساسية (Core)', nameEn: 'Core Engine', description: 'إدارة الهوية والتسجيل والأدوار وتوثيق النشاط', isCore: true, enabled: true, dependencies: [] },
  { code: 'patients', nameAr: 'سجلات المرضى (Patients)', nameEn: 'Patients EHR', description: 'الملفات الطبية وتاريخ الزيارات وراية الحساسية', isCore: false, enabled: true, dependencies: ['core'] },
  { code: 'scheduling', nameAr: 'الحجوزات والمواعيد (Scheduling)', nameEn: 'Scheduling & Appointments', description: 'إدارة طابور الانتظار وحجز المواعيد وأوقات العمل', isCore: false, enabled: true, dependencies: ['core', 'patients'] },
  { code: 'clinical', nameAr: 'المعاينة السريرية (Clinical)', nameEn: 'Doctor Clinical Sessions', description: 'فحص الحساسية والمخططات التخصصية والروشتات', isCore: false, enabled: true, dependencies: ['core', 'patients', 'scheduling'] },
  { code: 'billing', nameAr: 'الفوترة والقبض (Billing)', nameEn: 'Billing & Ledger', description: 'الفواتير والمدفوعات المجزأة وسندات القبض برقم تسلسلي', isCore: false, enabled: true, dependencies: ['core', 'patients'] },
  { code: 'finance', nameAr: 'المالية وتقفيل اليوم (Finance)', nameEn: 'Daily Closing & Expenses', description: 'الإغلاق المالي اليومي، تسوية الصندوق، والمصاريف التشغيلية', isCore: false, enabled: true, dependencies: ['core', 'billing'] },
  { code: 'inventory', nameAr: 'المستودع والمخزون (Inventory)', nameEn: 'FEFO Inventory Control', description: 'صرف الأدوية والمستلزمات بتقنية ما ينتهي أولاً يصرف أولاً', isCore: false, enabled: false, dependencies: ['core'] },
  { code: 'reporting', nameAr: 'التقارير المتقدمة (Reporting)', nameEn: 'Heavy Dapper Analytics', description: 'تقارير الإيرادات وتصدير الجداول والمؤشرات التحليلية', isCore: false, enabled: true, dependencies: ['core', 'billing'] },
];

export const ClinicModuleMatrixScreen: React.FC = () => {
  const [selectedClinic, setSelectedClinic] = useState('عيادات الصفوة التخصصية (/alsafwa-specialty)');
  const [modules, setModules] = useState<ModuleItem[]>(INITIAL_MODULES);
  const [showStepUpModal, setShowStepUpModal] = useState(false);
  const [pendingModuleCode, setPendingModuleCode] = useState<string | null>(null);
  const [stepUpPassword, setStepUpPassword] = useState('');
  const [feedbackMessage, setFeedbackMessage] = useState<string | null>(null);

  const handleToggleClick = (code: string) => {
    const mod = modules.find((m) => m.code === code);
    if (!mod || mod.isCore) return;

    setPendingModuleCode(code);
    setShowStepUpModal(true);
  };

  const handleConfirmStepUp = (e: React.FormEvent) => {
    e.preventDefault();
    if (!stepUpPassword) return;

    setModules((prev) =>
      prev.map((m) => (m.code === pendingModuleCode ? { ...m, enabled: !m.enabled } : m))
    );

    const targetMod = modules.find((m) => m.code === pendingModuleCode);
    const actionText = targetMod?.enabled ? 'تعطيل' : 'تفعيل';
    setFeedbackMessage(`تم ${actionText} وحدة "${targetMod?.nameAr}" بنجاح بعد التحقق الإضافي (Step-Up).`);
    setShowStepUpModal(false);
    setStepUpPassword('');
    setPendingModuleCode(null);
  };

  return (
    <div style={{ display: 'grid', gap: '1.5rem' }}>
      <div>
        <h2 style={{ fontSize: '1.25rem', fontWeight: 700, color: 'var(--admin-text-primary)' }}>
          مصفوفة تفعيل الموديلات (Module Toggle Matrix)
        </h2>
        <p style={{ fontSize: '0.875rem', color: 'var(--admin-text-secondary)' }}>
          التحكم الحصري للمشرف الأعلى في صلاحيات وتراخيص الميزات لكل عيادة على حدة
        </p>
      </div>

      {feedbackMessage && (
        <div
          style={{
            padding: '0.875rem 1.25rem',
            backgroundColor: 'var(--admin-status-success-bg)',
            color: 'var(--admin-status-success)',
            borderRadius: 'var(--admin-radius-input)',
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

      {/* Clinic Selector Card */}
      <div className="admin-card">
        <label style={{ display: 'block', fontSize: '0.8125rem', fontWeight: 600, marginBottom: '0.5rem' }}>
          حدد العيادة المستهدفة للتعديل:
        </label>
        <select
          value={selectedClinic}
          onChange={(e) => setSelectedClinic(e.target.value)}
          className="admin-input"
          style={{ maxWidth: '420px', cursor: 'pointer' }}
        >
          <option value="عيادات الصفوة التخصصية (/alsafwa-specialty)">عيادات الصفوة التخصصية (/alsafwa-specialty)</option>
          <option value="مركز النخبة لطب الأسنان (/al-nokhba-dental)">مركز النخبة لطب الأسنان (/al-nokhba-dental)</option>
          <option value="عيادة الشفاء للقلب (/al-shifa-clinic)">عيادة الشفاء للقلب (/al-shifa-clinic)</option>
        </select>
      </div>

      {/* Module Grid */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(320px, 1fr))', gap: '1rem' }}>
        {modules.map((mod) => (
          <div
            key={mod.code}
            className="admin-card"
            style={{
              display: 'flex',
              flexDirection: 'column',
              justifyContent: 'space-between',
              borderInlineStart: mod.enabled
                ? '4px solid var(--admin-accent-primary)'
                : '4px solid var(--admin-border-strong)',
            }}
          >
            <div>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: '0.5rem' }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                  <Layers size={18} color="var(--admin-accent-primary)" />
                  <h4 style={{ fontSize: '0.9375rem', fontWeight: 700 }}>{mod.nameAr}</h4>
                </div>
                {mod.isCore ? (
                  <StatusChip status="info" label="إجباري (Core)" showDot={false} />
                ) : mod.enabled ? (
                  <StatusChip status="success" label="مفعل" />
                ) : (
                  <StatusChip status="danger" label="معطل" />
                )}
              </div>

              <p style={{ fontSize: '0.8125rem', color: 'var(--admin-text-secondary)', marginBottom: '0.75rem' }}>
                {mod.description}
              </p>

              {mod.dependencies.length > 0 && (
                <div style={{ fontSize: '0.75rem', color: 'var(--admin-text-muted)', marginBottom: '1rem' }}>
                  <span>المتطلبات المسبقة: </span>
                  <span style={{ fontFamily: 'monospace' }}>{mod.dependencies.join(' + ')}</span>
                </div>
              )}
            </div>

            <div>
              {mod.isCore ? (
                <span style={{ fontSize: '0.75rem', color: 'var(--admin-text-muted)' }}>
                  لا يمكن تعطيل النواة الأساسية للنظام
                </span>
              ) : (
                <button
                  type="button"
                  onClick={() => handleToggleClick(mod.code)}
                  className={`admin-btn ${mod.enabled ? 'admin-btn-danger' : 'admin-btn-primary'}`}
                  style={{ width: '100%', minHeight: '36px', fontSize: '0.8125rem' }}
                >
                  {mod.enabled ? 'تعطيل الوحدة' : 'تفعيل الوحدة'}
                </button>
              )}
            </div>
          </div>
        ))}
      </div>

      {/* Step-Up Password Modal */}
      {showStepUpModal && (
        <div
          style={{
            position: 'fixed',
            inset: 0,
            backgroundColor: 'rgba(15, 23, 42, 0.4)',
            backdropFilter: 'blur(4px)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            padding: '1rem',
            zIndex: 1000,
          }}
        >
          <div className="admin-card" style={{ width: '100%', maxWidth: '440px', padding: '1.75rem' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', color: 'var(--admin-status-warning)', marginBottom: '1rem' }}>
              <AlertTriangle size={24} />
              <h3 style={{ fontSize: '1.125rem', fontWeight: 700, color: 'var(--admin-text-primary)' }}>
                التحقق الأمني الإضافي (Step-Up Password)
              </h3>
            </div>

            <p style={{ fontSize: '0.875rem', color: 'var(--admin-text-secondary)', marginBottom: '1.25rem' }}>
              يتطلب تعديل تراخيص الموديلات للعيادات إدخال كلمة مرور المشرف الأعلى لمنع التغيير غير المصرح به.
            </p>

            <form onSubmit={handleConfirmStepUp} style={{ display: 'grid', gap: '1rem' }}>
              <div>
                <label style={{ display: 'block', fontSize: '0.8125rem', fontWeight: 600, marginBottom: '0.375rem' }}>
                  أدخل كلمة مرور المشرف الأعلى للتأكيد:
                </label>
                <div style={{ position: 'relative' }}>
                  <input
                    type="password"
                    value={stepUpPassword}
                    onChange={(e) => setStepUpPassword(e.target.value)}
                    className="admin-input"
                    placeholder="••••••••••••"
                    required
                    autoFocus
                  />
                  <Lock size={18} style={{ position: 'absolute', insetInlineEnd: '12px', top: '13px', color: 'var(--admin-text-muted)' }} />
                </div>
              </div>

              <div style={{ display: 'flex', gap: '0.5rem', justifyContent: 'flex-end', marginTop: '0.5rem' }}>
                <button
                  type="button"
                  onClick={() => setShowStepUpModal(false)}
                  className="admin-btn admin-btn-secondary"
                >
                  إلغاء
                </button>
                <button type="submit" className="admin-btn admin-btn-primary">
                  <ShieldCheck size={18} />
                  <span>تأكيد التعديل</span>
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};
