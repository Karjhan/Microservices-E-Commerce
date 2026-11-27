'use client';

import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { ReferenceList, type FieldErrors } from '@/components/admin/reference-list';
import { useFilamentTypes } from '@/hooks/queries/use-filament-types';
import {
  useCreateReference,
  useDeleteReference,
  useSetReferenceActive,
  useUpdateReference,
} from '@/hooks/queries/use-reference-data';
import type { FilamentType } from '@/types/reference-data';

interface FilamentTypeFormValue {
  name: string;
  code: string;
  diameterMm: string;
  colorOptions: string;
}

const MAX_DIAMETER_MM = 10;
const MAX_COLORS = 50;
const MAX_COLOR_LENGTH = 40;

function parseColors(text: string): string[] {
  const seen = new Set<string>();
  return text
    .split(',')
    .map((c) => c.trim())
    .filter((c) => {
      const key = c.toLowerCase();
      if (!c || seen.has(key)) return false;
      seen.add(key);
      return true;
    });
}

function parseDiameter(text: string): number | null {
  const trimmed = text.trim().replace(',', '.');
  return trimmed === '' ? null : Number(trimmed);
}

function validate(value: FilamentTypeFormValue): FieldErrors | null {
  const errors: FieldErrors = {};

  const diameter = parseDiameter(value.diameterMm);
  if (diameter !== null) {
    if (!Number.isFinite(diameter) || diameter <= 0 || diameter > MAX_DIAMETER_MM) {
      errors.diameterMm = [`Diameter must be greater than 0 and at most ${MAX_DIAMETER_MM} mm.`];
    } else if (Math.abs(diameter * 100 - Math.round(diameter * 100)) > 1e-6) {
      errors.diameterMm = ['Diameter can have at most two decimal places.'];
    }
  }

  const colors = parseColors(value.colorOptions);
  if (colors.length > MAX_COLORS) errors.colorOptions = [`At most ${MAX_COLORS} colors are allowed.`];
  else if (colors.some((c) => c.length > MAX_COLOR_LENGTH))
    errors.colorOptions = [`A color can be at most ${MAX_COLOR_LENGTH} characters.`];

  return Object.keys(errors).length > 0 ? errors : null;
}

function toBody(value: FilamentTypeFormValue) {
  return {
    name: value.name.trim(),
    diameterMm: parseDiameter(value.diameterMm),
    colorOptions: parseColors(value.colorOptions),
  };
}

export default function AdminFilamentTypesPage() {
  const { data, isLoading, isError, refetch } = useFilamentTypes(true);
  const create = useCreateReference('filament-types');
  const update = useUpdateReference('filament-types');
  const setActive = useSetReferenceActive('filament-types');
  const remove = useDeleteReference('filament-types');

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold">Filament types</h1>
        <p className="text-sm text-muted-foreground mt-1">
          The materials products can be printed with, such as PLA or PETG.
        </p>
      </div>

      <ReferenceList<FilamentType, FilamentTypeFormValue>
        singular="filament type"
        plural="filament types"
        entries={data}
        isLoading={isLoading}
        isError={isError}
        onRetry={() => refetch()}
        columns={[
          { header: 'Code', cell: (f) => <span className="font-mono text-xs">{f.code}</span> },
          { header: 'Diameter', cell: (f) => (f.diameterMm == null ? '—' : `${f.diameterMm} mm`) },
          {
            header: 'Colors',
            cell: (f) =>
              f.colorOptions.length === 0 ? (
                '—'
              ) : (
                <div className="flex max-w-xs flex-wrap gap-1">
                  {f.colorOptions.map((c) => (
                    <Badge key={c} variant="outline" className="text-xs">
                      {c}
                    </Badge>
                  ))}
                </div>
              ),
          },
        ]}
        emptyValue={{ name: '', code: '', diameterMm: '', colorOptions: '' }}
        toValue={(f) => ({
          name: f.name,
          code: f.code,
          diameterMm: f.diameterMm == null ? '' : String(f.diameterMm),
          colorOptions: f.colorOptions.join(', '),
        })}
        validate={validate}
        renderFields={(value, patch, errors, mode) => (
          <>
            <div className="space-y-1">
              <Label htmlFor="filament-code">Code</Label>
              <Input
                id="filament-code"
                value={value.code}
                maxLength={32}
                readOnly={mode === 'edit'}
                disabled={mode === 'edit'}
                placeholder={mode === 'create' ? 'Optional, made from the name when left empty' : undefined}
                aria-invalid={!!errors.code}
                onChange={(e) => patch({ code: e.target.value })}
              />
              <p className="text-xs text-muted-foreground">
                {mode === 'edit'
                  ? 'The code identifies the filament type and cannot be changed.'
                  : 'Letters, digits, dashes or underscores. It cannot be changed later.'}
              </p>
              {errors.code && (
                <p role="alert" className="text-sm text-destructive">
                  {errors.code[0]}
                </p>
              )}
            </div>
            <div className="space-y-1">
              <Label htmlFor="filament-diameter">Diameter (mm)</Label>
              <Input
                id="filament-diameter"
                inputMode="decimal"
                value={value.diameterMm}
                placeholder="1.75"
                aria-invalid={!!errors.diameterMm}
                onChange={(e) => patch({ diameterMm: e.target.value })}
              />
              {errors.diameterMm && (
                <p role="alert" className="text-sm text-destructive">
                  {errors.diameterMm[0]}
                </p>
              )}
            </div>
            <div className="space-y-1">
              <Label htmlFor="filament-colors">Color options</Label>
              <Input
                id="filament-colors"
                value={value.colorOptions}
                placeholder="Black, White, Red"
                aria-invalid={!!errors.colorOptions}
                onChange={(e) => patch({ colorOptions: e.target.value })}
              />
              <p className="text-xs text-muted-foreground">Separate colors with commas.</p>
              {errors.colorOptions && (
                <p role="alert" className="text-sm text-destructive">
                  {errors.colorOptions[0]}
                </p>
              )}
            </div>
          </>
        )}
        onCreate={(value) => {
          const code = value.code.trim();
          return create.mutateAsync({ ...toBody(value), ...(code ? { code } : {}) });
        }}
        onUpdate={(entry, value) => update.mutateAsync({ id: entry.id, body: toBody(value) })}
        onSetActive={(entry, active) => setActive.mutateAsync({ id: entry.id, active })}
        onDelete={(entry) => remove.mutateAsync(entry.id)}
      />
    </div>
  );
}
