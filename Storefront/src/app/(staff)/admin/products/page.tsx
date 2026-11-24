'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useProducts } from '@/hooks/queries/use-products';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { EmptyState } from '@/components/shared/empty-state';
import { ErrorFallback } from '@/components/shared/error-fallback';
import { TableSkeleton } from '@/components/shared/loading-skeletons';
import { formatCurrency } from '@/lib/utils';
import { Plus } from 'lucide-react';
import type { ProductFilters } from '@/lib/api/products';

const STATUSES = ['Draft', 'Active', 'Inactive', 'OutOfStock', 'Discontinued'];

export default function AdminProductsPage() {
  const [filters, setFilters] = useState<ProductFilters>({ page: 1, pageSize: 20 });
  const { data, isLoading, isError, refetch } = useProducts(filters);

  if (isError) return <ErrorFallback onRetry={() => refetch()} />;

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <h1 className="text-2xl font-bold">Products</h1>
        <Button className="w-full sm:w-auto" render={<Link href="/admin/products/new" />}><Plus className="mr-2 h-4 w-4" />New Product</Button>
      </div>

      <Select
        value={filters.status || ''}
        onValueChange={(v) => setFilters((prev) => ({ ...prev, status: v || undefined }))}
      >
        <SelectTrigger className="w-full sm:w-[160px]">
          <SelectValue placeholder="All Statuses" />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="all">All Statuses</SelectItem>
          {STATUSES.map((s) => (<SelectItem key={s} value={s}>{s}</SelectItem>))}
        </SelectContent>
      </Select>

      {isLoading ? (
        <TableSkeleton rows={5} cols={5} />
      ) : data && data.items.length > 0 ? (
        <>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Name</TableHead>
                <TableHead>Price</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Materials</TableHead>
                <TableHead></TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {data.items.map((product) => (
                <TableRow key={product.id}>
                  <TableCell className="font-medium">{product.name}</TableCell>
                  <TableCell>{formatCurrency(product.price, product.currency)}</TableCell>
                  <TableCell><Badge variant="secondary">{product.status}</Badge></TableCell>
                  <TableCell>
                    <div className="flex gap-1">
                      {product.supportedMaterials.map((m) => (
                        <Badge key={m} variant="outline" className="text-xs">{m}</Badge>
                      ))}
                    </div>
                  </TableCell>
                  <TableCell>
                    <Button variant="ghost" size="sm" render={<Link href={`/admin/products/${product.id}`} />}>Edit</Button>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
          {data.totalPages > 1 && (
            <div className="flex justify-center gap-2">
              <Button variant="outline" disabled={filters.page === 1} onClick={() => setFilters((p) => ({ ...p, page: (p.page || 1) - 1 }))}>Previous</Button>
              <span className="flex items-center px-4 text-sm text-muted-foreground">Page {data.page} of {data.totalPages}</span>
              <Button variant="outline" disabled={data.page >= data.totalPages} onClick={() => setFilters((p) => ({ ...p, page: (p.page || 1) + 1 }))}>Next</Button>
            </div>
          )}
        </>
      ) : (
        <EmptyState title="No products" description="Create your first product to get started" />
      )}
    </div>
  );
}
