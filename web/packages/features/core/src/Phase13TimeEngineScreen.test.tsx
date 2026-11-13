import React from 'react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { Phase13TimeEngineScreen } from './Phase13TimeEngineScreen';

const mutateMock = vi.fn();

vi.mock('@clinic/api-client', () => ({
  useOfflineMutation: () => ({
    mutate: mutateMock,
    isPending: false,
  }),
}));

const renderWithQuery = (ui: React.ReactElement) => {
  const client = new QueryClient();
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
};

describe('Phase13TimeEngineScreen', () => {
  beforeEach(() => {
    mutateMock.mockReset();
  });

  it('renders suggested vs applied stats and dispatches settings mutation', () => {
    renderWithQuery(<Phase13TimeEngineScreen clinicId="clinic-1" />);

    expect(screen.getByText('محرك متوسط الزمن الذكي والتأخير')).toBeTruthy();
    expect(screen.getByTestId('stat-row-stat-1').textContent).toContain('36 د');

    fireEvent.change(screen.getByLabelText('الحد الأدنى للعينات'), { target: { value: '8' } });
    fireEvent.click(screen.getByRole('button', { name: 'حفظ إعدادات المحرك' }));

    expect(mutateMock).toHaveBeenCalledWith({
      minSampleSize: 8,
      trimFraction: 0.1,
      maxStepChangePercent: 20,
    });
  });

  it('toggles doctor engine on/off and reverts applied duration to category default locally', () => {
    renderWithQuery(<Phase13TimeEngineScreen clinicId="clinic-1" />);

    fireEvent.click(screen.getByRole('button', { name: 'إيقاف للطبيب' }));
    expect(screen.getByTestId('doctor-engine-row-doc-1').textContent).toContain('متوقف (الافتراضي)');
    expect(screen.getByTestId('stat-row-stat-1').textContent).toContain('30 د');
  });

  it('toggles excused status on a session row with reason and enforces read-only inspect mode', () => {
    const { rerender } = renderWithQuery(<Phase13TimeEngineScreen clinicId="clinic-1" />);

    fireEvent.click(screen.getByRole('button', { name: 'استثناء من الحساب' }));
    expect(screen.getByTestId('duration-row-visit-1').textContent).toContain('مستثنى: حالة طارئة مطولة');

    const qc = new QueryClient();
    rerender(
      <QueryClientProvider client={qc}>
        <Phase13TimeEngineScreen clinicId="clinic-1" readOnlyInspectMode={true} />
      </QueryClientProvider>,
    );

    expect(screen.getByTestId('time-engine-readonly-badge')).toBeTruthy();
    expect((screen.getByRole('button', { name: 'حفظ إعدادات المحرك' }) as HTMLButtonElement).disabled).toBe(true);
  });
});

