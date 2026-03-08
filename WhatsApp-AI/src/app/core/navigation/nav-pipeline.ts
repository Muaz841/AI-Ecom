export interface SidebarModule {
  id: string;
  label: string;
  route: string;
  icon: string;
  order: number;
  requiredPermissions?: string[];
  requiredRoles?: string[];
}


export const SIDEBAR_PIPELINE: SidebarModule[] = [
  { id: 'dashboard', label: 'Dashboard', route: '/dashboard', icon: 'pi pi-home', order: 10 },  
  { id: 'messaging', label: 'Messaging', route: '/messaging', icon: 'pi pi-comments', order: 20, requiredPermissions: ['conversations.read'] },
  { id: 'content', label: 'Content AI', route: '/content', icon: 'pi pi-bolt', order: 30, requiredPermissions: ['ai.manage'] },
  { id: 'products', label: 'Products', route: '/products', icon: 'pi pi-box', order: 40, requiredPermissions: ['products.manage'] },
  { id: 'scheduling', label: 'Scheduling', route: '/scheduling', icon: 'pi pi-calendar', order: 50, requiredPermissions: ['products.manage'] },
  { id: 'settings', label: 'Settings', route: '/settings', icon: 'pi pi-cog', order: 60, requiredPermissions: ['integrations.read'] },
  { id: 'rbac', label: 'RBAC', route: '/admin/rbac', icon: 'pi pi-shield', order: 70, requiredPermissions: ['roles.manage'], requiredRoles: ['super_admin'] },
];
