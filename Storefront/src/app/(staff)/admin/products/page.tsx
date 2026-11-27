'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { ArrowDownAZ, ArrowUpAZ, Plus, Search } from 'lucide-react';
import { useAdminProducts } from '@/hooks/queries/use-admin-products';
import { useBrands } from '@/hooks/queries/use-brands';
import { useFilamentTypes } from '@/hooks/queries/use-filament-types';
import { NativeSelect } from '@/components/shared/native-select';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { EmptyState } from '@/components/shared/empty-state';
import { ErrorFallback } from '@/components/shared/error-fallback';
import { TableSkeleton } from '@/components/shared/loading-skeletons';
import { formatCurrency } from '@/lib/utils';
import { PRODUCT_STATUSES } from '@/types/product';
import type { AdminProductFilters } from '@/lib/api/products';

type SortBy = NonNullable<AdminProductFilters['sortBy']>;

const SORT_LABELS: Record<SortBy, string> = {
  createdAt: 'Newest',
  name: 'Name',
  price: 'Price',
  status: 'Status',
};

export default function AdminProductsPage() {
  const [search, setSearch] = useState('');
  const [filters, setFilters] = useState<AdminProductFilters>({
    page: 1,
    pageSize: 20,
    sortBy: 'createdAt',
    sortDir: 'desc',
  });

  useEffect(() => {
    const handle = setTimeout(() => {
      setFilters((prev) => {
        const q = search.trim() || undefined;
        return prev.q === q ? prev : { ...prev, q, page: 1 };
      });
    }, 300);
    return () => clearTimeout(handle);
  }, [search]);

  const { data, isLoading, isError, refetch } = useAdminProducts(filters);
  const { data: brands } = useBrands(true);
  const { data: filamentTypes } = useFilamentTypes(true);

  if (isError) return <ErrorFallback onRetry={() => refetch()} />;

  const sortDir = filters.sortDir ?? 'desc';

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <h1 className="text-2xl font-bold">Products</h1>
        <Button className="w-full sm:w-auto" render={<Link href="/admin/products/new" />}>
          <Plus className="mr-2 h-4 w-4" />
          New Product
        </Button>
      </div>

      <div className="flex flex-col gap-3 sm:flex-row sm:items-center">
        <div className="relative flex-1">
          <Search className="pointer-events-none absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground" aria-hidden />
          <Input
            type="search"
            aria-label="Search products"
            placeholder="Search by name or description"
            className="pl-8"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </div>

        <Select
          value={filters.status ?? 'all'}
          onValueChange={(v) =>
            setFilters((prev) => ({ ...prev, status: !v || v === 'all' ? undefined : v, page: 1 }))
          }
        >
          <SelectTrigger className="w-full sm:w-[160px]" aria-label="Filter by status">
            <SelectValue placeholder="All Statuses" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All Statuses</SelectItem>
            {PRODUCT_STATUSES.map((s) => (
              <SelectItem key={s} value={s}>
                {s}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>

        <NativeSelect
          aria-label="Filter by brand"
          className="sm:w-[160px]"
          value={filters.brandId ?? ''}
          onChange={(e) => setFilters((prev) => ({ ...prev, brandId: e.target.value || undefined, page: 1 }))}
        >
          <option value="">All Brands</option>
          {brands?.map((b) => (
            <option key={b.id} value={b.id}>
              {b.name}
            </option>
          ))}
        </NativeSelect>

        <NativeSelect
          aria-label="Filter by filament type"
          className="sm:w-[170px]"
          value={filters.filamentType ?? ''}
          onChange={(e) => setFilters((prev) => ({ ...prev, filamentType: e.target.value || undefined, page: 1 }))}
        >
          <option value="">All Filament Types</option>
          {filamentTypes?.map((f) => (
            <option key={f.id} value={f.code}>
              {f.name}
            </option>
          ))}
        </NativeSelect>

        <Select
          value={filters.sortBy ?? 'createdAt'}
          onValueChange={(v) => setFilters((prev) => ({ ...prev, sortBy: (v as SortBy) || 'createdAt', page: 1 }))}
        >
          <SelectTrigger className="w-full sm:w-[140px]" aria-label="Sort by">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {(Object.keys(SORT_LABELS) as SortBy[]).map((s) => (
              <SelectItem key={s} value={s}>
                {SORT_LABELS[s]}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>

        <Button
          type="button"
          variant="outline"
          size="icon"
          aria-label={sortDir === 'asc' ? 'Sorted ascending, switch to descending' : 'Sorted descending, switch to ascending'}
          onClick={() => setFilters((prev) => ({ ...prev, sortDir: sortDir === 'asc' ? 'desc' : 'asc', page: 1 }))}
        >
          {sortDir === 'asc' ? <ArrowUpAZ className="h-4 w-4" /> : <ArrowDownAZ className="h-4 w-4" />}
        </Button>
      </div>

      {isLoading ? (
        <TableSkeleton rows={5} cols={6} />
      ) : data && data.items.length > 0 ? (
        <>
          <div className="overflow-x-auto">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Name</TableHead>
                  <TableHead>Brand</TableHead>
                  <TableHead>Price</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Filament types</TableHead>
                  <TableHead></TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {data.items.map((product) => (
                  <TableRow key={product.id} data-testid="product-row">
                    <TableCell className="font-medium">{product.name}</TableCell>
                    <TableCell>{product.brand?.name ?? '—'}</TableCell>
                    <TableCell>{formatCurrency(product.price, product.currency)}</TableCell>
                    <TableCell>
                      <Badge variant={product.status === 'Active' ? 'default' : 'secondary'}>{product.status}</Badge>
                    </TableCell>
                    <TableCell>
                      <div className="flex flex-wrap gap-1">
                        {(product.filamentTypes ?? []).map((f) => (
                          <Badge key={f.id} variant="outline" className="text-xs">
                            {f.name}
                          </Badge>
                        ))}
                      </div>
                    </TableCell>
                    <TableCell>
                      <Button variant="ghost" size="sm" render={<Link href={`/admin/products/${product.id}`} />}>
                        Edit
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
          {data.totalPages > 1 && (
            <div className="flex justify-center gap-2">
              <Button
                variant="outline"
                disabled={filters.page === 1}
                onClick={() => setFilters((p) => ({ ...p, page: (p.page || 1) - 1 }))}
              >
                Previous
              </Button>
              <span className="flex items-center px-4 text-sm text-muted-foreground">
                Page {data.page} of {data.totalPages}
              </span>
              <Button
                variant="outline"
                disabled={data.page >= data.totalPages}
                onClick={() => setFilters((p) => ({ ...p, page: (p.page || 1) + 1 }))}
              >
                Next
              </Button>
            </div>
          )}
        </>
      ) : (
        <EmptyState
          title="No products"
          description={
            filters.q || filters.status || filters.brandId || filters.filamentType ? 'Nothing matches these filters' : 'Create your first product to get started'
          }
        />
      )}
    </div>
  );
}
