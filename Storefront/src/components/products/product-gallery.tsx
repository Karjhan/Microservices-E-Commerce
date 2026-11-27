'use client';

import Image from 'next/image';
import { useState } from 'react';
import { ImageOff } from 'lucide-react';
import { cn, resolveMediaUrl } from '@/lib/utils';
import type { ProductImage } from '@/types/product';

interface ProductGalleryProps {
  images: ProductImage[];
  productName: string;
}

export function ProductGallery({ images, productName }: ProductGalleryProps) {
  // Primary first, then the order the admin chose.
  const sorted = [...images].sort(
    (a, b) => (b.isPrimary ? 1 : 0) - (a.isPrimary ? 1 : 0) || (a.sortOrder ?? 0) - (b.sortOrder ?? 0)
  );
  const [selectedIndex, setSelectedIndex] = useState(0);
  const selected = sorted[Math.min(selectedIndex, sorted.length - 1)];

  if (!selected) {
    return (
      <div
        className="aspect-square rounded-lg bg-muted flex flex-col items-center justify-center gap-2 text-muted-foreground"
        data-testid="gallery-placeholder"
      >
        <ImageOff className="h-10 w-10" aria-hidden />
        <span>No images available</span>
      </div>
    );
  }

  const imageSrc = resolveMediaUrl(selected.url);

  return (
    <div className="space-y-4">
      <div className="relative aspect-square overflow-hidden rounded-lg bg-muted">
        <Image
          src={imageSrc!}
          alt={`${productName} - Image ${selectedIndex + 1}`}
          fill
          className="object-cover"
          sizes="(max-width: 768px) 100vw, 50vw"
          priority={selectedIndex === 0}
        />
      </div>
      {sorted.length > 1 && (
        <div className="flex gap-2 overflow-x-auto pb-2">
          {sorted.map((image, index) => (
            <button
              key={image.imageId}
              onClick={() => setSelectedIndex(index)}
              className={cn(
                'relative h-16 w-16 flex-shrink-0 overflow-hidden rounded-md border-2 transition-colors',
                index === selectedIndex
                  ? 'border-primary'
                  : 'border-transparent hover:border-muted-foreground/50'
              )}
              aria-label={`View image ${index + 1}`}
            >
              <Image
                src={image.url}
                alt={`${productName} thumbnail ${index + 1}`}
                fill
                className="object-cover"
                sizes="64px"
              />
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
