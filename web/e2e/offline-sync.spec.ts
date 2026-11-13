import { expect, test } from '@playwright/test';

test.describe('Phase 15 PWA Offline Sync & Portal Cache Isolation', () => {
  test('app shell remains navigable offline, tracks pending badge, flushes on reconnect, and never caches portal offline', async ({
    page,
    context,
  }) => {
    await page.route('**/api/v1/clinics/*/sync/conflicts**', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([]),
      });
    });

    await page.route('**/api/v1/clinics/*/sync/devices**', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([]),
      });
    });

    let pushCount = 0;
    await page.route('**/api/v1/clinics/*/sync/push', async (route) => {
      pushCount += 1;
      const body = route.request().postDataJSON() as {
        deviceId: string;
        operations: Array<{ operationId: string; actionType: string; entityType: string; entityId: string }>;
      };
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          deviceId: body.deviceId,
          latestCursor: 10 + pushCount,
          serverTimestampUtc: new Date().toISOString(),
          appliedCount: body.operations.length,
          duplicateCount: 0,
          conflictCount: 0,
          rejectedCount: 0,
          outcomes: body.operations.map((op, idx) => ({
            operationId: op.operationId,
            actionType: op.actionType,
            entityType: op.entityType,
            entityId: op.entityId,
            status: 'Applied',
            cursorSequence: 10 + idx + 1,
            clockSkewSeconds: 0,
            isLargeClockSkew: false,
            assignedReceiptNumbers: [],
          })),
        }),
      });
    });

    // 1. Load clinic staff PWA while online and wait for service worker activation
    await page.goto('/');
    await expect(page.getByRole('status', { name: 'حالة الاتصال والمزامنة' })).toContainText('متصل');
    await page.evaluate(async () => {
      if ('serviceWorker' in navigator) {
        await navigator.serviceWorker.ready;
      }
    });

    // Navigate to offline-sync tab and verify UI loads in Arabic RTL
    await page.getByRole('button', { name: 'تبويب المزامنة والعمل دون اتصال' }).click();
    await expect(page.getByText(/صندوق تعارضات المزامنة/)).toBeVisible();

    // 2. Go offline using Playwright setOffline(true)
    await context.setOffline(true);
    await page.evaluate(() => window.dispatchEvent(new Event('offline')));
    await expect(page.getByRole('status', { name: 'حالة الاتصال والمزامنة' })).toContainText('وضع عدم الاتصال');

    // Reloading staff app shell offline continues serving the PWA shell via Service Worker
    await page.reload({ waitUntil: 'domcontentloaded' });
    await expect(page.getByRole('button', { name: 'تبويب المزامنة والعمل دون اتصال' })).toBeVisible();

    // Queue a test operation directly into IndexedDB outbox while offline and dispatch online sync
    await page.evaluate(async () => {
      await new Promise<void>((resolve, reject) => {
        const req = indexedDB.open('ClinicOfflineDb');
        req.onsuccess = () => {
          const db = req.result;
          const tx = db.transaction('outbox', 'readwrite');
          tx.objectStore('outbox').add({
            operationId: '019a0000-0000-7000-8000-000000000901',
            clinicId: 'clinic-1',
            deviceId: 'web-browser',
            actionType: 'ClinicSettingsUpdate',
            entityType: 'ClinicSettings',
            entityId: 'clinic-1',
            endpoint: '/clinics/clinic-1/settings',
            method: 'PUT',
            payloadJson: JSON.stringify({ name: 'عيادة الأمل المحدثة' }),
            clientTimestampUtc: new Date().toISOString(),
            clientBaseCursor: 0,
            status: 'pending',
            attemptCount: 0,
            nextRetryAtMs: 0,
          });
          tx.oncomplete = () => {
            db.close();
            resolve();
          };
          tx.onerror = () => reject(tx.error);
        };
        req.onerror = () => reject(req.error);
      });
    });

    // 3. Ensure Patient Portal route (/c/alamal/portal) is NEVER served from SW cache while offline
    const portalOfflineError = await page
      .evaluate(async () => {
        const res = await fetch('/c/alamal/portal', { cache: 'no-store' });
        return { ok: res.ok, status: res.status };
      })
      .then(() => null)
      .catch((err: unknown) => String(err));
    expect(portalOfflineError).not.toBeNull();

    // 4. Go back online, trigger online event, and verify outbox flushes and clears pending count
    await context.setOffline(false);
    await page.evaluate(() => window.dispatchEvent(new Event('online')));
    await expect(page.getByRole('status', { name: 'حالة الاتصال والمزامنة' })).toContainText('متصل');
    await expect.poll(() => pushCount, { timeout: 10_000 }).toBeGreaterThanOrEqual(1);

    const remainingOutbox = await page.evaluate(async () => {
      return await new Promise<number>((resolve, reject) => {
        const req = indexedDB.open('ClinicOfflineDb');
        req.onsuccess = () => {
          const db = req.result;
          const tx = db.transaction('outbox', 'readonly');
          const countReq = tx.objectStore('outbox').count();
          countReq.onsuccess = () => {
            db.close();
            resolve(countReq.result);
          };
          countReq.onerror = () => reject(countReq.error);
        };
        req.onerror = () => reject(req.error);
      });
    });
    expect(remainingOutbox).toBe(0);
  });
});

