import Link from 'next/link';
import { Button } from '@/components/ui/button';

export default function NotFound() {
  return (
    <div className="flex min-h-screen flex-col items-center justify-center gap-4 px-4">
      <h2 className="text-4xl font-bold">404</h2>
      <p className="text-muted-foreground text-lg">Page not found</p>
      <p className="text-muted-foreground text-center max-w-md">
        The page you are looking for does not exist or has been moved.
      </p>
      <Button render={<Link href="/" />}>Go Home</Button>
    </div>
  );
}
