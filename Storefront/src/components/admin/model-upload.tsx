'use client';

import { useRef, useState } from 'react';
import { toast } from 'sonner';
import { Box, Trash2, Upload } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { ConfirmDialog } from '@/components/admin/confirm-dialog';
import { useDeleteProductModel, useUploadProductModel } from '@/hooks/queries/use-admin-products';
import { errorMessage } from '@/lib/api/client';
import { MODEL_ACCEPT, MODEL_TYPES_HINT, formatFileSize, validateModelFile } from '@/schemas/product';
import type { ProductModel } from '@/types/product';

interface ModelUploadProps {
  productId: string;
  model: ProductModel | null;
}

/** Attach, replace or remove the product's single 3D model and say whether shoppers can preview it. */
export function ModelUpload({ productId, model }: ModelUploadProps) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [problem, setProblem] = useState<string | null>(null);
  const [confirmRemove, setConfirmRemove] = useState(false);

  const upload = useUploadProductModel(productId);
  const remove = useDeleteProductModel(productId);

  function handleFile(files: FileList | null) {
    const file = files?.[0];
    if (!file) return;

    const invalid = validateModelFile(file);
    if (invalid) {
      setProblem(invalid);
    } else {
      setProblem(null);
      upload.mutate(file, {
        onSuccess: () => toast.success(model ? '3D model replaced.' : '3D model attached.'),
        onError: (e) => setProblem(errorMessage(e, 'Upload failed.')),
      });
    }

    if (inputRef.current) inputRef.current.value = '';
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>3D model</CardTitle>
      </CardHeader>
      <CardContent className="space-y-4">
        <p className="text-sm text-muted-foreground">Accepted: {MODEL_TYPES_HINT}.</p>

        {model ? (
          <div className="flex items-start gap-3 rounded-lg border p-3" data-testid="model-info">
            <Box className="mt-1 h-6 w-6 shrink-0 text-muted-foreground" aria-hidden />
            <div className="min-w-0 space-y-1">
              <p className="truncate font-medium">{model.originalFilename}</p>
              <div className="flex flex-wrap items-center gap-2 text-sm text-muted-foreground">
                <Badge variant="secondary" className="uppercase">
                  {model.format}
                </Badge>
                <span>{formatFileSize(model.sizeBytes)}</span>
                <span>
                  {model.isPreviewable
                    ? 'Shoppers get a 3D preview.'
                    : 'No preview for this format; shoppers cannot rotate it online.'}
                </span>
              </div>
            </div>
          </div>
        ) : (
          <p className="rounded-md border border-dashed p-4 text-center text-sm text-muted-foreground">
            No 3D model attached.
          </p>
        )}

        {problem && (
          <p role="alert" className="text-sm text-destructive">
            {problem}
          </p>
        )}

        <div className="flex flex-wrap gap-2">
          <input
            ref={inputRef}
            id="product-model-input"
            type="file"
            accept={MODEL_ACCEPT}
            className="sr-only"
            onChange={(e) => handleFile(e.target.files)}
          />
          <Button
            type="button"
            variant="outline"
            disabled={upload.isPending || remove.isPending}
            onClick={() => inputRef.current?.click()}
          >
            <Upload className="mr-2 h-4 w-4" />
            {upload.isPending ? 'Uploading...' : model ? 'Replace model' : 'Attach model'}
          </Button>
          {model && (
            <Button
              type="button"
              variant="destructive"
              disabled={upload.isPending || remove.isPending}
              onClick={() => setConfirmRemove(true)}
            >
              <Trash2 className="mr-2 h-4 w-4" />
              Remove
            </Button>
          )}
        </div>
      </CardContent>

      <ConfirmDialog
        open={confirmRemove}
        onOpenChange={setConfirmRemove}
        title="Remove the 3D model?"
        description="The file is deleted from storage and shoppers lose the preview."
        confirmLabel="Remove"
        pending={remove.isPending}
        onConfirm={() =>
          remove.mutate(undefined, {
            onSuccess: () => {
              setConfirmRemove(false);
              toast.success('3D model removed.');
            },
            onError: (e) => {
              setConfirmRemove(false);
              toast.error(errorMessage(e, 'Could not remove the model.'));
            },
          })
        }
      />
    </Card>
  );
}
