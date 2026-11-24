'use client';

import { useQuery } from '@tanstack/react-query';
import { recommend } from '@/lib/api/search';
import { ProductCard } from './product-card';
import { ProductCardSkeleton } from '@/components/shared/loading-skeletons';
import { ScrollArea, ScrollBar } from '@/components/ui/scroll-area';
import type { ProductListItem } from '@/types/product';

interface ProductRecommendationsProps {
  productId: string;
  limit?: number;
}

export function ProductRecommendations({ productId, limit = 8 }: ProductRecommendationsProps) {
  const { data, isLoading, isError } = useQuery({
    queryKey: ['recommendations', productId],
    queryFn: () => recommend({ product_id: productId, limit }),
    enabled: !!productId,
    staleTime: 5 * 60 * 1000,
  });

  if (isError || (!isLoading && (!data || data.results.length === 0))) return null;

  return (
    <section className="space-y-4">
      <h2 className="text-xl font-semibold">You Might Also Like</h2>
      <ScrollArea className="w-full whitespace-nowrap">
        <div className="flex gap-4 pb-4">
          {isLoading
            ? Array.from({ length: 4 }).map((_, i) => (
                <div key={i} className="w-[min(16rem,calc(100vw-4rem))] flex-shrink-0 sm:w-64">
                  <ProductCardSkeleton />
                </div>
              ))
            : data?.results.map((result) => (
                <div key={result.product_id} className="w-[min(16rem,calc(100vw-4rem))] flex-shrink-0 sm:w-64">
                  <ProductCard
                    product={{
                      id: result.product_id,
                      name: result.name,
                      slug: result.slug,
                      shortDescription: '',
                      price: 0,
                      currency: 'EUR',
                      status: 'Active',
                      supportedMaterials: [],
                      compatiblePrinters: [],
                      images: [],
                      downloadCount: 0,
                      averageRating: 0,
                      createdAt: '',
                      updatedAt: null,
                    } as ProductListItem}
                  />
                </div>
              ))}
        </div>
        <ScrollBar orientation="horizontal" />
      </ScrollArea>
    </section>
  );
}
