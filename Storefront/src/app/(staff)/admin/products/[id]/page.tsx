'use client';

import { use } from 'react';
import { useRouter } from 'next/navigation';
import { useMutation } from '@tanstack/react-query';
import { useProduct } from '@/hooks/queries/use-products';
import { updateProduct, deleteProduct } from '@/lib/api/products';
import { ProductForm } from '@/components/admin/product-form';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { ErrorFallback } from '@/components/shared/error-fallback';
import type { CreateProductFormValues } from '@/schemas/product';

export default function AdminEditProductPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const router = useRouter();
  const { data: product, isLoading, isError, refetch } = useProduct(id);

  const updateMutation = useMutation({
    mutationFn: (data: CreateProductFormValues) => updateProduct(id, data as any),
    onSuccess: () => router.push('/admin/products'),
  });

  const deleteMutation = useMutation({
    mutationFn: () => deleteProduct(id),
    onSuccess: () => router.push('/admin/products'),
  });

  if (isLoading) return <div className="space-y-4"><Skeleton className="h-8 w-48" /><Skeleton className="h-96 w-full" /></div>;
  if (isError || !product) return <ErrorFallback title="Product not found" onRetry={() => refetch()} />;

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold">Edit Product</h1>
        <Button
          variant="destructive"
          onClick={() => { if (confirm('Delete this product?')) deleteMutation.mutate(); }}
          disabled={deleteMutation.isPending}
        >
          {deleteMutation.isPending ? 'Deleting...' : 'Delete Product'}
        </Button>
      </div>
      <ProductForm
        defaultValues={product as any}
        onSubmit={(data) => updateMutation.mutate(data)}
        isLoading={updateMutation.isPending}
        submitLabel="Update Product"
      />
      {updateMutation.error && <p className="text-sm text-destructive">Failed to update product.</p>}
    </div>
  );
}
