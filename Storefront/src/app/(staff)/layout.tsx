import { redirect } from 'next/navigation';
import { auth } from '@/lib/auth';
import { STAFF_ROLE } from '@/lib/auth.config';
import { StaffShell } from '@/components/admin/staff-shell';

export default async function StaffLayout({ children }: { children: React.ReactNode }) {
  const session = await auth();

  if (!session?.user) {
    redirect('/login?callbackUrl=/admin/products');
  }

  if ((session.user as { role?: string }).role !== STAFF_ROLE) {
    redirect('/');
  }

  return <StaffShell>{children}</StaffShell>;
}
