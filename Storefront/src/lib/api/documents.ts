import { apiGet, buildQueryString } from './client';
import type { DocumentMetadata, DocumentListResponse } from '@/types/document';

export interface DocumentFilters {
  orderId?: string;
  paymentId?: string;
  documentType?: string;
  status?: string;
  page?: number;
  pageSize?: number;
}

export async function listDocuments(filters: DocumentFilters): Promise<DocumentListResponse> {
  const qs = buildQueryString(filters);
  return apiGet<DocumentListResponse>(`/documents${qs}`);
}

export async function getDocumentById(documentId: string): Promise<DocumentMetadata> {
  return apiGet<DocumentMetadata>(`/documents/${documentId}`);
}
