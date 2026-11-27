import NextAuth from 'next-auth';
import Credentials from 'next-auth/providers/credentials';
import Google from 'next-auth/providers/google';
import { authConfig } from './auth.config';
import { login, googleAuth, refreshToken } from '@/lib/api/auth';

/** Anything that is not exactly Admin is a customer; the services apply the same rule. */
function normalizeRole(role: unknown): 'Admin' | 'Customer' {
  return role === 'Admin' ? 'Admin' : 'Customer';
}

/** Reads the role from an access token issued by our own Auth service (the services still verify it). */
function roleFromAccessToken(accessToken: string): 'Admin' | 'Customer' {
  try {
    const payload = JSON.parse(
      Buffer.from(accessToken.split('.')[1], 'base64url').toString('utf8')
    );
    return normalizeRole(
      payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] ?? payload.role
    );
  } catch {
    return 'Customer';
  }
}

export const { handlers, auth, signIn, signOut } = NextAuth({
  ...authConfig,
  providers: [
    Credentials({
      credentials: {
        email: {},
        password: {},
      },
      async authorize(credentials) {
        try {
          const result = await login(
            credentials.email as string,
            credentials.password as string
          );
          return {
            id: result.user.id,
            email: result.user.email,
            role: normalizeRole(result.user.role),
            accessToken: result.tokens.accessToken,
            refreshToken: result.tokens.refreshToken,
            accessTokenExpires: Date.now() + result.tokens.expiresIn * 1000,
          } as any;
        } catch {
          return null;
        }
      },
    }),
    Google({
      clientId: process.env.GOOGLE_CLIENT_ID,
      clientSecret: process.env.GOOGLE_CLIENT_SECRET,
    }),
  ],
  session: { strategy: 'jwt' },
  callbacks: {
    async jwt({ token, user, account }) {
      if (user) {
        token.id = (user as any).id;
        token.role = normalizeRole((user as any).role);
        token.accessToken = (user as any).accessToken;
        token.refreshToken = (user as any).refreshToken;
        token.accessTokenExpires = (user as any).accessTokenExpires;
      }

      if (account?.provider === 'google' && account.id_token) {
        try {
          const result = await googleAuth(account.id_token);
          token.id = result.user.id;
          token.role = normalizeRole(result.user.role);
          token.accessToken = result.tokens.accessToken;
          token.refreshToken = result.tokens.refreshToken;
          token.accessTokenExpires = Date.now() + result.tokens.expiresIn * 1000;
        } catch {
          return token;
        }
      }

      if (Date.now() < (token.accessTokenExpires as number)) {
        return token;
      }

      try {
        const refreshed = await refreshToken(token.refreshToken as string);
        token.accessToken = refreshed.accessToken;
        token.refreshToken = refreshed.refreshToken;
        token.accessTokenExpires = Date.now() + refreshed.expiresIn * 1000;
        // A role change (for example an administrator being demoted) takes effect on the next refresh.
        token.role = roleFromAccessToken(refreshed.accessToken);
        token.error = undefined;
      } catch {
        token.error = 'RefreshAccessTokenError';
      }

      return token;
    },
    async session({ session, token }) {
      if (session.user) {
        (session as any).user.id = token.id;
        (session as any).user.role = token.role;
        (session as any).user.isAdmin = token.role === 'Admin';
        (session as any).accessToken = token.accessToken;
        (session as any).error = token.error;
      }
      return session;
    },
  },
});
