export interface AuthEndpointConfig {
  register: string;
  login: string;
  refresh: string;
  logout: string;
  forgotPassword: string;
  verifyOtp: string;
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
    getThread: (id: string) => string;
    messages: (threadId: string) => string;
  };
  platformSettings: {
    meta: string;
  };
  rbac: {
    permissions: string;
    roles: string;
    roleById: (roleId: string) => string;
    rolePermissions: (roleId: string) => string;
    users: string;
    userRole: (userId: string, roleId: string) => string;
  };
  aiSettings: {
    get: string;
    save: string;
    models: (provider: string) => string;
  };
  aiProfile: {
    get: string;
    save: string;
  };
  products: {
    list: string;
    getById: (id: string) => string;
    upload: string;
    update: (id: string) => string;
    delete: (id: string) => string;
    addImage: (id: string) => string;
    removeImage: (id: string, imageId: string) => string;
    setPrimaryImage: (id: string, imageId: string) => string;
  };
  imagePipeline: {
    generate: string;
    generateFromPose: string;
    poses: string;
    poseById: (id: string) => string;
  };
  metaAds: {
    settings: {
      get: string;
      save: string;
    };
    knowledge: {
      list: string;
      add: string;
      update: (id: string) => string;
      delete: (id: string) => string;
    };
    decisions: {
      list: string;
      approve: (id: string) => string;
      reject: (id: string) => string;
    };
  };
  dev: {
    webhookTest: string;
  };
}

export const APP_CONFIG: AppConfig = {
  apiBaseUrl: 'https://localhost:44372',
  //apiBaseUrl: 'https://impurely-overforged-atlas.ngrok-free.dev',
  auth: {
    register: '/api/auth/register',
    login: '/api/auth/login',
    refresh: '/api/auth/refresh',
    logout: '/api/auth/logout',
    forgotPassword: '/api/auth/password/forgot',
    verifyOtp: '/api/auth/password/verify-otp',
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
    getThread: (id: string) => `/api/conversations/${id}`,
    messages: (threadId: string) => `/api/conversations/${threadId}/messages`,
  },
  platformSettings: {
    meta: '/api/host/platform/meta',
  },
  rbac: {
    permissions: '/api/rbac/permissions',
    roles: '/api/rbac/roles',
    roleById: (roleId: string) => `/api/rbac/roles/${roleId}`,
    rolePermissions: (roleId: string) => `/api/rbac/roles/${roleId}/permissions`,
    users: '/api/rbac/users',
    userRole: (userId: string, roleId: string) => `/api/rbac/users/${userId}/roles/${roleId}`,
  },
  aiSettings: {
    get: '/api/host/ai-settings',
    save: '/api/host/ai-settings',
    models: (provider: string) => `/api/host/ai-settings/models?provider=${encodeURIComponent(provider)}`,
  },
  aiProfile: {
    get: '/api/v1/tenant/ai-profile',
    save: '/api/v1/tenant/ai-profile',
  },
  products: {
    list:           '/api/products',
    getById:        (id: string) => `/api/products/${id}`,
    upload:         '/api/products/upload',
    update:         (id: string) => `/api/products/${id}`,
    delete:         (id: string) => `/api/products/${id}`,
    addImage:       (id: string) => `/api/products/${id}/images`,
    removeImage:    (id: string, imageId: string) => `/api/products/${id}/images/${imageId}`,
    setPrimaryImage:(id: string, imageId: string) => `/api/products/${id}/images/${imageId}/primary`,
  },
  imagePipeline: {
    generate:         '/api/image-pipeline/generate',
    generateFromPose: '/api/image-pipeline/generate-from-pose',
    poses:            '/api/image-pipeline/poses',
    poseById:         (id: string) => `/api/image-pipeline/poses/${id}`,
  },
  metaAds: {
    settings: {
      get:  '/api/host/marketing',
      save: '/api/host/marketing',
    },
    knowledge: {
      list:   '/api/meta-ads/knowledge',
      add:    '/api/meta-ads/knowledge',
      update: (id: string) => `/api/meta-ads/knowledge/${id}`,
      delete: (id: string) => `/api/meta-ads/knowledge/${id}`,
    },
    decisions: {
      list:    '/api/meta-ads/decisions',
      approve: (id: string) => `/api/meta-ads/decisions/${id}/approve`,
      reject:  (id: string) => `/api/meta-ads/decisions/${id}/reject`,
    },
  },
  dev: {
    webhookTest: '/api/dev/webhooks/test',
  },
};
