'use client';

import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  createProduct,
  deleteProduct,
  deleteProductImage,
  deleteProductModel,
  getAdminProduct,
  getAdminProducts,
  getProductModel,
  reorderProductImages,
  setPrimaryProductImage,
  setProductStatus,
  updateProduct,
  uploadProductImage,
  uploadProductModel,
  type AdminProductFilters,
} from '@/lib/api/products';
import { queryKeys } from '@/lib/constants';
import type { ProductStatus } from '@/types/product';

export function useAdminProducts(filters: AdminProductFilters = {}) {
  return useQuery({
    queryKey: queryKeys.products.admin(filters),
    queryFn: () => getAdminProducts(filters),
    placeholderData: keepPreviousData,
  });
}

/** One product of any status, read with the admin token. */
export function useAdminProduct(id: string) {
  return useQuery({
    queryKey: queryKeys.products.detail(id),
    queryFn: () => getAdminProduct(id),
    enabled: !!id,
  });
}

/** Model metadata plus a fresh presigned link (they expire, so they are never cached for long). */
export function useProductModel(productId: string, enabled = true, skipAuth = true) {
  return useQuery({
    queryKey: queryKeys.products.model(productId),
    queryFn: () => getProductModel(productId, skipAuth),
    enabled: enabled && !!productId,
    staleTime: 5 * 60 * 1000,
    retry: false,
  });
}

/**
 * Every admin mutation clears all product queries (lists, details, admin lists, models), so a shopper who
 * reloads, and the admin's own screens, see the change straight away (spec US2 scenario 7).
 */
function useInvalidateProducts() {
  const queryClient = useQueryClient();
  return () => queryClient.invalidateQueries({ queryKey: queryKeys.products.all });
}

export function useCreateProduct() {
  const invalidate = useInvalidateProducts();
  return useMutation({
    mutationFn: (data: Record<string, unknown>) => createProduct(data),
    onSuccess: invalidate,
  });
}

export function useUpdateProduct(id: string) {
  const invalidate = useInvalidateProducts();
  return useMutation({
    mutationFn: (data: Record<string, unknown>) => updateProduct(id, data),
    onSuccess: invalidate,
  });
}

export function useSetProductStatus(id: string) {
  const invalidate = useInvalidateProducts();
  return useMutation({
    mutationFn: (status: ProductStatus) => setProductStatus(id, status),
    onSuccess: invalidate,
  });
}

export function useDeleteProduct() {
  const invalidate = useInvalidateProducts();
  return useMutation({
    mutationFn: (id: string) => deleteProduct(id, true),
    onSuccess: invalidate,
  });
}

export function useUploadProductImage(productId: string) {
  const invalidate = useInvalidateProducts();
  return useMutation({
    mutationFn: ({ file, isPrimary }: { file: File; isPrimary?: boolean }) =>
      uploadProductImage(productId, file, isPrimary),
    onSuccess: invalidate,
  });
}

export function useSetPrimaryImage(productId: string) {
  const invalidate = useInvalidateProducts();
  return useMutation({
    mutationFn: (imageId: string) => setPrimaryProductImage(productId, imageId),
    onSuccess: invalidate,
  });
}

export function useReorderImages(productId: string) {
  const invalidate = useInvalidateProducts();
  return useMutation({
    mutationFn: (imageIds: string[]) => reorderProductImages(productId, imageIds),
    onSuccess: invalidate,
  });
}

export function useDeleteProductImage(productId: string) {
  const invalidate = useInvalidateProducts();
  return useMutation({
    mutationFn: (imageId: string) => deleteProductImage(productId, imageId),
    onSuccess: invalidate,
  });
}

export function useUploadProductModel(productId: string) {
  const invalidate = useInvalidateProducts();
  return useMutation({
    mutationFn: (file: File) => uploadProductModel(productId, file),
    onSuccess: invalidate,
  });
}

export function useDeleteProductModel(productId: string) {
  const invalidate = useInvalidateProducts();
  return useMutation({
    mutationFn: () => deleteProductModel(productId),
    onSuccess: invalidate,
  });
}
