'use client';

import { useQuery } from '@tanstack/react-query';
import { listNotifications, getNotificationById } from '@/lib/api/notifications';
import { queryKeys } from '@/lib/constants';
import type { NotificationFilters } from '@/types/notification';

export function useNotifications(filters: NotificationFilters = {}) {
  return useQuery({
    queryKey: queryKeys.notifications.list(filters),
    queryFn: () => listNotifications(filters),
  });
}

export function useNotification(id: string) {
  return useQuery({
    queryKey: queryKeys.notifications.detail(id),
    queryFn: () => getNotificationById(id),
    enabled: !!id,
  });
}
