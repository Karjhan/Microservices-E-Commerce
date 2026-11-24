'use client';

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { listOrders, getOrderById, getOrdersByUserId, createOrder, cancelOrder, updateShippingAddress, type OrderFilters } from '@/lib/api/orders';
import { queryKeys } from '@/lib/constants';
import type { CreateOrderRequest } from '@/types/order';

export function useOrders(filters: OrderFilters = {}) {
  return useQuery({
    queryKey: queryKeys.orders.list(filters),
    queryFn: () => listOrders(filters),
  });
}

export function useOrder(id: string) {
  return useQuery({
    queryKey: queryKeys.orders.detail(id),
    queryFn: () => getOrderById(id),
    enabled: !!id,
  });
}

export function useUserOrders(userId: string) {
  return useQuery({
    queryKey: queryKeys.orders.byUser(userId),
    queryFn: () => getOrdersByUserId(userId),
    enabled: !!userId,
  });
}

export function useCreateOrder() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: CreateOrderRequest) => createOrder(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.orders.all });
    },
  });
}

export function useCancelOrder() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ orderId, reason }: { orderId: string; reason: string }) => cancelOrder(orderId, reason),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.orders.all });
    },
  });
}

export function useUpdateShippingAddress() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ orderId, address }: { orderId: string; address: Record<string, unknown> }) =>
      updateShippingAddress(orderId, address),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.orders.all });
    },
  });
}
