import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import {
  Phase6SchedulingScreen,
  type AppointmentItem,
  type QueueEntryItem,
  type SlotOfferItem,
} from './Phase6SchedulingScreen';

const SAMPLE_APPOINTMENTS: AppointmentItem[] = [
  {
    id: 'appt-1',
    clinicId: 'clinic-1',
    patientId: 'pat-1',
    patientName: 'أحمد محمد',
    doctorId: 'doc-1',
    doctorName: 'د. سارة',
    categoryId: 'cat-1',
    categoryNameAr: 'كشف أسنان',
    startUtc: '2026-06-01T10:00:00Z',
    endUtc: '2026-06-01T10:30:00Z',
    status: 'Booked',
    priority: 'Vip',
    source: 'Staff',
    allowedNextStatuses: ['Confirmed', 'Arrived', 'Cancelled'],
  },
];

const SAMPLE_QUEUE: QueueEntryItem[] = [
  {
    id: 'q-1',
    appointmentId: 'appt-1',
    patientId: 'pat-1',
    patientName: 'أحمد محمد',
    doctorId: 'doc-1',
    doctorName: 'د. سارة',
    queueDate: '2026-06-01',
    orderIndex: 1,
    status: 'Waiting',
    priority: 'Vip',
    pushCount: 0,
    vipJumpCount: 1,
  },
];

const SAMPLE_OFFERS: SlotOfferItem[] = [
  {
    id: 'offer-1',
    waitingListEntryId: 'wl-1',
    patientId: 'pat-2',
    doctorId: 'doc-1',
    categoryId: 'cat-1',
    slotStartUtc: '2026-06-01T11:30:00Z',
    slotEndUtc: '2026-06-01T12:00:00Z',
    status: 'Pending',
  },
];

describe('Phase6SchedulingScreen', () => {
  it('renders only server-provided allowedNextStatuses transitions and fires status/reschedule callbacks', () => {
    const onTransitionStatus = vi.fn();
    const onReschedule = vi.fn();

    render(
      <Phase6SchedulingScreen
        appointments={SAMPLE_APPOINTMENTS}
        queueEntries={SAMPLE_QUEUE}
        slotOffers={SAMPLE_OFFERS}
        onTransitionStatus={onTransitionStatus}
        onReschedule={onReschedule}
      />
    );

    const card = screen.getByTestId('appointment-card-appt-1');
    expect(card.textContent).toContain('VIP أولوية');
    expect(card.textContent).toContain('وصول للعيادة');
    // InSession and Completed are NOT valid directly from Booked
    expect(screen.queryByText('داخل الكشف')).toBeNull();
    expect(screen.queryByText('إنهاء الكشف')).toBeNull();

    fireEvent.click(screen.getByText('وصول للعيادة'));
    expect(onTransitionStatus).toHaveBeenCalledWith('appt-1', 'Arrived');

    fireEvent.click(screen.getByText('بعد أسبوع'));
    expect(onReschedule).toHaveBeenCalledWith('appt-1', 'after_week');
  });

  it('hides queue board gracefully while keeping appointments functional when queue module is disabled', () => {
    render(
      <Phase6SchedulingScreen
        appointments={SAMPLE_APPOINTMENTS}
        queueEntries={SAMPLE_QUEUE}
        isQueueEnabled={false}
      />
    );

    expect(screen.getByTestId('appointment-card-appt-1')).toBeTruthy();
    expect(screen.getByTestId('queue-module-disabled-notice')).toBeTruthy();
    expect(screen.queryByTestId('queue-entry-q-1')).toBeNull();
  });

  it('hides all write actions in Super Admin read-only inspect mode and invokes queue/offer callbacks when active', () => {
    const { rerender } = render(
      <Phase6SchedulingScreen
        appointments={SAMPLE_APPOINTMENTS}
        queueEntries={SAMPLE_QUEUE}
        slotOffers={SAMPLE_OFFERS}
        readOnlyInspectMode={true}
      />
    );

    expect(screen.queryByText('تأكيد حجز الموعد')).toBeNull();
    expect(screen.queryByText('تأخير دور (متأخر)')).toBeNull();
    expect(screen.queryByText('قبول وحجز الموعد')).toBeNull();

    const onPushLate = vi.fn();
    const onAcceptSlotOffer = vi.fn();

    rerender(
      <Phase6SchedulingScreen
        appointments={SAMPLE_APPOINTMENTS}
        queueEntries={SAMPLE_QUEUE}
        slotOffers={SAMPLE_OFFERS}
        readOnlyInspectMode={false}
        onPushLate={onPushLate}
        onAcceptSlotOffer={onAcceptSlotOffer}
      />
    );

    fireEvent.click(screen.getByText('تأخير دور (متأخر)'));
    expect(onPushLate).toHaveBeenCalledWith('q-1');

    fireEvent.click(screen.getByText('قبول وحجز الموعد'));
    expect(onAcceptSlotOffer).toHaveBeenCalledWith('offer-1');
  });
});

