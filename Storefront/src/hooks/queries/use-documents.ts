'use client';

import { useQuery } from '@tanstack/react-query';
import { listDocuments, getDocumentById, type DocumentFilters } from '@/lib/api/documents';
import { queryKeys } from '@/lib/constants';

export function useDocuments(filters: DocumentFilters) {
  return useQuery({
    queryKey: queryKeys.documents.list(filters),
    queryFn: () => listDocuments(filters),
    enabled: !!(filters.orderId || filters.paymentId),
  });
}

export function useDocument(id: string) {
  return useQuery({
    queryKey: queryKeys.documents.detail(id),
    queryFn: () => getDocumentById(id),
    enabled: !!id,
  });
}
