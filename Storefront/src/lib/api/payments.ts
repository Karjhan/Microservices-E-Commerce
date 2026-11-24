import { apiGet, apiPost, buildQueryString } from './client';
import type { Payment, CreatePaymentRequest, PaginatedPayments } from '@/types/payment';

export async function createPayment(data: CreatePaymentRequest): Promise<Payment> {
  return apiPost<Payment>('/payments', data);
}

export async function processPayment(paymentId: string): Promise<Payment> {
  return apiPost<Payment>(`/payments/${paymentId}/process`);
}

export async function refundPayment(paymentId: string, amount?: number): Promise<Payment> {
  return apiPost<Payment>(`/payments/${paymentId}/refund`, amount ? { amount } : {});
}

export async function getPaymentById(paymentId: string): Promise<Payment> {
  return apiGet<Payment>(`/payments/${paymentId}`);
}

export async function getPaymentByOrder(orderId: string): Promise<Payment> {
  return apiGet<Payment>(`/payments/by-order/${orderId}`);
}

export interface PaymentFilters {
  userId?: string;
  orderId?: string;
  status?: string;
  method?: string;
  fromDate?: string;
  toDate?: string;
  page?: number;
  pageSize?: number;
}

export async function listPayments(filters: PaymentFilters = {}): Promise<PaginatedPayments> {
  const qs = buildQueryString(filters);
  return apiGet<PaginatedPayments>(`/payments${qs}`);
}
