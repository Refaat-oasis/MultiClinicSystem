import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { ApiClient } from './ApiClient';
import { ProblemDetails } from './ProblemDetails';

describe('ApiClient', () => {
  const originalFetch = globalThis.fetch;
  let client: ApiClient;

  beforeEach(() => {
    client = new ApiClient('/api/v1');
  });

  afterEach(() => {
    globalThis.fetch = originalFetch;
  });

  it('includes X-Request-Id header on all requests', async () => {
    let capturedHeaders: Headers | undefined;

    globalThis.fetch = vi.fn().mockImplementation((_url, init) => {
      capturedHeaders = new Headers(init?.headers);
      return Promise.resolve(new Response(JSON.stringify({ status: 'ok' }), { status: 200 }));
    });

    await client.get('/health');

    expect(capturedHeaders).toBeDefined();
    expect(capturedHeaders?.has('X-Request-Id')).toBe(true);
  });

  it('includes X-Operation-Id header when provided in mutation options', async () => {
    let capturedHeaders: Headers | undefined;

    globalThis.fetch = vi.fn().mockImplementation((_url, init) => {
      capturedHeaders = new Headers(init?.headers);
      return Promise.resolve(new Response(JSON.stringify({ success: true }), { status: 200 }));
    });

    await client.post('/items', { name: 'test' }, { operationId: '0194ff72-0000-7000-8000-000000000001' });

    expect(capturedHeaders?.get('X-Operation-Id')).toBe('0194ff72-0000-7000-8000-000000000001');
  });

  it('throws ProblemDetails when server responds with 400+', async () => {
    const errorPayload: ProblemDetails = {
      type: 'https://errors.clinic.local/VALIDATION_ERROR',
      title: 'Validation Failed',
      status: 400,
      code: 'VALIDATION_ERROR',
    };

    globalThis.fetch = vi.fn().mockImplementation(() => {
      return Promise.resolve(new Response(JSON.stringify(errorPayload), { status: 400 }));
    });

    await expect(client.get('/invalid')).rejects.toMatchObject({
      title: 'Validation Failed',
      status: 400,
      code: 'VALIDATION_ERROR',
    });
  });
});

