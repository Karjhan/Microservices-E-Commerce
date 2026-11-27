import * as React from 'react';
import { cn } from '@/lib/utils';

/**
 * A plain browser select, styled like `Input`. Used where the options are looked up by id and the selected id has to
 * be what the form holds, which the styled `Select` does not do without extra mapping.
 */
export function NativeSelect({ className, children, ...props }: React.ComponentProps<'select'>) {
  return (
    <select
      className={cn(
        'h-8 w-full min-w-0 rounded-lg border border-input bg-transparent px-2 py-1 text-base transition-colors outline-none focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50 disabled:cursor-not-allowed disabled:opacity-50 aria-invalid:border-destructive aria-invalid:ring-3 aria-invalid:ring-destructive/20 md:text-sm dark:bg-input/30 dark:[&>option]:bg-background',
        className
      )}
      {...props}
    >
      {children}
    </select>
  );
}
