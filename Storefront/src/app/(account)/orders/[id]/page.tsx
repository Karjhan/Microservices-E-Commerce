'use client';

import { use } from 'react';
import Image from 'next/image';
import { useOrder, useCancelOrder, useUpdateShippingAddress } from '@/hooks/queries/use-orders';
import { usePaymentByOrder } from '@/hooks/queries/use-payments';
import { PaymentStatus } from '@/components/orders/payment-status';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Separator } from '@/components/ui/separator';
import { Skeleton } from '@/components/ui/skeleton';
import { ErrorFallback } from '@/components/shared/error-fallback';
import { formatCurrency, formatDate, resolveMediaUrl } from '@/lib/utils';

export default function OrderDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const { data: order, isLoading, isError, refetch } = useOrder(id);
  const { data: payment } = usePaymentByOrder(id);
  const cancelMutation = useCancelOrder();

  if (isLoading) {
    return <div className="space-y-4"><Skeleton className="h-8 w-48" /><Skeleton className="h-64 w-full" /></div>;
  }

  if (isError || !order) return <ErrorFallback title="Order not found" onRetry={() => refetch()} />;

  const canCancel = ['PendingPayment', 'Paid'].includes(order.status);

  const handleCancel = async () => {
    if (confirm('Are you sure you want to cancel this order?')) {
      await cancelMutation.mutateAsync({ orderId: id, reason: 'Customer requested cancellation' });
      refetch();
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold">Order #{order.id.slice(0, 8)}</h1>
          <p className="text-muted-foreground">{formatDate(order.createdAt)}</p>
        </div>
        <Badge variant="secondary" className="text-base">{order.status}</Badge>
      </div>

      <div className="grid gap-6 md:grid-cols-2">
        <Card>
          <CardHeader><CardTitle>Status Timeline</CardTitle></CardHeader>
          <CardContent>
            <dl className="space-y-2 text-sm">
              <div className="flex justify-between"><dt>Created</dt><dd>{formatDate(order.createdAt)}</dd></div>
              {order.paidAt && <div className="flex justify-between"><dt>Paid</dt><dd>{formatDate(order.paidAt)}</dd></div>}
              {order.shippedAt && <div className="flex justify-between"><dt>Shipped</dt><dd>{formatDate(order.shippedAt)}</dd></div>}
              {order.deliveredAt && <div className="flex justify-between"><dt>Delivered</dt><dd>{formatDate(order.deliveredAt)}</dd></div>}
              {order.cancelledAt && <div className="flex justify-between"><dt>Cancelled</dt><dd>{formatDate(order.cancelledAt)}</dd></div>}
            </dl>
            {order.cancellationReason && (
              <p className="mt-2 text-sm text-muted-foreground">Reason: {order.cancellationReason}</p>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader><CardTitle>Shipping Address</CardTitle></CardHeader>
          <CardContent className="text-sm">
            {order.shippingAddress.fullName && <p className="font-medium">{order.shippingAddress.fullName}</p>}
            <p>{order.shippingAddress.street}</p>
            <p>{order.shippingAddress.city}, {order.shippingAddress.state} {order.shippingAddress.zipCode}</p>
            <p>{order.shippingAddress.country}</p>
            {order.shippingAddress.phone && <p>{order.shippingAddress.phone}</p>}
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader><CardTitle>Items</CardTitle></CardHeader>
        <CardContent>
          <div className="space-y-4">
            {order.items.map((item) => (
              <div key={item.id} className="flex items-center gap-4">
                <div className="relative h-16 w-16 overflow-hidden rounded bg-muted flex-shrink-0">
                  {item.productImageUrl ? (
                    <Image src={resolveMediaUrl(item.productImageUrl)!} alt={item.productName} fill className="object-cover" sizes="64px" />
                  ) : (
                    <div className="h-full w-full flex items-center justify-center text-xs text-muted-foreground">No img</div>
                  )}
                </div>
                <div className="flex-1">
                  <p className="font-medium">{item.productName}</p>
                  <p className="text-sm text-muted-foreground">Qty: {item.quantity} × {formatCurrency(item.unitPrice, item.currency)}</p>
                </div>
                <p className="font-bold">{formatCurrency(item.lineTotal, item.currency)}</p>
              </div>
            ))}
            <Separator />
            <div className="flex justify-between text-lg font-bold">
              <span>Total</span>
              <span>{formatCurrency(order.totalAmount, order.currency)}</span>
            </div>
          </div>
        </CardContent>
      </Card>

      {payment && (
        <Card>
          <CardHeader><CardTitle>Payment</CardTitle></CardHeader>
          <CardContent>
            <PaymentStatus status={payment.status} />
            <p className="text-sm text-muted-foreground mt-2">
              {formatCurrency(payment.amount, payment.currency)} via {payment.method}
            </p>
          </CardContent>
        </Card>
      )}

      {canCancel && (
        <Button variant="destructive" onClick={handleCancel} disabled={cancelMutation.isPending}>
          {cancelMutation.isPending ? 'Cancelling...' : 'Cancel Order'}
        </Button>
      )}
    </div>
  );
}
