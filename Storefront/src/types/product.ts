export type ProductStatus = 'Draft' | 'Active' | 'Inactive' | 'OutOfStock' | 'Discontinued';
export type Material = 'PLA' | 'ABS' | 'PETG' | 'Resin' | 'Nylon' | 'TPU';
export type PrinterType = 'FDM' | 'SLA' | 'SLS';

export interface ProductImage {
  imageId: string;
  objectKey: string;
  url: string;
  isPrimary: boolean;
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
  settings: PrintSettings;
  size: ProductSize;
  tags: string[];
  attributes: ProductAttribute[];
  relatedProducts: ProductRelation[];
  supportedMaterials: Material[];
  compatiblePrinters: PrinterType[];
  images: ProductImage[];
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
