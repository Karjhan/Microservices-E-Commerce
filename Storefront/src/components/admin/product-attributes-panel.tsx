'use client';

import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { addProductAttribute, deleteProductAttribute } from '@/lib/api/products';
import { errorMessage } from '@/lib/api/client';
import { queryKeys } from '@/lib/constants';
import type { ProductAttribute } from '@/types/product';

interface ProductAttributesPanelProps {
  productId: string;
  attributes: ProductAttribute[];
}

/** Free-form key/value attributes (colour, finish, ...). Each change is saved straight away. */
export function ProductAttributesPanel({ productId, attributes }: ProductAttributesPanelProps) {
  const queryClient = useQueryClient();
  const [key, setKey] = useState('');
  const [value, setValue] = useState('');
  const refresh = () => queryClient.invalidateQueries({ queryKey: queryKeys.products.all });

  const add = useMutation({
    mutationFn: () => addProductAttribute(productId, key.trim(), value.trim()),
    onSuccess: () => {
      setKey('');
      setValue('');
      return refresh();
    },
    onError: (e) => toast.error(errorMessage(e, 'Could not add the attribute.')),
  });

  const remove = useMutation({
    mutationFn: (attributeId: string) => deleteProductAttribute(productId, attributeId),
    onSuccess: refresh,
    onError: (e) => toast.error(errorMessage(e, 'Could not remove the attribute.')),
  });

  const canAdd = key.trim().length > 0 && value.trim().length > 0;

  return (
    <Card>
      <CardHeader>
        <CardTitle>Attributes</CardTitle>
      </CardHeader>
      <CardContent className="space-y-4">
        {attributes.length > 0 && (
          <ul className="space-y-2">
            {attributes.map((attribute) => (
              <li key={attribute.attributeId} className="flex items-center justify-between gap-2 text-sm">
                <span>
                  <span className="font-medium capitalize">{attribute.key}</span>: {attribute.value}
                </span>
                <Button
                  type="button"
                  size="icon-sm"
                  variant="ghost"
                  aria-label={`Remove attribute ${attribute.key}`}
                  disabled={remove.isPending}
                  onClick={() => remove.mutate(attribute.attributeId)}
                >
                  <Trash2 className="h-4 w-4" />
                </Button>
              </li>
            ))}
          </ul>
        )}
        <div className="grid gap-2 sm:grid-cols-[1fr_1fr_auto] sm:items-end">
          <div className="space-y-1">
            <Label htmlFor="attribute-key">Name</Label>
            <Input id="attribute-key" value={key} onChange={(e) => setKey(e.target.value)} />
          </div>
          <div className="space-y-1">
            <Label htmlFor="attribute-value">Value</Label>
            <Input id="attribute-value" value={value} onChange={(e) => setValue(e.target.value)} />
          </div>
          <Button type="button" variant="outline" disabled={!canAdd || add.isPending} onClick={() => add.mutate()}>
            Add
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}
