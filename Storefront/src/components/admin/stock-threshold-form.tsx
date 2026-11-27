'use client';

import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
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
import { useSetLowStockThreshold } from '@/hooks/queries/use-inventory';
import { ApiClientError, errorMessage } from '@/lib/api/client';
import type { InventoryRecord } from '@/lib/api/inventory';
import {
  lowStockThresholdSchema,
  MAX_LOW_STOCK_THRESHOLD,
  type LowStockThresholdFormValues,
} from '@/schemas/inventory';

interface StockThresholdFormProps {
  record: InventoryRecord | null;
  productName?: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function StockThresholdForm(props: StockThresholdFormProps) {
  if (!props.open || !props.record) return null;
  return <StockThresholdBody {...props} record={props.record} />;
}

function StockThresholdBody({
  record,
  productName,
  open,
  onOpenChange,
}: StockThresholdFormProps & { record: InventoryRecord }) {
  const mutation = useSetLowStockThreshold();
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm<LowStockThresholdFormValues>({
    resolver: zodResolver(lowStockThresholdSchema),
    defaultValues: { lowStockThreshold: record.lowStockThreshold },
  });

  const onSubmit = (values: LowStockThresholdFormValues) =>
    mutation.mutate(
      { productId: record.productId, lowStockThreshold: values.lowStockThreshold },
      {
        onSuccess: () => {
          toast.success('Low-stock threshold saved.');
          onOpenChange(false);
        },
        onError: (error) => {
          const messages = error instanceof ApiClientError ? error.fieldErrors?.lowStockThreshold : undefined;
          if (messages) setError('lowStockThreshold', { type: 'server', message: messages.join(' ') });
          else toast.error(errorMessage(error, 'Could not save the threshold.'));
        },
      }
    );

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Low-stock threshold</DialogTitle>
          <DialogDescription>{productName ?? record.productId}</DialogDescription>
        </DialogHeader>
        <form onSubmit={handleSubmit(onSubmit)} className="space-y-4" noValidate>
          <div className="space-y-2">
            <Label htmlFor="stock-threshold">Mark as low when available stock is at or below</Label>
            <Input
              id="stock-threshold"
              type="number"
              min={0}
              max={MAX_LOW_STOCK_THRESHOLD}
              step="1"
              inputMode="numeric"
              aria-invalid={!!errors.lowStockThreshold}
              {...register('lowStockThreshold', { valueAsNumber: true })}
            />
            {errors.lowStockThreshold && (
              <p role="alert" className="text-sm text-destructive">
                {errors.lowStockThreshold.message}
              </p>
            )}
            <p className="text-xs text-muted-foreground">Currently {record.available} available.</p>
          </div>
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
