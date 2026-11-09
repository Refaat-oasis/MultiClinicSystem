/**
 * RFC 7807 Problem Details representation returned by the backend API.
 */
export interface ProblemDetails {
  type?: string;
  title: string;
  status: number;
  detail?: string;
  instance?: string;
  code?: string;
  requestId?: string;
  errors?: Record<string, string[]>;
}

