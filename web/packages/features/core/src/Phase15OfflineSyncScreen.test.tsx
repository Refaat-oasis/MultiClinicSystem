import React from 'react';
import { describe, it, expect } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { Phase15OfflineSyncScreen } from './Phase15OfflineSyncScreen';

const renderWithQuery = (ui: React.ReactElement) => {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(<QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>);
};

describe('Phase15OfflineSyncScreen', () => {
  it('renders conflict inbox, resolves conflicts, switches to number blocks, and enforces module-off state', () => {
    renderWithQuery(
      <Phase15OfflineSyncScreen clinicId="clinic-15" enabledModules={['patients', 'offline-sync']} />
    );

    expect(screen.getByText(/صندوق تعارضات المزامنة/)).toBeTruthy();
    expect(screen.getByText(/SLOT_TAKEN/)).toBeTruthy();

    fireEvent.click(screen.getByRole('button', { name: /اعتماد نسخة الخادم/ }));
    expect(screen.getAllByText(/ResolvedKeepServer/).length).toBeGreaterThanOrEqual(1);

    fireEvent.click(screen.getByRole('button', { name: /قائمة الانتظار وكتل الإيصالات/ }));
    expect(screen.getByText(/كتل أرقام الإيصالات المسبقة/)).toBeTruthy();
    expect(screen.getByText(/Range: 1 – 50/)).toBeTruthy();
  });

  it('shows disabled message when offline-sync module is turned off', () => {
    renderWithQuery(
      <Phase15OfflineSyncScreen clinicId="clinic-15" enabledModules={['patients']} />
    );

    expect(screen.getByText(/وحدة المزامنة دون اتصال غير مفعلة/)).toBeTruthy();
  });

  it('hides mutation actions in Super Admin readOnlyInspectMode', () => {
    renderWithQuery(
      <Phase15OfflineSyncScreen
        clinicId="clinic-15"
        enabledModules={['offline-sync']}
        readOnlyInspectMode={true}
      />
    );

    expect(screen.getByText(/وضع الفحص للقراءة فقط/)).toBeTruthy();
    expect(screen.queryByRole('button', { name: /اعتماد نسخة الخادم/ })).toBeNull();
  });
});
