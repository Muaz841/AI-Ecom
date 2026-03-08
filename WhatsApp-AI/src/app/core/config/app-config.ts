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
  integrations: {
    list: string;
    start: (channel: string) => string;
    disconnect: (connectionId: string) => string;
  };
}

export const APP_CONFIG: AppConfig = {
  apiBaseUrl: 'https://localhost:44372',
  auth: {
    register: '/api/auth/register',
    login: '/api/auth/login',
    refresh: '/api/auth/refresh',
    logout: '/api/auth/logout',
    forgotPassword: '/api/auth/password/forgot',
    resetPassword: '/api/auth/password/reset',
    me: '/api/auth/me',
  },
  integrations: {
    list: '/api/integrations/meta',
    start: (channel: string) => `/api/integrations/meta/${channel}/start`,
    disconnect: (connectionId: string) => `/api/integrations/meta/${connectionId}`,
  },
};
