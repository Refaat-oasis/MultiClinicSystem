import { describe, it, expect } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { App } from './App';

describe('Admin App Shell & Phase 2 Super Admin Views', () => {
  it('renders platform admin heading and host isolation badge', () => {
    render(<App />);
    expect(screen.getByText('إدارة المنصة المركزية (Platform Admin)')).toBeDefined();
    expect(screen.getByText(/admin\.clinic\.local/)).toBeDefined();
  });

  it('renders security and tenant management cards', () => {
    render(<App />);
    expect(screen.getByText('نطاق المضيف (Host Isolation)')).toBeDefined();
    expect(screen.getByText('التحقق الآمن للمشرفين (2FA)')).toBeDefined();
  });

  it('renders 3-field Super Admin login and toggles between hourly code and recovery key', () => {
    render(<App />);
    fireEvent.click(screen.getByRole('button', { name: /تسجيل الدخول \(2FA\)/ }));

    expect(screen.getByText('تسجيل دخول المشرف الأعلى (Super Admin Sign In)')).toBeDefined();
    expect(screen.getByLabelText(/البريد الإلكتروني للمشرف/)).toBeDefined();
    expect(screen.getByLabelText(/كلمة المرور/)).toBeDefined();
    expect(screen.getByRole('group', { name: 'Verification Code Input' })).toBeDefined();

    fireEvent.click(screen.getByRole('button', { name: /استخدام مفتاح الاسترداد لمرة واحدة/ }));
    expect(screen.getByLabelText(/مفتاح الاسترداد لمرة واحدة/)).toBeDefined();
  });

  it('renders mandatory enrollment screen with 3600s setup key and password field', () => {
    render(<App />);
    fireEvent.click(screen.getByRole('button', { name: /الإعداد الإلزامي/ }));

    expect(screen.getByText(/إعداد الأمان الإلزامي للمشرف الأعلى/)).toBeDefined();
    expect(screen.getByText('DEMO3600HOURLYTOTPSECRET')).toBeDefined();
    expect(screen.getByLabelText(/كلمة المرور الجديدة/)).toBeDefined();
  });

  it('renders owners management screen with create form and quota field', () => {
    render(<App />);
    fireEvent.click(screen.getByRole('button', { name: /الملاك/ }));

    expect(screen.getByText('دعوة مالك عيادات جديد (Invite Clinic Owner)')).toBeDefined();
    expect(screen.getByLabelText(/الحد الأقصى للعيادات/)).toBeDefined();
  });

  it('renders read-only inspect clinic banner with PatientAccessLog notice and disabled writes', () => {
    render(<App />);
    fireEvent.click(screen.getByRole('button', { name: /وضع الفحص/ }));

    expect(screen.getByText(/PatientAccessLog/)).toBeDefined();
    expect(screen.getByText(/Writes Disabled in Inspect Mode/)).toBeDefined();
  });

  it('renders clinic directory screen with step-up password input for module control and transfers', () => {
    render(<App />);
    fireEvent.click(screen.getByRole('button', { name: /العيادات/ }));

    expect(screen.getByText(/دليل العيادات ونقل الملكية/)).toBeDefined();
    expect(screen.getByLabelText(/كلمة مرور التحقق الإضافي/)).toBeDefined();
  });
});
