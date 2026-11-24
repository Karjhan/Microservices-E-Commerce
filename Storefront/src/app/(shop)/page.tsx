'use client';

import Link from 'next/link';
import { useProducts } from '@/hooks/queries/use-products';
import { ProductCard } from '@/components/products/product-card';
import { ProductCardSkeleton } from '@/components/shared/loading-skeletons';
import { Button } from '@/components/ui/button';
import { ArrowRight, Package } from 'lucide-react';

export default function HomePage() {
  const { data, isLoading } = useProducts({ page: 1, pageSize: 8 });

  return (
    <div className="space-y-16">
      <section className="py-12 text-center space-y-6">
        <div className="inline-flex items-center gap-2 rounded-full border px-4 py-1.5 text-sm text-muted-foreground">
          <Package className="h-4 w-4" />
          Premium 3D-Printed Objects
        </div>
        <h1 className="text-4xl sm:text-5xl md:text-6xl font-bold tracking-tight">
          Discover Unique <br className="hidden sm:inline" />
          3D-Printed Creations
        </h1>
        <p className="mx-auto max-w-2xl text-lg text-muted-foreground">
          Browse our curated catalog of high-quality 3D-printed objects. From functional tools to decorative art,
          find the perfect piece printed with precision.
        </p>
        <div className="flex flex-col sm:flex-row justify-center gap-3 sm:gap-4 px-2">
          <Button size="lg" className="w-full sm:w-auto" render={<Link href="/products" />}>
            Browse Catalog
            <ArrowRight className="ml-2 h-4 w-4" />
          </Button>
          <Button size="lg" variant="outline" className="w-full sm:w-auto" render={<Link href="/search" />}>Search Products</Button>
        </div>
      </section>

      <section className="space-y-6">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <h2 className="text-2xl font-bold">Featured Products</h2>
          <Button variant="ghost" render={<Link href="/products" />}>
            View all <ArrowRight className="ml-1 h-4 w-4" />
          </Button>
        </div>

        {isLoading ? (
          <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-4">
            {Array.from({ length: 4 }).map((_, i) => (
              <ProductCardSkeleton key={i} />
            ))}
          </div>
        ) : data && data.items.length > 0 ? (
          <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-4">
            {data.items.slice(0, 8).map((product) => (
              <ProductCard key={product.id} product={product} />
            ))}
          </div>
        ) : (
          <p className="text-center text-muted-foreground py-12">
            No products available yet. Check back soon!
          </p>
        )}
      </section>
    </div>
  );
}
