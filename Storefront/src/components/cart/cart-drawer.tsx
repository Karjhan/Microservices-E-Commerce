'use client';

import Image from 'next/image';
import Link from 'next/link';
import { Minus, Plus, Trash2 } from 'lucide-react';
import { Sheet, SheetContent, SheetHeader, SheetTitle, SheetTrigger } from '@/components/ui/sheet';
import { Button } from '@/components/ui/button';
import { Separator } from '@/components/ui/separator';
import { ScrollArea } from '@/components/ui/scroll-area';
import { useCart } from '@/hooks/use-cart';
import { formatCurrency, resolveMediaUrl } from '@/lib/utils';

export function CartDrawer({ trigger }: { trigger: React.ReactNode }) {
  const { items, totals, removeItem, updateQuantity } = useCart();

  return (
    <Sheet>
      <SheetTrigger>{trigger}</SheetTrigger>
      <SheetContent className="flex flex-col w-full sm:max-w-lg">
        <SheetHeader>
          <SheetTitle>Shopping Cart ({totals.itemCount})</SheetTitle>
        </SheetHeader>

        {items.length === 0 ? (
          <div className="flex-1 flex items-center justify-center">
            <p className="text-muted-foreground">Your cart is empty</p>
          </div>
        ) : (
          <>
            <ScrollArea className="flex-1 -mx-6 px-6">
              <div className="space-y-4 py-4">
                {items.map((item) => (
                  <div key={item.productId} className="flex gap-4">
                    <div className="relative h-16 w-16 overflow-hidden rounded-md bg-muted flex-shrink-0">
                      {item.imageUrl ? (
                        <Image src={resolveMediaUrl(item.imageUrl)!} alt={item.productName} fill className="object-cover" sizes="64px" />
                      ) : (
                        <div className="h-full w-full flex items-center justify-center text-xs text-muted-foreground">No img</div>
                      )}
                    </div>
                    <div className="flex-1 min-w-0">
                      <Link href={`/products/${item.productId}`} className="text-sm font-medium hover:underline line-clamp-1">
                        {item.productName}
                      </Link>
                      <p className="text-sm font-bold">{formatCurrency(item.price, item.currency)}</p>
                      <div className="flex items-center gap-2 mt-1">
                        <Button variant="outline" size="icon" className="h-7 w-7" onClick={() => updateQuantity(item.productId, item.quantity - 1)} disabled={item.quantity <= 1}>
                          <Minus className="h-3 w-3" />
                        </Button>
                        <span className="text-sm w-8 text-center">{item.quantity}</span>
                        <Button variant="outline" size="icon" className="h-7 w-7" onClick={() => updateQuantity(item.productId, item.quantity + 1)}>
                          <Plus className="h-3 w-3" />
                        </Button>
                        <Button variant="ghost" size="icon" className="h-7 w-7 text-destructive" onClick={() => removeItem(item.productId)}>
                          <Trash2 className="h-3 w-3" />
                        </Button>
                      </div>
                    </div>
                    <p className="text-sm font-bold whitespace-nowrap">{formatCurrency(item.price * item.quantity, item.currency)}</p>
                  </div>
                ))}
              </div>
            </ScrollArea>

            <Separator />
            <div className="space-y-4 pt-4">
              <div className="flex justify-between text-lg font-bold">
                <span>Subtotal</span>
                <span>{formatCurrency(totals.subtotal, totals.currency)}</span>
              </div>
              <Button className="w-full" size="lg" render={<Link href="/checkout" />}>Proceed to Checkout</Button>
              <Button variant="outline" className="w-full" render={<Link href="/cart" />}>View Cart</Button>
            </div>
          </>
        )}
      </SheetContent>
    </Sheet>
  );
}
