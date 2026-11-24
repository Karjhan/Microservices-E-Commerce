import { apiGet, buildQueryString } from './client';
import type { NotificationRecord, PaginatedNotifications, NotificationFilters } from '@/types/notification';

export async function listNotifications(filters: NotificationFilters = {}): Promise<PaginatedNotifications> {
  const qs = buildQueryString(filters);
  return apiGet<PaginatedNotifications>(`/notifications${qs}`);
}

export async function getNotificationById(id: string): Promise<NotificationRecord> {
  return apiGet<NotificationRecord>(`/notifications/${id}`);
}
