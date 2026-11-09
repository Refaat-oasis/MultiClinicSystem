import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { App } from './App';

describe('Admin App Shell', () => {
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
});

