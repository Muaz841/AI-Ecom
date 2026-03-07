export interface LoginRequest {
  clientId: string;
  email: string;
  password: string;
}

export interface AuthResponse {
  success: boolean;
  accessToken: string | null;
  refreshToken: string | null;
  accessTokenExpiresAtUtc: string | null;
  userId: string | null;
  email: string | null;
  role: string | null;
  errorMessage: string | null;
}

export interface UserProfile {
  userId: string;
  email: string;
  tenantId: string;
  roles: string[];
  permissions: string[];
}

export interface AuthSession {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAtUtc: string | null;
  profile: UserProfile;
}
