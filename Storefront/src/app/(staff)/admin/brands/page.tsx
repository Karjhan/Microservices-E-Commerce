'use client';

import { useRef, useState } from 'react';
import Image from 'next/image';
import { toast } from 'sonner';
import { ImagePlus } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { ReferenceList } from '@/components/admin/reference-list';
import { useBrands } from '@/hooks/queries/use-brands';
import {
  useCreateReference,
  useDeleteReference,
  useRemoveBrandLogo,
  useSetReferenceActive,
  useUpdateReference,
  useUploadBrandLogo,
} from '@/hooks/queries/use-reference-data';
import { errorMessage } from '@/lib/api/client';
import { resolveMediaUrl } from '@/lib/utils';
import { IMAGE_ACCEPT, IMAGE_TYPES_HINT, validateImageFile } from '@/schemas/product';
import type { Brand } from '@/types/reference-data';

function BrandLogoActions({ brand }: { brand: Brand }) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [busy, setBusy] = useState(false);
  const upload = useUploadBrandLogo();
  const remove = useRemoveBrandLogo();

  const handleFile = async (files: FileList | null) => {
    const file = files?.[0];
    if (!file) return;
    const problem = validateImageFile(file);
    if (problem) {
      toast.error(problem);
    } else {
      setBusy(true);
      try {
        await upload.mutateAsync({ brandId: brand.id, file });
        toast.success(`Saved the logo of "${brand.name}".`);
      } catch (e) {
        toast.error(errorMessage(e, 'Could not upload the logo.'));
      } finally {
        setBusy(false);
      }
    }
    if (inputRef.current) inputRef.current.value = '';
  };

  const handleRemove = async () => {
    setBusy(true);
    try {
      await remove.mutateAsync(brand.id);
      toast.success(`Removed the logo of "${brand.name}".`);
    } catch (e) {
      toast.error(errorMessage(e, 'Could not remove the logo.'));
    } finally {
      setBusy(false);
    }
  };

  return (
    <>
      <input
        ref={inputRef}
        type="file"
        accept={IMAGE_ACCEPT}
        className="sr-only"
        aria-label={`Logo file for ${brand.name}`}
        title={IMAGE_TYPES_HINT}
        onChange={(e) => void handleFile(e.target.files)}
      />
      <Button variant="ghost" size="sm" disabled={busy} onClick={() => inputRef.current?.click()}>
        <ImagePlus className="mr-1 h-4 w-4" />
        {brand.logoUrl ? 'Replace logo' : 'Add logo'}
      </Button>
      {brand.logoUrl && (
        <Button variant="ghost" size="sm" disabled={busy} onClick={() => void handleRemove()}>
          Remove logo
        </Button>
      )}
    </>
  );
}

export default function AdminBrandsPage() {
  const { data, isLoading, isError, refetch } = useBrands(true);
  const create = useCreateReference('brands');
  const update = useUpdateReference('brands');
  const setActive = useSetReferenceActive('brands');
  const remove = useDeleteReference('brands');

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold">Brands</h1>
        <p className="text-sm text-muted-foreground mt-1">
          The makers of the products. A logo is optional. {IMAGE_TYPES_HINT}.
        </p>
      </div>

      <ReferenceList<Brand, { name: string }>
        singular="brand"
        plural="brands"
        entries={data}
        isLoading={isLoading}
        isError={isError}
        onRetry={() => refetch()}
        columns={[
          {
            header: 'Logo',
            cell: (brand) => {
              const src = resolveMediaUrl(brand.logoUrl);
              return src ? (
                <Image src={src} alt={`${brand.name} logo`} width={32} height={32} className="h-8 w-8 object-contain" />
              ) : (
                <span className="text-muted-foreground">—</span>
              );
            },
          },
        ]}
        rowActions={(brand) => <BrandLogoActions brand={brand} />}
        emptyValue={{ name: '' }}
        toValue={(entry) => ({ name: entry.name })}
        onCreate={(value) => create.mutateAsync({ name: value.name.trim() })}
        onUpdate={(entry, value) => update.mutateAsync({ id: entry.id, body: { name: value.name.trim() } })}
        onSetActive={(entry, active) => setActive.mutateAsync({ id: entry.id, active })}
        onDelete={(entry) => remove.mutateAsync(entry.id)}
      />
    </div>
  );
}
