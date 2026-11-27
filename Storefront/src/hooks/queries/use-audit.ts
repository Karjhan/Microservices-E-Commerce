'use client';

import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { getMergedAudit, AUDIT_PAGE_SIZE } from '@/lib/api/audit';
import { queryKeys } from '@/lib/constants';

export function useAudit(page = 1, pageSize = AUDIT_PAGE_SIZE) {
  return useQuery({
    queryKey: queryKeys.audit.list({ page, pageSize }),
    queryFn: () => getMergedAudit(page, pageSize),
    placeholderData: keepPreviousData,
    staleTime: 10 * 1000,
  });
}
