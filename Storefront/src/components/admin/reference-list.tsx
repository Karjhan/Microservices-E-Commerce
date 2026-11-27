'use client';

import { useState, type ReactNode } from 'react';
import { toast } from 'sonner';
import { Pencil, Plus, Trash2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { ConfirmDialog } from '@/components/admin/confirm-dialog';
import { EmptyState } from '@/components/shared/empty-state';
import { ErrorFallback } from '@/components/shared/error-fallback';
import { TableSkeleton } from '@/components/shared/loading-skeletons';
import { ApiClientError, errorMessage } from '@/lib/api/client';

export interface ReferenceEntry {
  id: string;
  name: string;
  isActive: boolean;
  productCount: number | null;
}

export type FieldErrors = Record<string, string[] | undefined>;

interface ReferenceListProps<T extends ReferenceEntry, V extends { name: string }> {
  singular: string;
  plural: string;
  entries: T[] | undefined;
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
  columns?: { header: string; cell: (entry: T) => ReactNode }[];
  rowActions?: (entry: T) => ReactNode;
  emptyValue: V;
  toValue: (entry: T) => V;
  renderFields?: (
    value: V,
    patch: (changes: Partial<V>) => void,
    errors: FieldErrors,
    mode: 'create' | 'edit'
  ) => ReactNode;
  validate?: (value: V) => FieldErrors | null;
  onCreate: (value: V) => Promise<unknown>;
  onUpdate: (entry: T, value: V) => Promise<unknown>;
  onSetActive: (entry: T, active: boolean) => Promise<unknown>;
  onDelete: (entry: T) => Promise<unknown>;
}

type DialogState<T> = { mode: 'create' } | { mode: 'edit'; entry: T } | null;

export function ReferenceList<T extends ReferenceEntry, V extends { name: string }>({
  singular,
  plural,
  entries,
  isLoading,
  isError,
  onRetry,
  columns = [],
  rowActions,
  emptyValue,
  toValue,
  renderFields,
  validate,
  onCreate,
  onUpdate,
  onSetActive,
  onDelete,
}: ReferenceListProps<T, V>) {
  const [dialog, setDialog] = useState<DialogState<T>>(null);
  const [value, setValue] = useState<V>(emptyValue);
  const [errors, setErrors] = useState<FieldErrors>({});
  const [saving, setSaving] = useState(false);

  const [toDelete, setToDelete] = useState<T | null>(null);
  const [deleting, setDeleting] = useState(false);
  const [inUse, setInUse] = useState<{ entry: T; productCount: number | null } | null>(null);
  const [deactivating, setDeactivating] = useState(false);
  const [togglingId, setTogglingId] = useState<string | null>(null);

  const openCreate = () => {
    setValue(emptyValue);
    setErrors({});
    setDialog({ mode: 'create' });
  };

  const openEdit = (entry: T) => {
    setValue(toValue(entry));
    setErrors({});
    setDialog({ mode: 'edit', entry });
  };

  const patch = (changes: Partial<V>) => setValue((prev) => ({ ...prev, ...changes }));

  const submit = async () => {
    if (!dialog) return;
    const local = validate?.(value);
    const nameProblem = value.name.trim().length < 2 ? { name: ['Name must be at least 2 characters.'] } : null;
    const found = { ...(nameProblem ?? {}), ...(local ?? {}) };
    if (Object.keys(found).length > 0) {
      setErrors(found);
      return;
    }

    setSaving(true);
    try {
      if (dialog.mode === 'create') await onCreate(value);
      else await onUpdate(dialog.entry, value);
      toast.success(dialog.mode === 'create' ? `Added the ${singular}.` : `Saved the ${singular}.`);
      setDialog(null);
    } catch (e) {
      if (e instanceof ApiClientError && e.fieldErrors) {
        setErrors(e.fieldErrors);
      } else {
        toast.error(errorMessage(e, `Could not save the ${singular}.`));
      }
    } finally {
      setSaving(false);
    }
  };

  const toggleActive = async (entry: T) => {
    setTogglingId(entry.id);
    try {
      await onSetActive(entry, !entry.isActive);
      toast.success(entry.isActive ? `Deactivated "${entry.name}".` : `Activated "${entry.name}".`);
    } catch (e) {
      toast.error(errorMessage(e, `Could not update the ${singular}.`));
    } finally {
      setTogglingId(null);
    }
  };

  const confirmDelete = async () => {
    if (!toDelete) return;
    const entry = toDelete;
    setDeleting(true);
    try {
      await onDelete(entry);
      toast.success(`Deleted "${entry.name}".`);
      setToDelete(null);
    } catch (e) {
      setToDelete(null);
      if (e instanceof ApiClientError && e.status === 409 && e.code === 'in_use') {
        const count = e.details?.productCount;
        setInUse({ entry, productCount: typeof count === 'number' ? count : null });
      } else {
        toast.error(errorMessage(e, `Could not delete the ${singular}.`));
      }
    } finally {
      setDeleting(false);
    }
  };

  const deactivateInstead = async () => {
    if (!inUse) return;
    setDeactivating(true);
    try {
      await onSetActive(inUse.entry, false);
      toast.success(`Deactivated "${inUse.entry.name}".`);
      setInUse(null);
    } catch (e) {
      toast.error(errorMessage(e, `Could not deactivate the ${singular}.`));
    } finally {
      setDeactivating(false);
    }
  };

  if (isError) return <ErrorFallback onRetry={onRetry} />;

  const label = `${singular.charAt(0).toUpperCase()}${singular.slice(1)}`;

  return (
    <div className="space-y-4">
      <div className="flex justify-end">
        <Button className="w-full sm:w-auto" onClick={openCreate}>
          <Plus className="mr-2 h-4 w-4" />
          Add {singular}
        </Button>
      </div>

      {isLoading ? (
        <TableSkeleton rows={5} cols={3 + columns.length} />
      ) : entries && entries.length > 0 ? (
        <div className="overflow-x-auto">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Name</TableHead>
                {columns.map((c) => (
                  <TableHead key={c.header}>{c.header}</TableHead>
                ))}
                <TableHead>Products</TableHead>
                <TableHead>Status</TableHead>
                <TableHead></TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {entries.map((entry) => (
                <TableRow key={entry.id} data-testid="reference-row">
                  <TableCell className="font-medium">{entry.name}</TableCell>
                  {columns.map((c) => (
                    <TableCell key={c.header}>{c.cell(entry)}</TableCell>
                  ))}
                  <TableCell>{entry.productCount ?? '—'}</TableCell>
                  <TableCell>
                    <Badge variant={entry.isActive ? 'default' : 'secondary'}>
                      {entry.isActive ? 'Active' : 'Inactive'}
                    </Badge>
                  </TableCell>
                  <TableCell>
                    <div className="flex flex-wrap justify-end gap-1">
                      {rowActions?.(entry)}
                      <Button
                        variant="ghost"
                        size="sm"
                        aria-label={`Edit ${entry.name}`}
                        onClick={() => openEdit(entry)}
                      >
                        <Pencil className="mr-1 h-4 w-4" />
                        Edit
                      </Button>
                      <Button
                        variant="ghost"
                        size="sm"
                        disabled={togglingId === entry.id}
                        onClick={() => toggleActive(entry)}
                      >
                        {entry.isActive ? 'Deactivate' : 'Activate'}
                      </Button>
                      <Button
                        variant="ghost"
                        size="icon-sm"
                        aria-label={`Delete ${entry.name}`}
                        onClick={() => setToDelete(entry)}
                      >
                        <Trash2 className="h-4 w-4" />
                      </Button>
                    </div>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      ) : (
        <EmptyState title={`No ${plural} yet`} description={`Add a ${singular} to get started`} />
      )}

      <Dialog open={dialog !== null} onOpenChange={(open) => !open && !saving && setDialog(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{dialog?.mode === 'edit' ? `Edit ${singular}` : `Add ${singular}`}</DialogTitle>
            <DialogDescription>
              {label} names are unique. Capitals and extra spaces do not make a name different.
            </DialogDescription>
          </DialogHeader>
          <form
            className="space-y-4"
            noValidate
            onSubmit={(e) => {
              e.preventDefault();
              void submit();
            }}
          >
            <div className="space-y-1">
              <Label htmlFor="reference-name">Name</Label>
              <Input
                id="reference-name"
                value={value.name}
                maxLength={80}
                autoFocus
                aria-invalid={!!errors.name}
                onChange={(e) => patch({ name: e.target.value } as Partial<V>)}
              />
              {errors.name && (
                <p role="alert" className="text-sm text-destructive">
                  {errors.name[0]}
                </p>
              )}
            </div>
            {renderFields?.(value, patch, errors, dialog?.mode ?? 'create')}
            <DialogFooter>
              <Button type="button" variant="outline" disabled={saving} onClick={() => setDialog(null)}>
                Cancel
              </Button>
              <Button type="submit" disabled={saving}>
                {saving ? 'Saving...' : 'Save'}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      <ConfirmDialog
        open={toDelete !== null}
        onOpenChange={(open) => !open && !deleting && setToDelete(null)}
        title={`Delete ${singular}?`}
        description={`"${toDelete?.name ?? ''}" will be removed for good.`}
        pending={deleting}
        onConfirm={confirmDelete}
      />

      <ConfirmDialog
        open={inUse !== null}
        onOpenChange={(open) => !open && !deactivating && setInUse(null)}
        title={`This ${singular} is in use`}
        description={
          inUse?.productCount != null
            ? `"${inUse.entry.name}" is used by ${inUse.productCount} ${inUse.productCount === 1 ? 'product' : 'products'}, so it cannot be deleted. Deactivate it instead: products keep it, but it can no longer be chosen for others.`
            : `"${inUse?.entry.name ?? ''}" is used by products, so it cannot be deleted. Deactivate it instead: products keep it, but it can no longer be chosen for others.`
        }
        confirmLabel="Deactivate instead"
        pending={deactivating}
        onConfirm={deactivateInstead}
      />
    </div>
  );
}
