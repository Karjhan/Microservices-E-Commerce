'use client';

import { toast } from 'sonner';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { useSetProductStatus } from '@/hooks/queries/use-admin-products';
import { errorMessage } from '@/lib/api/client';
import { STATUS_TRANSITIONS, type ProductStatus } from '@/types/product';

const ACTION_LABELS: Record<ProductStatus, string> = {
  Draft: 'Move back to draft',
  Active: 'Publish',
  Inactive: 'Deactivate',
  OutOfStock: 'Mark out of stock',
  Discontinued: 'Discontinue',
};

const HELP: Record<ProductStatus, string> = {
  Draft: 'Only visible to administrators.',
  Active: 'Visible to shoppers and included in search and recommendations.',
  Inactive: 'Hidden from shoppers; can be published again.',
  OutOfStock: 'Hidden from shoppers until it is published again.',
  Discontinued: 'Hidden from shoppers for good; the status cannot be changed any more.',
};

interface ProductStatusControlProps {
  productId: string;
  status: ProductStatus;
}

/** Shows the current status and only the transitions the server will accept. */
export function ProductStatusControl({ productId, status }: ProductStatusControlProps) {
  const mutation = useSetProductStatus(productId);
  const next = STATUS_TRANSITIONS[status] ?? [];

  return (
    <Card>
      <CardHeader>
        <CardTitle>Status</CardTitle>
      </CardHeader>
      <CardContent className="space-y-3">
        <div className="flex items-center gap-2">
          <Badge data-testid="product-status">{status}</Badge>
          <span className="text-sm text-muted-foreground">{HELP[status]}</span>
        </div>
        {next.length > 0 && (
          <div className="flex flex-wrap gap-2">
            {next.map((target) => (
              <Button
                key={target}
                type="button"
                variant={target === 'Discontinued' ? 'destructive' : target === 'Active' ? 'default' : 'outline'}
                size="sm"
                disabled={mutation.isPending}
                onClick={() =>
                  mutation.mutate(target, {
                    onSuccess: () => toast.success(`Status changed to ${target}.`),
                    onError: (e) => toast.error(errorMessage(e, 'Could not change the status.')),
                  })
                }
              >
                {ACTION_LABELS[target]}
              </Button>
            ))}
          </div>
        )}
      </CardContent>
    </Card>
  );
}
