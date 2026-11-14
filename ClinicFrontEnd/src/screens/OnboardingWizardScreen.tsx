import React, { useState } from 'react';
import { Building2, Stethoscope, Clock, Receipt, Check, ArrowLeft, ArrowRight } from 'lucide-react';

export const OnboardingWizardScreen: React.FC = () => {
  const [currentStep, setCurrentStep] = useState(1);
  const [clinicName, setClinicName] = useState('عيادة المستقبل التخصصية');
  const [slug, setSlug] = useState('al-mustaqbal-clinic');
  const [currency, setCurrency] = useState('EGP');
  const [specialty, setSpecialty] = useState('dental');
  const [startHour, setStartHour] = useState('09:00');
  const [endHour, setEndHour] = useState('21:00');
  const [receiptPrefix, setReceiptPrefix] = useState('REC-2026');
  const [blockStart, setBlockStart] = useState(1001);
  const [isCompleted, setIsCompleted] = useState(false);

  const steps = [
    { num: 1, label: 'هوية العيادة', icon: <Building2 size={16} /> },
    { num: 2, label: 'التخصص الطبي', icon: <Stethoscope size={16} /> },
    { num: 3, label: 'ساعات العمل والغرف', icon: <Clock size={16} /> },
    { num: 4, label: 'نطاقات الترقيم (Blocks)', icon: <Receipt size={16} /> },
  ];

  const handleNext = () => {
    if (currentStep < 4) {
      setCurrentStep(currentStep + 1);
    } else {
      setIsCompleted(true);
    }
  };

  const handlePrev = () => {
    if (currentStep > 1) {
      setCurrentStep(currentStep - 1);
    }
  };

  return (
    <div style={{ display: 'grid', gap: '1.5rem' }}>
      <div>
        <h2 style={{ fontSize: '1.25rem', fontWeight: 700, color: 'var(--clinic-text-primary)' }}>
          معالج إعداد وتهيئة العيادة الجديدة (Clinic Onboarding Wizard)
        </h2>
        <p style={{ fontSize: '0.875rem', color: 'var(--clinic-text-secondary)' }}>
          إتمام المتطلبات المسبقة لنقل العيادة من قيد التهيئة (PendingOnboarding) إلى الحالة النشطة (Active)
        </p>
      </div>

      {isCompleted && (
        <div
          style={{
            padding: '1rem 1.25rem',
            backgroundColor: 'var(--clinic-status-success-bg)',
            color: 'var(--clinic-status-success)',
            borderRadius: 'var(--clinic-radius-input)',
            fontWeight: 600,
            fontSize: '0.875rem',
            display: 'flex',
            alignItems: 'center',
            gap: '0.5rem',
          }}
        >
          <Check size={20} />
          <span>تهانينا! تم استكمال معالج التهيئة بنجاح، وأصبحت العيادة جاهزة لاستقبال المرضى وحجز المواعيد.</span>
        </div>
      )}

      {/* Stepper Bar */}
      <div
        className="clinic-card"
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          padding: '1rem 1.5rem',
          flexWrap: 'wrap',
          gap: '1rem',
        }}
      >
        {steps.map((st) => {
          const isActive = currentStep === st.num;
          const isDone = currentStep > st.num;
          return (
            <div
              key={st.num}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: '0.5rem',
                color: isActive
                  ? 'var(--clinic-accent-primary)'
                  : isDone
                  ? 'var(--clinic-status-success)'
                  : 'var(--clinic-text-muted)',
                fontWeight: isActive ? 700 : 500,
                fontSize: '0.875rem',
              }}
            >
              <div
                style={{
                  width: '28px',
                  height: '28px',
                  borderRadius: '50%',
                  backgroundColor: isActive
                    ? 'var(--clinic-accent-primary)'
                    : isDone
                    ? 'var(--clinic-status-success)'
                    : 'var(--clinic-bg-subtle)',
                  color: isActive || isDone ? '#ffffff' : 'inherit',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  fontSize: '0.8125rem',
                }}
              >
                {isDone ? <Check size={14} /> : st.num}
              </div>
              <span>{st.label}</span>
            </div>
          );
        })}
      </div>

      {/* Step Content */}
      <div className="clinic-card" style={{ padding: '2rem' }}>
        {currentStep === 1 && (
          <div style={{ display: 'grid', gap: '1.25rem' }}>
            <h3 style={{ fontSize: '1.125rem', fontWeight: 700 }}>1. هوية العيادة والاسم البرمجي (Slug)</h3>
            <div>
              <label style={{ display: 'block', fontSize: '0.8125rem', fontWeight: 600, marginBottom: '0.375rem' }}>
                اسم العيادة الرسمي
              </label>
              <input
                type="text"
                value={clinicName}
                onChange={(e) => setClinicName(e.target.value)}
                className="clinic-input"
                required
              />
            </div>
            <div>
              <label style={{ display: 'block', fontSize: '0.8125rem', fontWeight: 600, marginBottom: '0.375rem' }}>
                الاسم الفريد للرابط (Slug)
              </label>
              <input
                type="text"
                value={slug}
                onChange={(e) => setSlug(e.target.value)}
                className="clinic-input"
                dir="ltr"
                required
              />
            </div>
            <div>
              <label style={{ display: 'block', fontSize: '0.8125rem', fontWeight: 600, marginBottom: '0.375rem' }}>
                العملة الأساسية للمعاملات
              </label>
              <select
                value={currency}
                onChange={(e) => setCurrency(e.target.value)}
                className="clinic-input"
                style={{ maxWidth: '240px' }}
              >
                <option value="EGP">EGP — الجنيه المصري</option>
                <option value="SAR">SAR — الريال السعودي</option>
                <option value="USD">USD — الدولار الأمريكي</option>
              </select>
            </div>
          </div>
        )}

        {currentStep === 2 && (
          <div style={{ display: 'grid', gap: '1.25rem' }}>
            <h3 style={{ fontSize: '1.125rem', fontWeight: 700 }}>2. التخصص الطبي للعيادة ومكونات الفحص</h3>
            <div>
              <label style={{ display: 'block', fontSize: '0.8125rem', fontWeight: 600, marginBottom: '0.75rem' }}>
                اختر التخصص الطبي لتفعيل الإضافات المناسبة:
              </label>
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '1rem' }}>
                {[
                  { code: 'dental', name: 'طب وجراحة الأسنان', desc: 'تفعيل مخطط الأسنان التفاعلي (Odontogram)' },
                  { code: 'cardiology', name: 'أمراض القلب والأوعية', desc: 'تفعيل سجل تخطيط القلب ومؤشرات الضغط' },
                  { code: 'general_practice', name: 'الطب العام والباطنية', desc: 'فحص العلامات الحيوية والروشتات' },
                ].map((spec) => (
                  <div
                    key={spec.code}
                    onClick={() => setSpecialty(spec.code)}
                    style={{
                      padding: '1.25rem',
                      borderRadius: 'var(--clinic-radius-input)',
                      border: specialty === spec.code ? '2px solid var(--clinic-accent-primary)' : '1px solid var(--clinic-border-strong)',
                      backgroundColor: specialty === spec.code ? 'var(--clinic-accent-soft)' : 'var(--clinic-bg-surface)',
                      cursor: 'pointer',
                    }}
                  >
                    <div style={{ fontWeight: 700, color: specialty === spec.code ? 'var(--clinic-accent-primary)' : 'inherit' }}>
                      {spec.name}
                    </div>
                    <div style={{ fontSize: '0.75rem', color: 'var(--clinic-text-muted)', marginTop: '0.375rem' }}>
                      {spec.desc}
                    </div>
                  </div>
                ))}
              </div>
            </div>
          </div>
        )}

        {currentStep === 3 && (
          <div style={{ display: 'grid', gap: '1.25rem' }}>
            <h3 style={{ fontSize: '1.125rem', fontWeight: 700 }}>3. مواعيد العمل اليومية</h3>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '1rem' }}>
              <div>
                <label style={{ display: 'block', fontSize: '0.8125rem', fontWeight: 600, marginBottom: '0.375rem' }}>
                  بداية دوام العيادة
                </label>
                <input
                  type="time"
                  value={startHour}
                  onChange={(e) => setStartHour(e.target.value)}
                  className="clinic-input num-tabular"
                />
              </div>
              <div>
                <label style={{ display: 'block', fontSize: '0.8125rem', fontWeight: 600, marginBottom: '0.375rem' }}>
                  نهاية الدوام
                </label>
                <input
                  type="time"
                  value={endHour}
                  onChange={(e) => setEndHour(e.target.value)}
                  className="clinic-input num-tabular"
                />
              </div>
            </div>
          </div>
        )}

        {currentStep === 4 && (
          <div style={{ display: 'grid', gap: '1.25rem' }}>
            <h3 style={{ fontSize: '1.125rem', fontWeight: 700 }}>4. نطاقات الترقيم المالي والملفات (Number Blocks)</h3>
            <p style={{ fontSize: '0.8125rem', color: 'var(--clinic-text-secondary)' }}>
              وفق القواعد الصارمة للمنظومة: يتم حجز كتل الترقيم مسبقاً (Block Allocation) لتفادي أي تكرار أثناء العمل دون اتصال. يُمنع توليد الأرقام عبر <code>MAX()+1</code>.
            </p>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '1rem' }}>
              <div>
                <label style={{ display: 'block', fontSize: '0.8125rem', fontWeight: 600, marginBottom: '0.375rem' }}>
                  بادئة إيصالات القبض (Receipt Prefix)
                </label>
                <input
                  type="text"
                  value={receiptPrefix}
                  onChange={(e) => setReceiptPrefix(e.target.value)}
                  className="clinic-input"
                  dir="ltr"
                />
              </div>
              <div>
                <label style={{ display: 'block', fontSize: '0.8125rem', fontWeight: 600, marginBottom: '0.375rem' }}>
                  رقم بداية الكتلة المخصصة
                </label>
                <input
                  type="number"
                  value={blockStart}
                  onChange={(e) => setBlockStart(Number(e.target.value))}
                  className="clinic-input num-tabular"
                />
              </div>
            </div>
          </div>
        )}

        {/* Wizard Controls */}
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: '2rem', paddingTop: '1.5rem', borderTop: '1px solid var(--clinic-border-subtle)' }}>
          <button
            type="button"
            onClick={handlePrev}
            disabled={currentStep === 1}
            className="clinic-btn clinic-btn-secondary"
          >
            <ArrowRight size={16} />
            <span>السابق</span>
          </button>

          <button
            type="button"
            onClick={handleNext}
            className="clinic-btn clinic-btn-primary"
            style={{ minWidth: '140px' }}
          >
            <span>{currentStep === 4 ? 'إكمال التهيئة والتشغيل' : 'التالي'}</span>
            {currentStep < 4 ? <ArrowLeft size={16} /> : <Check size={16} />}
          </button>
        </div>
      </div>
    </div>
  );
};
