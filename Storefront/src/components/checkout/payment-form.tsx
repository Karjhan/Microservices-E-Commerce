'use client';

import { useState } from 'react';
import { CardElement, useStripe, useElements } from '@stripe/react-stripe-js';
import { Button } from '@/components/ui/button';

interface PaymentFormProps {
  onPaymentMethod: (paymentMethodId: string) => void;
  isProcessing: boolean;
}

export function PaymentForm({ onPaymentMethod, isProcessing }: PaymentFormProps) {
  const stripe = useStripe();
  const elements = useElements();
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!stripe || !elements) return;

    setError(null);
    const cardElement = elements.getElement(CardElement);
    if (!cardElement) return;

    const { error: stripeError, paymentMethod } = await stripe.createPaymentMethod({
      type: 'card',
      card: cardElement,
    });

    if (stripeError) {
      setError(stripeError.message || 'Payment failed');
      return;
    }

    if (paymentMethod) {
      onPaymentMethod(paymentMethod.id);
    }
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-4">
      <div className="rounded-md border p-4">
        <CardElement
          options={{
            style: {
              base: {
                fontSize: '16px',
                color: 'var(--foreground)',
                '::placeholder': { color: 'var(--muted-foreground)' },
              },
            },
          }}
        />
      </div>
      {error && (
        <p className="text-sm text-destructive">{error}</p>
      )}
      <Button type="submit" className="w-full" disabled={!stripe || isProcessing}>
        {isProcessing ? 'Processing...' : 'Pay Now'}
      </Button>
    </form>
  );
}
