import React, { useState } from 'react';
import { Globe, Calendar, Clock, Stethoscope, Check, X, Shield } from 'lucide-react';

export const PatientPortalScreen: React.FC = () => {
  const [selectedDoctor, setSelectedDoctor] = useState('د. أحمد الرفاعي (طب وجراحة الأسنان)');
  const [selectedDate, setSelectedDate] = useState('2026-11-20');
  const [selectedSlot, setSelectedSlot] = useState('05:30 PM');
  const [booked, setBooked] = useState(false);

  const availableSlots = ['04:00 PM', '04:30 PM', '05:00 PM', '05:30 PM', '06:00 PM', '07:00 PM'];

  const handleBooking = (e: React.FormEvent) => {
    e.preventDefault();
    setBooked(true);
  };

  return (
    <div style={{ display: 'grid', gap: '1.5rem', maxWidth: '800px', margin: '0 auto' }}>
      <div style={{ textAlign: 'center' }}>
        <div
          style={{
            width: '3.5rem',
            height: '3.5rem',
            margin: '0 auto 0.75rem',
            borderRadius: 'var(--clinic-radius-card)',
            backgroundColor: 'var(--clinic-accent-soft)',
            color: 'var(--clinic-accent-primary)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
          }}
        >
          <Globe size={30} />
        </div>
        <h2 style={{ fontSize: '1.375rem', fontWeight: 700, color: 'var(--clinic-text-primary)' }}>
          بوابة المرضى والحجز الإلكتروني الذاتي (Patient Portal)
        </h2>
        <p style={{ fontSize: '0.875rem', color: 'var(--clinic-text-secondary)', marginTop: '0.25rem' }}>
          حجز المواعيد، متابعة الزيارات القادمة، والاطلاع على الخطة العلاجية بأمان
        </p>
      </div>

      {booked && (
        <div
          style={{
            padding: '1rem 1.25rem',
            backgroundColor: 'var(--clinic-status-success-bg)',
            color: 'var(--clinic-status-success)',
            borderRadius: 'var(--clinic-radius-card)',
            fontWeight: 600,
            fontSize: '0.875rem',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
            <Check size={20} />
            <span>تم تأكيد حجز موعدك بنجاح في تاريخ {selectedDate} الساعة {selectedSlot}!</span>
          </div>
          <button
            onClick={() => setBooked(false)}
            className="clinic-btn clinic-btn-secondary"
            style={{ minHeight: '30px', padding: '0.25rem 0.5rem', fontSize: '0.75rem' }}
          >
            <X size={14} />
            <span>إلغاء</span>
          </button>
        </div>
      )}

      {/* Booking Card */}
      <form onSubmit={handleBooking} className="clinic-card" style={{ padding: '2rem' }}>
        <h3 style={{ fontSize: '1.125rem', fontWeight: 700, marginBottom: '1.25rem' }}>
          حجز موعد كشف جديد
        </h3>

        <div style={{ display: 'grid', gap: '1.25rem' }}>
          <div>
            <label style={{ display: 'block', fontSize: '0.8125rem', fontWeight: 600, marginBottom: '0.375rem' }}>
              اختر الطبيب المعالج
            </label>
            <div style={{ position: 'relative' }}>
              <select
                value={selectedDoctor}
                onChange={(e) => setSelectedDoctor(e.target.value)}
                className="clinic-input"
              >
                <option value="د. أحمد الرفاعي (طب وجراحة الأسنان)">د. أحمد الرفاعي (طب وجراحة الأسنان)</option>
                <option value="د. سارة محمود (تقويم وتجميل الأسنان)">د. سارة محمود (تقويم وتجميل الأسنان)</option>
                <option value="د. كريم عبد العزيز (الطب العام)">د. كريم عبد العزيز (الطب العام)</option>
              </select>
              <Stethoscope size={18} style={{ position: 'absolute', insetInlineEnd: '12px', top: '13px', color: 'var(--clinic-text-muted)' }} />
            </div>
          </div>

          <div>
            <label style={{ display: 'block', fontSize: '0.8125rem', fontWeight: 600, marginBottom: '0.375rem' }}>
              تاريخ الزيارة المطلوب
            </label>
            <div style={{ position: 'relative' }}>
              <input
                type="date"
                value={selectedDate}
                onChange={(e) => setSelectedDate(e.target.value)}
                className="clinic-input num-tabular"
                required
              />
              <Calendar size={18} style={{ position: 'absolute', insetInlineEnd: '12px', top: '13px', color: 'var(--clinic-text-muted)' }} />
            </div>
          </div>

          <div>
            <label style={{ display: 'block', fontSize: '0.8125rem', fontWeight: 600, marginBottom: '0.5rem' }}>
              الأوقات المتاحة لهذا اليوم
            </label>
            <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
              {availableSlots.map((slot) => {
                const isSelected = selectedSlot === slot;
                return (
                  <button
                    key={slot}
                    type="button"
                    onClick={() => setSelectedSlot(slot)}
                    style={{
                      padding: '0.5rem 0.875rem',
                      borderRadius: 'var(--clinic-radius-input)',
                      border: isSelected ? '2px solid var(--clinic-accent-primary)' : '1px solid var(--clinic-border-strong)',
                      backgroundColor: isSelected ? 'var(--clinic-accent-soft)' : 'var(--clinic-bg-surface)',
                      color: isSelected ? 'var(--clinic-accent-primary)' : 'var(--clinic-text-primary)',
                      fontWeight: 600,
                      fontSize: '0.8125rem',
                      cursor: 'pointer',
                    }}
                  >
                    <span className="num-tabular">{slot}</span>
                  </button>
                );
              })}
            </div>
          </div>

          <button type="submit" className="clinic-btn clinic-btn-primary" style={{ width: '100%', marginTop: '0.75rem' }}>
            <Clock size={18} />
            <span>تأكيد حجز الموعد</span>
          </button>
        </div>

        {/* Security & Privacy Notice */}
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: '0.5rem',
            padding: '0.75rem 1rem',
            backgroundColor: 'var(--clinic-bg-subtle)',
            borderRadius: 'var(--clinic-radius-input)',
            fontSize: '0.75rem',
            color: 'var(--clinic-text-muted)',
            marginTop: '1.5rem',
          }}
        >
          <Shield size={16} color="var(--clinic-accent-primary)" />
          <span>
            كافة بياناتك الطبية تخضع لأعلى معايير الخصوصية. لا يتم تخزين أي صفحات سريرية في كاش المتصفح (Cache-Control: no-store).
          </span>
        </div>
      </form>
    </div>
  );
};
