'use client';

import { useQuery } from '@tanstack/react-query';
import { search } from '@/lib/api/search';
import { queryKeys } from '@/lib/constants';
import type { SearchFilters } from '@/types/search';

export function useSearch(query: string, filters: SearchFilters = {}, limit = 10) {
  return useQuery({
    queryKey: queryKeys.search.results(query, { ...filters, limit }),
    queryFn: () => search({ query, filters, limit }),
    enabled: query.length > 0,
    staleTime: 30 * 1000,
  });
}
