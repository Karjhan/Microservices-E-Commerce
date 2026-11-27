export type UserRole = 'Admin' | 'Customer';
export type AuthProvider = 'Internal' | 'Google';

export interface User {
  id: string;
  email: string;
  role: UserRole;
  provider: AuthProvider;
  createdAt: string;
}

export interface AuthTokens {
  accessToken: string;
  refreshToken: string;
}

export interface SessionUser {
  id: string;
  email: string;
  role: UserRole;
  accessToken: string;
  refreshToken: string;
  accessTokenExpires: number;
}
