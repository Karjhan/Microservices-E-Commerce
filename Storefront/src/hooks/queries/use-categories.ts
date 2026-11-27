'use client';

import { listCategories } from '@/lib/api/reference-data';
import type { Category } from '@/types/reference-data';
import { useReferenceList } from './use-reference-data';

/** Active categories, or every category (admin only) when `includeInactive` is set. */
export const useCategories = (includeInactive = false) =>
  useReferenceList<Category>('categories', listCategories, includeInactive);
