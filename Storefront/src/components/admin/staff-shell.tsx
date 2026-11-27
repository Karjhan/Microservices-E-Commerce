'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import {
  Package,
  BarChart3,
  ShoppingCart,
  CreditCard,
  ScrollText,
  Tag,
  Spool,
  FolderTree,
} from 'lucide-react';
import { SiteHeader } from '@/components/layout/site-header';
import { SiteFooter } from '@/components/layout/site-footer';
import { cn } from '@/lib/utils';

export const staffNavItems = [
  { href: '/admin/products', label: 'Products', icon: Package },
  { href: '/admin/brands', label: 'Brands', icon: Tag },
  { href: '/admin/filament-types', label: 'Filament types', icon: Spool },
  { href: '/admin/categories', label: 'Categories', icon: FolderTree },
  { href: '/admin/inventory', label: 'Inventory', icon: BarChart3 },
  { href: '/admin/orders', label: 'Orders', icon: ShoppingCart },
  { href: '/admin/payments', label: 'Payments', icon: CreditCard },
  { href: '/admin/audit', label: 'Audit', icon: ScrollText },
];

export function StaffShell({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();

  return (
    <div className="flex min-h-screen flex-col">
      <SiteHeader />
      <div className="page-container flex-1 py-6 sm:py-8">
        <div className="flex flex-col md:flex-row gap-6 md:gap-8">
          <aside className="md:w-56 shrink-0">
            <h2 className="text-sm font-semibold text-muted-foreground mb-4 uppercase tracking-wider hidden md:block">Admin</h2>
            <nav
              aria-label="Admin"
              data-testid="staff-nav"
              className="flex gap-1 overflow-x-auto pb-1 md:flex-col md:overflow-visible md:pb-0"
            >
              {staffNavItems.map(({ href, label, icon: Icon }) => (
                <Link
                  key={href}
                  href={href}
                  className={cn(
                    'flex shrink-0 items-center gap-2 rounded-md px-3 py-2 text-sm font-medium transition-colors',
                    pathname.startsWith(href)
                      ? 'bg-accent text-accent-foreground'
                      : 'text-muted-foreground hover:bg-accent hover:text-accent-foreground'
                  )}
                >
                  <Icon className="h-4 w-4" />
                  {label}
                </Link>
              ))}
            </nav>
          </aside>
          <main className="flex-1 min-w-0">{children}</main>
        </div>
      </div>
      <SiteFooter />
    </div>
  );
}
