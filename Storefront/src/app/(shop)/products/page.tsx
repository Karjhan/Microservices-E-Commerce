'use client';

import { useState } from 'react';
import { useProducts } from '@/hooks/queries/use-products';
import { ProductCard } from '@/components/products/product-card';
import { ProductCardSkeleton } from '@/components/shared/loading-skeletons';
import { EmptyState } from '@/components/shared/empty-state';
import { ErrorFallback } from '@/components/shared/error-fallback';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Search } from 'lucide-react';
import type { ProductFilters } from '@/lib/api/products';

const MATERIALS = ['PLA', 'ABS', 'PETG', 'Resin', 'Nylon', 'TPU'];
const PRINTER_TYPES = ['FDM', 'SLA', 'SLS'];

export default function ProductsPage() {
  const [filters, setFilters] = useState<ProductFilters>({ page: 1, pageSize: 12 });
  const [searchInput, setSearchInput] = useState('');
  const { data, isLoading, isError, refetch } = useProducts(filters);

  const handleSearch = () => {
    setFilters((prev) => ({ ...prev, search: searchInput || undefined, page: 1 }));
  };

  if (isError) return <ErrorFallback onRetry={() => refetch()} />;

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl sm:text-3xl font-bold">Products</h1>
        <p className="text-muted-foreground mt-1">Browse our 3D-printed objects catalog</p>
      </div>

      <div className="flex flex-col gap-4">
        <div className="flex flex-1 gap-2 min-w-0">
          <Input
            placeholder="Search products..."
            value={searchInput}
            onChange={(e) => setSearchInput(e.target.value)}
            onKeyDown={(e) => e.key === 'Enter' && handleSearch()}
            className="min-w-0"
          />
          <Button onClick={handleSearch} size="icon" className="shrink-0" aria-label="Search">
            <Search className="h-4 w-4" />
          </Button>
        </div>

        <div className="flex flex-col sm:flex-row gap-4">
        <Select
          value={filters.material || ''}
          onValueChange={(v) => setFilters((prev) => ({ ...prev, material: v || undefined, page: 1 }))}
        >
          <SelectTrigger className="w-full sm:w-[140px]">
            <SelectValue placeholder="Material" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All Materials</SelectItem>
            {MATERIALS.map((m) => (
              <SelectItem key={m} value={m}>{m}</SelectItem>
            ))}
          </SelectContent>
        </Select>

        <Select
          value={filters.printerType || ''}
          onValueChange={(v) => setFilters((prev) => ({ ...prev, printerType: v || undefined, page: 1 }))}
        >
          <SelectTrigger className="w-full sm:w-[140px]">
            <SelectValue placeholder="Printer" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All Printers</SelectItem>
            {PRINTER_TYPES.map((p) => (
              <SelectItem key={p} value={p}>{p}</SelectItem>
            ))}
          </SelectContent>
        </Select>
        </div>
      </div>

      {isLoading ? (
        <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
          {Array.from({ length: 8 }).map((_, i) => (
            <ProductCardSkeleton key={i} />
          ))}
        </div>
      ) : data && data.items.length > 0 ? (
        <>
          <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
            {data.items.map((product) => (
              <ProductCard key={product.id} product={product} />
            ))}
          </div>

          {data.totalPages > 1 && (
            <div className="flex justify-center gap-2">
              <Button
                variant="outline"
                disabled={filters.page === 1}
                onClick={() => setFilters((prev) => ({ ...prev, page: (prev.page || 1) - 1 }))}
              >
                Previous
              </Button>
              <span className="flex items-center px-4 text-sm text-muted-foreground">
                Page {data.page} of {data.totalPages}
              </span>
              <Button
                variant="outline"
                disabled={data.page >= data.totalPages}
                onClick={() => setFilters((prev) => ({ ...prev, page: (prev.page || 1) + 1 }))}
              >
                Next
              </Button>
            </div>
          )}
        </>
      ) : (
        <EmptyState
          title="No products found"
          description="Try adjusting your filters or search terms"
        />
      )}
    </div>
  );
}
