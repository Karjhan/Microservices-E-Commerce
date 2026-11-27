/** Admin-managed lists a product is classified with (FR-021). */
export type ReferenceResource = 'brands' | 'categories' | 'filament-types';

interface ReferenceEntryBase {
  id: string;
  name: string;
  isActive: boolean;
  /** How many products use it. Only returned to admins who ask for inactive entries too. */
  productCount: number | null;
  createdAt: string;
  updatedAt: string;
}

export interface Brand extends ReferenceEntryBase {
  logoUrl: string | null;
}

export type Category = ReferenceEntryBase;

export interface FilamentType extends ReferenceEntryBase {
  /** Stable identifier, for example PLA. Set when the filament type is created and never changed. */
  code: string;
  diameterMm: number | null;
  colorOptions: string[];
}

/** The brand as a product shows it. */
export interface ProductBrand {
  id: string;
  name: string;
  logoUrl: string | null;
}

/** A filament type as a product shows it. */
export interface ProductFilamentType {
  id: string;
  name: string;
  code: string;
}
