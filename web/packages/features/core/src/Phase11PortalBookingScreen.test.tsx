import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { Phase11PortalBookingScreen } from './Phase11PortalBookingScreen';

describe('Phase11PortalBookingScreen', () => {
  it('reviews pending portal requests, updates booking rules, and toggles patient online-booking block', () => {
    const onReviewRequest = vi.fn();
    const onUpdateSettings = vi.fn();
    const onSetPatientBlock = vi.fn();

    render(
      <Phase11PortalBookingScreen
        clinicId="clinic-1"
        onReviewRequest={onReviewRequest}
        onUpdateSettings={onUpdateSettings}
        onSetPatientBlock={onSetPatientBlock}
      />,
    );

    // Approve pending request
    fireEvent.change(screen.getByLabelText('ملاحظة المراجعة للطلب SM-0012'), {
      target: { value: 'موعد مؤكد مع الدكتور عمر' },
    });
    fireEvent.click(screen.getByText('موافقة وتأكيد'));
    expect(onReviewRequest).toHaveBeenCalledWith({
      clinicId: 'clinic-1',
      appointmentId: 'req-demo-1',
      action: 'approve',
      reviewNotes: 'موعد مؤكد مع الدكتور عمر',
    });
    expect(screen.getByTestId('portal-request-status-req-demo-1').textContent).toContain('مؤكد');

    // Update mode and save booking settings
    fireEvent.change(screen.getByLabelText('وضع اعتماد الحجز'), {
      target: { value: 'RequiresApproval' },
    });
    fireEvent.click(screen.getByText('حفظ قواعد الحجز الأونلاين'));
    expect(onUpdateSettings).toHaveBeenCalledWith(
      expect.objectContaining({
        clinicId: 'clinic-1',
        mode: 'RequiresApproval',
      }),
    );

    // Unblock blocked patient (NoShowCount 3 -> unblocked)
    expect(screen.getByTestId('patient-noshow-count').textContent).toContain('3');
    expect(screen.getByTestId('patient-booking-block-badge').textContent).toContain('محظور');
    fireEvent.click(screen.getByText('رفع الحظر وإعادة التفعيل'));
    expect(onSetPatientBlock).toHaveBeenCalledWith({
      clinicId: 'clinic-1',
      patientId: 'pat-demo-2',
      blocked: false,
      reason: null,
    });
    expect(screen.getByTestId('patient-booking-block-badge').textContent).toContain('مسموح');
  });

  it('disables write controls in Super Admin readOnlyInspectMode', () => {
    render(
      <Phase11PortalBookingScreen
        clinicId="clinic-1"
        readOnlyInspectMode={true}
        initialInspectSnapshot={{
          clinicId: 'clinic-1',
          pendingRequestsCount: 2,
          todayPortalBookingsCount: 5,
          blockedPatientsCount: 1,
          pendingRequests: [],
          settings: {
            clinicId: 'clinic-1',
            mode: 'RequiresApproval',
            minLeadTimeMinutes: 60,
            maxDaysAhead: 14,
            slotStepMinutes: 30,
            maxActiveFutureBookingsPerPatient: 2,
            maxBookingsPerDay: 20,
            cancellationCutoffHours: 12,
            autoBlockAfterNoShows: 3,
            approvalHoldHours: 12,
            showPricesWhenAvailable: true,
            updatedAtUtc: '2026-08-10T08:00:00Z',
          },
        }}
      />,
    );

    expect(screen.getByText('وضع الفحص للقراءة فقط — حجوزات البوابة')).toBeTruthy();
    expect(screen.queryByText('حفظ قواعد الحجز الأونلاين')).toBeNull();
    expect((screen.getByLabelText('وضع اعتماد الحجز') as HTMLSelectElement).disabled).toBe(true);
  });
});
