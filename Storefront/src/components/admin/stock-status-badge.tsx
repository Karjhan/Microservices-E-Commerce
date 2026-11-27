'use client';

import { Badge } from '@/components/ui/badge';
import { cn } from '@/lib/utils';
import type { StockStatus } from '@/lib/api/inventory';

const CONFIG: Record<StockStatus, { label: string; className: string }> = {
  ok: { label: 'In stock', className: 'bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200' },
  low: { label: 'Low stock', className: 'bg-yellow-100 text-yellow-800 dark:bg-yellow-900 dark:text-yellow-200' },
  out: { label: 'Out of stock', className: 'bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-200' },
};

export function StockStatusBadge({ status, className }: { status: StockStatus; className?: string }) {
  const config = CONFIG[status];
  return (
    <Badge variant="secondary" data-testid="stock-status" data-status={status} className={cn(config.className, className)}>
      {config.label}
    </Badge>
  );
}
