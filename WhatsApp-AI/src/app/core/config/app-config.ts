export interface AuthEndpointConfig {
  register: string;
  login: string;
  refresh: string;
  logout: string;
  forgotPassword: string;
  resetPassword: string;
  me: string;
}

export interface AppConfig {
  apiBaseUrl: string;
  auth: AuthEndpointConfig;
}

export const APP_CONFIG: AppConfig = {
  apiBaseUrl: 'http://localhost:5131',
  auth: {
    register: '/api/auth/register',
    login: '/api/auth/login',
    refresh: '/api/auth/refresh',
    logout: '/api/auth/logout',
    forgotPassword: '/api/auth/password/forgot',
    resetPassword: '/api/auth/password/reset',
    me: '/api/auth/me',
  },
};
