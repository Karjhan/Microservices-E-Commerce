'use client';

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { getPaymentById, getPaymentByOrder, createPayment, processPayment, listPayments, type PaymentFilters } from '@/lib/api/payments';
import { queryKeys } from '@/lib/constants';
import type { CreatePaymentRequest } from '@/types/payment';

export function usePayment(id: string) {
  return useQuery({
    queryKey: queryKeys.payments.detail(id),
    queryFn: () => getPaymentById(id),
    enabled: !!id,
  });
}

export function usePaymentByOrder(orderId: string) {
  return useQuery({
    queryKey: queryKeys.payments.byOrder(orderId),
    queryFn: () => getPaymentByOrder(orderId),
    enabled: !!orderId,
    retry: false,
  });
}

export function usePayments(filters: PaymentFilters = {}) {
  return useQuery({
    queryKey: queryKeys.payments.list(filters),
    queryFn: () => listPayments(filters),
  });
}

export function useCreatePayment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: CreatePaymentRequest) => createPayment(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.payments.all });
    },
  });
}

export function useProcessPayment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (paymentId: string) => processPayment(paymentId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.payments.all });
      queryClient.invalidateQueries({ queryKey: queryKeys.orders.all });
    },
  });
}
