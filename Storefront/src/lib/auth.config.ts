import type { NextAuthConfig } from 'next-auth';

// Only administrators may use the staff area (research R2). The services enforce the same rule.
export const STAFF_ROLE = 'Admin';

export const authConfig: NextAuthConfig = {
  pages: {
    signIn: '/login',
  },
  providers: [],
  callbacks: {
    authorized({ auth, request: { nextUrl } }) {
      const isLoggedIn = !!auth?.user;
      const pathname = nextUrl.pathname;

      const publicPaths = ['/login', '/register', '/products', '/search', '/'];
      const isPublic = publicPaths.some(
        (p) => pathname === p || pathname.startsWith(`${p}/`)
      );

      const staffPaths = ['/admin'];
      const isStaffRoute = staffPaths.some((p) => pathname.startsWith(p));

      if (isStaffRoute) {
        if (!isLoggedIn) return false;
        const role = (auth as any)?.user?.role;
        return role === STAFF_ROLE;
      }

      if (!isPublic && !isLoggedIn) return false;

      return true;
    },
  },
};
