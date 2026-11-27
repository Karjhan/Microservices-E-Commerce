import { apiGet, buildQueryString } from './client';

export type AuditOutcome = 'success' | 'refused' | 'failed';

export type AuditSource = 'auth' | 'catalog' | 'inventory' | 'orders' | 'payments';

export interface AuditEntry {
  id: string;
  occurredAt: string;
  actorId: string | null;
  actorEmail: string | null;
  action: string;
  targetType: string;
  targetId: string | null;
  outcome: AuditOutcome;
  details: string;
  correlationId: string | null;
  source: AuditSource;
}

interface ServiceAuditPage {
  items: Omit<AuditEntry, 'source'>[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface MergedAuditPage {
  items: AuditEntry[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  unavailable: AuditSource[];
}

const AUDIT_ENDPOINTS: Record<AuditSource, string> = {
  auth: '/auth/admin/audit',
  catalog: '/products/admin/audit',
  inventory: '/inventory/admin/audit',
  orders: '/orders/admin/audit',
  payments: '/payments/admin/audit',
};

export const MAX_ENTRIES_PER_SERVICE = 100;

export const AUDIT_PAGE_SIZE = 20;

export async function getMergedAudit(
  page = 1,
  pageSize = AUDIT_PAGE_SIZE
): Promise<MergedAuditPage> {
  const depth = Math.min(page * pageSize, MAX_ENTRIES_PER_SERVICE);
  const sources = Object.keys(AUDIT_ENDPOINTS) as AuditSource[];

  const results = await Promise.allSettled(
    sources.map((source) =>
      apiGet<ServiceAuditPage>(
        `${AUDIT_ENDPOINTS[source]}${buildQueryString({ page: 1, pageSize: depth })}`
      )
    )
  );

  const entries: AuditEntry[] = [];
  const unavailable: AuditSource[] = [];
  let reachableTotal = 0;
  let firstError: unknown;

  results.forEach((result, index) => {
    const source = sources[index];
    if (result.status === 'fulfilled') {
      reachableTotal += Math.min(result.value.totalCount, MAX_ENTRIES_PER_SERVICE);
      entries.push(...result.value.items.map((item) => ({ ...item, source })));
    } else {
      unavailable.push(source);
      firstError ??= result.reason;
    }
  });

  if (unavailable.length === sources.length) {
    throw firstError;
  }

  entries.sort((a, b) => Date.parse(b.occurredAt) - Date.parse(a.occurredAt));

  const start = (page - 1) * pageSize;

  return {
    items: entries.slice(start, start + pageSize),
    page,
    pageSize,
    totalCount: reachableTotal,
    totalPages: Math.max(1, Math.ceil(reachableTotal / pageSize)),
    unavailable,
  };
}
