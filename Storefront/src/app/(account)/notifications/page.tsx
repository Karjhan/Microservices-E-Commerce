'use client';

import { useState } from 'react';
import { useNotifications } from '@/hooks/queries/use-notifications';
import { Badge } from '@/components/ui/badge';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { EmptyState } from '@/components/shared/empty-state';
import { ErrorFallback } from '@/components/shared/error-fallback';
import { TableSkeleton } from '@/components/shared/loading-skeletons';
import { formatDate } from '@/lib/utils';
import type { NotificationFilters, NotificationRecord } from '@/types/notification';

const STATUSES = ['Pending', 'Sent', 'Failed', 'DeadLettered'] as const;

export default function NotificationsPage() {
  const [filters, setFilters] = useState<NotificationFilters>({ page: 1, pageSize: 20 });
  const [selected, setSelected] = useState<NotificationRecord | null>(null);
  const { data, isLoading, isError, refetch } = useNotifications(filters);

  if (isError) return <ErrorFallback onRetry={() => refetch()} />;

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold">Notifications</h1>

      <div className="flex gap-4">
        <Select
          value={filters.status || ''}
          onValueChange={(v) => setFilters((prev) => ({ ...prev, status: (v || undefined) as any }))}
        >
          <SelectTrigger className="w-full sm:w-[160px]">
            <SelectValue placeholder="All Statuses" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All Statuses</SelectItem>
            {STATUSES.map((s) => (
              <SelectItem key={s} value={s}>{s}</SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {isLoading ? (
        <TableSkeleton rows={5} cols={6} />
      ) : data && data.items.length > 0 ? (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Event</TableHead>
              <TableHead>Type</TableHead>
              <TableHead>Channel</TableHead>
              <TableHead>Subject</TableHead>
              <TableHead>Status</TableHead>
              <TableHead>Date</TableHead>
              <TableHead></TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {data.items.map((notification) => (
              <TableRow key={notification.id}>
                <TableCell className="text-xs">{notification.eventType}</TableCell>
                <TableCell>{notification.notificationType}</TableCell>
                <TableCell>{notification.channel}</TableCell>
                <TableCell className="max-w-[200px] truncate">{notification.subject}</TableCell>
                <TableCell><Badge variant="secondary">{notification.status}</Badge></TableCell>
                <TableCell className="text-sm">{formatDate(notification.createdAt)}</TableCell>
                <TableCell>
                  <Dialog>
                    <DialogTrigger
                      render={
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => setSelected(notification)}
                        />
                      }
                    >
                      Details
                    </DialogTrigger>
                    <DialogContent>
                      <DialogHeader>
                        <DialogTitle>Notification Details</DialogTitle>
                      </DialogHeader>
                      {selected && (
                        <dl className="space-y-3 text-sm">
                          <div><dt className="text-muted-foreground">Event Type</dt><dd>{selected.eventType}</dd></div>
                          <div><dt className="text-muted-foreground">Type</dt><dd>{selected.notificationType}</dd></div>
                          <div><dt className="text-muted-foreground">Channel</dt><dd>{selected.channel}</dd></div>
                          <div><dt className="text-muted-foreground">Recipient</dt><dd>{selected.recipient}</dd></div>
                          <div><dt className="text-muted-foreground">Subject</dt><dd>{selected.subject}</dd></div>
                          {selected.bodyPreview && <div><dt className="text-muted-foreground">Preview</dt><dd>{selected.bodyPreview}</dd></div>}
                          <div><dt className="text-muted-foreground">Status</dt><dd><Badge variant="secondary">{selected.status}</Badge></dd></div>
                          <div><dt className="text-muted-foreground">Attempts</dt><dd>{selected.attemptCount}</dd></div>
                          <div><dt className="text-muted-foreground">Created</dt><dd>{formatDate(selected.createdAt)}</dd></div>
                          {selected.sentAt && <div><dt className="text-muted-foreground">Sent</dt><dd>{formatDate(selected.sentAt)}</dd></div>}
                          {selected.errorDetails && <div><dt className="text-muted-foreground">Error</dt><dd className="text-destructive">{selected.errorDetails}</dd></div>}
                        </dl>
                      )}
                    </DialogContent>
                  </Dialog>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      ) : (
        <EmptyState title="No notifications" description="Your notification history will appear here" />
      )}
    </div>
  );
}