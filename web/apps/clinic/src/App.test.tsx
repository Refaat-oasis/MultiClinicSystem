import { describe, it, expect } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { App } from './App';

describe('Clinic App Shell - Phase 1', () => {
  const createQueryClient = () =>
    new QueryClient({
      defaultOptions: { queries: { retry: false } },
    });

  it('renders clinic title and Phase 1 navigation bar', () => {
    render(
      <QueryClientProvider client={createQueryClient()}>
        <App />
      </QueryClientProvider>
    );

    expect(screen.getByText('عيادة الأمل التخصصية')).toBeDefined();
    expect(screen.getByText(/المرحلة الأولى: الهوية والأمان/i)).toBeDefined();
    expect(screen.getByRole('button', { name: 'تبويب تسجيل الدخول' })).toBeDefined();
    expect(screen.getByRole('button', { name: 'تبويب تفعيل الحساب' })).toBeDefined();
  });

  it('switches between auth tabs seamlessly', () => {
    render(
      <QueryClientProvider client={createQueryClient()}>
        <App />
      </QueryClientProvider>
    );

    // Initial tab shows login screen
    expect(screen.getByText('تسجيل الدخول إلى العيادة')).toBeDefined();

    // Click activation tab
    const activateTabBtn = screen.getByRole('button', { name: 'تبويب تفعيل الحساب' });
    fireEvent.click(activateTabBtn);
    expect(screen.getByText('تفعيل حساب العيادة')).toBeDefined();

    // Click forgot password tab
    const forgotTabBtn = screen.getByRole('button', { name: 'تبويب استعادة كلمة المرور' });
    fireEvent.click(forgotTabBtn);
    expect(screen.getByRole('heading', { name: 'استعادة كلمة المرور' })).toBeDefined();

    // Click devices tab
    const devicesTabBtn = screen.getByRole('button', { name: 'تبويب إدارة الأجهزة' });
    fireEvent.click(devicesTabBtn);
    expect(screen.getByText('إدارة الأجهزة والجلسات النشطة')).toBeDefined();
  });
});
