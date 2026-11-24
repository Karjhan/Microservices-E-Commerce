'use client';

import Image from 'next/image';
import { useState } from 'react';
import { cn, resolveMediaUrl } from '@/lib/utils';
import type { ProductImage } from '@/types/product';

interface ProductGalleryProps {
  images: ProductImage[];
  productName: string;
}

export function ProductGallery({ images, productName }: ProductGalleryProps) {
  const sorted = [...images].sort((a, b) => (b.isPrimary ? 1 : 0) - (a.isPrimary ? 1 : 0));
  const [selectedIndex, setSelectedIndex] = useState(0);
  const selected = sorted[selectedIndex];
  const imageSrc = resolveMediaUrl(selected.url);

  if (sorted.length === 0) {
    return (
      <div className="aspect-square rounded-lg bg-muted flex items-center justify-center text-muted-foreground">
        No images available
      </div>
    );
  }

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
