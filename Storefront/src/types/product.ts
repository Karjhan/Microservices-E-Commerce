import type { ProductBrand, ProductFilamentType } from './reference-data';

export type ProductStatus = 'Draft' | 'Active' | 'Inactive' | 'OutOfStock' | 'Discontinued';
/** A filament type code. Admins can add new ones, so this is no longer a fixed list. */
export type Material = string;
export type PrinterType = 'FDM' | 'SLA' | 'SLS';

export interface ProductImage {
  imageId: string;
  objectKey: string;
  url: string;
  isPrimary: boolean;
  sortOrder: number;
  contentType: string;
  sizeBytes: number;
}

export type ModelFormat = 'glb' | 'gltf' | 'stl' | '3mf' | 'obj';

export interface ProductModel {
  modelId: string;
  originalFilename: string;
  format: ModelFormat;
  sizeBytes: number;
  isPreviewable: boolean;
  uploadedAt: string;
  /** Short-lived download link; only present when the model is fetched from its own endpoint. */
  url?: string | null;
}

export interface PrintSettings {
  layerHeight: number | null;
  infillPercentage: number | null;
  nozzleSize: number | null;
  printTimeMinutes: number | null;
  filamentUsedGrams: number | null;
  supportsRequired: boolean;
}

export interface ProductSize {
  widthMm: number;
  heightMm: number;
  depthMm: number;
}

export interface ProductAttribute {
  attributeId: string;
  key: string;
  value: string;
}

export interface ProductRelation {
  relationId: string;
  relatedProductId: string;
  type: string;
}

export interface Product {
  id: string;
  name: string;
  slug: string;
  shortDescription: string;
  longDescription: string;
  price: number;
  currency: string;
  status: ProductStatus;
  categoryId: string;
  categoryName: string | null;
  brand: ProductBrand | null;
  filamentTypes: ProductFilamentType[];
  settings: PrintSettings;
  size: ProductSize;
  tags: string[];
  attributes: ProductAttribute[];
  relatedProducts: ProductRelation[];
  supportedMaterials: Material[];
  compatiblePrinters: PrinterType[];
  images: ProductImage[];
  model: ProductModel | null;
  downloadCount: number;
  averageRating: number;
  createdAt: string;
  updatedAt: string | null;
}

export interface ProductListItem {
  id: string;
  name: string;
  slug: string;
  shortDescription: string;
  price: number;
  currency: string;
  status: ProductStatus;
  brand?: ProductBrand | null;
  filamentTypes?: ProductFilamentType[];
  supportedMaterials: Material[];
  compatiblePrinters: PrinterType[];
  images: ProductImage[];
  downloadCount: number;
  averageRating: number;
  createdAt: string;
  updatedAt: string | null;
}

export interface PaginatedProducts {
  items: ProductListItem[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}


export interface PaginatedAdminProducts {
  items: Product[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

/** Allowed next statuses, mirroring the server's transition table (data-model.md). */
export const STATUS_TRANSITIONS: Record<ProductStatus, ProductStatus[]> = {
  Draft: ['Active', 'Discontinued'],
  Active: ['Draft', 'Inactive', 'OutOfStock', 'Discontinued'],
  Inactive: ['Active', 'Discontinued'],
  OutOfStock: ['Active', 'Inactive', 'Discontinued'],
  Discontinued: [],
};

export const PRODUCT_STATUSES: ProductStatus[] = ['Draft', 'Active', 'Inactive', 'OutOfStock', 'Discontinued'];