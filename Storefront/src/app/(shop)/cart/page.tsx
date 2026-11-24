'use client';

import Image from 'next/image';
import Link from 'next/link';
import { Minus, Plus, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Separator } from '@/components/ui/separator';
import { useCart } from '@/hooks/use-cart';
import { formatCurrency, resolveMediaUrl } from '@/lib/utils';
import { EmptyState } from '@/components/shared/empty-state';

export default function CartPage() {
  const { items, totals, removeItem, updateQuantity, clearCart } = useCart();

  if (items.length === 0) {
    return (
      <EmptyState
        title="Your cart is empty"
        description="Browse our catalog to find something you like"
        action={<Button render={<Link href="/products" />}>Browse Products</Button>}
      />
    );
  }

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h1 className="text-2xl sm:text-3xl font-bold">Shopping Cart</h1>
        <Button variant="ghost" size="sm" className="text-destructive" onClick={clearCart}>
          Clear Cart
        </Button>
      </div>

      <div className="grid gap-8 lg:grid-cols-3">
        <div className="lg:col-span-2 space-y-4">
          {items.map((item) => (
            <div key={item.productId} className="flex flex-col gap-4 rounded-lg border p-4 sm:flex-row">
              <div className="flex gap-4 flex-1 min-w-0">
                <div className="relative h-24 w-24 overflow-hidden rounded-md bg-muted flex-shrink-0">
                  {item.imageUrl ? (
                    <Image src={resolveMediaUrl(item.imageUrl)!} alt={item.productName} fill className="object-cover" sizes="96px" />
                  ) : (
                    <div className="h-full w-full flex items-center justify-center text-xs text-muted-foreground">No img</div>
                  )}
                </div>
                <div className="flex-1 min-w-0">
                  <Link href={`/products/${item.productId}`} className="font-medium hover:underline">{item.productName}</Link>
                  {item.material && <p className="text-sm text-muted-foreground">{item.material}</p>}
                  <p className="font-bold mt-1">{formatCurrency(item.price, item.currency)}</p>
                  <div className="flex items-center gap-2 mt-2">
                    <Button variant="outline" size="icon" className="h-8 w-8" onClick={() => updateQuantity(item.productId, item.quantity - 1)} disabled={item.quantity <= 1}>
                      <Minus className="h-4 w-4" />
                    </Button>
                    <span className="w-10 text-center">{item.quantity}</span>
                    <Button variant="outline" size="icon" className="h-8 w-8" onClick={() => updateQuantity(item.productId, item.quantity + 1)}>
                      <Plus className="h-4 w-4" />
                    </Button>
                  </div>
                </div>
              </div>
              <div className="flex items-center justify-between gap-4 border-t pt-3 sm:flex-col sm:items-end sm:justify-between sm:border-t-0 sm:pt-0">
                <p className="font-bold">{formatCurrency(item.price * item.quantity, item.currency)}</p>
                <Button variant="ghost" size="icon" className="text-destructive" onClick={() => removeItem(item.productId)}>
                  <Trash2 className="h-4 w-4" />
                </Button>
              </div>
            </div>
          ))}
        </div>

        <div className="rounded-lg border p-6 h-fit space-y-4">
          <h2 className="text-lg font-bold">Order Summary</h2>
          <Separator />
          <div className="flex justify-between">
            <span className="text-muted-foreground">Items ({totals.itemCount})</span>
            <span>{formatCurrency(totals.subtotal, totals.currency)}</span>
          </div>
          <Separator />
          <div className="flex justify-between text-lg font-bold">
            <span>Total</span>
            <span>{formatCurrency(totals.subtotal, totals.currency)}</span>
          </div>
          <Button className="w-full" size="lg" render={<Link href="/checkout" />}>Proceed to Checkout</Button>
        </div>
      </div>
    </div>
  );
}
