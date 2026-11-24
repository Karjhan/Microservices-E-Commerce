'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useOrders } from '@/hooks/queries/use-orders';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { EmptyState } from '@/components/shared/empty-state';
import { ErrorFallback } from '@/components/shared/error-fallback';
import { ListSkeleton } from '@/components/shared/loading-skeletons';
import { formatCurrency, formatDate } from '@/lib/utils';
import type { OrderFilters } from '@/lib/api/orders';

const ORDER_STATUSES = ['PendingPayment', 'Paid', 'Processing', 'Shipped', 'Delivered', 'Cancelled', 'PaymentFailed'];

export default function OrdersPage() {
  const [filters, setFilters] = useState<OrderFilters>({ page: 1, pageSize: 20 });
  const { data, isLoading, isError, refetch } = useOrders(filters);

  if (isError) return <ErrorFallback onRetry={() => refetch()} />;

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold">My Orders</h1>

      <div className="flex gap-4">
        <Select
          value={filters.status || ''}
          onValueChange={(v) => setFilters((prev) => ({ ...prev, status: v || undefined, page: 1 }))}
        >
          <SelectTrigger className="w-full sm:w-[180px]">
            <SelectValue placeholder="All Statuses" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All Statuses</SelectItem>
            {ORDER_STATUSES.map((s) => (
              <SelectItem key={s} value={s}>{s}</SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {isLoading ? (
        <ListSkeleton rows={5} />
      ) : data && data.items.length > 0 ? (
        <div className="space-y-4">
          {data.items.map((order) => (
            <Link key={order.id} href={`/orders/${order.id}`}>
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
          ))}
        </div>
      ) : (
        <EmptyState
          title="No orders yet"
          description="When you place an order, it will appear here"
          action={<Button render={<Link href="/products" />}>Browse Products</Button>}
        />
      )}
    </div>
  );
}