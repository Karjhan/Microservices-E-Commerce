import {
  apiGet,
  apiPost,
  apiPut,
  apiDelete,
  apiPostFormData,
  apiPutFormData,
  buildQueryString,
  serverApiGet,
} from './client';
import type {
  Product,
  PaginatedProducts,
  PaginatedAdminProducts,
  ProductImage,
  ProductModel,
  ProductStatus,
} from '@/types/product';

export interface ProductFilters {
  categoryId?: string;
  minPrice?: number;
  maxPrice?: number;
  tags?: string[];
  material?: string;
  printerType?: string;
  search?: string;
  status?: string;
  page?: number;
  pageSize?: number;
}

export async function listProducts(filters: ProductFilters = {}): Promise<PaginatedProducts> {
  const qs = buildQueryString(filters);
  return apiGet<PaginatedProducts>(`/products${qs}`, { skipAuth: true });
}

export async function listProductsServer(filters: ProductFilters = {}, token?: string): Promise<PaginatedProducts> {
  const qs = buildQueryString(filters);
  return serverApiGet<PaginatedProducts>(`/products${qs}`, token);
}

export async function getProductById(id: string): Promise<Product> {
  return apiGet<Product>(`/products/${id}`, { skipAuth: true });
}

export async function getProductByIdServer(id: string, token?: string): Promise<Product> {
  return serverApiGet<Product>(`/products/${id}`, token);
}

// ---- Admin (AdminOnly on the server) ---------------------------------------------------------------------------

export interface AdminProductFilters {
  q?: string;
  status?: string;
  categoryId?: string;
  sortBy?: 'name' | 'price' | 'createdAt' | 'status';
  sortDir?: 'asc' | 'desc';
  page?: number;
  pageSize?: number;
}

/** Every status, unlike the public list which only returns Active products. */
export async function getAdminProducts(filters: AdminProductFilters = {}): Promise<PaginatedAdminProducts> {
  return apiGet<PaginatedAdminProducts>(`/products/admin${buildQueryString(filters)}`);
}

/** Reads one product of any status (admins only see non-Active ones). */
export async function getAdminProduct(id: string): Promise<Product> {
  return apiGet<Product>(`/products/${id}`);
}

export async function createProduct(data: Record<string, unknown>): Promise<{ productId: string }> {
  return apiPost<{ productId: string }>('/products', data);
}

export async function updateProduct(id: string, data: Record<string, unknown>): Promise<void> {
  return apiPut<void>(`/products/${id}`, data);
}

export async function setProductStatus(
  id: string,
  status: ProductStatus
): Promise<{ productId: string; previousStatus: string; status: string }> {
  return apiPost(`/products/${id}/status`, { status });
}

/** Deleting is irreversible, so the server requires an explicit confirmation. */
export async function deleteProduct(id: string, confirm = true): Promise<void> {
  return apiDelete<void>(`/products/${id}${confirm ? '?confirm=true' : ''}`);
}

export async function uploadProductImage(
  productId: string,
  file: File,
  isPrimary?: boolean
): Promise<ProductImage> {
  const formData = new FormData();
  formData.append('file', file);
  if (isPrimary !== undefined) formData.append('isPrimary', String(isPrimary));
  return apiPostFormData<ProductImage>(`/products/${productId}/images`, formData);
}

export async function setPrimaryProductImage(productId: string, imageId: string): Promise<ProductImage[]> {
  return apiPost<ProductImage[]>(`/products/${productId}/images/${imageId}/primary`);
}

/** The list must contain every image id of the product exactly once. */
export async function reorderProductImages(productId: string, imageIds: string[]): Promise<ProductImage[]> {
  return apiPut<ProductImage[]>(`/products/${productId}/images/order`, { imageIds });
}

export async function deleteProductImage(productId: string, imageId: string): Promise<void> {
  return apiDelete<void>(`/products/${productId}/images/${imageId}`);
}

/** Attaches or replaces the product's single 3D model. */
export async function uploadProductModel(productId: string, file: File): Promise<ProductModel> {
  const formData = new FormData();
  formData.append('file', file);
  return apiPutFormData<ProductModel>(`/products/${productId}/model`, formData);
}

export async function deleteProductModel(productId: string): Promise<void> {
  return apiDelete<void>(`/products/${productId}/model`);
}

/** Model metadata with a short-lived download link (needs the token for non-Active products). */
export async function getProductModel(productId: string, skipAuth = true): Promise<ProductModel> {
  return apiGet<ProductModel>(`/products/${productId}/model`, { skipAuth });
}
export async function addProductAttribute(productId: string, key: string, value: string): Promise<{ attributeId: string }> {
  return apiPost<{ attributeId: string }>(`/products/${productId}/attributes`, { key, value });
}

export async function deleteProductAttribute(productId: string, attributeId: string): Promise<void> {
  return apiDelete<void>(`/products/${productId}/attributes/${attributeId}`);
}

export async function addProductRelation(productId: string, relatedProductId: string, type: string): Promise<{ relationId: string }> {
  return apiPost<{ relationId: string }>(`/products/${productId}/relations`, { relatedProductId, type });
}

export async function deleteProductRelation(productId: string, relationId: string): Promise<void> {
  return apiDelete<void>(`/products/${productId}/relations/${relationId}`);
}
