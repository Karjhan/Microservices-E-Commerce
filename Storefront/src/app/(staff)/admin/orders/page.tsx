'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useOrders } from '@/hooks/queries/use-orders';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { ErrorFallback } from '@/components/shared/error-fallback';
import { TableSkeleton } from '@/components/shared/loading-skeletons';
import { EmptyState } from '@/components/shared/empty-state';
import { formatCurrency, formatDate } from '@/lib/utils';
import type { OrderFilters } from '@/lib/api/orders';

const STATUSES = ['PendingPayment', 'Paid', 'Processing', 'Shipped', 'Delivered', 'Cancelled', 'PaymentFailed'];

export default function AdminOrdersPage() {
  const [filters, setFilters] = useState<OrderFilters>({ page: 1, pageSize: 20 });
  const { data, isLoading, isError, refetch } = useOrders(filters);

  if (isError) return <ErrorFallback onRetry={() => refetch()} />;

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold">All Orders</h1>
      <div className="flex flex-col sm:flex-row gap-4">
        <Select value={filters.status || ''} onValueChange={(v) => setFilters((p) => ({ ...p, status: v || undefined, page: 1 }))}>
          <SelectTrigger className="w-full sm:w-[180px]"><SelectValue placeholder="All Statuses" /></SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All Statuses</SelectItem>
            {STATUSES.map((s) => (<SelectItem key={s} value={s}>{s}</SelectItem>))}
          </SelectContent>
        </Select>
        <Input placeholder="User ID" className="max-w-[200px]" value={filters.userId || ''} onChange={(e) => setFilters((p) => ({ ...p, userId: e.target.value || undefined, page: 1 }))} />
      </div>

      {isLoading ? (<TableSkeleton rows={5} cols={5} />) : data && data.items.length > 0 ? (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Order</TableHead>
              <TableHead>User</TableHead>
              <TableHead>Status</TableHead>
              <TableHead>Total</TableHead>
              <TableHead>Date</TableHead>
              <TableHead></TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {data.items.map((order) => (
              <TableRow key={order.id}>
                <TableCell className="font-mono text-xs">#{order.id.slice(0, 8)}</TableCell>
                <TableCell className="font-mono text-xs">{order.userId.slice(0, 8)}</TableCell>
                <TableCell><Badge variant="secondary">{order.status}</Badge></TableCell>
                <TableCell>{formatCurrency(order.totalAmount, order.currency)}</TableCell>
                <TableCell className="text-sm">{formatDate(order.createdAt)}</TableCell>
                <TableCell><Button variant="ghost" size="sm" render={<Link href={`/admin/orders/${order.id}`} />}>View</Button></TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      ) : (<EmptyState title="No orders" />)}
    </div>
  );
}