'use client';

import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  adjustStock,
  getAdminStock,
  getStock,
  getStockMovements,
  listInventory,
  setLowStockThreshold,
  type AdjustStockInput,
  type InventoryFilters,
} from '@/lib/api/inventory';
import { queryKeys } from '@/lib/constants';

export function useInventory(productId: string) {
  return useQuery({
    queryKey: queryKeys.inventory.detail(productId),
    queryFn: () => getStock(productId),
    enabled: !!productId,
  });
}

export function useAdminInventory(productId: string, enabled = true) {
  return useQuery({
    queryKey: queryKeys.inventory.adminDetail(productId),
    queryFn: () => getAdminStock(productId),
    enabled: enabled && !!productId,
    retry: false,
  });
}

export function useInventoryList(filters: InventoryFilters = {}) {
  return useQuery({
    queryKey: queryKeys.inventory.list(filters),
    queryFn: () => listInventory(filters),
    placeholderData: keepPreviousData,
  });
}

export function useStockMovements(
  productId: string,
  paging: { page?: number; pageSize?: number } = {},
  enabled = true
) {
  return useQuery({
    queryKey: queryKeys.inventory.movements(productId, paging),
    queryFn: () => getStockMovements(productId, paging),
    enabled: enabled && !!productId,
    placeholderData: keepPreviousData,
  });
}

function useInvalidateInventory() {
  const queryClient = useQueryClient();
  return () => {
    queryClient.invalidateQueries({ queryKey: queryKeys.inventory.all });
    queryClient.invalidateQueries({ queryKey: queryKeys.products.all });
  };
}

export function useAdjustStock() {
  const invalidate = useInvalidateInventory();
  return useMutation({
    mutationFn: ({ productId, ...input }: AdjustStockInput & { productId: string }) =>
      adjustStock(productId, input),
    onSuccess: invalidate,
  });
}

export function useSetLowStockThreshold() {
  const invalidate = useInvalidateInventory();
  return useMutation({
    mutationFn: ({ productId, lowStockThreshold }: { productId: string; lowStockThreshold: number }) =>
      setLowStockThreshold(productId, lowStockThreshold),
    onSuccess: invalidate,
  });
}
