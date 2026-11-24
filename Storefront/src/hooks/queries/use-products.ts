'use client';

import { useQuery } from '@tanstack/react-query';
import { listProducts, getProductById, type ProductFilters } from '@/lib/api/products';
import { queryKeys } from '@/lib/constants';

export function useProducts(filters: ProductFilters = {}) {
  return useQuery({
    queryKey: queryKeys.products.list(filters),
    queryFn: () => listProducts(filters),
  });
}

export function useProduct(idOrSlug: string) {
  return useQuery({
    queryKey: queryKeys.products.detail(idOrSlug),
    queryFn: () => getProductById(idOrSlug),
    enabled: !!idOrSlug,
  });
}
