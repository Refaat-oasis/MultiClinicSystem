import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { Phase10PortalScreen } from './Phase10PortalScreen';

describe('Phase10PortalScreen', () => {
  it('requires documented consent before inviting patient and supports printed one-time PIN', () => {
    const onInvitePatient = vi.fn();
    render(<Phase10PortalScreen clinicId="clinic-1" onInvitePatient={onInvitePatient} />);

    // Attempt without consent
    fireEvent.click(screen.getByText('دعوة المريض إلى البوابة'));
    expect(onInvitePatient).not.toHaveBeenCalled();
    expect(screen.getByRole('alert').textContent).toContain('موافقة المريض');

    // Check consent and printed PIN without email
    fireEvent.click(screen.getByLabelText('موافقة المريض الموثقة على البوابة'));
    fireEvent.click(screen.getByLabelText('إصدار رقم سري مطبوع لمرة واحدة'));
    fireEvent.click(screen.getByText('دعوة المريض إلى البوابة'));

    expect(onInvitePatient).toHaveBeenCalledWith(
      expect.objectContaining({
        clinicId: 'clinic-1',
        consentGiven: true,
        generatePrintedPin: true,
      }),
    );
    expect(screen.getByTestId('printed-pin-card').textContent).toContain('A9K4M7P2Q5');
    expect(screen.getByTestId('portal-status-badge').textContent).toContain('مدعو');
  });

  it('toggles visibility and saves clinic portal settings while blocking writes in inspect mode', () => {
    const onUpdateSettings = vi.fn();
    const { rerender } = render(
      <Phase10PortalScreen clinicId="clinic-1" onUpdateSettings={onUpdateSettings} />,
    );

    fireEvent.click(screen.getByText('حفظ إعدادات البوابة'));
    expect(onUpdateSettings).toHaveBeenCalledTimes(1);

    rerender(
      <Phase10PortalScreen
        clinicId="clinic-1"
        readOnlyInspectMode={true}
        initialInspectSnapshot={{
          clinicId: 'clinic-1',
          totalAccountsCount: 4,
          activeAccountsCount: 2,
          invitedAccountsCount: 1,
          disabledAccountsCount: 1,
          patientViewsLast30DaysCount: 19,
          settings: {
            clinicId: 'clinic-1',
            showPricesByDefault: true,
            showDoctorNames: true,
            defaultVisitVisibility: true,
            termsTextAr: 'شروط',
            termsTextEn: 'Terms',
            welcomeMessageAr: 'أهلاً بكم',
            welcomeMessageEn: 'Welcome',
            updatedAtUtc: '2026-06-08T09:00:00Z',
          },
        }}
      />,
    );

    expect(screen.queryByText('حفظ إعدادات البوابة')).toBeNull();
    expect(screen.getByText('4')).toBeTruthy();
  });
});

