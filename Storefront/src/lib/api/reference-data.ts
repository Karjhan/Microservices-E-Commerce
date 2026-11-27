import { apiDelete, apiGet, apiPost, apiPut, apiPutFormData, buildQueryString } from './client';
import type { Brand, Category, FilamentType, ReferenceResource } from '@/types/reference-data';

// Listing is public (active entries). Asking for inactive ones needs an Admin token.

function list<T>(resource: ReferenceResource, includeInactive: boolean): Promise<T[]> {
  const qs = buildQueryString({ includeInactive: includeInactive ? true : undefined });
  return apiGet<T[]>(`/${resource}${qs}`, { skipAuth: !includeInactive });
}

export const listBrands = (includeInactive = false) => list<Brand>('brands', includeInactive);
export const listCategories = (includeInactive = false) => list<Category>('categories', includeInactive);
export const listFilamentTypes = (includeInactive = false) =>
  list<FilamentType>('filament-types', includeInactive);

// ---- Admin (AdminOnly on the server) ---------------------------------------------------------------------------

export function createReference(resource: ReferenceResource, body: Record<string, unknown>): Promise<{ id: string }> {
  return apiPost<{ id: string }>(`/${resource}`, body);
}

export function updateReference(resource: ReferenceResource, id: string, body: Record<string, unknown>): Promise<void> {
  return apiPut<void>(`/${resource}/${id}`, body);
}

/** Deactivated entries stay on the products that already use them but cannot be chosen again. */
export function setReferenceActive(resource: ReferenceResource, id: string, active: boolean): Promise<void> {
  return apiPost<void>(`/${resource}/${id}/${active ? 'activate' : 'deactivate'}`);
}

/** Refused with 409 `in_use` (and `details.productCount`) while products still use the entry. */
export function deleteReference(resource: ReferenceResource, id: string): Promise<void> {
  return apiDelete<void>(`/${resource}/${id}`);
}

export function uploadBrandLogo(brandId: string, file: File): Promise<{ logoUrl: string }> {
  const formData = new FormData();
  formData.append('file', file);
  return apiPutFormData<{ logoUrl: string }>(`/brands/${brandId}/logo`, formData);
}

export function removeBrandLogo(brandId: string): Promise<void> {
  return apiDelete<void>(`/brands/${brandId}/logo`);
}
