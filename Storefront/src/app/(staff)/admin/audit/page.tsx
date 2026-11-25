'use client';

import { useState } from 'react';
import { AlertTriangle } from 'lucide-react';
import { useAudit } from '@/hooks/queries/use-audit';
import { AUDIT_PAGE_SIZE, MAX_ENTRIES_PER_SERVICE, type AuditEntry } from '@/lib/api/audit';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { ErrorFallback } from '@/components/shared/error-fallback';
import { TableSkeleton } from '@/components/shared/loading-skeletons';
import { EmptyState } from '@/components/shared/empty-state';
import { formatDate } from '@/lib/utils';

const OUTCOME_VARIANT: Record<AuditEntry['outcome'], 'secondary' | 'destructive' | 'outline'> = {
  success: 'secondary',
  refused: 'destructive',
  failed: 'outline',
};

function describeTarget(entry: AuditEntry) {
  return entry.targetId ? `${entry.targetType} ${entry.targetId}` : entry.targetType;
}

function shortId(value: string | null) {
  return value ? value.slice(0, 8) : '';
}

export default function AdminAuditPage() {
  const [page, setPage] = useState(1);
  const { data, isLoading, isError, isFetching, refetch } = useAudit(page, AUDIT_PAGE_SIZE);

  if (isError) return <ErrorFallback title="Could not load the audit trail" onRetry={() => refetch()} />;

  const maxPage = Math.min(
    data?.totalPages ?? 1,
    Math.floor(MAX_ENTRIES_PER_SERVICE / AUDIT_PAGE_SIZE)
  );

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold">Audit trail</h1>
        <p className="text-sm text-muted-foreground">
          Every administrative change and every refused attempt, newest first.
        </p>
      </div>

      {data && data.unavailable.length > 0 && (
        <Alert>
          <AlertTriangle className="h-4 w-4" />
          <AlertTitle>Some entries are missing</AlertTitle>
          <AlertDescription>
            Could not reach: {data.unavailable.join(', ')}. Entries from those services are not shown.
          </AlertDescription>
        </Alert>
      )}

      {isLoading ? (
        <TableSkeleton rows={8} cols={6} />
      ) : data && data.items.length > 0 ? (
        <>
          <div className="overflow-x-auto" data-testid="audit-table">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>When</TableHead>
                  <TableHead>Who</TableHead>
                  <TableHead>Action</TableHead>
                  <TableHead>Target</TableHead>
                  <TableHead>Outcome</TableHead>
                  <TableHead>Service</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {data.items.map((entry) => (
                  <TableRow key={`${entry.source}-${entry.id}`}>
                    <TableCell className="whitespace-nowrap text-sm">{formatDate(entry.occurredAt)}</TableCell>
                    <TableCell className="text-sm">
                      {entry.actorEmail ?? (entry.actorId ? shortId(entry.actorId) : 'Anonymous')}
                    </TableCell>
                    <TableCell className="font-mono text-xs">{entry.action}</TableCell>
                    <TableCell className="max-w-[16rem] truncate font-mono text-xs" title={describeTarget(entry)}>
                      {describeTarget(entry)}
                    </TableCell>
                    <TableCell>
                      <Badge variant={OUTCOME_VARIANT[entry.outcome]}>{entry.outcome}</Badge>
                    </TableCell>
                    <TableCell className="text-sm capitalize">{entry.source}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>

          <div className="flex items-center justify-between gap-4">
            <p className="text-sm text-muted-foreground">
              Page {data.page} of {maxPage}
              {data.totalPages > maxPage && ` (showing the most recent ${MAX_ENTRIES_PER_SERVICE} entries per service)`}
            </p>
            <div className="flex gap-2">
              <Button
                variant="outline"
                size="sm"
                disabled={page <= 1 || isFetching}
                onClick={() => setPage((p) => Math.max(1, p - 1))}
              >
                Newer
              </Button>
              <Button
                variant="outline"
                size="sm"
                disabled={page >= maxPage || isFetching}
                onClick={() => setPage((p) => Math.min(maxPage, p + 1))}
              >
                Older
              </Button>
            </div>
          </div>
        </>
      ) : (
        <EmptyState title="No audit entries yet" description="Administrative activity will appear here." />
      )}
    </div>
  );
}
