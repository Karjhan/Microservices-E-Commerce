import { z } from 'zod';

export const MAX_STOCK_QUANTITY = 1_000_000;
export const MAX_LOW_STOCK_THRESHOLD = 100_000;
export const DEFAULT_LOW_STOCK_THRESHOLD = 5;
export const REASON_MIN_LENGTH = 3;
export const REASON_MAX_LENGTH = 200;

const reasonSchema = z
  .string()
  .trim()
  .min(REASON_MIN_LENGTH, `Reason must be at least ${REASON_MIN_LENGTH} characters`)
  .max(REASON_MAX_LENGTH, `Reason must be at most ${REASON_MAX_LENGTH} characters`);

const wholeNumber = (message: string) => z.number({ error: message }).int(message);

export const stockAdjustmentSchema = z
  .object({
    mode: z.enum(['delta', 'set']),
    quantity: wholeNumber('Enter a whole number'),
    reason: reasonSchema,
  })
  .superRefine((value, ctx) => {
    if (value.mode === 'delta') {
      if (value.quantity === 0) {
        ctx.addIssue({ code: 'custom', path: ['quantity'], message: 'The change cannot be zero' });
      } else if (Math.abs(value.quantity) > MAX_STOCK_QUANTITY) {
        ctx.addIssue({
          code: 'custom',
          path: ['quantity'],
          message: `The change cannot be more than ${MAX_STOCK_QUANTITY.toLocaleString()}`,
        });
      }
      return;
    }

    if (value.quantity < 0) {
      ctx.addIssue({ code: 'custom', path: ['quantity'], message: 'The count cannot be negative' });
    } else if (value.quantity > MAX_STOCK_QUANTITY) {
      ctx.addIssue({
        code: 'custom',
        path: ['quantity'],
        message: `The count cannot be more than ${MAX_STOCK_QUANTITY.toLocaleString()}`,
      });
    }
  });

export type StockAdjustmentFormValues = z.infer<typeof stockAdjustmentSchema>;

export const lowStockThresholdSchema = z.object({
  lowStockThreshold: wholeNumber('Enter a whole number')
    .min(0, 'The threshold cannot be negative')
    .max(MAX_LOW_STOCK_THRESHOLD, `The threshold cannot be more than ${MAX_LOW_STOCK_THRESHOLD.toLocaleString()}`),
});

export type LowStockThresholdFormValues = z.infer<typeof lowStockThresholdSchema>;
