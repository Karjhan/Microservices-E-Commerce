import { auth } from '@/lib/auth';
import { NextResponse } from 'next/server';
import type { NextRequest } from 'next/server';

const publicPaths = [
  '/login',
  '/register',
  '/products',
  '/search',
  '/',
  '/api/auth',
  '/_next',
  '/favicon.ico',
];

const staffPaths = ['/admin'];

export default auth((req: NextRequest & { auth: any }) => {
  const { pathname } = req.nextUrl;

  const isPublic = publicPaths.some(
    (p) => pathname === p || pathname.startsWith(`${p}/`) || pathname.startsWith(`${p}?`)
  );

  if (isPublic) return NextResponse.next();

  const isLoggedIn = !!req.auth;

  if (!isLoggedIn) {
    const loginUrl = new URL('/login', req.nextUrl.origin);
    loginUrl.searchParams.set('callbackUrl', pathname);
    return NextResponse.redirect(loginUrl);
  }

  const isStaffRoute = staffPaths.some((p) => pathname.startsWith(p));
  if (isStaffRoute) {
    const role = req.auth?.user?.role;
    const staffRoles = ['Admin', 'Manager', 'Support'];
    if (!staffRoles.includes(role)) {
      return NextResponse.redirect(new URL('/', req.nextUrl.origin));
    }
  }

  return NextResponse.next();
});

export const config = {
  matcher: ['/((?!_next/static|_next/image|favicon.ico|.*\\..*).*)'],
};
