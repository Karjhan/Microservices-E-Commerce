'use client';

import { use } from 'react';
import { useProduct } from '@/hooks/queries/use-products';
import { ProductGallery } from '@/components/products/product-gallery';
import { ErrorFallback } from '@/components/shared/error-fallback';
import { Skeleton } from '@/components/ui/skeleton';
import { Badge } from '@/components/ui/badge';
import { Separator } from '@/components/ui/separator';
import { Button } from '@/components/ui/button';
import { ShoppingCart } from 'lucide-react';
import { useCart } from '@/hooks/use-cart';
import { useInventory } from '@/hooks/queries/use-inventory';
import { AvailabilityBadge } from '@/components/products/availability-badge';
import { ProductRecommendations } from '@/components/products/product-recommendations';
import { ProductViewerSkeleton } from '@/components/products/product-viewer-skeleton';
import { useProductModel } from '@/hooks/queries/use-admin-products';
import { formatFileSize } from '@/schemas/product';
import type { ProductModel } from '@/types/product';
import { formatCurrency, resolveMediaUrl } from '@/lib/utils';
import dynamic from 'next/dynamic';
import Image from 'next/image';

const ProductViewer = dynamic(
  () => import('@/components/products/product-viewer').then((m) => m.ProductViewer),
  { ssr: false, loading: () => <ProductViewerSkeleton /> }
);

const ModelInfoCard = dynamic(
  () => import('@/components/products/product-viewer').then((m) => m.ModelInfoCard),
  { ssr: false }
);

function ModelPreview({ productId, model }: { productId: string; model: ProductModel }) {
  const { data, isLoading } = useProductModel(productId, model.isPreviewable);

  if (!model.isPreviewable) {
    return (
      <div>
        <h2 className="text-xl font-semibold mb-3">3D Model</h2>
        <ModelInfoCard
          filename={model.originalFilename}
          format={model.format}
          sizeLabel={formatFileSize(model.sizeBytes)}
        />
      </div>
    );
  }

  if (isLoading) return <ProductViewerSkeleton />;
  if (!data?.url) return null;

  return (
    <div>
      <h2 className="text-xl font-semibold mb-3">3D Preview</h2>
      <ProductViewer modelUrl={data.url} format={model.format} />
    </div>
  );
}

function formatSetting(value: number | null | undefined, suffix = '') {
  return value == null ? '—' : `${value}${suffix}`;
}

export default function ProductDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const { data: product, isLoading, isError, refetch } = useProduct(id);
  const { addItem } = useCart();
  const { data: inventory } = useInventory(product?.id || '');
  const isOutOfStock = inventory ? inventory.available <= 0 : false;

  if (isLoading) {
    return (
      <div className="grid gap-8 md:grid-cols-2">
        <Skeleton className="aspect-square rounded-lg" />
        <div className="space-y-4">
          <Skeleton className="h-8 w-3/4" />
          <Skeleton className="h-6 w-1/4" />
          <Skeleton className="h-20 w-full" />
        </div>
      </div>
    );
  }

  if (isError || !product) {
    return <ErrorFallback title="Product not found" onRetry={() => refetch()} />;
  }

  const handleAddToCart = () => {
    const primaryImage = product.images.find((img) => img.isPrimary) || product.images[0];
    addItem({
      productId: product.id,
      productName: product.name,
      slug: product.slug,
      price: product.price,
      currency: product.currency,
      imageUrl: resolveMediaUrl(primaryImage?.url)!,
      material: product.supportedMaterials[0] || null,
    });
  };

  return (
    <div className="space-y-8">
      <div className="grid gap-8 md:grid-cols-2">
        <ProductGallery images={product.images} productName={product.name} />

        <div className="space-y-6">
          <div>
            {product.brand && (
              <div className="mb-2 flex items-center gap-2 text-sm text-muted-foreground" data-testid="product-brand">
                {product.brand.logoUrl && (
                  <Image
                    src={resolveMediaUrl(product.brand.logoUrl)!}
                    alt=""
                    width={24}
                    height={24}
                    className="h-6 w-6 object-contain"
                  />
                )}
                <span>{product.brand.name}</span>
              </div>
            )}
            <h1 className="text-3xl font-bold">{product.name}</h1>
            <p className="text-2xl font-bold mt-2">{formatCurrency(product.price, product.currency)}</p>
          </div>

          <p className="text-muted-foreground">{product.shortDescription}</p>

          <div className="flex flex-wrap gap-2">
            {product.filamentTypes.map((f) => (
              <Badge key={f.id} variant="secondary">{f.name}</Badge>
            ))}
            {product.compatiblePrinters.map((p) => (
              <Badge key={p} variant="outline">{p}</Badge>
            ))}
          </div>

          {inventory && <AvailabilityBadge available={inventory.available} />}

          <Button size="lg" className="w-full" onClick={handleAddToCart} disabled={isOutOfStock}>
            <ShoppingCart className="mr-2 h-5 w-5" />
            {isOutOfStock ? 'Out of Stock' : 'Add to Cart'}
          </Button>

          <Separator />

          <div>
            <h2 className="text-lg font-semibold mb-3">Print Settings</h2>
            <dl className="grid grid-cols-2 gap-2 text-sm">
              <dt className="text-muted-foreground">Layer Height</dt>
              <dd>{formatSetting(product.settings.layerHeight, 'mm')}</dd>
              <dt className="text-muted-foreground">Infill</dt>
              <dd>{product.settings.infillPercentage == null ? '—' : `${product.settings.infillPercentage}%`}</dd>
              <dt className="text-muted-foreground">Nozzle Size</dt>
              <dd>{formatSetting(product.settings.nozzleSize, 'mm')}</dd>
              <dt className="text-muted-foreground">Print Time</dt>
              <dd>{product.settings.printTimeMinutes == null ? '—' : `${product.settings.printTimeMinutes} min`}</dd>
              <dt className="text-muted-foreground">Filament Used</dt>
              <dd>{product.settings.filamentUsedGrams == null ? '—' : `${product.settings.filamentUsedGrams}g`}</dd>
              <dt className="text-muted-foreground">Supports</dt>
              <dd>{product.settings.supportsRequired ? 'Required' : 'Not required'}</dd>
            </dl>
          </div>

          <div>
            <h2 className="text-lg font-semibold mb-3">Dimensions</h2>
            <dl className="grid grid-cols-2 gap-2 text-sm">
              <dt className="text-muted-foreground">Width</dt>
              <dd>{formatSetting(product.size.widthMm, 'mm')}</dd>
              <dt className="text-muted-foreground">Height</dt>
              <dd>{formatSetting(product.size.heightMm, 'mm')}</dd>
              <dt className="text-muted-foreground">Depth</dt>
              <dd>{formatSetting(product.size.depthMm, 'mm')}</dd>
            </dl>
          </div>

          {product.tags.length > 0 && (
            <div>
              <h2 className="text-lg font-semibold mb-2">Tags</h2>
              <div className="flex flex-wrap gap-1">
                {product.tags.map((tag) => (
                  <Badge key={tag} variant="outline" className="text-xs">{tag}</Badge>
                ))}
              </div>
            </div>
          )}

          {product.attributes.length > 0 && (
            <div>
              <h2 className="text-lg font-semibold mb-3">Attributes</h2>
              <dl className="grid grid-cols-2 gap-2 text-sm">
                {product.attributes.map((attr) => (
                  <div key={attr.attributeId}>
                    <dt className="text-muted-foreground capitalize">{attr.key}</dt>
                    <dd>{attr.value}</dd>
                  </div>
                ))}
              </dl>
            </div>
          )}
        </div>
      </div>

      {product.model && <ModelPreview productId={product.id} model={product.model} />}

      {product.longDescription && (
        <div>
          <h2 className="text-xl font-semibold mb-3">Description</h2>
          <div className="prose prose-sm dark:prose-invert max-w-none">
            <p>{product.longDescription}</p>
          </div>
        </div>
      )}

      <ProductRecommendations productId={product.id} />
    </div>
  );
}