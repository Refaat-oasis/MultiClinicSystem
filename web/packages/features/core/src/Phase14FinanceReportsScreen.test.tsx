import React from 'react';
import { describe, it, expect } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { Phase14FinanceReportsScreen } from './Phase14FinanceReportsScreen';

const renderWithQuery = (ui: React.ReactElement) => {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(<QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>);
};

describe('Phase14FinanceReportsScreen', () => {
  it('supports fixed cost creation/voiding, payroll finalization without commission, report export, and dry-run import gating', () => {
    renderWithQuery(
      <Phase14FinanceReportsScreen
        clinicId="clinic-14"
        enabledModules={['patients', 'scheduling', 'billing', 'inventory', 'finance', 'reports', 'excel-import']}
      />,
    );

    expect(screen.getByText(/التكاليف الثابتة المتكررة/)).toBeTruthy();
    expect(screen.queryByText(/عمولة/)).toBeNull();

    fireEvent.change(screen.getByLabelText('اسم التكلفة الثابتة'), {
      target: { value: 'فاتورة إنترنت' },
    });
    fireEvent.click(screen.getByRole('button', { name: /إضافة تكلفة ثابتة/ }));
    expect(screen.getAllByText(/فاتورة إنترنت/).length).toBeGreaterThanOrEqual(1);

    fireEvent.click(screen.getByRole('button', { name: /اعتماد كشف الرواتب/ }));
    expect(screen.getByText(/Finalized/)).toBeTruthy();

    fireEvent.click(screen.getByRole('button', { name: /التقارير والتصدير/ }));
    expect(screen.getByText(/46500\.00/)).toBeTruthy();
    fireEvent.click(screen.getByRole('button', { name: /تصدير Excel/ }));
    expect(screen.getByText(/تجهيز ملف التقرير بصيغة Excel/)).toBeTruthy();

    fireEvent.click(screen.getByRole('button', { name: /استيراد إكسل/ }));
    fireEvent.click(screen.getByRole('button', { name: /فحص ملف بأخطاء/ }));
    expect(screen.getByText(/PHONE_INVALID/)).toBeTruthy();

    fireEvent.click(screen.getByRole('button', { name: /فحص واعتماد القالب السليم/ }));
    expect(screen.getByText(/تم استيراد 3 صف بنجاح/)).toBeTruthy();
  });

  it('disables write buttons in read-only inspect mode and scopes import targets to enabled modules', () => {
    renderWithQuery(
      <Phase14FinanceReportsScreen
        clinicId="clinic-14"
        readOnlyInspectMode={true}
        enabledModules={['patients', 'finance', 'reports', 'excel-import']}
      />,
    );

    expect(screen.queryByRole('button', { name: /إضافة تكلفة ثابتة/ })).toBeNull();
    expect(screen.queryByRole('button', { name: /اعتماد كشف الرواتب/ })).toBeNull();

    fireEvent.click(screen.getByRole('button', { name: /استيراد إكسل/ }));
    const select = screen.getByLabelText('البيانات المستهدفة للاستيراد') as HTMLSelectElement;
    const values = Array.from(select.options).map((o) => o.value);
    expect(values).toContain('Patients');
    expect(values).toContain('FixedCosts');
    expect(values).not.toContain('InventoryItems');
    expect(values).not.toContain('Appointments');
  });
});
