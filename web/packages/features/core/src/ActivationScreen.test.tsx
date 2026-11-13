import { describe, it, expect } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { ActivationScreen } from './ActivationScreen';

describe('ActivationScreen', () => {
  it('renders activation form with 6-digit code boxes and password fields', () => {
    render(<ActivationScreen initialEmail="doctor@clinic.local" />);

    expect(screen.getByText('تفعيل حساب العيادة')).toBeDefined();
    expect(screen.getByLabelText('البريد الإلكتروني المدعو')).toBeDefined();
    expect(screen.getByText(/رمز التحقق الأمني \(6 أرقام\)/i)).toBeDefined();

    // 6 digit boxes
    const digitInputs = screen.getAllByRole('textbox').filter((el) =>
      el.getAttribute('aria-label')?.includes('Digit')
    );
    expect(digitInputs.length).toBe(6);
  });

  it('validates incomplete 6-digit code', async () => {
    const { container } = render(<ActivationScreen initialEmail="doctor@clinic.local" />);

    const form = container.querySelector('form')!;
    fireEvent.submit(form);

    const errorAlert = await screen.findByRole('alert');
    expect(errorAlert.textContent).toContain('يرجى إدخال رمز التحقق كاملاً');
  });
});
