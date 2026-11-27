'use client';

import { useRef, useState } from 'react';
import Image from 'next/image';
import { toast } from 'sonner';
import { ArrowDown, ArrowUp, ImagePlus, Star, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { ConfirmDialog } from '@/components/admin/confirm-dialog';
import {
  useDeleteProductImage,
  useReorderImages,
  useSetPrimaryImage,
  useUploadProductImage,
} from '@/hooks/queries/use-admin-products';
import { errorMessage } from '@/lib/api/client';
import { resolveMediaUrl } from '@/lib/utils';
import { IMAGE_ACCEPT, IMAGE_TYPES_HINT, formatFileSize, validateImageFile } from '@/schemas/product';
import type { ProductImage } from '@/types/product';

interface ProductMediaPanelProps {
  productId: string;
  productName: string;
  images: ProductImage[];
}

/** Image grid for one product: upload, primary badge, keyboard-accessible reordering, delete with confirmation. */
export function ProductMediaPanel({ productId, productName, images }: ProductMediaPanelProps) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [pendingDelete, setPendingDelete] = useState<ProductImage | null>(null);
  const [rejections, setRejections] = useState<string[]>([]);

  const upload = useUploadProductImage(productId);
  const setPrimary = useSetPrimaryImage(productId);
  const reorder = useReorderImages(productId);
  const remove = useDeleteProductImage(productId);

  const ordered = [...images].sort((a, b) => a.sortOrder - b.sortOrder);
  const busy = upload.isPending || setPrimary.isPending || reorder.isPending || remove.isPending;

  async function handleFiles(files: FileList | null) {
    if (!files || files.length === 0) return;
    const problems: string[] = [];

    // Sequential on purpose: each upload appends to the sort order, and the first one may become primary.
    for (const file of Array.from(files)) {
      const problem = validateImageFile(file);
      if (problem) {
        problems.push(problem);
        continue;
      }
      try {
        await upload.mutateAsync({ file });
      } catch (error) {
        problems.push(`${file.name}: ${errorMessage(error, 'Upload failed.')}`);
      }
    }

    setRejections(problems);
    if (inputRef.current) inputRef.current.value = '';
  }

  function move(index: number, delta: -1 | 1) {
    const target = index + delta;
    if (target < 0 || target >= ordered.length) return;
    const ids = ordered.map((i) => i.imageId);
    [ids[index], ids[target]] = [ids[target], ids[index]];
    reorder.mutate(ids, { onError: (e) => toast.error(errorMessage(e, 'Could not reorder the images.')) });
  }

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between gap-2">
        <CardTitle>Images</CardTitle>
        <div>
          <input
            ref={inputRef}
            id="product-image-input"
            type="file"
            accept={IMAGE_ACCEPT}
            multiple
            className="sr-only"
            onChange={(e) => void handleFiles(e.target.files)}
          />
          <Button type="button" variant="outline" disabled={busy} onClick={() => inputRef.current?.click()}>
            <ImagePlus className="mr-2 h-4 w-4" />
            {upload.isPending ? 'Uploading...' : 'Add images'}
          </Button>
        </div>
      </CardHeader>
      <CardContent className="space-y-4">
        <p className="text-sm text-muted-foreground">Accepted: {IMAGE_TYPES_HINT}. The first image becomes the main one.</p>

        {rejections.length > 0 && (
          <ul role="alert" className="space-y-1 text-sm text-destructive">
            {rejections.map((message) => (
              <li key={message}>{message}</li>
            ))}
          </ul>
        )}

        {ordered.length === 0 ? (
          <p className="rounded-md border border-dashed p-6 text-center text-sm text-muted-foreground">
            No images yet. Shoppers will see a placeholder.
          </p>
        ) : (
          <ul className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3" data-testid="image-grid">
            {ordered.map((image, index) => (
              <li key={image.imageId} className="space-y-2 rounded-lg border p-2">
                <div className="relative aspect-square overflow-hidden rounded-md bg-muted">
                  <Image
                    src={resolveMediaUrl(image.url)!}
                    alt={`${productName} image ${index + 1}`}
                    fill
                    className="object-cover"
                    sizes="(max-width: 640px) 100vw, 200px"
                  />
                  {image.isPrimary && (
                    <Badge className="absolute left-2 top-2" data-testid="primary-badge">
                      Primary
                    </Badge>
                  )}
                </div>
                <p className="truncate text-xs text-muted-foreground">
                  {image.contentType} · {formatFileSize(image.sizeBytes)}
                </p>
                <div className="flex flex-wrap gap-1">
                  <Button
                    type="button"
                    size="sm"
                    variant="outline"
                    disabled={busy || image.isPrimary}
                    onClick={() =>
                      setPrimary.mutate(image.imageId, {
                        onError: (e) => toast.error(errorMessage(e, 'Could not change the main image.')),
                      })
                    }
                  >
                    <Star className="mr-1 h-3 w-3" />
                    Set primary
                  </Button>
                  <Button
                    type="button"
                    size="icon-sm"
                    variant="outline"
                    aria-label={`Move image ${index + 1} up`}
                    disabled={busy || index === 0}
                    onClick={() => move(index, -1)}
                  >
                    <ArrowUp className="h-4 w-4" />
                  </Button>
                  <Button
                    type="button"
                    size="icon-sm"
                    variant="outline"
                    aria-label={`Move image ${index + 1} down`}
                    disabled={busy || index === ordered.length - 1}
                    onClick={() => move(index, 1)}
                  >
                    <ArrowDown className="h-4 w-4" />
                  </Button>
                  <Button
                    type="button"
                    size="icon-sm"
                    variant="destructive"
                    aria-label={`Delete image ${index + 1}`}
                    disabled={busy}
                    onClick={() => setPendingDelete(image)}
                  >
                    <Trash2 className="h-4 w-4" />
                  </Button>
                </div>
              </li>
            ))}
          </ul>
        )}
      </CardContent>

      <ConfirmDialog
        open={pendingDelete !== null}
        onOpenChange={(open) => !open && setPendingDelete(null)}
        title="Delete this image?"
        description={
          pendingDelete?.isPrimary && ordered.length > 1
            ? 'This is the main image. The next image will become the main one.'
            : 'The image file is removed from storage and cannot be recovered.'
        }
        pending={remove.isPending}
        onConfirm={() => {
          if (!pendingDelete) return;
          remove.mutate(pendingDelete.imageId, {
            onSuccess: () => setPendingDelete(null),
            onError: (e) => {
              setPendingDelete(null);
              toast.error(errorMessage(e, 'Could not delete the image.'));
            },
          });
        }}
      />
    </Card>
  );
}
