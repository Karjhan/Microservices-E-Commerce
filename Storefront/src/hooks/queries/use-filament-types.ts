'use client';

import { listFilamentTypes } from '@/lib/api/reference-data';
import type { FilamentType } from '@/types/reference-data';
import { useReferenceList } from './use-reference-data';

/** Active filament types, or every filament type (admin only) when `includeInactive` is set. */
export const useFilamentTypes = (includeInactive = false) =>
  useReferenceList<FilamentType>('filament-types', listFilamentTypes, includeInactive);
