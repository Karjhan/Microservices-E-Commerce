'use client';

import { useRef, useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { toast } from 'sonner';
import { ImagePlus, Upload, X } from 'lucide-react';
import { ProductForm } from '@/components/admin/product-form';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { useCreateProduct } from '@/hooks/queries/use-admin-products';
import { ApiClientError, errorMessage } from '@/lib/api/client';
import { uploadProductImage, uploadProductModel } from '@/lib/api/products';
import {
  IMAGE_ACCEPT,
  IMAGE_TYPES_HINT,
  MODEL_ACCEPT,
  MODEL_TYPES_HINT,
  formatFileSize,
  validateImageFile,
  validateModelFile,
  type CreateProductFormValues,
} from '@/schemas/product';

interface FailedUpload {
  kind: 'image' | 'model';
  file: File;
  reason: string;
}

export default function AdminNewProductPage() {
  const router = useRouter();
  const createMutation = useCreateProduct();
  const imageInput = useRef<HTMLInputElement>(null);
  const modelInput = useRef<HTMLInputElement>(null);

  const [images, setImages] = useState<File[]>([]);
  const [model, setModel] = useState<File | null>(null);
  const [fileProblems, setFileProblems] = useState<string[]>([]);
  const [createdId, setCreatedId] = useState<string | null>(null);
  const [failures, setFailures] = useState<FailedUpload[]>([]);
  const [uploading, setUploading] = useState(false);

  function pickImages(files: FileList | null) {
    if (!files) return;
    const accepted: File[] = [];
    const problems: string[] = [];
    for (const file of Array.from(files)) {
      const problem = validateImageFile(file);
      if (problem) problems.push(problem);
      else accepted.push(file);
    }
    setImages((current) => [...current, ...accepted]);
    setFileProblems(problems);
    if (imageInput.current) imageInput.current.value = '';
  }

  function pickModel(files: FileList | null) {
    const file = files?.[0];
    if (!file) return;
    const problem = validateModelFile(file);
    setFileProblems(problem ? [problem] : []);
    if (!problem) setModel(file);
    if (modelInput.current) modelInput.current.value = '';
  }

  async function uploadMedia(productId: string, queue: { images: File[]; model: File | null }) {
    const failed: FailedUpload[] = [];

    for (const file of queue.images) {
      try {
        await uploadProductImage(productId, file);
      } catch (error) {
        failed.push({ kind: 'image', file, reason: errorMessage(error, 'Upload failed.') });
      }
    }

    if (queue.model) {
      try {
        await uploadProductModel(productId, queue.model);
      } catch (error) {
        failed.push({ kind: 'model', file: queue.model, reason: errorMessage(error, 'Upload failed.') });
      }
    }

    return failed;
  }

  async function finishUploads(productId: string, queue: { images: File[]; model: File | null }) {
    setUploading(true);
    const failed = await uploadMedia(productId, queue);
    setUploading(false);
    setFailures(failed);

    if (failed.length === 0) {
      toast.success('Product created as a draft. Publish it when it is ready.');
      router.push(`/admin/products/${productId}`);
    }
  }

  async function handleSubmit(values: CreateProductFormValues) {
    setFileProblems([]);
    try {
      const { productId } = await createMutation.mutateAsync(values as unknown as Record<string, unknown>);
      setCreatedId(productId);
      await finishUploads(productId, { images, model });
    } catch {
    }
  }

  function retryFailed() {
    if (!createdId) return;
    void finishUploads(createdId, {
      images: failures.filter((f) => f.kind === 'image').map((f) => f.file),
      model: failures.find((f) => f.kind === 'model')?.file ?? null,
    });
  }

  const serverFieldErrors =
    createMutation.error instanceof ApiClientError ? (createMutation.error.fieldErrors ?? null) : null;
  const generalError =
    createMutation.error && !serverFieldErrors
      ? errorMessage(createMutation.error, 'Failed to create the product. Please try again.')
      : null;
  const locked = createdId !== null;

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold">Create Product</h1>
      <p className="text-sm text-muted-foreground">
        New products start as drafts: shoppers cannot see them until you publish.
      </p>

      {locked && failures.length > 0 && (
        <Alert variant="destructive" role="alert">
          <AlertTitle>The product was saved as a draft, but some files were not stored</AlertTitle>
          <AlertDescription className="space-y-3">
            <ul className="list-disc pl-5">
              {failures.map((f) => (
                <li key={`${f.kind}-${f.file.name}`}>
                  {f.file.name}: {f.reason}
                </li>
              ))}
            </ul>
            <div className="flex flex-wrap gap-2">
              <Button type="button" variant="outline" size="sm" onClick={retryFailed} disabled={uploading}>
                {uploading ? 'Retrying...' : 'Retry upload'}
              </Button>
              <Button type="button" variant="outline" size="sm" render={<Link href={`/admin/products/${createdId}`} />}>
                Open the product
              </Button>
            </div>
          </AlertDescription>
        </Alert>
      )}

      <fieldset disabled={locked} className="space-y-6 disabled:opacity-60">
        <ProductForm
          onSubmit={(data) => void handleSubmit(data)}
          isLoading={createMutation.isPending || uploading}
          serverErrors={serverFieldErrors}
        />

        <Card>
          <CardHeader>
            <CardTitle>Images and 3D model (optional)</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <p className="text-sm text-muted-foreground">
              Files are uploaded right after the product is created. You can also add them later.
            </p>

            <div className="space-y-2">
              <input
                ref={imageInput}
                type="file"
                accept={IMAGE_ACCEPT}
                multiple
                className="sr-only"
                id="new-product-images"
                onChange={(e) => pickImages(e.target.files)}
              />
              <Button type="button" variant="outline" onClick={() => imageInput.current?.click()}>
                <ImagePlus className="mr-2 h-4 w-4" />
                Choose images
              </Button>
              <p className="text-xs text-muted-foreground">{IMAGE_TYPES_HINT}. The first image becomes the main one.</p>
              {images.length > 0 && (
                <ul className="space-y-1 text-sm" data-testid="staged-images">
                  {images.map((file, index) => (
                    <li key={`${file.name}-${index}`} className="flex items-center justify-between gap-2">
                      <span className="truncate">
                        {file.name} · {formatFileSize(file.size)}
                      </span>
                      <Button
                        type="button"
                        size="icon-sm"
                        variant="ghost"
                        aria-label={`Remove ${file.name}`}
                        onClick={() => setImages((current) => current.filter((_, i) => i !== index))}
                      >
                        <X className="h-4 w-4" />
                      </Button>
                    </li>
                  ))}
                </ul>
              )}
            </div>

            <div className="space-y-2">
              <input
                ref={modelInput}
                type="file"
                accept={MODEL_ACCEPT}
                className="sr-only"
                id="new-product-model"
                onChange={(e) => pickModel(e.target.files)}
              />
              <Button type="button" variant="outline" onClick={() => modelInput.current?.click()}>
                <Upload className="mr-2 h-4 w-4" />
                {model ? 'Change 3D model' : 'Choose 3D model'}
              </Button>
              <p className="text-xs text-muted-foreground">{MODEL_TYPES_HINT}.</p>
              {model && (
                <p className="flex items-center justify-between gap-2 text-sm" data-testid="staged-model">
                  <span className="truncate">
                    {model.name} · {formatFileSize(model.size)}
                  </span>
                  <Button
                    type="button"
                    size="icon-sm"
                    variant="ghost"
                    aria-label={`Remove ${model.name}`}
                    onClick={() => setModel(null)}
                  >
                    <X className="h-4 w-4" />
                  </Button>
                </p>
              )}
            </div>

            {fileProblems.length > 0 && (
              <ul role="alert" className="space-y-1 text-sm text-destructive">
                {fileProblems.map((p) => (
                  <li key={p}>{p}</li>
                ))}
              </ul>
            )}
          </CardContent>
        </Card>
      </fieldset>

      {generalError && (
        <p role="alert" className="text-sm text-destructive">
          {generalError}
        </p>
      )}
    </div>
  );
}
