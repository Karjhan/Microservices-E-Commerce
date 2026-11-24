'use client';

import { useRouter } from 'next/navigation';
import { useMutation } from '@tanstack/react-query';
import { createProduct } from '@/lib/api/products';
import { ProductForm } from '@/components/admin/product-form';
import type { CreateProductFormValues } from '@/schemas/product';

export default function AdminNewProductPage() {
  const router = useRouter();
  const mutation = useMutation({
    mutationFn: (data: CreateProductFormValues) => createProduct(data as any),
    onSuccess: () => router.push('/admin/products'),
  });

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold">Create Product</h1>
      <ProductForm onSubmit={(data) => mutation.mutate(data)} isLoading={mutation.isPending} />
      {mutation.error && (
        <p className="text-sm text-destructive">Failed to create product. Please try again.</p>
      )}
    </div>
  );
}
