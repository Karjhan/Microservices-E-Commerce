'use client';

import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { createProductSchema, type CreateProductFormValues } from '@/schemas/product';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Separator } from '@/components/ui/separator';

const MATERIALS = ['PLA', 'ABS', 'PETG', 'Resin', 'Nylon', 'TPU'] as const;
const PRINTERS = ['FDM', 'SLA', 'SLS'] as const;

interface ProductFormProps {
  defaultValues?: Partial<CreateProductFormValues>;
  onSubmit: (data: CreateProductFormValues) => void;
  isLoading?: boolean;
  submitLabel?: string;
}

export function ProductForm({ defaultValues, onSubmit, isLoading, submitLabel = 'Create Product' }: ProductFormProps) {
  const { register, handleSubmit, formState: { errors }, watch, setValue } = useForm<CreateProductFormValues>({
    resolver: zodResolver(createProductSchema),
    defaultValues: {
      currency: 'EUR',
      tags: [],
      supportedMaterials: [],
      compatiblePrinters: [],
      settings: { layerHeight: 0.2, infillPercentage: 20, nozzleSize: 0.4, printTimeMinutes: 60, filamentUsedGrams: 50, supportsRequired: false },
      size: { widthMm: 0, heightMm: 0, depthMm: 0 },
      ...defaultValues,
    },
  });

  const selectedMaterials = watch('supportedMaterials') || [];
  const selectedPrinters = watch('compatiblePrinters') || [];

  const toggleMaterial = (m: typeof MATERIALS[number]) => {
    const current = selectedMaterials;
    const next = current.includes(m) ? current.filter((x) => x !== m) : [...current, m];
    setValue('supportedMaterials', next as any);
  };

  const togglePrinter = (p: typeof PRINTERS[number]) => {
    const current = selectedPrinters;
    const next = current.includes(p) ? current.filter((x) => x !== p) : [...current, p];
    setValue('compatiblePrinters', next as any);
  };

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-6">
      <Card>
        <CardHeader><CardTitle>Basic Info</CardTitle></CardHeader>
        <CardContent className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="name">Name</Label>
            <Input id="name" {...register('name')} />
            {errors.name && <p className="text-sm text-destructive">{errors.name.message}</p>}
          </div>
          <div className="space-y-2">
            <Label htmlFor="shortDescription">Short Description</Label>
            <Input id="shortDescription" {...register('shortDescription')} />
            {errors.shortDescription && <p className="text-sm text-destructive">{errors.shortDescription.message}</p>}
          </div>
          <div className="space-y-2">
            <Label htmlFor="longDescription">Long Description</Label>
            <Textarea id="longDescription" rows={4} {...register('longDescription')} />
            {errors.longDescription && <p className="text-sm text-destructive">{errors.longDescription.message}</p>}
          </div>
          <div className="grid gap-4 sm:grid-cols-3">
            <div className="space-y-2">
              <Label htmlFor="price">Price</Label>
              <Input id="price" type="number" step="0.01" {...register('price', { valueAsNumber: true })} />
              {errors.price && <p className="text-sm text-destructive">{errors.price.message}</p>}
            </div>
            <div className="space-y-2">
              <Label htmlFor="currency">Currency</Label>
              <Input id="currency" {...register('currency')} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="categoryId">Category ID</Label>
              <Input id="categoryId" {...register('categoryId')} />
              {errors.categoryId && <p className="text-sm text-destructive">{errors.categoryId.message}</p>}
            </div>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>Materials & Printers</CardTitle></CardHeader>
        <CardContent className="space-y-4">
          <div className="space-y-2">
            <Label>Supported Materials</Label>
            <div className="flex flex-wrap gap-1">
              {MATERIALS.map((m) => (
                <Badge key={m} variant={selectedMaterials.includes(m) ? 'default' : 'outline'} className="cursor-pointer" onClick={() => toggleMaterial(m)}>
                  {m}
                </Badge>
              ))}
            </div>
            {errors.supportedMaterials && <p className="text-sm text-destructive">{errors.supportedMaterials.message}</p>}
          </div>
          <div className="space-y-2">
            <Label>Compatible Printers</Label>
            <div className="flex flex-wrap gap-1">
              {PRINTERS.map((p) => (
                <Badge key={p} variant={selectedPrinters.includes(p) ? 'default' : 'outline'} className="cursor-pointer" onClick={() => togglePrinter(p)}>
                  {p}
                </Badge>
              ))}
            </div>
            {errors.compatiblePrinters && <p className="text-sm text-destructive">{errors.compatiblePrinters.message}</p>}
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>Print Settings</CardTitle></CardHeader>
        <CardContent>
          <div className="grid gap-4 sm:grid-cols-3">
            <div className="space-y-2">
              <Label>Layer Height (mm)</Label>
              <Input type="number" step="0.01" {...register('settings.layerHeight', { valueAsNumber: true })} />
            </div>
            <div className="space-y-2">
              <Label>Infill (%)</Label>
              <Input type="number" {...register('settings.infillPercentage', { valueAsNumber: true })} />
            </div>
            <div className="space-y-2">
              <Label>Nozzle Size (mm)</Label>
              <Input type="number" step="0.1" {...register('settings.nozzleSize', { valueAsNumber: true })} />
            </div>
            <div className="space-y-2">
              <Label>Print Time (min)</Label>
              <Input type="number" {...register('settings.printTimeMinutes', { valueAsNumber: true })} />
            </div>
            <div className="space-y-2">
              <Label>Filament Used (g)</Label>
              <Input type="number" {...register('settings.filamentUsedGrams', { valueAsNumber: true })} />
            </div>
            <div className="flex items-center gap-2 pt-6">
              <input type="checkbox" id="supports" {...register('settings.supportsRequired')} />
              <Label htmlFor="supports">Supports Required</Label>
            </div>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>Dimensions</CardTitle></CardHeader>
        <CardContent>
          <div className="grid gap-4 sm:grid-cols-3">
            <div className="space-y-2">
              <Label>Width (mm)</Label>
              <Input type="number" {...register('size.widthMm', { valueAsNumber: true })} />
            </div>
            <div className="space-y-2">
              <Label>Height (mm)</Label>
              <Input type="number" {...register('size.heightMm', { valueAsNumber: true })} />
            </div>
            <div className="space-y-2">
              <Label>Depth (mm)</Label>
              <Input type="number" {...register('size.depthMm', { valueAsNumber: true })} />
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
