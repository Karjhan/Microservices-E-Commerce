'use client';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import type { PaymentStatus as PaymentStatusType } from '@/types/payment';

const statusConfig: Record<PaymentStatusType, { label: string; variant: 'default' | 'secondary' | 'destructive' | 'outline' }> = {
  Pending: { label: 'Pending', variant: 'outline' },
  Processing: { label: 'Processing', variant: 'secondary' },
  Completed: { label: 'Paid', variant: 'default' },
  Failed: { label: 'Failed', variant: 'destructive' },
  Cancelled: { label: 'Cancelled', variant: 'outline' },
  PartiallyRefunded: { label: 'Partially Refunded', variant: 'secondary' },
  Refunded: { label: 'Refunded', variant: 'outline' },
};

interface PaymentStatusProps {
  status: PaymentStatusType;
  onRetry?: () => void;
}

export function PaymentStatus({ status, onRetry }: PaymentStatusProps) {
  const config = statusConfig[status] || { label: status, variant: 'outline' as const };

  return (
    <div className="flex items-center gap-2">
      <Badge variant={config.variant}>{config.label}</Badge>
      {status === 'Failed' && onRetry && (
        <Button variant="outline" size="sm" onClick={onRetry}>
          Retry Payment
        </Button>
      )}
    </div>
  );
}
