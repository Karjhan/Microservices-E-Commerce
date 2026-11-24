import { apiGet, apiPost, apiPut, buildQueryString, serverApiGet } from './client';
import type { Order, PaginatedOrders, CreateOrderRequest, CreateOrderResponse } from '@/types/order';

export interface OrderFilters {
  userId?: string;
  status?: string;
  fromDate?: string;
  toDate?: string;
  page?: number;
  pageSize?: number;
}

export async function createOrder(data: CreateOrderRequest): Promise<CreateOrderResponse> {
  return apiPost<CreateOrderResponse>('/orders', data);
}

export async function listOrders(filters: OrderFilters = {}): Promise<PaginatedOrders> {
  const qs = buildQueryString(filters);
  return apiGet<PaginatedOrders>(`/orders${qs}`);
}

export async function listOrdersServer(filters: OrderFilters = {}, token?: string): Promise<PaginatedOrders> {
  const qs = buildQueryString(filters);
  return serverApiGet<PaginatedOrders>(`/orders${qs}`, token);
}

export async function getOrderById(orderId: string): Promise<Order> {
  return apiGet<Order>(`/orders/${orderId}`);
}

export async function getOrdersByUserId(userId: string): Promise<Order[]> {
  return apiGet<Order[]>(`/orders/user/${userId}`);
}

export async function cancelOrder(orderId: string, reason: string): Promise<void> {
  return apiPost<void>(`/orders/${orderId}/cancel`, { reason });
}

export async function updateShippingAddress(orderId: string, address: Record<string, unknown>): Promise<void> {
  return apiPut<void>(`/orders/${orderId}/shipping-address`, address);
}