import { generateCorrelationId } from '@/lib/utils';
import type { ApiError } from '@/types/api';

const API_BASE_URL = process.env.NEXT_PUBLIC_API_GATEWAY_URL || 'http://localhost:8080';

export class ApiClientError extends Error {
  constructor(
    public status: number,
    public code: string,
    public details?: Record<string, unknown>
  ) {
    super(`API Error: ${code}`);
    this.name = 'ApiClientError';
  }
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

async function handleResponse<T>(response: Response): Promise<T> {
  if (!response.ok) {
    let errorBody: ApiError = { error: 'Unknown error', code: 'UNKNOWN' };
    try {
      errorBody = await response.json();
    } catch {}

    if (response.status === 401 && typeof window !== 'undefined') {
      window.location.href = '/login';
    }

    throw new ApiClientError(
      response.status,
      errorBody.code || `HTTP_${response.status}`,
      errorBody.details
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
  const token = options.skipAuth ? null : await getAuthToken();
  const headers: Record<string, string> = {
    'X-Correlation-Id': generateCorrelationId(),
    ...options.headers,
  };
  if (token) headers['Authorization'] = `Bearer ${token}`;

  const response = await fetch(`${API_BASE_URL}${path}`, {
    method: 'POST',
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
