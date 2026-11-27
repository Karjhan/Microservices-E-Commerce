'use client';

import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Badge } from '@/components/ui/badge';
import { useFilamentTypes } from '@/hooks/queries/use-filament-types';
import type { SearchFilters as SearchFiltersType } from '@/types/search';

const PRINTERS = ['FDM', 'SLA', 'SLS'];

interface SearchFilterPanelProps {
  filters: SearchFiltersType;
  onChange: (filters: SearchFiltersType) => void;
  onClear: () => void;
}

export function SearchFilterPanel({ filters, onChange, onClear }: SearchFilterPanelProps) {
  // The materials are the admin-managed filament types; search matches on their codes.
  const { data: filamentTypes } = useFilamentTypes();
  const toggleMaterial = (m: string) => {
    const current = filters.materials || [];
    const next = current.includes(m) ? current.filter((x) => x !== m) : [...current, m];
    onChange({ ...filters, materials: next.length > 0 ? next : undefined });
  };

  const togglePrinter = (p: string) => {
    const current = filters.printers || [];
    const next = current.includes(p) ? current.filter((x) => x !== p) : [...current, p];
    onChange({ ...filters, printers: next.length > 0 ? next : undefined });
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h3 className="font-semibold">Filters</h3>
        <Button variant="ghost" size="sm" onClick={onClear}>Clear all</Button>
      </div>

      <div className="space-y-2">
        <Label>Materials</Label>
        <div className="flex flex-wrap gap-1">
          {(filamentTypes ?? []).map((f) => (
            <Badge
              key={f.id}
              variant={filters.materials?.includes(f.code) ? 'default' : 'outline'}
              className="cursor-pointer"
              onClick={() => toggleMaterial(f.code)}
            >
              {f.name}
            </Badge>
          ))}
        </div>
      </div>

      <div className="space-y-2">
        <Label>Printer Type</Label>
        <div className="flex flex-wrap gap-1">
          {PRINTERS.map((p) => (
            <Badge
              key={p}
              variant={filters.printers?.includes(p) ? 'default' : 'outline'}
              className="cursor-pointer"
              onClick={() => togglePrinter(p)}
            >
              {p}
            </Badge>
          ))}
        </div>
      </div>

      <div className="space-y-2">
        <Label>Price Range</Label>
        <div className="flex gap-2">
          <Input
            type="number"
            placeholder="Min"
            value={filters.price_min ?? ''}
            onChange={(e) => onChange({ ...filters, price_min: e.target.value ? Number(e.target.value) : undefined })}
          />
          <Input
            type="number"
            placeholder="Max"
            value={filters.price_max ?? ''}
            onChange={(e) => onChange({ ...filters, price_max: e.target.value ? Number(e.target.value) : undefined })}
          />
        </div>
      </div>

      <div className="space-y-2">
        <Label>Min Rating</Label>
        <Input
          type="number"
          min={0}
          max={5}
          step={0.5}
          placeholder="0-5"
          value={filters.min_rating ?? ''}
          onChange={(e) => onChange({ ...filters, min_rating: e.target.value ? Number(e.target.value) : undefined })}
        />
      </div>
    </div>
  );
}
