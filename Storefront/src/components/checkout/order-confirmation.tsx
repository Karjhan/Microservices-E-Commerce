import Link from 'next/link';
import { CheckCircle } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';

interface OrderConfirmationProps {
  orderId: string;
}

export function OrderConfirmation({ orderId }: OrderConfirmationProps) {
  return (
    <div className="max-w-md mx-auto py-12">
      <Card>
        <CardContent className="pt-6 text-center space-y-4">
          <CheckCircle className="h-16 w-16 text-green-500 mx-auto" />
          <h2 className="text-2xl font-bold">Order Placed!</h2>
          <p className="text-muted-foreground">
            Your order has been created successfully.
          </p>
          <p className="text-sm">
            Order ID: <span className="font-mono font-medium">{orderId}</span>
          </p>
          <div className="flex flex-col gap-2 pt-4">
            <Button render={<Link href={`/orders/${orderId}`} />}>View Order</Button>
            <Button variant="outline" render={<Link href="/products" />}>Continue Shopping</Button>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
