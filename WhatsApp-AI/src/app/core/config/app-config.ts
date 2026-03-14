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
  tenants: {
    list: string;
    get: (id: string) => string;
    create: string;
    suspend: (id: string) => string;
    activate: (id: string) => string;
  };
  conversations: {
    list: string;
    messages: (threadId: string) => string;
  };
  platformSettings: {
    meta: string;
  };
}

export const APP_CONFIG: AppConfig = {
  //apiBaseUrl: 'https://localhost:44372',
  apiBaseUrl: 'https://impurely-overforged-atlas.ngrok-free.dev',
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
  tenants: {
    list: '/api/host/tenants',
    get: (id: string) => `/api/host/tenants/${id}`,
    create: '/api/host/tenants',
    suspend: (id: string) => `/api/host/tenants/${id}/suspend`,
    activate: (id: string) => `/api/host/tenants/${id}/activate`,
  },
  conversations: {
    list: '/api/conversations',
    messages: (threadId: string) => `/api/conversations/${threadId}/messages`,
  },
  platformSettings: {
    meta: '/api/host/platform/meta',
  },
};
