export type DocumentType = 'Invoice' | 'Receipt' | 'CreditNote';
export type DocumentStatus = 'Pending' | 'Completed' | 'Failed';

export interface DocumentMetadata {
  id: string;
  documentType: DocumentType;
  idempotencyKey: string;
  templateVersion: string;
  status: DocumentStatus;
  storageObjectKey: string | null;
  storageBucket: string | null;
  orderId: string;
  paymentId: string;
  userId: string;
  correlationId: string | null;
  failureReason: string | null;
  fileSizeBytes: number;
  createdAt: string;
  completedAt: string | null;
  failedAt: string | null;
}

export interface DocumentListResponse {
  items: DocumentMetadata[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}
