export interface SidebarModule {
  id: string;
  label: string;
  route: string;
  icon: string;
  order: number;
  subtitle?: string;
  requiredPermissions?: string[];
  requiredRoles?: string[];
  roleMatchMode?: 'any' | 'all';
}

export function resolveVisibleSidebarModules(
  modules: SidebarModule[],
  roles: string[],
  permissions: string[],
): SidebarModule[] {
  return modules
    .filter((module) => {
      const permissionPass =
        !module.requiredPermissions ||
        module.requiredPermissions.every((permission) => permissions.includes(permission));
      const rolePass =
        !module.requiredRoles ||
        (module.roleMatchMode === 'all'
          ? module.requiredRoles.every((role) => roles.includes(role))
          : module.requiredRoles.some((role) => roles.includes(role)));
      return permissionPass && rolePass;
    })
    .sort((a, b) => a.order - b.order);
}

export const SIDEBAR_PIPELINE: SidebarModule[] = [
  {
    id: 'dashboard',
    label: 'Dashboard',
    route: '/dashboard',
    icon: 'pi pi-home',
    order: 10,
    subtitle: 'Live operational pulse of your tenant.',
  },
  {
    id: 'messaging',
    label: 'Messaging',
    route: '/messaging',
    icon: 'pi pi-comments',
    order: 20,
    requiredPermissions: ['conversations.read'],
    subtitle: 'Unified inbox with AI-assisted handling flow.',
  },
  {
    id: 'content',
    label: 'Content AI',
    route: '/content',
    icon: 'pi pi-bolt',
    order: 30,
    requiredPermissions: ['ai.manage'],
    subtitle: 'Generate conversion-first captions and campaign copy.',
  },
  {
    id: 'products',
    label: 'Products',
    route: '/products',
    icon: 'pi pi-box',
    order: 40,
    requiredPermissions: ['products.manage'],
    subtitle: 'Catalog intelligence, stock visibility, and variant control.',
  },
  {
    id: 'scheduling',
    label: 'Scheduling',
    route: '/scheduling',
    icon: 'pi pi-calendar',
    order: 50,
    requiredPermissions: ['products.manage'],
    subtitle: 'Plan and publish channel content with confidence.',
  },
  {
    id: 'settings',
    label: 'Integrations',
    route: '/settings',
    icon: 'pi pi-cog',
    order: 60,
    requiredPermissions: ['integrations.read'],
    subtitle: 'Connect Instagram, Facebook, and WhatsApp channels.',
  },
  {
    id: 'rbac',
    label: 'Access Control',
    route: '/admin/rbac',
    icon: 'pi pi-shield',
    order: 70,
    requiredPermissions: ['roles.manage'],
    subtitle: 'Manage roles, permissions, and user access.',
  },
  {
    id: 'tenants',
    label: 'Tenant Management',
    route: '/host/tenants',
    icon: 'pi pi-building',
    order: 80,
    requiredPermissions: ['tenants.manage'],
    subtitle: 'Create and manage platform tenants.',
  },
  {
    id: 'platform-settings',
    label: 'Platform Settings',
    route: '/host/platform',
    icon: 'pi pi-sliders-h',
    order: 90,
    requiredPermissions: ['platform.settings'],
    subtitle: 'Configure platform-level Meta OAuth credentials and global settings.',
  },
  {
    id: 'ai-settings',
    label: 'AI Provider',
    route: '/host/ai-settings',
    icon: 'pi pi-microchip-ai',
    order: 92,
    requiredPermissions: ['platform.settings'],
    subtitle: 'Configure AI provider, model selection, and API keys for the platform.',
  },
  {
    id: 'ai-profile',
    label: 'AI Persona',
    route: '/settings/ai-profile',
    icon: 'pi pi-user-edit',
    order: 63,
    requiredPermissions: ['ai.manage'],
    subtitle: 'Define the AI assistant persona, tone, brand rules, and safety guardrails for your store.',
  },
  {
    id: 'webhook-tester',
    label: 'Webhook Tester',
    route: '/dev/webhooks',
    icon: 'pi pi-send',
    order: 95,
    requiredPermissions: [],
    subtitle: 'Simulate incoming Meta webhook events for development testing.',
  },
];
