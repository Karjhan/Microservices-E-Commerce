'use client';

import { useSession } from 'next-auth/react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { Badge } from '@/components/ui/badge';

export default function ProfilePage() {
  const { data: session, status } = useSession();

  if (status === 'loading') {
    return (
      <div className="space-y-4">
        <Skeleton className="h-8 w-48" />
        <Skeleton className="h-32 w-full" />
      </div>
    );
  }

  const user = session?.user;
  const role = (user as any)?.role;

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold">Profile</h1>
      <Card>
        <CardHeader>
          <CardTitle>Account Information</CardTitle>
        </CardHeader>
        <CardContent>
          <dl className="space-y-4">
            <div>
              <dt className="text-sm text-muted-foreground">Email</dt>
              <dd className="font-medium">{user?.email}</dd>
            </div>
            {role && (
              <div>
                <dt className="text-sm text-muted-foreground">Role</dt>
                <dd><Badge variant="secondary">{role}</Badge></dd>
              </div>
            )}
          </dl>
        </CardContent>
      </Card>
    </div>
  );
}
