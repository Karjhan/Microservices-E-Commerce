'use client';

import { Badge } from '@/components/ui/badge';
import { cn } from '@/lib/utils';

type AvailabilitySignal = 'in-stock' | 'low-stock' | 'out-of-stock';

function deriveAvailabilitySignal(available: number): AvailabilitySignal {
  if (available <= 0) return 'out-of-stock';
  if (available <= 10) return 'low-stock';
  return 'in-stock';
}

const signalConfig: Record<AvailabilitySignal, { label: string; className: string }> = {
  'in-stock': { label: 'In Stock', className: 'bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200' },
  'low-stock': { label: 'Low Stock', className: 'bg-yellow-100 text-yellow-800 dark:bg-yellow-900 dark:text-yellow-200' },
  'out-of-stock': { label: 'Out of Stock', className: 'bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-200' },
};

interface AvailabilityBadgeProps {
  available: number;
  className?: string;
}

export function AvailabilityBadge({ available, className }: AvailabilityBadgeProps) {
  const signal = deriveAvailabilitySignal(available);
  const config = signalConfig[signal];

  return (
    <Badge variant="secondary" className={cn(config.className, className)}>
      {config.label}
      {signal === 'low-stock' && ` (${available} left)`}
    </Badge>
  );
}
