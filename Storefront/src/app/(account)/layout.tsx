'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { SiteHeader } from '@/components/layout/site-header';
import { SiteFooter } from '@/components/layout/site-footer';
import { cn } from '@/lib/utils';
import { Package, FileText, Bell, User } from 'lucide-react';

const accountNavItems = [
  { href: '/orders', label: 'Orders', icon: Package },
  { href: '/documents', label: 'Documents', icon: FileText },
  { href: '/notifications', label: 'Notifications', icon: Bell },
  { href: '/profile', label: 'Profile', icon: User },
];

export default function AccountLayout({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();

  return (
    <div className="flex min-h-screen flex-col">
      <SiteHeader />
      <div className="page-container flex-1 py-6 sm:py-8">
        <div className="flex flex-col md:flex-row gap-6 md:gap-8">
          <aside className="md:w-56 shrink-0">
            <nav className="flex gap-1 overflow-x-auto pb-1 md:flex-col md:overflow-visible md:pb-0">
              {accountNavItems.map(({ href, label, icon: Icon }) => (
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
