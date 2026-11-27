import NextAuth from 'next-auth';
import Credentials from 'next-auth/providers/credentials';
import Google from 'next-auth/providers/google';
import { authConfig } from './auth.config';
import { login, googleAuth, refreshToken } from '@/lib/api/auth';

/** Anything that is not exactly Admin is a customer; the services apply the same rule. */
function normalizeRole(role: unknown): 'Admin' | 'Customer' {
  return role === 'Admin' ? 'Admin' : 'Customer';
}

function claimsFromAccessToken(accessToken: string) {
  const payload = JSON.parse(
    Buffer.from(accessToken.split('.')[1], 'base64url').toString('utf8')
  );
  return {
    id: payload.sub as string,
    email: payload.email as string,
    role: normalizeRole(
      payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] ?? payload.role
    ),
    accessTokenExpires: (payload.exp as number) * 1000,
  };
}

function userFromTokens(tokens: { accessToken: string; refreshToken: string }) {
  return {
    ...claimsFromAccessToken(tokens.accessToken),
    accessToken: tokens.accessToken,
    refreshToken: tokens.refreshToken,
  };
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
          const tokens = await login(
            credentials.email as string,
            credentials.password as string
          );
          return userFromTokens(tokens) as any;
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
          const user = userFromTokens(await googleAuth(account.id_token));
          token.id = user.id;
          token.role = user.role;
          token.accessToken = user.accessToken;
          token.refreshToken = user.refreshToken;
          token.accessTokenExpires = user.accessTokenExpires;
        } catch {
          return token;
        }
      }

      if (Date.now() < (token.accessTokenExpires as number)) {
        return token;
      }

      try {
        const refreshed = userFromTokens(await refreshToken(token.refreshToken as string));
        token.accessToken = refreshed.accessToken;
        token.refreshToken = refreshed.refreshToken;
        token.accessTokenExpires = refreshed.accessTokenExpires;
        token.role = refreshed.role;
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
