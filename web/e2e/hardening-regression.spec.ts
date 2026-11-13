import { expect, test } from '@playwright/test';

test.describe('Phase 16 Bilingual Arabic RTL / English & Theme Hardening Regression', () => {
  test('supports Arabic RTL default, English LTR toggle, dark/light themes, and core tab navigation without uncaught page errors', async ({
    page,
  }) => {
    const pageErrors: string[] = [];
    page.on('pageerror', (err) => pageErrors.push(err.message));

    await page.goto('/');
    await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');
    await expect(page.getByRole('heading', { level: 1 })).toContainText('عيادة الأمل التخصصية');

    // Toggle theme Light <-> Dark
    const darkBtn = page.getByRole('button', { name: /Switch to Dark Theme|الوضع الداكن/i });
    if (await darkBtn.count()) {
      await darkBtn.first().click();
      await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');
    }

    // Toggle language Arabic RTL <-> English LTR
    const langBtn = page.getByRole('button', { name: /تغيير اللغة إلى الإنجليزية|English/i });
    await langBtn.first().click();
    await expect(page.locator('html')).toHaveAttribute('dir', 'ltr');

    // Switch back to Arabic RTL
    const arBtn = page.getByRole('button', { name: /Switch to Arabic|العربية/i });
    if (await arBtn.count()) {
      await arBtn.first().click();
      await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');
    }

    // Verify major clinical/financial/sync screens mount cleanly without runtime exceptions
    await page.getByRole('button', { name: 'تبويب الفواتير والخزينة' }).click();
    await page.getByRole('button', { name: 'تبويب المالية والتقارير' }).click();
    await page.getByRole('button', { name: 'تبويب المزامنة والعمل دون اتصال' }).click();
    await expect(page.getByText(/صندوق تعارضات المزامنة/)).toBeVisible();

    expect(pageErrors).toEqual([]);
  });
});

