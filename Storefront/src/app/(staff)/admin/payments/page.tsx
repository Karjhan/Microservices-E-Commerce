'use client';

import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { usePayments } from '@/hooks/queries/use-payments';
import { refundPayment } from '@/lib/api/payments';
import { PaymentStatus } from '@/components/orders/payment-status';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { Label } from '@/components/ui/label';
import { ErrorFallback } from '@/components/shared/error-fallback';
import { TableSkeleton } from '@/components/shared/loading-skeletons';
import { EmptyState } from '@/components/shared/empty-state';
import { formatCurrency, formatDate } from '@/lib/utils';
import { queryKeys } from '@/lib/constants';
import type { PaymentFilters } from '@/lib/api/payments';

const STATUSES = ['Pending', 'Processing', 'Completed', 'Failed', 'Cancelled', 'PartiallyRefunded', 'Refunded'];

export default function AdminPaymentsPage() {
  const [filters, setFilters] = useState<PaymentFilters>({ page: 1, pageSize: 20 });
  const { data, isLoading, isError, refetch } = usePayments(filters);
  const queryClient = useQueryClient();
  const [refundAmount, setRefundAmount] = useState('');

  const refundMutation = useMutation({
    mutationFn: ({ paymentId, amount }: { paymentId: string; amount?: number }) => refundPayment(paymentId, amount),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.payments.all });
    },
  });

  if (isError) return <ErrorFallback onRetry={() => refetch()} />;

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold">Payments</h1>
      <div className="flex gap-4">
        <Select value={filters.status || ''} onValueChange={(v) => setFilters((p) => ({ ...p, status: v || undefined }))}>
          <SelectTrigger className="w-full sm:w-[180px]"><SelectValue placeholder="All Statuses" /></SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All Statuses</SelectItem>
            {STATUSES.map((s) => (<SelectItem key={s} value={s}>{s}</SelectItem>))}
          </SelectContent>
        </Select>
      </div>

      {isLoading ? (<TableSkeleton rows={5} cols={6} />) : data && data.items.length > 0 ? (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Payment</TableHead>
              <TableHead>Order</TableHead>
              <TableHead>Amount</TableHead>
              <TableHead>Method</TableHead>
              <TableHead>Status</TableHead>
              <TableHead>Date</TableHead>
              <TableHead></TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {data.items.map((payment) => (
              <TableRow key={payment.id}>
                <TableCell className="font-mono text-xs">#{payment.id.slice(0, 8)}</TableCell>
                <TableCell className="font-mono text-xs">{payment.orderId.slice(0, 8)}</TableCell>
                <TableCell>{formatCurrency(payment.amount, payment.currency)}</TableCell>
                <TableCell>{payment.method}</TableCell>
                <TableCell><PaymentStatus status={payment.status} /></TableCell>
                <TableCell className="text-sm">{formatDate(payment.createdAt)}</TableCell>
                <TableCell>
                  {payment.status === 'Completed' && (
                    <Dialog>
                      <DialogTrigger render={<Button variant="outline" size="sm" />}>
                        Refund
                      </DialogTrigger>
                      <DialogContent>
                        <DialogHeader><DialogTitle>Refund Payment</DialogTitle></DialogHeader>
                        <div className="space-y-4">
                          <p className="text-sm">Original amount: {formatCurrency(payment.amount, payment.currency)}</p>
                          <div className="space-y-2">
                            <Label>Refund Amount (leave empty for full refund)</Label>
                            <Input type="number" step="0.01" value={refundAmount} onChange={(e) => setRefundAmount(e.target.value)} placeholder="Full refund" />
                          </div>
                          <div className="flex gap-2">
                            <Button onClick={() => refundMutation.mutate({ paymentId: payment.id, amount: refundAmount ? Number(refundAmount) : undefined })} disabled={refundMutation.isPending}>
                              {refundMutation.isPending ? 'Processing...' : 'Confirm Refund'}
                            </Button>
                          </div>
                        </div>
                      </DialogContent>
                    </Dialog>
                  )}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      ) : (<EmptyState title="No payments" />)}
    </div>
  );
}