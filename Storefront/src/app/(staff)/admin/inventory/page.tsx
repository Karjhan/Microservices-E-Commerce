'use client';

import { useMemo, useState } from 'react';
import Link from 'next/link';
import { History, SlidersHorizontal, Gauge } from 'lucide-react';
import { useAdminProducts } from '@/hooks/queries/use-admin-products';
import { useInventoryList } from '@/hooks/queries/use-inventory';
import { Button } from '@/components/ui/button';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { EmptyState } from '@/components/shared/empty-state';
import { ErrorFallback } from '@/components/shared/error-fallback';
import { TableSkeleton } from '@/components/shared/loading-skeletons';
import { StockAdjustDialog } from '@/components/admin/stock-adjust-dialog';
import { StockHistorySheet } from '@/components/admin/stock-history-sheet';
import { StockStatusBadge } from '@/components/admin/stock-status-badge';
import { StockThresholdForm } from '@/components/admin/stock-threshold-form';
import type { StockStatusFilter } from '@/lib/api/inventory';

const PAGE_SIZE = 20;

const FILTERS: { value: StockStatusFilter; label: string }[] = [
  { value: 'all', label: 'All' },
  { value: 'low', label: 'Low stock' },
  { value: 'out', label: 'Out of stock' },
];

type Dialog = { kind: 'adjust' | 'history' | 'threshold'; productId: string } | null;

export default function AdminInventoryPage() {
  const [status, setStatus] = useState<StockStatusFilter>('all');
  const [page, setPage] = useState(1);
  const [dialog, setDialog] = useState<Dialog>(null);

  const { data, isLoading, isError, refetch } = useInventoryList({ status, page, pageSize: PAGE_SIZE });

  const productIds = useMemo(() => data?.items.map((i) => i.productId) ?? [], [data]);
  const { data: products } = useAdminProducts({
    productIds,
    page: 1,
    pageSize: Math.max(productIds.length, 1),
  });
  const names = useMemo(
    () => new Map((products?.items ?? []).map((p) => [p.id, p.name] as const)),
    [products]
  );

  const selected = dialog ? (data?.items.find((i) => i.productId === dialog.productId) ?? null) : null;
  const selectedName = dialog ? names.get(dialog.productId) : undefined;

  if (isError) return <ErrorFallback onRetry={() => refetch()} />;

  const close = (kind: NonNullable<Dialog>['kind']) => (open: boolean) => {
    if (!open) setDialog((d) => (d?.kind === kind ? null : d));
  };

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-1">
        <h1 className="text-2xl font-bold">Inventory</h1>
        <p className="text-sm text-muted-foreground">
          The products that need attention first: out of stock, then low on stock.
        </p>
      </div>

      <div className="flex flex-wrap gap-2" role="group" aria-label="Stock filter">
        {FILTERS.map((f) => (
          <Button
            key={f.value}
            type="button"
            size="sm"
            variant={status === f.value ? 'default' : 'outline'}
            aria-pressed={status === f.value}
            onClick={() => {
              setStatus(f.value);
              setPage(1);
            }}
          >
            {f.label}
          </Button>
        ))}
      </div>

      {isLoading ? (
        <TableSkeleton rows={5} cols={7} />
      ) : data && data.items.length === 0 ? (
        <EmptyState
          title={status === 'all' ? 'No stock records yet' : 'Nothing to show'}
          description={
            status === 'all'
              ? 'Products get a stock record when their first stock is added.'
              : 'No products are in this state right now.'
          }
        />
      ) : data ? (
        <>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Product</TableHead>
                <TableHead>On hand</TableHead>
                <TableHead>Reserved</TableHead>
                <TableHead>Available</TableHead>
                <TableHead>Threshold</TableHead>
                <TableHead>Status</TableHead>
                <TableHead className="text-right">Actions</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {data.items.map((item) => {
                const name = names.get(item.productId);
                return (
                  <TableRow key={item.productId} data-testid="inventory-row">
                    <TableCell>
                      <Link
                        href={`/admin/products/${item.productId}`}
                        className="font-medium hover:underline"
                      >
                        {name ?? <span className="font-mono text-xs">{item.productId.slice(0, 8)}</span>}
                      </Link>
                    </TableCell>
                    <TableCell>{item.onHand}</TableCell>
                    <TableCell>{item.reserved}</TableCell>
                    <TableCell>{item.available}</TableCell>
                    <TableCell>{item.lowStockThreshold}</TableCell>
                    <TableCell>
                      <StockStatusBadge status={item.status} />
                    </TableCell>
                    <TableCell>
                      <div className="flex justify-end gap-1">
                        <Button
                          variant="outline"
                          size="sm"
                          onClick={() => setDialog({ kind: 'adjust', productId: item.productId })}
                        >
                          <SlidersHorizontal className="mr-1 h-3.5 w-3.5" aria-hidden />
                          Adjust
                        </Button>
                        <Button
                          variant="outline"
                          size="sm"
                          onClick={() => setDialog({ kind: 'history', productId: item.productId })}
                        >
                          <History className="mr-1 h-3.5 w-3.5" aria-hidden />
                          History
                        </Button>
                        <Button
                          variant="ghost"
                          size="sm"
                          aria-label={`Low-stock threshold${name ? ` for ${name}` : ''}`}
                          onClick={() => setDialog({ kind: 'threshold', productId: item.productId })}
                        >
                          <Gauge className="h-3.5 w-3.5" aria-hidden />
                        </Button>
                      </div>
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>

          {data.totalPages > 1 && (
            <div className="flex justify-center gap-2">
              <Button variant="outline" disabled={page === 1} onClick={() => setPage((p) => p - 1)}>
                Previous
              </Button>
              <span className="flex items-center px-4 text-sm text-muted-foreground">
                Page {page} of {data.totalPages}
              </span>
              <Button variant="outline" disabled={page >= data.totalPages} onClick={() => setPage((p) => p + 1)}>
                Next
              </Button>
            </div>
          )}
        </>
      ) : null}

      <StockAdjustDialog
        record={selected}
        productName={selectedName}
        open={dialog?.kind === 'adjust'}
        onOpenChange={close('adjust')}
      />
      <StockThresholdForm
        record={selected}
        productName={selectedName}
        open={dialog?.kind === 'threshold'}
        onOpenChange={close('threshold')}
      />
      <StockHistorySheet
        productId={dialog?.kind === 'history' ? dialog.productId : null}
        productName={selectedName}
        open={dialog?.kind === 'history'}
        onOpenChange={close('history')}
      />
    </div>
  );
}
