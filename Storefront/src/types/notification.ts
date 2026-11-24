export type NotificationChannel = 'Email' | 'Sms' | 'Push';
export type NotificationStatus = 'Pending' | 'Sent' | 'Failed' | 'DeadLettered';

export type NotificationType =
  | 'Welcome'
  | 'OrderConfirmation'
  | 'PaymentReceipt'
  | 'ShippingUpdate'
  | 'OrderCancelled'
  | 'RefundProcessed';

export interface NotificationRecord {
  id: string;
  idempotencyKey: string;
  eventType: string;
  notificationType: NotificationType;
  channel: NotificationChannel;
  recipient: string;
  subject: string;
  bodyPreview: string | null;
  status: NotificationStatus;
  attemptCount: number;
  correlationId: string | null;
  errorDetails: string | null;
  createdAt: string;
  sentAt: string | null;
  updatedAt: string;
}

export interface PaginatedNotifications {
  items: NotificationRecord[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface NotificationFilters {
  eventId?: string;
  eventType?: string;
  recipient?: string;
  status?: NotificationStatus;
  page?: number;
  pageSize?: number;
}
