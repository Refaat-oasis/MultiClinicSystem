import { v7 as uuidv7 } from 'uuid';
import { ProblemDetails } from './ProblemDetails';

export interface RequestOptions extends RequestInit {
  operationId?: string;
}

export class ApiClient {
  private baseUrl: string;

  constructor(baseUrl: string = '/api/v1') {
    this.baseUrl = baseUrl;
  }

  public async request<TResponse>(endpoint: string, options: RequestOptions = {}): Promise<TResponse> {
    const url = `${this.baseUrl}${endpoint.startsWith('/') ? endpoint : `/${endpoint}`}`;
    const headers = new Headers(options.headers);

    headers.set('Accept', 'application/json');
    if (!headers.has('Content-Type') && options.body) {
      headers.set('Content-Type', 'application/json');
    }

    if (!headers.has('X-Request-Id')) {
      headers.set('X-Request-Id', uuidv7());
    }

    if (options.operationId) {
      headers.set('X-Operation-Id', options.operationId);
    }

    const response = await fetch(url, {
      ...options,
      headers,
    });

    if (!response.ok) {
      let problem: ProblemDetails;
      try {
        problem = await response.json();
      } catch {
        problem = {
          title: response.statusText || 'An unexpected error occurred',
          status: response.status,
          code: 'HTTP_ERROR',
        };
      }
      throw problem;
    }

    if (response.status === 204) {
      return {} as TResponse;
    }

    return (await response.json()) as TResponse;
  }

  public get<T>(endpoint: string, options?: RequestOptions): Promise<T> {
    return this.request<T>(endpoint, { ...options, method: 'GET' });
  }

  public post<T>(endpoint: string, body?: unknown, options?: RequestOptions): Promise<T> {
    return this.request<T>(endpoint, {
      ...options,
      method: 'POST',
      body: body ? JSON.stringify(body) : undefined,
    });
  }
}

export const apiClient = new ApiClient();

