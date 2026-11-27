'use client';

import { useSession } from 'next-auth/react';
import type { UserRole } from '@/types/user';

export function useAuth() {
  const { data: session, status } = useSession();

  const user = session?.user;
  const role = (user as any)?.role as UserRole | undefined;

  return {
    session,
    user,
    role,
    isAuthenticated: status === 'authenticated',
    isLoading: status === 'loading',
    isAdmin: role === 'Admin',
    accessToken: (session as any)?.accessToken as string | undefined,
  };
}
