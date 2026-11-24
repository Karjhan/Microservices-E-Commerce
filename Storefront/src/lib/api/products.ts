import { apiGet, apiPost, apiPut, apiDelete, apiPostFormData, buildQueryString, serverApiGet } from './client';
import type { Product, PaginatedProducts } from '@/types/product';

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

export async function createProduct(data: Record<string, unknown>): Promise<{ productId: string }> {
  return apiPost<{ productId: string }>('/products', data);
}

export async function updateProduct(id: string, data: Record<string, unknown>): Promise<void> {
  return apiPut<void>(`/products/${id}`, data);
}

export async function deleteProduct(id: string): Promise<void> {
  return apiDelete<void>(`/products/${id}`);
}

export async function uploadProductImage(productId: string, file: File): Promise<{ imageId: string; url: string }> {
  const formData = new FormData();
  formData.append('file', file);
  return apiPostFormData<{ imageId: string; url: string }>(`/products/${productId}/images`, formData);
}

export async function deleteProductImage(productId: string, imageId: string): Promise<void> {
  return apiDelete<void>(`/products/${productId}/images/${imageId}`);
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
