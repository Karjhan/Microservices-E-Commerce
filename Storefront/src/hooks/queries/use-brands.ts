'use client';

import { listBrands } from '@/lib/api/reference-data';
import type { Brand } from '@/types/reference-data';
import { useReferenceList } from './use-reference-data';

/** Active brands, or every brand (admin only) when `includeInactive` is set. */
export const useBrands = (includeInactive = false) => useReferenceList<Brand>('brands', listBrands, includeInactive);
