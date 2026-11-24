import Link from 'next/link';
import { Badge } from '@/components/ui/badge';
import { formatCurrency, formatDate } from '@/lib/utils';
import type { Order } from '@/types/order';

export function OrderCard({ order }: { order: Order }) {
  return (
    <Link href={`/orders/${order.id}`}>
      <div className="flex items-center justify-between rounded-lg border p-4 hover:bg-accent transition-colors">
        <div className="space-y-1">
          <p className="font-medium">Order #{order.id.slice(0, 8)}</p>
          <p className="text-sm text-muted-foreground">{formatDate(order.createdAt)}</p>
          <p className="text-sm">{order.items.length} item(s)</p>
        </div>
        <div className="text-right space-y-1">
          <p className="font-bold">{formatCurrency(order.totalAmount, order.currency)}</p>
          <Badge variant="secondary">{order.status}</Badge>
        </div>
      </div>
    </Link>
  );
}
