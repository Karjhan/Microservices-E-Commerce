'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  createReference,
  deleteReference,
  removeBrandLogo,
  setReferenceActive,
  updateReference,
  uploadBrandLogo,
} from '@/lib/api/reference-data';
import { queryKeys } from '@/lib/constants';
import type { ReferenceResource } from '@/types/reference-data';

/** The lists change rarely, so shoppers' filter menus can reuse them for a few minutes. */
const LIST_STALE_MS = 5 * 60 * 1000;

export function useReferenceList<T>(
  resource: ReferenceResource,
  fetcher: (includeInactive: boolean) => Promise<T[]>,
  includeInactive: boolean
) {
  return useQuery({
    queryKey: queryKeys.referenceData.list(resource, includeInactive),
    queryFn: () => fetcher(includeInactive),
    staleTime: includeInactive ? 0 : LIST_STALE_MS,
  });
}

/**
 * A change to a brand, category or filament type also changes the names products carry, so product queries are
 * cleared with the lists.
 */
function useInvalidateReferenceData() {
  const queryClient = useQueryClient();
  return async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: queryKeys.referenceData.all }),
      queryClient.invalidateQueries({ queryKey: queryKeys.products.all }),
    ]);
  };
}

export function useCreateReference(resource: ReferenceResource) {
  const invalidate = useInvalidateReferenceData();
  return useMutation({
    mutationFn: (body: Record<string, unknown>) => createReference(resource, body),
    onSuccess: invalidate,
  });
}

export function useUpdateReference(resource: ReferenceResource) {
  const invalidate = useInvalidateReferenceData();
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: Record<string, unknown> }) => updateReference(resource, id, body),
    onSuccess: invalidate,
  });
}

export function useSetReferenceActive(resource: ReferenceResource) {
  const invalidate = useInvalidateReferenceData();
  return useMutation({
    mutationFn: ({ id, active }: { id: string; active: boolean }) => setReferenceActive(resource, id, active),
    onSuccess: invalidate,
  });
}

export function useDeleteReference(resource: ReferenceResource) {
  const invalidate = useInvalidateReferenceData();
  return useMutation({
    mutationFn: (id: string) => deleteReference(resource, id),
    onSuccess: invalidate,
  });
}

export function useUploadBrandLogo() {
  const invalidate = useInvalidateReferenceData();
  return useMutation({
    mutationFn: ({ brandId, file }: { brandId: string; file: File }) => uploadBrandLogo(brandId, file),
    onSuccess: invalidate,
  });
}

export function useRemoveBrandLogo() {
  const invalidate = useInvalidateReferenceData();
  return useMutation({
    mutationFn: (brandId: string) => removeBrandLogo(brandId),
    onSuccess: invalidate,
  });
}
