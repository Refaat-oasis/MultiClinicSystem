import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { Phase12InventoryScreen } from './Phase12InventoryScreen';

function renderWithQuery(ui: React.ReactElement) {
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(<QueryClientProvider client={qc}>{ui}</QueryClientProvider>);
}

describe('Phase12InventoryScreen', () => {
  it('recalculates unit cost live when pack size/price changes, submits purchase, adjusts stock, and resolves alert', () => {
    const onReceivePurchaseInvoice = vi.fn();
    const onAdjustStock = vi.fn();
    const onResolveAlert = vi.fn();
    const onUpdateSettings = vi.fn();

    renderWithQuery(
      <Phase12InventoryScreen
        clinicId="clinic-1"
        onReceivePurchaseInvoice={onReceivePurchaseInvoice}
        onAdjustStock={onAdjustStock}
        onResolveAlert={onResolveAlert}
        onUpdateSettings={onUpdateSettings}
      />,
    );

    // Edit pack size (30 -> 25) and pack price (300 -> 750) => unit cost = 750 / 25 = 30
    fireEvent.change(screen.getByLabelText('حجم العبوة'), { target: { value: '25' } });
    fireEvent.change(screen.getByLabelText('سعر العبوة'), { target: { value: '750' } });
    expect(screen.getByTestId('computed-unit-cost').textContent).toBe('30');

    fireEvent.click(screen.getByText('استلام فاتورة الشراء'));
    expect(onReceivePurchaseInvoice).toHaveBeenCalledWith(
      expect.objectContaining({
        clinicId: 'clinic-1',
        lines: [
          expect.objectContaining({
            itemId: 'inv-item-1',
            packSize: 25,
            packCount: 1,
            packPriceAmount: 750,
          }),
        ],
      }),
    );
    // Initial qty 18 + 25 = 43
    expect(screen.getByTestId('inventory-item-qty-inv-item-1').textContent).toContain('43');

    // Manual adjustment
    fireEvent.click(screen.getByText('تنفيذ التسوية اليدوية'));
    expect(onAdjustStock).toHaveBeenCalledWith(
      expect.objectContaining({
        clinicId: 'clinic-1',
        itemId: 'inv-item-1',
        quantityDelta: 5,
      }),
    );

    // Resolve low-stock alert
    fireEvent.click(screen.getByText('تأكيد المعالجة'));
    expect(onResolveAlert).toHaveBeenCalledWith({
      clinicId: 'clinic-1',
      alertId: 'alert-1',
    });

    // Switch negative stock policy & save settings
    fireEvent.change(screen.getByLabelText('سياسة الرصيد السالب'), {
      target: { value: 'AllowWithWarning' },
    });
    fireEvent.click(screen.getByText('حفظ إعدادات المخزون'));
    expect(onUpdateSettings).toHaveBeenCalledWith(
      expect.objectContaining({
        clinicId: 'clinic-1',
        negativeStockPolicy: 'AllowWithWarning',
      }),
    );
  });

  it('disables write actions in Super Admin readOnlyInspectMode', () => {
    renderWithQuery(<Phase12InventoryScreen clinicId="clinic-1" readOnlyInspectMode={true} />);
    expect((screen.getByText('استلام فاتورة الشراء') as HTMLButtonElement).disabled).toBe(true);
    expect((screen.getByText('تنفيذ التسوية اليدوية') as HTMLButtonElement).disabled).toBe(true);
    expect((screen.getByText('حفظ إعدادات المخزون') as HTMLButtonElement).disabled).toBe(true);
    expect(screen.getByText('وضع المراجعة فقط — المخزون')).toBeTruthy();
  });
});
