import { z } from 'zod';

const printSettingsSchema = z.object({
  layerHeight: z.number().min(0.01).max(1),
  infillPercentage: z.number().min(0).max(100),
  nozzleSize: z.number().min(0.1).max(2),
  printTimeMinutes: z.number().min(1),
  filamentUsedGrams: z.number().min(0),
  supportsRequired: z.boolean(),
});

const productSizeSchema = z.object({
  widthMm: z.number().min(0),
  heightMm: z.number().min(0),
  depthMm: z.number().min(0),
});

const materials = ['PLA', 'ABS', 'PETG', 'Resin', 'Nylon', 'TPU'] as const;
const printerTypes = ['FDM', 'SLA', 'SLS'] as const;

export const createProductSchema = z.object({
  name: z.string().min(1, 'Name is required').max(200),
  shortDescription: z.string().min(1, 'Short description is required').max(500),
  longDescription: z.string().min(1, 'Long description is required'),
  price: z.number().min(0.01, 'Price must be positive'),
  categoryId: z.string().min(1, 'Category is required'),
  currency: z.string(),
  settings: printSettingsSchema,
  size: productSizeSchema,
  tags: z.array(z.string()),
  supportedMaterials: z.array(z.enum(materials)).min(1, 'At least one material required'),
  compatiblePrinters: z.array(z.enum(printerTypes)).min(1, 'At least one printer type required'),
});

export const updateProductSchema = createProductSchema.omit({ categoryId: true }).partial();

export const addAttributeSchema = z.object({
  key: z.string().min(1, 'Key is required'),
  value: z.string().min(1, 'Value is required'),
});

export const addRelationSchema = z.object({
  relatedProductId: z.string().min(1, 'Related product ID is required'),
  type: z.string().min(1, 'Relation type is required'),
});

export type CreateProductFormValues = z.infer<typeof createProductSchema>;
export type UpdateProductFormValues = z.infer<typeof updateProductSchema>;
export type AddAttributeFormValues = z.infer<typeof addAttributeSchema>;
export type AddRelationFormValues = z.infer<typeof addRelationSchema>;
