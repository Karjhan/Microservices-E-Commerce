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

const printerTypes = ['FDM', 'SLA', 'SLS'] as const;

/** Prices are euro amounts: two decimals at most, checked in cents to avoid floating-point noise. */
function hasAtMostTwoDecimals(value: number): boolean {
  return Number.isFinite(value) && Math.abs(value * 100 - Math.round(value * 100)) < 1e-6;
}

export const createProductSchema = z.object({
  name: z
    .string()
    .trim()
    .min(2, 'Name must be at least 2 characters')
    .max(120, 'Name must be at most 120 characters'),
  shortDescription: z.string().min(1, 'Short description is required').max(500),
  longDescription: z.string().min(1, 'Long description is required'),
  price: z
    .number({ error: 'Price is required' })
    .min(0, 'Price cannot be negative')
    .refine(hasAtMostTwoDecimals, 'Price can have at most two decimal places'),
  categoryId: z.string().min(1, 'Category is required'),
  /** Optional: null leaves the product without a brand. */
  brandId: z.string().min(1).nullable(),
  filamentTypeIds: z.array(z.string()).min(1, 'At least one filament type required'),
  currency: z.literal('EUR', { error: 'Only EUR is supported' }),
  settings: printSettingsSchema,
  size: productSizeSchema,
  tags: z.array(z.string()),
  compatiblePrinters: z.array(z.enum(printerTypes)).min(1, 'At least one printer type required'),
});

export const updateProductSchema = createProductSchema.partial();

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


// ---- Media files (FR-015): checked in the browser first; the server re-checks by content ------------------------

export const IMAGE_MAX_BYTES = 5 * 1024 * 1024;
export const MODEL_MAX_BYTES = 100 * 1024 * 1024;
export const IMAGE_TYPES_HINT = 'JPEG, PNG or WebP images up to 5 MB';
export const MODEL_TYPES_HINT = 'GLB, GLTF or STL (previewable), 3MF or OBJ (download only), up to 100 MB';
export const IMAGE_ACCEPT = 'image/jpeg,image/png,image/webp';
export const MODEL_ACCEPT = '.glb,.gltf,.stl,.3mf,.obj';

const IMAGE_EXTENSIONS = ['jpg', 'jpeg', 'png', 'webp'];
export const MODEL_EXTENSIONS = ['glb', 'gltf', 'stl', '3mf', 'obj'];

function extensionOf(filename: string): string {
  const dot = filename.lastIndexOf('.');
  return dot < 0 ? '' : filename.slice(dot + 1).toLowerCase();
}

/** Returns an explanation when the file would be refused, otherwise null. */
export function validateImageFile(file: { name: string; size: number }): string | null {
  if (!IMAGE_EXTENSIONS.includes(extensionOf(file.name))) {
    return `"${file.name}" is not an accepted image. Allowed: ${IMAGE_TYPES_HINT}.`;
  }
  if (file.size > IMAGE_MAX_BYTES) {
    return `"${file.name}" is larger than 5 MB.`;
  }
  return null;
}

export function validateModelFile(file: { name: string; size: number }): string | null {
  if (!MODEL_EXTENSIONS.includes(extensionOf(file.name))) {
    return `"${file.name}" is not an accepted 3D model. Allowed: ${MODEL_TYPES_HINT}.`;
  }
  if (file.size > MODEL_MAX_BYTES) {
    return `"${file.name}" is larger than 100 MB.`;
  }
  return null;
}

export function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}