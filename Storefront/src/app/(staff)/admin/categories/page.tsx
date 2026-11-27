'use client';

import { ReferenceList } from '@/components/admin/reference-list';
import { useCategories } from '@/hooks/queries/use-categories';
import {
  useCreateReference,
  useDeleteReference,
  useSetReferenceActive,
  useUpdateReference,
} from '@/hooks/queries/use-reference-data';
import type { Category } from '@/types/reference-data';

export default function AdminCategoriesPage() {
  const { data, isLoading, isError, refetch } = useCategories(true);
  const create = useCreateReference('categories');
  const update = useUpdateReference('categories');
  const setActive = useSetReferenceActive('categories');
  const remove = useDeleteReference('categories');

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold">Categories</h1>
        <p className="text-sm text-muted-foreground mt-1">Every product belongs to one category.</p>
      </div>

      <ReferenceList<Category, { name: string }>
        singular="category"
        plural="categories"
        entries={data}
        isLoading={isLoading}
        isError={isError}
        onRetry={() => refetch()}
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
