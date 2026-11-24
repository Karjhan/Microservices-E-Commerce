import Image from 'next/image';
import Link from 'next/link';
import { Card, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { formatCurrency, resolveMediaUrl } from '@/lib/utils';
import type { ProductListItem } from '@/types/product';

interface ProductCardProps {
  product: ProductListItem;
}

export function ProductCard({ product }: ProductCardProps) {
  const primaryImage = product.images.find((img) => img.isPrimary) || product.images[0];
  const imageSrc = resolveMediaUrl(primaryImage?.url);

  return (
    <Link href={`/products/${product.id}`}>
      <Card className="group overflow-hidden transition-shadow hover:shadow-lg h-full">
        <div className="relative aspect-square overflow-hidden bg-muted">
          {imageSrc ? (
            <Image
              src={imageSrc}
              alt={product.name}
              fill
              className="object-cover transition-transform group-hover:scale-105"
              sizes="(max-width: 640px) 100vw, (max-width: 1024px) 50vw, 33vw"
            />
          ) : (
            <div className="flex h-full items-center justify-center text-muted-foreground">
              No image
            </div>
          )}
        </div>
        <CardContent className="p-4 space-y-2">
          <h3 className="font-semibold line-clamp-1 group-hover:text-primary transition-colors">
            {product.name}
          </h3>
          <p className="text-sm text-muted-foreground line-clamp-2">
            {product.shortDescription}
          </p>
          <p className="text-lg font-bold">{formatCurrency(product.price, product.currency)}</p>
          <div className="flex flex-wrap gap-1">
            {product.supportedMaterials.slice(0, 3).map((material) => (
              <Badge key={material} variant="secondary" className="text-xs">
                {material}
              </Badge>
            ))}
            {product.compatiblePrinters.map((printer) => (
              <Badge key={printer} variant="outline" className="text-xs">
                {printer}
              </Badge>
            ))}
          </div>
          {product.averageRating > 0 && (
            <p className="text-xs text-muted-foreground">
              {'★'.repeat(Math.round(product.averageRating))}{'☆'.repeat(5 - Math.round(product.averageRating))} ({product.averageRating.toFixed(1)})
            </p>
          )}
        </CardContent>
      </Card>
    </Link>
  );
}
