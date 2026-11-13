import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { Phase8BillingScreen } from './Phase8BillingScreen';

describe('Phase8BillingScreen', () => {
  it('calculates net charge, requires reason for free session, and creates charge', () => {
    const onCreateCharge = vi.fn();
    render(<Phase8BillingScreen clinicId="clinic-1" onCreateCharge={onCreateCharge} />);

    // 300 session + 400 tool = 700
    expect(screen.getByTestId('charge-net-preview').textContent).toContain('700');

    // Toggle free session without reason -> blocked
    fireEvent.click(screen.getByTestId('free-session-checkbox'));
    expect(screen.getByTestId('charge-net-preview').textContent).toContain('400');

    fireEvent.click(screen.getByText('اعتماد الفاتورة'));
    expect(onCreateCharge).not.toHaveBeenCalled();

    fireEvent.change(screen.getByLabelText('سبب الجلسة المجانية'), {
      target: { value: 'متابعة ما بعد الجراحة' },
    });
    fireEvent.click(screen.getByText('اعتماد الفاتورة'));
    expect(onCreateCharge).toHaveBeenCalledWith(
      expect.objectContaining({
        clinicId: 'clinic-1',
        isFreeSession: true,
        freeSessionReason: 'متابعة ما بعد الجراحة',
        netTotalAmount: 400,
      }),
    );
  });

  it('records split payments, updates ledger balance, and closes/reopens the day', () => {
    const onRecordPayment = vi.fn();
    const onCloseDay = vi.fn();
    const onReopenDay = vi.fn();
    render(
      <Phase8BillingScreen
        clinicId="clinic-1"
        onRecordPayment={onRecordPayment}
        onCloseDay={onCloseDay}
        onReopenDay={onReopenDay}
      />,
    );

    expect(screen.getByTestId('ledger-balance').textContent).toContain('450');

    fireEvent.click(screen.getByText('التحصيل والإيصالات والمرتجعات'));
    fireEvent.change(screen.getByLabelText('مبلغ نقدي (Cash)'), { target: { value: '150' } });
    fireEvent.change(screen.getByLabelText('مبلغ بطاقة (Visa)'), { target: { value: '100' } });
    fireEvent.click(screen.getByText('تحصيل وإصدار الإيصالات'));

    expect(onRecordPayment).toHaveBeenCalledWith(
      expect.objectContaining({
        clinicId: 'clinic-1',
        splits: [
          { method: 'Cash', amount: 150 },
          { method: 'Visa', amount: 100 },
        ],
      }),
    );
    expect(screen.getByTestId('ledger-balance').textContent).toContain('200');

    fireEvent.click(screen.getByText('الموردون وإقفال اليومية'));
    fireEvent.click(screen.getByText('إقفال اليومية (أونلاين فقط)'));
    expect(onCloseDay).toHaveBeenCalledWith(
      expect.objectContaining({
        clinicId: 'clinic-1',
        closingDate: '2026-06-08',
      }),
    );
    expect(screen.getByTestId('closing-status-badge').textContent).toContain('مقفلة');

    // Reopen requires reason
    fireEvent.click(screen.getByText('إعادة فتح اليومية (أونلاين فقط)'));
    expect(onReopenDay).not.toHaveBeenCalled();

    fireEvent.change(screen.getByLabelText('سبب إعادة الفتح (يتطلب صلاحية المالك)'), {
      target: { value: 'تصحيح إيصال مسائي' },
    });
    fireEvent.click(screen.getByText('إعادة فتح اليومية (أونلاين فقط)'));
    expect(onReopenDay).toHaveBeenCalledWith(
      expect.objectContaining({
        clinicId: 'clinic-1',
        reason: 'تصحيح إيصال مسائي',
      }),
    );
    expect(screen.getByTestId('closing-status-badge').textContent).toContain('مفتوحة');
  });

  it('hides mutation actions in readOnlyInspectMode', () => {
    render(<Phase8BillingScreen clinicId="clinic-inspect" readOnlyInspectMode={true} />);

    expect(screen.getByText(/وضع الفحص للقراءة فقط/)).toBeTruthy();
    expect(screen.queryByText('اعتماد الفاتورة')).toBeNull();
    expect(screen.queryByText('حفظ في قائمة الأسعار (أونلاين فقط)')).toBeNull();
  });
});

