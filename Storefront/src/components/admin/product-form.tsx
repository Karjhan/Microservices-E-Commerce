'use client';

import { useEffect } from 'react';
import { useForm, type FieldPath } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { createProductSchema, type CreateProductFormValues } from '@/schemas/product';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import type { Product } from '@/types/product';

const MATERIALS = ['PLA', 'ABS', 'PETG', 'Resin', 'Nylon', 'TPU'] as const;
const PRINTERS = ['FDM', 'SLA', 'SLS'] as const;

const FORM_DEFAULTS: CreateProductFormValues = {
  name: '',
  shortDescription: '',
  longDescription: '',
  price: 0,
  categoryId: '',
  currency: 'EUR',
  tags: [],
  supportedMaterials: [],
  compatiblePrinters: [],
  settings: {
    layerHeight: 0.2,
    infillPercentage: 20,
    nozzleSize: 0.4,
    printTimeMinutes: 60,
    filamentUsedGrams: 50,
    supportsRequired: false,
  },
  size: { widthMm: 0, heightMm: 0, depthMm: 0 },
};

export function productToFormValues(product: Product): CreateProductFormValues {
  return {
    name: product.name,
    shortDescription: product.shortDescription,
    longDescription: product.longDescription,
    price: product.price,
    categoryId: product.categoryId,
    currency: 'EUR',
    tags: product.tags ?? [],
    supportedMaterials: product.supportedMaterials ?? [],
    compatiblePrinters: product.compatiblePrinters ?? [],
    settings: {
      layerHeight: product.settings?.layerHeight ?? FORM_DEFAULTS.settings.layerHeight,
      infillPercentage: product.settings?.infillPercentage ?? FORM_DEFAULTS.settings.infillPercentage,
      nozzleSize: product.settings?.nozzleSize ?? FORM_DEFAULTS.settings.nozzleSize,
      printTimeMinutes: product.settings?.printTimeMinutes ?? FORM_DEFAULTS.settings.printTimeMinutes,
      filamentUsedGrams: product.settings?.filamentUsedGrams ?? FORM_DEFAULTS.settings.filamentUsedGrams,
      supportsRequired: product.settings?.supportsRequired ?? false,
    },
    size: {
      widthMm: product.size?.widthMm ?? 0,
      heightMm: product.size?.heightMm ?? 0,
      depthMm: product.size?.depthMm ?? 0,
    },
  };
}

interface ProductFormProps {
  defaultValues?: Partial<CreateProductFormValues>;
  onSubmit: (data: CreateProductFormValues) => void;
  isLoading?: boolean;
  submitLabel?: string;
  serverErrors?: Record<string, string[]> | null;
}

function FieldError({ message }: { message?: string }) {
  if (!message) return null;
  return (
    <p role="alert" className="text-sm text-destructive">
      {message}
    </p>
  );
}

export function ProductForm({
  defaultValues,
  onSubmit,
  isLoading,
  submitLabel = 'Create Product',
  serverErrors,
}: ProductFormProps) {
  const {
    register,
    handleSubmit,
    formState: { errors },
    watch,
    setValue,
    setError,
  } = useForm<CreateProductFormValues>({
    resolver: zodResolver(createProductSchema),
    defaultValues: { ...FORM_DEFAULTS, ...defaultValues },
  });

  useEffect(() => {
    if (!serverErrors) return;
    const known: Record<string, FieldPath<CreateProductFormValues>> = {
      name: 'name',
      shortdescription: 'shortDescription',
      longdescription: 'longDescription',
      price: 'price',
      currency: 'currency',
      categoryid: 'categoryId',
      tags: 'tags',
      supportedmaterials: 'supportedMaterials',
      compatibleprinters: 'compatiblePrinters',
    };
    for (const [field, messages] of Object.entries(serverErrors)) {
      const target = known[field.toLowerCase()];
      if (target) setError(target, { type: 'server', message: messages.join(' ') });
    }
  }, [serverErrors, setError]);

  const selectedMaterials = watch('supportedMaterials') || [];
  const selectedPrinters = watch('compatiblePrinters') || [];
  const tags = watch('tags') || [];

  const toggleMaterial = (m: (typeof MATERIALS)[number]) => {
    const next = selectedMaterials.includes(m) ? selectedMaterials.filter((x) => x !== m) : [...selectedMaterials, m];
    setValue('supportedMaterials', next, { shouldValidate: true });
  };

  const togglePrinter = (p: (typeof PRINTERS)[number]) => {
    const next = selectedPrinters.includes(p) ? selectedPrinters.filter((x) => x !== p) : [...selectedPrinters, p];
    setValue('compatiblePrinters', next, { shouldValidate: true });
  };

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-6" noValidate>
      <Card>
        <CardHeader>
          <CardTitle>Basic Info</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="name">Name</Label>
            <Input id="name" aria-invalid={!!errors.name} {...register('name')} />
            <FieldError message={errors.name?.message} />
          </div>
          <div className="space-y-2">
            <Label htmlFor="shortDescription">Short Description</Label>
            <Input id="shortDescription" aria-invalid={!!errors.shortDescription} {...register('shortDescription')} />
            <FieldError message={errors.shortDescription?.message} />
          </div>
          <div className="space-y-2">
            <Label htmlFor="longDescription">Long Description</Label>
            <Textarea id="longDescription" rows={4} aria-invalid={!!errors.longDescription} {...register('longDescription')} />
            <FieldError message={errors.longDescription?.message} />
          </div>
          <div className="grid gap-4 sm:grid-cols-3">
            <div className="space-y-2">
              <Label htmlFor="price">Price</Label>
              <Input
                id="price"
                type="number"
                step="0.01"
                min="0"
                inputMode="decimal"
                aria-invalid={!!errors.price}
                {...register('price', { valueAsNumber: true })}
              />
              <FieldError message={errors.price?.message} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="currency">Currency</Label>
              <Input id="currency" readOnly aria-describedby="currency-hint" {...register('currency')} />
              <p id="currency-hint" className="text-xs text-muted-foreground">
                Prices are in euro.
              </p>
              <FieldError message={errors.currency?.message} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="categoryId">Category ID</Label>
              <Input id="categoryId" aria-invalid={!!errors.categoryId} {...register('categoryId')} />
              <FieldError message={errors.categoryId?.message} />
            </div>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Materials &amp; Printers</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="space-y-2">
            <Label>Supported Materials</Label>
            <div className="flex flex-wrap gap-1" role="group" aria-label="Supported materials">
              {MATERIALS.map((m) => (
                <button key={m} type="button" onClick={() => toggleMaterial(m)} aria-pressed={selectedMaterials.includes(m)}>
                  <Badge variant={selectedMaterials.includes(m) ? 'default' : 'outline'} className="cursor-pointer">
                    {m}
                  </Badge>
                </button>
              ))}
            </div>
            <FieldError message={errors.supportedMaterials?.message} />
          </div>
          <div className="space-y-2">
            <Label>Compatible Printers</Label>
            <div className="flex flex-wrap gap-1" role="group" aria-label="Compatible printers">
              {PRINTERS.map((p) => (
                <button key={p} type="button" onClick={() => togglePrinter(p)} aria-pressed={selectedPrinters.includes(p)}>
                  <Badge variant={selectedPrinters.includes(p) ? 'default' : 'outline'} className="cursor-pointer">
                    {p}
                  </Badge>
                </button>
              ))}
            </div>
            <FieldError message={errors.compatiblePrinters?.message} />
          </div>
          <div className="space-y-2">
            <Label htmlFor="tags">Tags</Label>
            <Input
              id="tags"
              placeholder="Comma-separated, e.g. desk, organizer"
              defaultValue={tags.join(', ')}
              onChange={(e) =>
                setValue(
                  'tags',
                  e.target.value
                    .split(',')
                    .map((t) => t.trim())
                    .filter(Boolean),
                  { shouldValidate: true }
                )
              }
            />
            <FieldError message={errors.tags?.message as string | undefined} />
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Print Settings</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid gap-4 sm:grid-cols-3">
            <div className="space-y-2">
              <Label htmlFor="layerHeight">Layer Height (mm)</Label>
              <Input id="layerHeight" type="number" step="0.01" {...register('settings.layerHeight', { valueAsNumber: true })} />
              <FieldError message={errors.settings?.layerHeight?.message} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="infill">Infill (%)</Label>
              <Input id="infill" type="number" {...register('settings.infillPercentage', { valueAsNumber: true })} />
              <FieldError message={errors.settings?.infillPercentage?.message} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="nozzle">Nozzle Size (mm)</Label>
              <Input id="nozzle" type="number" step="0.1" {...register('settings.nozzleSize', { valueAsNumber: true })} />
              <FieldError message={errors.settings?.nozzleSize?.message} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="printTime">Print Time (min)</Label>
              <Input id="printTime" type="number" {...register('settings.printTimeMinutes', { valueAsNumber: true })} />
              <FieldError message={errors.settings?.printTimeMinutes?.message} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="filament">Filament Used (g)</Label>
              <Input id="filament" type="number" {...register('settings.filamentUsedGrams', { valueAsNumber: true })} />
              <FieldError message={errors.settings?.filamentUsedGrams?.message} />
            </div>
            <div className="flex items-center gap-2 pt-6">
              <input type="checkbox" id="supports" {...register('settings.supportsRequired')} />
              <Label htmlFor="supports">Supports Required</Label>
            </div>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Dimensions</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid gap-4 sm:grid-cols-3">
            <div className="space-y-2">
              <Label htmlFor="width">Width (mm)</Label>
              <Input id="width" type="number" {...register('size.widthMm', { valueAsNumber: true })} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="height">Height (mm)</Label>
              <Input id="height" type="number" {...register('size.heightMm', { valueAsNumber: true })} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="depth">Depth (mm)</Label>
              <Input id="depth" type="number" {...register('size.depthMm', { valueAsNumber: true })} />
            </div>
          </div>
        </CardContent>
      </Card>

      <Button type="submit" size="lg" disabled={isLoading}>
        {isLoading ? 'Saving...' : submitLabel}
      </Button>
    </form>
  );
}
