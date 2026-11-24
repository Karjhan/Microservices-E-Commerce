import { SiteHeader } from '@/components/layout/site-header';
import { SiteFooter } from '@/components/layout/site-footer';
import { ChatWidget } from '@/components/chat/chat-widget';

export default function ShopLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex min-h-screen flex-col">
      <SiteHeader />
      <main className="page-container flex-1 py-6 sm:py-8">{children}</main>
      <SiteFooter />
      <ChatWidget />
    </div>
  );
}
