import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { Phase9TreatmentPlansScreen } from './Phase9TreatmentPlansScreen';

describe('Phase9TreatmentPlansScreen', () => {
  it('builds FixedTotal and PerSession plans and validates input', () => {
    const onCreatePlan = vi.fn();
    render(<Phase9TreatmentPlansScreen clinicId="clinic-1" onCreatePlan={onCreatePlan} />);

    fireEvent.click(screen.getByText('بناء خطة علاج جديدة'));
    expect(screen.getByTestId('plan-builder-total').textContent).toContain('6000');

    // Switch to PerSession (3 * 800 = 2400)
    fireEvent.change(screen.getByLabelText('نظام تسعير الخطة'), {
      target: { value: 'PerSession' },
    });
    expect(screen.getByTestId('plan-builder-total').textContent).toContain('2400');

    fireEvent.change(screen.getByLabelText('اسم خطة العلاج'), {
      target: { value: 'خطة علاج طبيعي 3 جلسات' },
    });
    fireEvent.click(screen.getByText('حفظ واعتماد الخطة'));

    expect(onCreatePlan).toHaveBeenCalledWith(
      expect.objectContaining({
        clinicId: 'clinic-1',
        name: 'خطة علاج طبيعي 3 جلسات',
        mode: 'PerSession',
      }),
    );
    expect(screen.getByRole('heading', { name: 'خطة علاج طبيعي 3 جلسات' })).toBeTruthy();
  });

  it('recalculates downstream session targets on slip, completes session, and collects installment', () => {
    const onRecordSessionSlip = vi.fn();
    const onCompleteSession = vi.fn();
    const onPayInstallment = vi.fn();
    render(
      <Phase9TreatmentPlansScreen
        clinicId="clinic-1"
        onRecordSessionSlip={onRecordSessionSlip}
        onCompleteSession={onCompleteSession}
        onPayInstallment={onPayInstallment}
      />,
    );

    // Slip session 1 to 2026-06-15 (+5 days), session 2 should move from 2026-07-01 to 2026-07-06
    expect(screen.getByTestId('session-target-2').textContent).toBe('2026-07-01');
    fireEvent.change(screen.getByLabelText('سبب التأجيل/الانزلاق #1'), {
      target: { value: 'تأخر المريض 5 أيام' },
    });
    fireEvent.click(screen.getByText('تسجيل انزلاق وإعادة حساب النوافذ #1'));

    expect(onRecordSessionSlip).toHaveBeenCalledWith(
      expect.objectContaining({
        clinicId: 'clinic-1',
        actualOrSlippedDate: '2026-06-15',
        reason: 'تأخر المريض 5 أيام',
      }),
    );
    expect(screen.getByTestId('session-target-2').textContent).toBe('2026-07-06');

    // Complete session 1 -> completion updates to 33.33% (1/3)
    fireEvent.click(screen.getByText('إتمام الجلسة #1'));
    expect(onCompleteSession).toHaveBeenCalled();
    expect(screen.getByTestId('plan-completion-pct').textContent).toContain('1/3');

    // Pay installment #2 (4500) -> remaining balance drops from 9000 to 4500
    expect(screen.getByTestId('plan-remaining-balance').textContent).toContain('9000');
    fireEvent.click(screen.getByText('تحصيل القسط #2'));
    expect(onPayInstallment).toHaveBeenCalledWith(
      expect.objectContaining({
        clinicId: 'clinic-1',
        amount: 4500,
      }),
    );
    expect(screen.getByTestId('plan-remaining-balance').textContent).toContain('4500');
  });

  it('hides mutation actions in readOnlyInspectMode', () => {
    render(<Phase9TreatmentPlansScreen clinicId="clinic-inspect" readOnlyInspectMode={true} />);

    expect(screen.getByText(/وضع الفحص للقراءة فقط/)).toBeTruthy();
    expect(screen.queryByText('بناء خطة علاج جديدة')).toBeNull();
    expect(screen.queryByText('إتمام الجلسة #1')).toBeNull();
    expect(screen.queryByText('تحصيل القسط #2')).toBeNull();
  });
});

