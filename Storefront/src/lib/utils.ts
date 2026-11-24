import { clsx, type ClassValue } from "clsx"
import { twMerge } from "tailwind-merge"

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}

export function formatCurrency(amount: number, currency = 'EUR'): string {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency,
  }).format(amount);
}

export function formatDate(dateString: string): string {
  return new Intl.DateTimeFormat('en-US', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  }).format(new Date(dateString));
}

export function generateCorrelationId(): string {
  return crypto.randomUUID();
}

export function resolveMediaUrl(url: string | null | undefined): string | null {
  if (!url) return null;

  const publicOrigin = (
    process.env.NEXT_PUBLIC_MINIO_PUBLIC_URL ?? 'http://localhost:9000'
  ).replace(/\/$/, '');

  let normalized = url.trim();
  if (!/^https?:\/\//i.test(normalized)) {
    normalized = `http://${normalized}`;
  }

  return normalized.replace(/^https?:\/\/minio(?::\d+)?/i, publicOrigin);
}