export type UserRole = 'Admin' | 'Customer' | 'Manager' | 'Support';
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
  expiresIn: number;
}

export interface AuthResponse {
  user: User;
  tokens: AuthTokens;
}

export interface SessionUser {
  id: string;
  email: string;
  role: UserRole;
  accessToken: string;
  refreshToken: string;
  accessTokenExpires: number;
}
