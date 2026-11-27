'use client';

import { use, useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { toast } from 'sonner';
import { ExternalLink } from 'lucide-react';
import { ConfirmDialog } from '@/components/admin/confirm-dialog';
import { ModelUpload } from '@/components/admin/model-upload';
import { ProductAttributesPanel } from '@/components/admin/product-attributes-panel';
import { ProductForm, productToFormValues } from '@/components/admin/product-form';
import { ProductMediaPanel } from '@/components/admin/product-media-panel';
import { ProductStatusControl } from '@/components/admin/product-status-control';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { ErrorFallback } from '@/components/shared/error-fallback';
import { useAdminProduct, useDeleteProduct, useUpdateProduct } from '@/hooks/queries/use-admin-products';
import { ApiClientError, errorMessage } from '@/lib/api/client';
import type { CreateProductFormValues } from '@/schemas/product';

export default function AdminEditProductPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const router = useRouter();
  const [confirmDelete, setConfirmDelete] = useState(false);
  const { data: product, isLoading, isError, refetch } = useAdminProduct(id);

  const updateMutation = useUpdateProduct(id);
  const deleteMutation = useDeleteProduct();

  if (isLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-8 w-48" />
        <Skeleton className="h-96 w-full" />
      </div>
    );
  }
  if (isError || !product) return <ErrorFallback title="Product not found" onRetry={() => refetch()} />;

  const fieldErrors =
    updateMutation.error instanceof ApiClientError ? (updateMutation.error.fieldErrors ?? null) : null;
  const generalError =
    updateMutation.error && !fieldErrors ? errorMessage(updateMutation.error, 'Failed to update the product.') : null;

  const handleSave = (values: CreateProductFormValues) =>
    updateMutation.mutate(values as unknown as Record<string, unknown>, {
      onSuccess: () => toast.success('Product saved.'),
    });

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <h1 className="text-2xl font-bold">Edit Product</h1>
        <div className="flex flex-wrap gap-2">
          {product.status === 'Active' && (
            <Button variant="outline" render={<Link href={`/products/${product.id}`} />}>
              <ExternalLink className="mr-2 h-4 w-4" />
              View in shop
            </Button>
          )}
          <Button variant="destructive" onClick={() => setConfirmDelete(true)} disabled={deleteMutation.isPending}>
            Delete Product
          </Button>
        </div>
      </div>

      <ProductStatusControl productId={product.id} status={product.status} />

      <ProductForm
        defaultValues={productToFormValues(product)}
        onSubmit={handleSave}
        isLoading={updateMutation.isPending}
        submitLabel="Update Product"
        serverErrors={fieldErrors}
      />
      {generalError && (
        <p role="alert" className="text-sm text-destructive">
          {generalError}
        </p>
      )}

      <ProductAttributesPanel productId={product.id} attributes={product.attributes} />
      <ProductMediaPanel productId={product.id} productName={product.name} images={product.images} />
      <ModelUpload productId={product.id} model={product.model} />

      <ConfirmDialog
        open={confirmDelete}
        onOpenChange={setConfirmDelete}
        title={`Delete "${product.name}"?`}
        description="The product, its images, its 3D model and its attributes are removed for good. Existing orders keep their own record of what was bought."
        confirmLabel="Delete product"
        pending={deleteMutation.isPending}
        onConfirm={() =>
          deleteMutation.mutate(id, {
            onSuccess: () => {
              toast.success('Product deleted.');
              router.push('/admin/products');
            },
            onError: (e) => {
              setConfirmDelete(false);
              toast.error(errorMessage(e, 'Could not delete the product.'));
            },
          })
        }
      />
    </div>
  );
}
