import { z } from 'zod';

export const shippingAddressSchema = z.object({
  street: z.string().min(1, 'Street is required'),
  city: z.string().min(1, 'City is required'),
  state: z.string().min(1, 'State is required'),
  country: z.string().min(1, 'Country is required'),
  zipCode: z.string().min(1, 'ZIP code is required'),
  fullName: z.string().optional().nullable(),
  phone: z.string().optional().nullable(),
});

export const createOrderSchema = z.object({
  items: z.array(z.object({
    productId: z.string(),
    productName: z.string(),
    unitPrice: z.number(),
    quantity: z.number().min(1),
    currency: z.string(),
    productImageUrl: z.string().nullable(),
  })).min(1, 'At least one item is required'),
  shippingAddress: shippingAddressSchema,
  currency: z.string().default('EUR'),
});

export type ShippingAddressFormValues = z.infer<typeof shippingAddressSchema>;
export type CreateOrderFormValues = z.infer<typeof createOrderSchema>;
