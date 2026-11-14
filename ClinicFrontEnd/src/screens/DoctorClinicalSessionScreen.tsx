import React, { useState } from 'react';
import { Stethoscope, Clock, ShieldAlert, Check, Plus, Trash2 } from 'lucide-react';
import { AlertBanner } from '../components/AlertBanner';
import { StatusChip } from '../components/StatusChip';
import { DentalJawOdontogram3D, ToothData } from '../components/DentalJawOdontogram3D';

interface PrescriptionLine {
  drugName: string;
  dosage: string;
  frequency: string;
  duration: string;
  isAllergyRisk: boolean;
}

export const DoctorClinicalSessionScreen: React.FC = () => {
  const sessionMinutes = 14;
  const [diagnosis, setDiagnosis] = useState('التهاب حاد في عصب الضرس الأول السفلي (K04.0 Pulpitis)');
  const [doctorNotes, setDoctorNotes] = useState('تم فحص الحيوية وتبين وجود استجابة متأخرة للحرارة. يحتاج تنظيف قنوات عصب.');
  const [selectedTooth, setSelectedTooth] = useState<number | null>(36);
  const [prescriptionLines, setPrescriptionLines] = useState<PrescriptionLine[]>([
    { drugName: 'Ibuprofen 400mg', dosage: 'قرص واحد', frequency: 'كل 8 ساعات بعد الأكل', duration: '5 أيام', isAllergyRisk: true },
    { drugName: 'Chlorhexidine Mouthwash 0.12%', dosage: '15 مل', frequency: 'مرتين يومياً', duration: '7 أيام', isAllergyRisk: false },
  ]);
  const [allergyOverrideReason, setAllergyOverrideReason] = useState('');
  const [savedFeedback, setSavedFeedback] = useState(false);

  const handleSaveSession = (e: React.FormEvent) => {
    e.preventDefault();
    if (prescriptionLines.some((p) => p.isAllergyRisk) && !allergyOverrideReason.trim()) {
      alert('تنبيه أمان: يوجد دواء يتعارض مع حساسية المريض المسجلة! يلزم كتابة سبب التجاوز السريري (Clinical Override).');
      return;
    }

    setSavedFeedback(true);
    setTimeout(() => setSavedFeedback(false), 4000);
  };

  const removePrescriptionLine = (index: number) => {
    setPrescriptionLines(prescriptionLines.filter((_, i) => i !== index));
  };

  return (
    <div style={{ display: 'grid', gap: '1.5rem' }}>
      {/* Session Top Bar with Live Timer */}
      <div
        className="clinic-card"
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          flexWrap: 'wrap',
          gap: '1rem',
          backgroundColor: 'var(--clinic-bg-subtle)',
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
          <div
            style={{
              width: '2.5rem',
              height: '2.5rem',
              borderRadius: 'var(--clinic-radius-input)',
              backgroundColor: 'var(--clinic-accent-soft)',
              color: 'var(--clinic-accent-primary)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
            }}
          >
            <Stethoscope size={22} />
          </div>
          <div>
            <h2 style={{ fontSize: '1.125rem', fontWeight: 700 }}>جلسة المعاينة الطبية المباشرة</h2>
            <span style={{ fontSize: '0.8125rem', color: 'var(--clinic-text-secondary)' }}>
              المريض: <strong>سارة عبد الله أحمد</strong> (ملف: P-2026-0042)
            </span>
          </div>
        </div>

        <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
          <div
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: '0.375rem',
              padding: '0.375rem 0.75rem',
              backgroundColor: 'var(--clinic-bg-surface)',
              borderRadius: 'var(--clinic-radius-pill)',
              border: '1px solid var(--clinic-border-strong)',
              fontSize: '0.8125rem',
              fontWeight: 600,
            }}
          >
            <Clock size={16} color="var(--clinic-accent-primary)" />
            <span>مدة الجلسة: <span className="num-tabular">{sessionMinutes} دقيقة</span></span>
          </div>

          {selectedTooth && (
            <span
              style={{
                fontSize: '0.75rem',
                fontWeight: 700,
                color: 'var(--clinic-accent-primary)',
                backgroundColor: 'var(--clinic-bg-surface)',
                padding: '0.25rem 0.5rem',
                borderRadius: 'var(--clinic-radius-pill)',
                border: '1px solid var(--clinic-border-strong)',
              }}
            >
              السن المستهدف: #{selectedTooth}
            </span>
          )}

          <StatusChip status="info" label="جلسة نشطة" />
        </div>
      </div>

      {/* Mandatory Red Allergy Banner */}
      <AlertBanner
        isAllergy={true}
        title="تنبيه سريري حرج — فحص الحساسية التلقائي"
        message="المريض مسجل لديه حساسية من مشتقات البنسلين والأسبرين. أي وصفة علاجية تحتوي على هذه المواد سيتم إيقافها برمجياً حتى يتم تأكيد التجاوز السريري."
      />

      {savedFeedback && (
        <div
          style={{
            padding: '0.875rem 1.25rem',
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
          <Check size={18} />
          <span>تم حفظ بيانات الجلسة السريرية والروشتة الطبية بنجاح!</span>
        </div>
      )}

      {/* 3D Dual-Arch Dental Odontogram (Specialty Module View) */}
      <DentalJawOdontogram3D
        onSelectTooth={(tooth: ToothData) => {
          setSelectedTooth(tooth.number);
          if (tooth.notes) {
            setDoctorNotes(`السن #${tooth.number} (${tooth.nameAr}): ${tooth.notes}`);
          }
        }}
      />

      {/* Clinical Notes & Diagnosis Form */}
      <form onSubmit={handleSaveSession} style={{ display: 'grid', gap: '1.25rem' }}>
        <div className="clinic-card">
          <h3 style={{ fontSize: '1rem', fontWeight: 700, marginBottom: '1rem' }}>
            التشخيص والملاحظات الطبية
          </h3>

          <div style={{ display: 'grid', gap: '1rem' }}>
            <div>
              <label style={{ display: 'block', fontSize: '0.8125rem', fontWeight: 600, marginBottom: '0.375rem' }}>
                التشخيص الطبي (ICD-10)
              </label>
              <input
                type="text"
                value={diagnosis}
                onChange={(e) => setDiagnosis(e.target.value)}
                className="clinic-input"
                required
              />
            </div>

            <div>
              <label style={{ display: 'block', fontSize: '0.8125rem', fontWeight: 600, marginBottom: '0.375rem' }}>
                ملاحظات الطبيب وتفاصيل الإجراء السريري
              </label>
              <textarea
                value={doctorNotes}
                onChange={(e) => setDoctorNotes(e.target.value)}
                className="clinic-input"
                rows={3}
                style={{ minHeight: '80px', resize: 'vertical' }}
              />
            </div>
          </div>
        </div>

        {/* Prescription Lines */}
        <div className="clinic-card">
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1rem' }}>
            <h3 style={{ fontSize: '1rem', fontWeight: 700 }}>الروشتة العلاجية (e-Prescription)</h3>
            <button
              type="button"
              onClick={() => {
                setPrescriptionLines([
                  ...prescriptionLines,
                  { drugName: 'Paracetamol 500mg', dosage: 'قرصين', frequency: 'عند اللزوم', duration: '3 أيام', isAllergyRisk: false },
                ]);
              }}
              className="clinic-btn clinic-btn-soft"
              style={{ minHeight: '32px', padding: '0.25rem 0.625rem', fontSize: '0.75rem' }}
            >
              <Plus size={14} />
              <span>إضافة صنف دوائي</span>
            </button>
          </div>

          <div style={{ display: 'grid', gap: '0.75rem' }}>
            {prescriptionLines.map((line, idx) => (
              <div
                key={idx}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'space-between',
                  padding: '0.75rem 1rem',
                  borderRadius: 'var(--clinic-radius-input)',
                  backgroundColor: line.isAllergyRisk ? '#fef2f2' : 'var(--clinic-bg-subtle)',
                  border: line.isAllergyRisk ? '1px solid #dc2626' : '1px solid var(--clinic-border-subtle)',
                  flexWrap: 'wrap',
                  gap: '0.5rem',
                }}
              >
                <div>
                  <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                    <strong style={{ fontSize: '0.875rem', color: line.isAllergyRisk ? '#dc2626' : 'inherit' }}>
                      {line.drugName}
                    </strong>
                    {line.isAllergyRisk && (
                      <span
                        style={{
                          fontSize: '0.6875rem',
                          backgroundColor: '#dc2626',
                          color: '#ffffff',
                          fontWeight: 700,
                          padding: '0.125rem 0.375rem',
                          borderRadius: '4px',
                        }}
                      >
                        تعارض حساسية!
                      </span>
                    )}
                  </div>
                  <div style={{ fontSize: '0.75rem', color: 'var(--clinic-text-secondary)', marginTop: '0.25rem' }}>
                    الجرعة: {line.dosage} • التكرار: {line.frequency} • المدة: {line.duration}
                  </div>
                </div>

                <button
                  type="button"
                  onClick={() => removePrescriptionLine(idx)}
                  style={{
                    background: 'none',
                    border: 'none',
                    color: 'var(--clinic-status-danger)',
                    cursor: 'pointer',
                    padding: '0.25rem',
                  }}
                  title="حذف"
                >
                  <Trash2 size={16} />
                </button>
              </div>
            ))}
          </div>

          {/* Clinical Override Field */}
          {prescriptionLines.some((p) => p.isAllergyRisk) && (
            <div style={{ marginTop: '1.25rem', padding: '1rem', borderRadius: 'var(--clinic-radius-input)', backgroundColor: '#fffbeb', border: '1px solid #d97706' }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', color: '#92400e', marginBottom: '0.5rem' }}>
                <ShieldAlert size={18} />
                <strong style={{ fontSize: '0.8125rem' }}>سبب التجاوز السريري الإلزامي (Clinical Override Reason):</strong>
              </div>
              <input
                type="text"
                value={allergyOverrideReason}
                onChange={(e) => setAllergyOverrideReason(e.target.value)}
                placeholder="اكتب التبرير الطبي لوصف الدواء رغم التحسس (مثال: تم إعطاء مضاد هيستامين وقائي)..."
                className="clinic-input"
                style={{ backgroundColor: '#ffffff', borderColor: '#d97706' }}
              />
            </div>
          )}
        </div>

        {/* Save Button */}
        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.75rem' }}>
          <button type="submit" className="clinic-btn clinic-btn-primary" style={{ minWidth: '180px' }}>
            <Check size={18} />
            <span>إنهاء وحفظ الجلسة الطبية</span>
          </button>
        </div>
      </form>
    </div>
  );
};
