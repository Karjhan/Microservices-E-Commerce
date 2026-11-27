'use client';

import { useState } from 'react';
import { Button } from '@/components/ui/button';
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
} from '@/components/ui/sheet';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { EmptyState } from '@/components/shared/empty-state';
import { TableSkeleton } from '@/components/shared/loading-skeletons';
import { useStockMovements } from '@/hooks/queries/use-inventory';
import type { StockMovement, StockMovementType } from '@/lib/api/inventory';
import { formatDate } from '@/lib/utils';

const PAGE_SIZE = 15;

const TYPE_LABELS: Record<StockMovementType, string> = {
  Adjust: 'Adjustment',
  Reserve: 'Reserved for order',
  Release: 'Released from order',
  Deduct: 'Shipped for order',
};

function formatDelta(delta: number): string {
  return delta > 0 ? `+${delta}` : String(delta);
}

function describeActor(movement: StockMovement): string {
  if (movement.actor) return movement.actor;
  if (movement.orderId) return `Order ${movement.orderId.slice(0, 8)}`;
  return 'System';
}

interface StockHistorySheetProps {
  productId: string | null;
  productName?: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function StockHistorySheet(props: StockHistorySheetProps) {
  if (!props.open || !props.productId) return null;
  return <StockHistoryBody {...props} productId={props.productId} />;
}

function StockHistoryBody({
  productId,
  productName,
  open,
  onOpenChange,
}: StockHistorySheetProps & { productId: string }) {
  const [page, setPage] = useState(1);
  const { data, isLoading, isError, refetch } = useStockMovements(productId, { page, pageSize: PAGE_SIZE });

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent side="right" className="data-[side=right]:w-full data-[side=right]:sm:max-w-2xl">
        <SheetHeader>
          <SheetTitle>Stock history</SheetTitle>
          <SheetDescription>{productName ?? productId}</SheetDescription>
        </SheetHeader>

        <div className="flex-1 overflow-y-auto px-4 pb-4" data-testid="stock-history">
          {isLoading ? (
            <TableSkeleton rows={5} cols={5} />
          ) : isError ? (
            <div className="space-y-3 py-8 text-center">
              <p className="text-sm text-muted-foreground">The history could not be loaded.</p>
              <Button variant="outline" size="sm" onClick={() => refetch()}>
                Try again
              </Button>
            </div>
          ) : !data || data.items.length === 0 ? (
            <EmptyState title="No changes yet" description="Stock changes will be listed here." />
          ) : (
            <>
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>When</TableHead>
                    <TableHead>Change</TableHead>
                    <TableHead>On hand</TableHead>
                    <TableHead>Who</TableHead>
                    <TableHead>Reason</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {data.items.map((movement) => (
                    <TableRow key={movement.id} data-testid="stock-history-row">
                      <TableCell className="whitespace-nowrap">{formatDate(movement.occurredAt)}</TableCell>
                      <TableCell>
                        <span className="font-medium">{formatDelta(movement.delta)}</span>
                        <span className="block text-xs text-muted-foreground">{TYPE_LABELS[movement.type]}</span>
                      </TableCell>
                      <TableCell>{movement.resultingOnHand}</TableCell>
                      <TableCell className="max-w-[10rem] truncate" title={describeActor(movement)}>
                        {describeActor(movement)}
                      </TableCell>
                      <TableCell>{movement.reason ?? ''}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
              {data.totalPages > 1 && (
                <div className="mt-4 flex items-center justify-center gap-2">
                  <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>
                    Newer
                  </Button>
                  <span className="px-2 text-sm text-muted-foreground">
                    Page {page} of {data.totalPages}
                  </span>
                  <Button
                    variant="outline"
                    size="sm"
                    disabled={page >= data.totalPages}
                    onClick={() => setPage((p) => p + 1)}
                  >
                    Older
                  </Button>
                </div>
              )}
            </>
          )}
        </div>
      </SheetContent>
    </Sheet>
  );
}
