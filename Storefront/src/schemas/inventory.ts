import { z } from 'zod';

export const stockAdjustmentSchema = z.object({
  delta: z.number().refine((v) => v !== 0, 'Delta cannot be zero'),
  reason: z.string().min(1, 'Reason is required'),
});

export type StockAdjustmentFormValues = z.infer<typeof stockAdjustmentSchema>;
