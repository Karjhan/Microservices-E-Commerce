import Link from 'next/link';
import { Package } from 'lucide-react';

export function SiteFooter() {
  return (
    <footer className="border-t bg-background">
      <div className="page-container flex flex-col items-center gap-4 py-8 md:flex-row md:justify-between">
        <div className="flex items-center gap-2">
          <Package className="h-4 w-4" />
          <span className="text-sm font-medium">3D Print Store</span>
        </div>
        <nav className="flex gap-4 text-sm text-muted-foreground">
          <Link href="/products" className="hover:text-foreground transition-colors">
            Products
          </Link>
          <Link href="/search" className="hover:text-foreground transition-colors">
            Search
          </Link>
        </nav>
        <p className="text-xs text-muted-foreground">
          &copy; {new Date().getFullYear()} 3D Print Store. All rights reserved.
        </p>
      </div>
    </footer>
  );
}
