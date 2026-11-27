import { generateCorrelationId } from '@/lib/utils';
import type { ApiError } from '@/types/api';

const API_BASE_URL = process.env.NEXT_PUBLIC_API_GATEWAY_URL || 'http://localhost:8080';

export class ApiClientError extends Error {
  constructor(
    public status: number,
    public code: string,
    public details?: Record<string, unknown>,
    public fieldErrors?: Record<string, string[]>,
    public detail?: string
  ) {
    super(detail || `API Error: ${code}`);
    this.name = 'ApiClientError';
  }
}

export function errorMessage(error: unknown, fallback = 'Something went wrong. Please try again.'): string {
  if (error instanceof ApiClientError) {
    if (error.status === 403) return FORBIDDEN_MESSAGE;
    return error.detail || fallback;
  }
  return fallback;
}

async function getAuthToken(): Promise<string | null> {
  if (typeof window === 'undefined') return null;
  try {
    const { getSession } = await import('next-auth/react');
    const session = await getSession();
    return (session as any)?.accessToken ?? null;
  } catch {
    return null;
  }
}

export const FORBIDDEN_MESSAGE =
  'You are not permitted to do this. If your role was changed recently, sign out and sign in again.';

export function isForbidden(error: unknown): boolean {
  return error instanceof ApiClientError && error.status === 403;
}

function redirectToSignIn() {
  if (typeof window === 'undefined') return;
  const { pathname, search } = window.location;
  if (pathname === '/login') return;
  const callbackUrl = encodeURIComponent(pathname + search);
  window.location.href = `/login?callbackUrl=${callbackUrl}`;
}

async function notifyForbidden() {
  if (typeof window === 'undefined') return;
  const { toast } = await import('sonner');
  toast.error(FORBIDDEN_MESSAGE, { id: 'forbidden' });
}

async function handleResponse<T>(response: Response): Promise<T> {
  if (!response.ok) {
    let errorBody: ApiError = { error: 'Unknown error', code: 'UNKNOWN' };
    try {
      errorBody = await response.json();
    } catch {}

    if (response.status === 401) {
      redirectToSignIn();
    }

    if (response.status === 403) {
      void notifyForbidden();
    }

    throw new ApiClientError(
      response.status,
      errorBody.code || `HTTP_${response.status}`,
      errorBody.details,
      errorBody.errors,
      errorBody.detail ||
        (typeof errorBody.error === 'string' && errorBody.error !== 'Unknown error' ? errorBody.error : undefined)
    );
  }

  if (response.status === 204) return undefined as T;
  return response.json();
}

interface RequestOptions {
  headers?: Record<string, string>;
  signal?: AbortSignal;
  skipAuth?: boolean;
}

export async function apiGet<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const token = options.skipAuth ? null : await getAuthToken();
  const headers: Record<string, string> = {
    'X-Correlation-Id': generateCorrelationId(),
    ...options.headers,
  };
  if (token) headers['Authorization'] = `Bearer ${token}`;

  const response = await fetch(`${API_BASE_URL}${path}`, {
    method: 'GET',
    headers,
    signal: options.signal,
  });
  return handleResponse<T>(response);
}

export async function apiPost<T>(path: string, body?: unknown, options: RequestOptions = {}): Promise<T> {
  const token = options.skipAuth ? null : await getAuthToken();
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    'X-Correlation-Id': generateCorrelationId(),
    ...options.headers,
  };
  if (token) headers['Authorization'] = `Bearer ${token}`;

  const response = await fetch(`${API_BASE_URL}${path}`, {
    method: 'POST',
    headers,
    body: body ? JSON.stringify(body) : undefined,
    signal: options.signal,
  });
  return handleResponse<T>(response);
}

export async function apiPut<T>(path: string, body?: unknown, options: RequestOptions = {}): Promise<T> {
  const token = options.skipAuth ? null : await getAuthToken();
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    'X-Correlation-Id': generateCorrelationId(),
    ...options.headers,
  };
  if (token) headers['Authorization'] = `Bearer ${token}`;

  const response = await fetch(`${API_BASE_URL}${path}`, {
    method: 'PUT',
    headers,
    body: body ? JSON.stringify(body) : undefined,
    signal: options.signal,
  });
  return handleResponse<T>(response);
}

export async function apiDelete<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const token = options.skipAuth ? null : await getAuthToken();
  const headers: Record<string, string> = {
    'X-Correlation-Id': generateCorrelationId(),
    ...options.headers,
  };
  if (token) headers['Authorization'] = `Bearer ${token}`;

  const response = await fetch(`${API_BASE_URL}${path}`, {
    method: 'DELETE',
    headers,
    signal: options.signal,
  });
  return handleResponse<T>(response);
}

export async function apiPostFormData<T>(path: string, formData: FormData, options: RequestOptions = {}): Promise<T> {
  return sendFormData<T>('POST', path, formData, options);
}

export async function apiPutFormData<T>(path: string, formData: FormData, options: RequestOptions = {}): Promise<T> {
  return sendFormData<T>('PUT', path, formData, options);
}

async function sendFormData<T>(
  method: 'POST' | 'PUT',
  path: string,
  formData: FormData,
  options: RequestOptions
): Promise<T> {
  const token = options.skipAuth ? null : await getAuthToken();
  const headers: Record<string, string> = {
    'X-Correlation-Id': generateCorrelationId(),
    ...options.headers,
  };
  if (token) headers['Authorization'] = `Bearer ${token}`;

  const response = await fetch(`${API_BASE_URL}${path}`, {
    method,
    headers,
    body: formData,
    signal: options.signal,
  });
  return handleResponse<T>(response);
}

// eslint-disable-next-line @typescript-eslint/no-explicit-any
export function buildQueryString(params: Record<string, any>): string {
  const searchParams = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value === undefined || value === null || value === '') continue;
    if (Array.isArray(value)) {
      value.forEach((v) => searchParams.append(key, String(v)));
    } else {
      searchParams.set(key, String(value));
    }
  }
  const qs = searchParams.toString();
  return qs ? `?${qs}` : '';
}

export async function serverApiGet<T>(path: string, token?: string): Promise<T> {
  const headers: Record<string, string> = {
    'X-Correlation-Id': generateCorrelationId(),
  };
  if (token) headers['Authorization'] = `Bearer ${token}`;

  const response = await fetch(`${API_BASE_URL}${path}`, {
    method: 'GET',
    headers,
    cache: 'no-store',
  });
  return handleResponse<T>(response);
}
