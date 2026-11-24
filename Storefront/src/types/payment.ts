export type PaymentStatus =
  | 'Pending'
  | 'Processing'
  | 'Completed'
  | 'Failed'
  | 'Cancelled'
  | 'PartiallyRefunded'
  | 'Refunded';

export type PaymentMethod = 'CreditCard' | 'DebitCard';
export type TransactionType = 'Charge' | 'Refund';

export interface PaymentTransaction {
  id: string;
  type: TransactionType;
  amount: number;
  currency: string;
  providerTransactionId: string | null;
  status: string;
  createdAt: string;
}

export interface Payment {
  id: string;
  orderId: string;
  userId: string;
  amount: number;
  currency: string;
  status: PaymentStatus;
  method: PaymentMethod;
  idempotencyKey: string;
  refundedAmount: number;
  expiresAt: string;
  transactions: PaymentTransaction[];
  createdAt: string;
  updatedAt: string | null;
}

export interface CreatePaymentRequest {
  orderId: string;
  amount: number;
  currency: string;
  method: PaymentMethod;
  paymentToken: string;
  idempotencyKey: string;
}

export interface PaginatedPayments {
  items: Payment[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}
