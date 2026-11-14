import React, { useState } from 'react';
import { Users, UserPlus, Clock, CheckCircle, ArrowRight } from 'lucide-react';
import { StatusChip } from '../components/StatusChip';
import { ClinicalTable, Column } from '../components/ClinicalTable';

interface QueueItem {
  token: string;
  patientName: string;
  phone: string;
  appointmentTime: string;
  doctorRoom: string;
  waitingMinutes: number;
  status: 'Waiting' | 'Called' | 'InRoom';
}

const INITIAL_QUEUE: QueueItem[] = [
  { token: 'A-01', patientName: 'سارة عبد الله أحمد', phone: '01012345678', appointmentTime: '09:30', doctorRoom: 'عيادة 1 (د. أحمد)', waitingMinutes: 5, status: 'InRoom' },
  { token: 'A-02', patientName: 'محمد عثمان الشريف', phone: '01198765432', appointmentTime: '10:00', doctorRoom: 'عيادة 2 (د. سارة)', waitingMinutes: 18, status: 'Waiting' },
  { token: 'A-03', patientName: 'ليلى إبراهيم فؤاد', phone: '01234567890', appointmentTime: '10:30', doctorRoom: 'عيادة 1 (د. أحمد)', waitingMinutes: 10, status: 'Waiting' },
];

export const ReceptionQueueScreen: React.FC = () => {
  const [queue, setQueue] = useState<QueueItem[]>(INITIAL_QUEUE);
  const [newPatientName, setNewPatientName] = useState('');
  const [newPhone, setNewPhone] = useState('');

  const handleQuickCheckIn = (e: React.FormEvent) => {
    e.preventDefault();
    if (!newPatientName.trim()) return;

    const nextToken = `A-0${queue.length + 1}`;
    const newItem: QueueItem = {
      token: nextToken,
      patientName: newPatientName,
      phone: newPhone || 'غير مسجل',
      appointmentTime: 'مباشر (Walk-in)',
      doctorRoom: 'عيادة 1 (د. أحمد)',
      waitingMinutes: 1,
      status: 'Waiting',
    };

    setQueue([...queue, newItem]);
    setNewPatientName('');
    setNewPhone('');
  };

  const handleCallPatient = (token: string) => {
    setQueue((prev) =>
      prev.map((q) => (q.token === token ? { ...q, status: 'Called' } : q))
    );
  };

  const columns: Column<QueueItem>[] = [
    {
      key: 'token',
      header: 'رقم الانتظار',
      render: (q) => (
        <span
          className="num-tabular"
          style={{
            fontSize: '1rem',
            fontWeight: 700,
            color: 'var(--clinic-accent-primary)',
            backgroundColor: 'var(--clinic-accent-soft)',
            padding: '0.25rem 0.5rem',
            borderRadius: 'var(--clinic-radius-input)',
          }}
        >
          {q.token}
        </span>
      ),
      align: 'center',
    },
    {
      key: 'patientName',
      header: 'اسم المريض ورقم الهاتف',
      render: (q) => (
        <div>
          <div style={{ fontWeight: 600 }}>{q.patientName}</div>
          <div style={{ fontSize: '0.75rem', color: 'var(--clinic-text-muted)' }} dir="ltr">
            {q.phone}
          </div>
        </div>
      ),
    },
    {
      key: 'doctorRoom',
      header: 'العيادة والطبيب المعالج',
      render: (q) => <span>{q.doctorRoom}</span>,
    },
    {
      key: 'waitingMinutes',
      header: 'مدة الانتظار',
      render: (q) => (
        <span className="num-tabular" style={{ color: q.waitingMinutes > 15 ? 'var(--clinic-status-warning)' : 'inherit' }}>
          {q.waitingMinutes} دقيقة
        </span>
      ),
      align: 'center',
    },
    {
      key: 'status',
      header: 'الحالة',
      render: (q) => {
        if (q.status === 'InRoom') return <StatusChip status="info" label="داخل الكشف" />;
        if (q.status === 'Called') return <StatusChip status="warning" label="تمت المناداة" />;
        return <StatusChip status="success" label="ينتظر دوره" />;
      },
    },
    {
      key: 'actions',
      header: 'الإجراء',
      render: (q) => (
        <button
          onClick={() => handleCallPatient(q.token)}
          disabled={q.status === 'InRoom'}
          className="clinic-btn clinic-btn-soft"
          style={{ minHeight: '32px', padding: '0.25rem 0.625rem', fontSize: '0.75rem' }}
        >
          {q.status === 'InRoom' ? <CheckCircle size={14} /> : <ArrowRight size={14} />}
          <span>{q.status === 'InRoom' ? 'بالداخل' : 'مناداة المريض'}</span>
        </button>
      ),
      align: 'center',
    },
  ];

  return (
    <div style={{ display: 'grid', gap: '1.5rem' }}>
      <div>
        <h2 style={{ fontSize: '1.25rem', fontWeight: 700, color: 'var(--clinic-text-primary)' }}>
          الاستقبال وطابور الانتظار المباشر (Reception & Live Queue)
        </h2>
        <p style={{ fontSize: '0.875rem', color: 'var(--clinic-text-secondary)' }}>
          تسجيل وصول المرضى، المناداة الإلكترونية، وتحويل الحالات إلى غرف الكشف
        </p>
      </div>

      {/* Quick Check-in Card */}
      <div className="clinic-card">
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '1rem' }}>
          <UserPlus size={20} color="var(--clinic-accent-primary)" />
          <h3 style={{ fontSize: '1rem', fontWeight: 700 }}>تسجيل مريض جديد في الطابور فوراً</h3>
        </div>

        <form onSubmit={handleQuickCheckIn} style={{ display: 'flex', gap: '0.75rem', flexWrap: 'wrap' }}>
          <input
            type="text"
            value={newPatientName}
            onChange={(e) => setNewPatientName(e.target.value)}
            placeholder="اسم المريض الثلاثي"
            className="clinic-input"
            style={{ flex: 2, minWidth: '220px' }}
            required
          />
          <input
            type="tel"
            value={newPhone}
            onChange={(e) => setNewPhone(e.target.value)}
            placeholder="رقم الهاتف المحمول"
            className="clinic-input"
            style={{ flex: 1, minWidth: '160px' }}
            dir="ltr"
          />
          <button type="submit" className="clinic-btn clinic-btn-primary" style={{ flexShrink: 0 }}>
            <Users size={18} />
            <span>تسجيل الدخول للطابور</span>
          </button>
        </form>
      </div>

      {/* Queue Table Card */}
      <div className="clinic-card">
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1.25rem' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
            <Clock size={20} color="var(--clinic-accent-primary)" />
            <h3 style={{ fontSize: '1rem', fontWeight: 700 }}>قائمة الانتظار النشطة حالياً</h3>
          </div>
          <span style={{ fontSize: '0.75rem', color: 'var(--clinic-text-muted)' }}>
            إجمالي المنتظرين: {queue.filter((q) => q.status === 'Waiting').length}
          </span>
        </div>

        <ClinicalTable
          columns={columns}
          data={queue}
          keyExtractor={(q) => q.token}
          emptyMessage="لا يوجد مرضى في صالة الانتظار حالياً"
        />
      </div>
    </div>
  );
};
