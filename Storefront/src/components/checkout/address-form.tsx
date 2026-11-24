'use client';

import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { shippingAddressSchema, type ShippingAddressFormValues } from '@/schemas/checkout';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';

interface AddressFormProps {
  onSubmit: (data: ShippingAddressFormValues) => void;
  defaultValues?: Partial<ShippingAddressFormValues>;
  isLoading?: boolean;
  submitLabel?: string;
}

export function AddressForm({ onSubmit, defaultValues, isLoading, submitLabel = 'Continue' }: AddressFormProps) {
  const { register, handleSubmit, formState: { errors } } = useForm<ShippingAddressFormValues>({
    resolver: zodResolver(shippingAddressSchema),
    defaultValues,
  });

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
      <div className="grid gap-4 sm:grid-cols-2">
        <div className="sm:col-span-2 space-y-2">
          <Label htmlFor="fullName">Full Name</Label>
          <Input id="fullName" {...register('fullName')} />
        </div>
        <div className="sm:col-span-2 space-y-2">
          <Label htmlFor="street">Street Address</Label>
          <Input id="street" {...register('street')} />
          {errors.street && <p className="text-sm text-destructive">{errors.street.message}</p>}
        </div>
        <div className="space-y-2">
          <Label htmlFor="city">City</Label>
          <Input id="city" {...register('city')} />
          {errors.city && <p className="text-sm text-destructive">{errors.city.message}</p>}
        </div>
        <div className="space-y-2">
          <Label htmlFor="state">State</Label>
          <Input id="state" {...register('state')} />
          {errors.state && <p className="text-sm text-destructive">{errors.state.message}</p>}
        </div>
        <div className="space-y-2">
          <Label htmlFor="country">Country</Label>
          <Input id="country" {...register('country')} />
          {errors.country && <p className="text-sm text-destructive">{errors.country.message}</p>}
        </div>
        <div className="space-y-2">
          <Label htmlFor="zipCode">ZIP Code</Label>
          <Input id="zipCode" {...register('zipCode')} />
          {errors.zipCode && <p className="text-sm text-destructive">{errors.zipCode.message}</p>}
        </div>
        <div className="sm:col-span-2 space-y-2">
          <Label htmlFor="phone">Phone (optional)</Label>
          <Input id="phone" type="tel" {...register('phone')} />
        </div>
      </div>
      <Button type="submit" className="w-full" disabled={isLoading}>
        {isLoading ? 'Processing...' : submitLabel}
      </Button>
    </form>
  );
}
