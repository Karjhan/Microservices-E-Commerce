'use client';

import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useInventoryList } from '@/hooks/queries/use-inventory';
import { adjustStock } from '@/lib/api/inventory';
import { stockAdjustmentSchema, type StockAdjustmentFormValues } from '@/schemas/inventory';
import { AvailabilityBadge } from '@/components/products/availability-badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { ErrorFallback } from '@/components/shared/error-fallback';
import { TableSkeleton } from '@/components/shared/loading-skeletons';
import { queryKeys } from '@/lib/constants';

export default function AdminInventoryPage() {
  const [depletedOnly, setDepletedOnly] = useState(false);
  const [page, setPage] = useState(1);
  const { data, isLoading, isError, refetch } = useInventoryList({ depletedOnly, page, pageSize: 20 });
  const queryClient = useQueryClient();

  const [adjustProductId, setAdjustProductId] = useState<string | null>(null);
  const { register, handleSubmit, reset, formState: { errors } } = useForm<StockAdjustmentFormValues>({
    resolver: zodResolver(stockAdjustmentSchema),
  });

  const adjustMutation = useMutation({
    mutationFn: (data: StockAdjustmentFormValues & { productId: string }) =>
      adjustStock(data.productId, data.delta, data.reason),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.inventory.all });
      setAdjustProductId(null);
      reset();
    },
  });

  if (isError) return <ErrorFallback onRetry={() => refetch()} />;

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold">Inventory</h1>

      <label className="flex items-center gap-2 text-sm">
        <input type="checkbox" checked={depletedOnly} onChange={(e) => { setDepletedOnly(e.target.checked); setPage(1); }} />
        Show depleted only
      </label>

      {isLoading ? (
        <TableSkeleton rows={5} cols={5} />
      ) : data ? (
        <>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Product ID</TableHead>
                <TableHead>On Hand</TableHead>
                <TableHead>Reserved</TableHead>
                <TableHead>Available</TableHead>
                <TableHead>Status</TableHead>
                <TableHead></TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {data.items.map((item) => (
                <TableRow key={item.productId}>
                  <TableCell className="font-mono text-xs">{item.productId.slice(0, 8)}</TableCell>
                  <TableCell>{item.onHand}</TableCell>
                  <TableCell>{item.reserved}</TableCell>
                  <TableCell>{item.available}</TableCell>
                  <TableCell><AvailabilityBadge available={item.available} /></TableCell>
                  <TableCell>
                    <Dialog>
                      <DialogTrigger
                        render={
                          <Button
                            variant="outline"
                            size="sm"
                            onClick={() => setAdjustProductId(item.productId)}
                          />
                        }
                      >
                        Adjust
                      </DialogTrigger>
                      <DialogContent>
                        <DialogHeader><DialogTitle>Stock Adjustment</DialogTitle></DialogHeader>
                        <form onSubmit={handleSubmit((data) => adjustMutation.mutate({ ...data, productId: adjustProductId! }))} className="space-y-4">
                          <div className="space-y-2">
                            <Label>Delta (positive to add, negative to remove)</Label>
                            <Input type="number" {...register('delta', { valueAsNumber: true })} />
                            {errors.delta && <p className="text-sm text-destructive">{errors.delta.message}</p>}
                          </div>
                          <div className="space-y-2">
                            <Label>Reason</Label>
                            <Input {...register('reason')} />
                            {errors.reason && <p className="text-sm text-destructive">{errors.reason.message}</p>}
                          </div>
                          <Button type="submit" disabled={adjustMutation.isPending}>
                            {adjustMutation.isPending ? 'Adjusting...' : 'Submit'}
                          </Button>
                        </form>
                      </DialogContent>
                    </Dialog>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
          {data.totalPages > 1 && (
            <div className="flex justify-center gap-2">
              <Button variant="outline" disabled={page === 1} onClick={() => setPage((p) => p - 1)}>Previous</Button>
              <span className="flex items-center px-4 text-sm text-muted-foreground">Page {page} of {data.totalPages}</span>
              <Button variant="outline" disabled={page >= data.totalPages} onClick={() => setPage((p) => p + 1)}>Next</Button>
            </div>
          )}
        </>
      ) : null}
    </div>
  );
}