import React, { useState } from 'react';
import { UserCheck, Heart, AlertTriangle, FileText, Calendar, Plus } from 'lucide-react';
import { AlertBanner } from '../components/AlertBanner';
import { StatusChip } from '../components/StatusChip';
import { ClinicalTable, Column } from '../components/ClinicalTable';

interface PatientVisitRecord {
  id: string;
  date: string;
  doctor: string;
  diagnosis: string;
  prescription: string;
  totalCharge: number;
}

const PAST_VISITS: PatientVisitRecord[] = [
  { id: 'v-01', date: '2026-11-02', doctor: 'د. أحمد الرفاعي', diagnosis: 'التهاب عصب السن الأيسر السفلي (Pulpitis)', prescription: 'أموكسيسيلين 500 ملغ (فحص الحساسية سليم)', totalCharge: 450 },
  { id: 'v-02', date: '2026-08-14', doctor: 'د. أحمد الرفاعي', diagnosis: 'تسوس سطحي وحشوة تجميلية', prescription: 'مسكن باراسيتامول 1000 ملغ', totalCharge: 350 },
  { id: 'v-03', date: '2026-03-10', doctor: 'د. كريم عبد العزيز', diagnosis: 'فحص دوري وارتفاع طفيف في ضغط الدم', prescription: 'متابعة الحمية وأملاح الطعام', totalCharge: 200 },
];

export const PatientEhrScreen: React.FC = () => {
  const [selectedPatient] = useState({
    fileNumber: 'P-2026-0042',
    name: 'سارة عبد الله أحمد',
    nationalId: '29804151234567',
    age: 28,
    bloodType: 'O+',
    allergies: ['حساسية مفرطة من البنسلين (Penicillin Allergy)', 'حساسية الأسبرين ومضادات الالتهاب (Aspirin/NSAIDs)'],
    chronicDiseases: ['ربو تحسسي خفيف'],
    lastVitals: { bp: '120/80 mmHg', hr: '74 bpm', temp: '36.8 °C', weight: '65 kg' },
  });

  const columns: Column<PatientVisitRecord>[] = [
    {
      key: 'date',
      header: 'تاريخ الزيارة',
      render: (v) => <span className="num-tabular" style={{ fontWeight: 600 }}>{v.date}</span>,
    },
    {
      key: 'doctor',
      header: 'الطبيب المعالج',
      render: (v) => <span>{v.doctor}</span>,
    },
    {
      key: 'diagnosis',
      header: 'التشخيص الطبي',
      render: (v) => <span style={{ fontWeight: 600 }}>{v.diagnosis}</span>,
    },
    {
      key: 'prescription',
      header: 'الروشتة / العلاج',
      render: (v) => <span style={{ color: 'var(--clinic-text-secondary)', fontSize: '0.8125rem' }}>{v.prescription}</span>,
    },
    {
      key: 'totalCharge',
      header: 'الرسوم',
      render: (v) => (
        <span className="num-tabular" style={{ fontWeight: 700, color: 'var(--clinic-accent-primary)' }}>
          {v.totalCharge} ج.م
        </span>
      ),
      align: 'left',
    },
  ];

  return (
    <div style={{ display: 'grid', gap: '1.5rem' }}>
      <div>
        <h2 style={{ fontSize: '1.25rem', fontWeight: 700, color: 'var(--clinic-text-primary)' }}>
          ملف المريض السريري الإلكتروني (EHR Patient Record)
        </h2>
        <p style={{ fontSize: '0.875rem', color: 'var(--clinic-text-secondary)' }}>
          السجل الطبي الشامل، التاريخ المرضي، وفحص موانع الاستعمال الدوائية
        </p>
      </div>

      {/* Mandatory Red Allergy Banner */}
      {selectedPatient.allergies.length > 0 && (
        <AlertBanner
          isAllergy={true}
          title="تحذير حساسية حرجة غير قابل للإغلاق!"
          message={`المريض يعاني من: ${selectedPatient.allergies.join(' • ')}. يُحظر وصف البنسلين ومشتقاته منعاً لحدوث صدمة تحسسية.`}
        />
      )}

      {/* Patient Header Card */}
      <div className="clinic-card" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: '1.5rem' }}>
        <div style={{ display: 'flex', gap: '1rem', alignItems: 'center' }}>
          <div
            style={{
              width: '4rem',
              height: '4rem',
              borderRadius: 'var(--clinic-radius-card)',
              backgroundColor: 'var(--clinic-accent-soft)',
              color: 'var(--clinic-accent-primary)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              fontSize: '1.5rem',
              fontWeight: 700,
            }}
          >
            {selectedPatient.name.charAt(0)}
          </div>

          <div>
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
              <h3 style={{ fontSize: '1.25rem', fontWeight: 700 }}>{selectedPatient.name}</h3>
              <StatusChip status="info" label={`رقم الملف: ${selectedPatient.fileNumber}`} showDot={false} />
            </div>
            <div style={{ display: 'flex', gap: '1rem', fontSize: '0.8125rem', color: 'var(--clinic-text-secondary)', marginTop: '0.25rem' }}>
              <span>الرقم القومي: <span className="num-tabular">{selectedPatient.nationalId}</span></span>
              <span>•</span>
              <span>العمر: <span className="num-tabular">{selectedPatient.age} سنة</span></span>
              <span>•</span>
              <span>فصيلة الدم: <span style={{ fontWeight: 700, color: '#dc2626' }}>{selectedPatient.bloodType}</span></span>
            </div>
          </div>
        </div>

        <button className="clinic-btn clinic-btn-primary">
          <Plus size={18} />
          <span>بدء كشف طبي جديد</span>
        </button>
      </div>

      {/* Vital Signs Grid */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))', gap: '1rem' }}>
        <div className="clinic-card" style={{ padding: '1rem' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', color: 'var(--clinic-text-secondary)', fontSize: '0.8125rem' }}>
            <Heart size={16} color="var(--clinic-accent-primary)" />
            <span>ضغط الدم (Blood Pressure)</span>
          </div>
          <div className="num-tabular" style={{ fontSize: '1.25rem', fontWeight: 700, marginTop: '0.25rem' }}>
            {selectedPatient.lastVitals.bp}
          </div>
        </div>

        <div className="clinic-card" style={{ padding: '1rem' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', color: 'var(--clinic-text-secondary)', fontSize: '0.8125rem' }}>
            <ActivityIcon size={16} color="#d97706" />
            <span>نبض القلب (Pulse)</span>
          </div>
          <div className="num-tabular" style={{ fontSize: '1.25rem', fontWeight: 700, marginTop: '0.25rem' }}>
            {selectedPatient.lastVitals.hr}
          </div>
        </div>

        <div className="clinic-card" style={{ padding: '1rem' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', color: 'var(--clinic-text-secondary)', fontSize: '0.8125rem' }}>
            <AlertTriangle size={16} color="#2563eb" />
            <span>درجة الحرارة (Temp)</span>
          </div>
          <div className="num-tabular" style={{ fontSize: '1.25rem', fontWeight: 700, marginTop: '0.25rem' }}>
            {selectedPatient.lastVitals.temp}
          </div>
        </div>

        <div className="clinic-card" style={{ padding: '1rem' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', color: 'var(--clinic-text-secondary)', fontSize: '0.8125rem' }}>
            <UserCheck size={16} color="#16a34a" />
            <span>الوزن المسجل (Weight)</span>
          </div>
          <div className="num-tabular" style={{ fontSize: '1.25rem', fontWeight: 700, marginTop: '0.25rem' }}>
            {selectedPatient.lastVitals.weight}
          </div>
        </div>
      </div>

      {/* Visit History */}
      <div className="clinic-card">
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '1.25rem' }}>
          <Calendar size={20} color="var(--clinic-accent-primary)" />
          <h3 style={{ fontSize: '1rem', fontWeight: 700 }}>سجل الزيارات والروشتات السابقة</h3>
        </div>

        <ClinicalTable
          columns={columns}
          data={PAST_VISITS}
          keyExtractor={(v) => v.id}
        />
      </div>
    </div>
  );
};

function ActivityIcon(props: { size?: number; color?: string }) {
  return <FileText size={props.size || 16} color={props.color || 'currentColor'} />;
}
