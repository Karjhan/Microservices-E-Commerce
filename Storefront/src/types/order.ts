export type OrderStatus =
  | 'PendingPayment'
  | 'Paid'
  | 'Processing'
  | 'Shipped'
  | 'Delivered'
  | 'Cancelled'
  | 'PaymentFailed';

export interface ShippingAddress {
  street: string;
  city: string;
  state: string;
  country: string;
  zipCode: string;
  fullName: string | null;
  phone: string | null;
}

export interface OrderItem {
  id: string;
  productId: string;
  productName: string;
  unitPrice: number;
  quantity: number;
  currency: string;
  productImageUrl: string | null;
  lineTotal: number;
}

export interface Order {
  id: string;
  userId: string;
  status: OrderStatus;
  totalAmount: number;
  currency: string;
  notes: string | null;
  shippingAddress: ShippingAddress;
  items: OrderItem[];
  createdAt: string;
  updatedAt: string | null;
  paidAt: string | null;
  shippedAt: string | null;
  deliveredAt: string | null;
  cancelledAt: string | null;
  cancellationReason: string | null;
}

export interface CreateOrderRequest {
  userId: string;
  currency: string;
  notes: string | null;
  recipientEmail: string | null;
  shippingAddress: ShippingAddress;
  items: Array<{
    productId: string;
    productName: string;
    unitPrice: number;
    quantity: number;
    currency: string;
    productImageUrl: string | null;
  }>;
}

export interface CreateOrderResponse {
  orderId: string;
}

export interface PaginatedOrders {
  items: Order[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}