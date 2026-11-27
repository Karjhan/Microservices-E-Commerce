'use client';

import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { useAdjustStock } from '@/hooks/queries/use-inventory';
import { ApiClientError, errorMessage } from '@/lib/api/client';
import type { InventoryRecord } from '@/lib/api/inventory';
import { queryKeys } from '@/lib/constants';
import { stockAdjustmentSchema, type StockAdjustmentFormValues } from '@/schemas/inventory';

interface StockAdjustDialogProps {
  record: InventoryRecord | null;
  productName?: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

const DEFAULTS: StockAdjustmentFormValues = { mode: 'delta', quantity: Number.NaN, reason: '' };

function describeFailure(error: unknown): { message: string; stale: boolean } {
  if (error instanceof ApiClientError) {
    if (error.status === 409 && error.code === 'stale_version') {
      return {
        stale: true,
        message:
          'Someone else changed this stock while you were editing. The latest figures are shown above; check them and submit again.',
      };
    }
    if (error.status === 422 && error.code === 'below_reserved') {
      return {
        stale: false,
        message:
          error.detail ||
          'The on-hand count cannot go below what is reserved for open orders. Enter a higher count.',
      };
    }
    if (error.status === 422 && error.code === 'below_zero') {
      return { stale: false, message: error.detail || 'The on-hand count cannot go below zero.' };
    }
  }
  return { stale: false, message: errorMessage(error, 'Could not change the stock.') };
}

export function StockAdjustDialog(props: StockAdjustDialogProps) {
  if (!props.open || !props.record) return null;
  return <StockAdjustBody {...props} record={props.record} />;
}

function StockAdjustBody({
  record,
  productName,
  open,
  onOpenChange,
}: StockAdjustDialogProps & { record: InventoryRecord }) {
  const queryClient = useQueryClient();
  const mutation = useAdjustStock();
  const [failure, setFailure] = useState<{ message: string; stale: boolean } | null>(null);

  const {
    register,
    handleSubmit,
    setError,
    watch,
    setValue,
    formState: { errors },
  } = useForm<StockAdjustmentFormValues>({
    resolver: zodResolver(stockAdjustmentSchema),
    defaultValues: DEFAULTS,
  });

  const mode = watch('mode');
  const quantity = watch('quantity');

  const hasQuantity = Number.isFinite(quantity);
  const resulting = !hasQuantity ? null : mode === 'set' ? quantity : record.onHand + quantity;

  const onSubmit = (values: StockAdjustmentFormValues) => {
    setFailure(null);
    mutation.mutate(
      {
        productId: record.productId,
        mode: values.mode,
        quantity: values.quantity,
        reason: values.reason.trim(),
        expectedVersion: record.version,
      },
      {
        onSuccess: () => {
          toast.success('Stock updated.');
          onOpenChange(false);
        },
        onError: (error) => {
          if (error instanceof ApiClientError && error.fieldErrors) {
            for (const [field, messages] of Object.entries(error.fieldErrors)) {
              const key = field.toLowerCase();
              if (key === 'reason' || key === 'quantity') {
                setError(key, { type: 'server', message: messages.join(' ') });
              }
            }
          }
          const described = describeFailure(error);
          setFailure(described);
          if (described.stale) queryClient.invalidateQueries({ queryKey: queryKeys.inventory.all });
        },
      }
    );
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Adjust stock</DialogTitle>
          <DialogDescription>{productName ?? record.productId}</DialogDescription>
        </DialogHeader>

        <dl className="grid grid-cols-3 gap-2 rounded-md border p-3 text-sm" data-testid="stock-current">
          <div>
            <dt className="text-muted-foreground">On hand</dt>
            <dd className="font-medium">{record.onHand}</dd>
          </div>
          <div>
            <dt className="text-muted-foreground">Reserved</dt>
            <dd className="font-medium">{record.reserved}</dd>
          </div>
          <div>
            <dt className="text-muted-foreground">Available</dt>
            <dd className="font-medium">{record.available}</dd>
          </div>
        </dl>

        <form onSubmit={handleSubmit(onSubmit)} className="space-y-4" noValidate>
          <div className="space-y-2">
            <Label>How do you want to change it?</Label>
            <div className="flex gap-2" role="group" aria-label="Adjustment type">
              <Button
                type="button"
                size="sm"
                variant={mode === 'delta' ? 'default' : 'outline'}
                aria-pressed={mode === 'delta'}
                onClick={() => setValue('mode', 'delta', { shouldValidate: true })}
              >
                Add or remove
              </Button>
              <Button
                type="button"
                size="sm"
                variant={mode === 'set' ? 'default' : 'outline'}
                aria-pressed={mode === 'set'}
                onClick={() => setValue('mode', 'set', { shouldValidate: true })}
              >
                Set exact count
              </Button>
            </div>
          </div>

          <div className="space-y-2">
            <Label htmlFor="stock-quantity">
              {mode === 'delta' ? 'Amount (positive adds, negative removes)' : 'New on-hand count'}
            </Label>
            <Input
              id="stock-quantity"
              type="number"
              step="1"
              inputMode="numeric"
              aria-invalid={!!errors.quantity}
              {...register('quantity', { valueAsNumber: true })}
            />
            {errors.quantity && (
              <p role="alert" className="text-sm text-destructive">
                {errors.quantity.message}
              </p>
            )}
            {resulting !== null && (
              <p className="text-xs text-muted-foreground" data-testid="stock-preview">
                On hand after this change: <span className="font-medium">{resulting}</span>
                {record.reserved > 0 && <> (reserved {record.reserved} stays reserved)</>}
              </p>
            )}
          </div>

          <div className="space-y-2">
            <Label htmlFor="stock-reason">Reason</Label>
            <Input
              id="stock-reason"
              placeholder="e.g. Delivery received, stocktake, damaged"
              aria-invalid={!!errors.reason}
              {...register('reason')}
            />
            {errors.reason && (
              <p role="alert" className="text-sm text-destructive">
                {errors.reason.message}
              </p>
            )}
          </div>

          {failure && (
            <p role="alert" data-testid="stock-error" className="text-sm text-destructive">
              {failure.message}
            </p>
          )}

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)} disabled={mutation.isPending}>
              Cancel
            </Button>
            <Button type="submit" disabled={mutation.isPending}>
              {mutation.isPending ? 'Saving...' : 'Save'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
