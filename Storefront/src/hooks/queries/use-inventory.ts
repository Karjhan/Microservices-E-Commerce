'use client';

import { useQuery } from '@tanstack/react-query';
import { getStock, listInventory } from '@/lib/api/inventory';
import { queryKeys } from '@/lib/constants';

export function useInventory(productId: string) {
  return useQuery({
    queryKey: queryKeys.inventory.detail(productId),
    queryFn: () => getStock(productId),
    enabled: !!productId,
  });
}

export function useInventoryList(filters: { depletedOnly?: boolean; page?: number; pageSize?: number } = {}) {
  return useQuery({
    queryKey: queryKeys.inventory.list(filters),
    queryFn: () => listInventory(filters),
  });
}
