'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useSession } from 'next-auth/react';
import { useCart } from '@/hooks/use-cart';
import { useCreateOrder } from '@/hooks/queries/use-orders';
import { AddressForm } from '@/components/checkout/address-form';
import { OrderConfirmation } from '@/components/checkout/order-confirmation';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Separator } from '@/components/ui/separator';
import { formatCurrency } from '@/lib/utils';
import type { ShippingAddressFormValues } from '@/schemas/checkout';
import type { ShippingAddress } from '@/types/order';
import Link from 'next/link';

type Step = 'shipping' | 'review' | 'confirmation';

export default function CheckoutPage() {
  const router = useRouter();
  const { data: session } = useSession();
  const { items, totals, clearCart } = useCart();
  const createOrder = useCreateOrder();
  const [step, setStep] = useState<Step>('shipping');
  const [shippingAddress, setShippingAddress] = useState<ShippingAddressFormValues | null>(null);
  const [orderId, setOrderId] = useState<string | null>(null);

  if (!session) {
    return (
      <div className="text-center py-12 space-y-4">
        <h1 className="text-2xl font-bold">Please sign in to checkout</h1>
        <Button render={<Link href="/login" />}>Sign in</Button>
      </div>
    );
  }

  if (items.length === 0 && step !== 'confirmation') {
    return (
      <div className="text-center py-12 space-y-4">
        <h1 className="text-2xl font-bold">Your cart is empty</h1>
        <Button render={<Link href="/products" />}>Browse Products</Button>
      </div>
    );
  }

  if (step === 'confirmation' && orderId) {
    return <OrderConfirmation orderId={orderId} />;
  }

  const handleShippingSubmit = (data: ShippingAddressFormValues) => {
    setShippingAddress(data);
    setStep('review');
  };

  const handlePlaceOrder = async () => {
    if (!shippingAddress) return;
    try {
      const result = await createOrder.mutateAsync({
        userId: (session as any).user.id,
        currency: totals.currency,
        notes: null,
        recipientEmail: session.user?.email || null,
        shippingAddress: shippingAddress as ShippingAddress,
        items: items.map((item) => ({
          productId: item.productId,
          productName: item.productName,
          unitPrice: item.price,
          quantity: item.quantity,
          currency: item.currency,
          productImageUrl: item.imageUrl,
        })),
      });
      setOrderId(result.orderId);
      clearCart();
      setStep('confirmation');
    } catch {}
  };

  return (
    <div className="max-w-2xl mx-auto space-y-6">
      <h1 className="text-3xl font-bold">Checkout</h1>

      <div className="flex gap-2 text-sm">
        <span className={step === 'shipping' ? 'font-bold' : 'text-muted-foreground'}>1. Shipping</span>
        <span className="text-muted-foreground">/</span>
        <span className={step === 'review' ? 'font-bold' : 'text-muted-foreground'}>2. Review & Pay</span>
      </div>

      {step === 'shipping' && (
        <Card>
          <CardHeader><CardTitle>Shipping Address</CardTitle></CardHeader>
          <CardContent>
            <AddressForm onSubmit={handleShippingSubmit} defaultValues={shippingAddress || undefined} />
          </CardContent>
        </Card>
      )}

      {step === 'review' && (
        <div className="space-y-6">
          <Card>
            <CardHeader><CardTitle>Order Summary</CardTitle></CardHeader>
            <CardContent className="space-y-4">
              {items.map((item) => (
                <div key={item.productId} className="flex justify-between">
                  <span>{item.productName} × {item.quantity}</span>
                  <span className="font-medium">{formatCurrency(item.price * item.quantity, item.currency)}</span>
                </div>
              ))}
              <Separator />
              <div className="flex justify-between text-lg font-bold">
                <span>Total</span>
                <span>{formatCurrency(totals.subtotal, totals.currency)}</span>
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader><CardTitle>Shipping Address</CardTitle></CardHeader>
            <CardContent>
              <p>{shippingAddress?.fullName}</p>
              <p>{shippingAddress?.street}</p>
              <p>{shippingAddress?.city}, {shippingAddress?.state} {shippingAddress?.zipCode}</p>
              <p>{shippingAddress?.country}</p>
              <Button variant="ghost" size="sm" className="mt-2" onClick={() => setStep('shipping')}>
                Edit Address
              </Button>
            </CardContent>
          </Card>

          {createOrder.error && (
            <div className="rounded-md bg-destructive/10 p-3 text-sm text-destructive">
              Failed to place order. Please try again.
            </div>
          )}

          <Button size="lg" className="w-full" onClick={handlePlaceOrder} disabled={createOrder.isPending}>
            {createOrder.isPending ? 'Placing Order...' : 'Place Order'}
          </Button>
        </div>
      )}
    </div>
  );
}
