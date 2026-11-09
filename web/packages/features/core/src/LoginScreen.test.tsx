import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import React from 'react';
import { LoginScreen } from './LoginScreen';

describe('LoginScreen', () => {
  it('renders login form in Arabic with email and password inputs', () => {
    render(<LoginScreen />);

    expect(screen.getByText('تسجيل الدخول إلى العيادة')).toBeDefined();
    expect(screen.getByLabelText('البريد الإلكتروني')).toBeDefined();
    expect(screen.getByLabelText('كلمة المرور')).toBeDefined();
    expect(screen.getByRole('button', { name: /تسجيل الدخول/i })).toBeDefined();
  });

  it('shows error message when submitted without required fields', async () => {
    const { container } = render(<LoginScreen />);

    const form = container.querySelector('form')!;
    fireEvent.submit(form);

    const errorAlert = await screen.findByRole('alert');
    expect(errorAlert.textContent).toContain('يرجى إدخال البريد الإلكتروني وكلمة المرور');
  });

  it('triggers navigation callbacks for forgot password and activation', () => {
    const onForgot = vi.fn();
    const onActivate = vi.fn();

    render(
      <LoginScreen
        onNavigateToForgot={onForgot}
        onNavigateToActivate={onActivate}
      />
    );

    fireEvent.click(screen.getByText('نسيت كلمة المرور؟'));
    expect(onForgot).toHaveBeenCalledTimes(1);

    fireEvent.click(screen.getByText('تفعيل حساب جديد'));
    expect(onActivate).toHaveBeenCalledTimes(1);
  });
});
