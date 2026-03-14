import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';
import { permissionGuard } from './core/auth/permission.guard';
import { LoginComponent } from './features/auth/login.component';
import { DashboardPlaceholderComponent } from './features/system/dashboard-placeholder.component';
import { UnauthorizedComponent } from './features/system/unauthorized.component';
import { AppShellComponent } from './features/layout/app-shell.component';
import { MetaOauthCallbackComponent } from './features/metaConnectionComponent/meta-oauth-callback.component';
import { SIDEBAR_PIPELINE } from './core/navigation/nav-pipeline';
import { MetaIntegrationsComponent } from './features/metaConnectionComponent/meta-integrations.component';
import { TenantManagementComponent } from './features/tenants/tenant-management.component';
import { TenantUsersComponent } from './features/tenants/tenant-users/tenant-users.component';
import { InboxComponent } from './features/messaging/inbox.component';
import { PlatformSettingsComponent } from './features/platform-settings/platform-settings.component';
import { RbacComponent } from './features/rbac/rbac.component';

const messagingModule = SIDEBAR_PIPELINE.find((module) => module.id === 'messaging');
const contentModule = SIDEBAR_PIPELINE.find((module) => module.id === 'content');
const productsModule = SIDEBAR_PIPELINE.find((module) => module.id === 'products');
const schedulingModule = SIDEBAR_PIPELINE.find((module) => module.id === 'scheduling');
const settingsModule = SIDEBAR_PIPELINE.find((module) => module.id === 'settings');
const rbacModule = SIDEBAR_PIPELINE.find((module) => module.id === 'rbac');
const tenantsModule = SIDEBAR_PIPELINE.find((module) => module.id === 'tenants');
const platformSettingsModule = SIDEBAR_PIPELINE.find((module) => module.id === 'platform-settings');

export const routes: Routes = [
  {
    path: 'auth/login',
    component: LoginComponent,
  },
  {
    path: 'integrations/meta/callback',
    component: MetaOauthCallbackComponent,
  },
  {
    path: '',
    canActivate: [authGuard],
    component: AppShellComponent,
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      {
        path: 'dashboard',
        canActivate: [permissionGuard],
        data: {
          permissions: settingsModule?.requiredPermissions ?? ['integrations.read'],
          title: 'Channel Integrations',
          subtitle: 'Connect your social messaging channels to automate customer conversations.',
        },
        component: MetaIntegrationsComponent,
      },
      {
        path: 'messaging',
        canActivate: [permissionGuard],
        data: { permissions: messagingModule?.requiredPermissions ?? [], title: messagingModule?.label ?? 'Messaging', subtitle: messagingModule?.subtitle ?? '' },
        component: InboxComponent,
      },
      {
        path: 'content',
        canActivate: [permissionGuard],
        data: { permissions: contentModule?.requiredPermissions ?? [], title: contentModule?.label ?? 'Content AI', subtitle: contentModule?.subtitle ?? '' },
        component: DashboardPlaceholderComponent,
      },
      {
        path: 'products',
        canActivate: [permissionGuard],
        data: { permissions: productsModule?.requiredPermissions ?? [], title: productsModule?.label ?? 'Products', subtitle: productsModule?.subtitle ?? '' },
        component: DashboardPlaceholderComponent,
      },
      {
        path: 'scheduling',
        canActivate: [permissionGuard],
        data: { permissions: schedulingModule?.requiredPermissions ?? [], title: schedulingModule?.label ?? 'Scheduling', subtitle: schedulingModule?.subtitle ?? '' },
        component: DashboardPlaceholderComponent,
      },
      {
        path: 'settings',
        canActivate: [permissionGuard],
        data: { permissions: settingsModule?.requiredPermissions ?? [], title: settingsModule?.label ?? 'Integrations', subtitle: settingsModule?.subtitle ?? '' },
        component: MetaIntegrationsComponent,
      },
      {
        path: 'admin/rbac',
        canActivate: [permissionGuard],
        data: {
          permissions: rbacModule?.requiredPermissions ?? ['roles.manage'],
          title: rbacModule?.label ?? 'Access Control',
          subtitle: rbacModule?.subtitle ?? 'Manage roles, permissions, and user access.',
        },
        component: RbacComponent,
      },
      {
        path: 'host/tenants',
        canActivate: [permissionGuard],
        data: {
          permissions: tenantsModule?.requiredPermissions ?? ['tenants.manage'],
          title: tenantsModule?.label ?? 'Tenant Management',
          subtitle: tenantsModule?.subtitle ?? '',
        },
        component: TenantManagementComponent,
      },
      {
        path: 'host/tenants/:id/users',
        canActivate: [permissionGuard],
        data: {
          permissions: tenantsModule?.requiredPermissions ?? ['tenants.manage'],
          title: 'Tenant Users',
          subtitle: 'View and manage users in this workspace.',
        },
        component: TenantUsersComponent,
      },
      {
        path: 'host/platform',
        canActivate: [permissionGuard],
        data: {
          permissions: platformSettingsModule?.requiredPermissions ?? ['platform.settings'],
          title: platformSettingsModule?.label ?? 'Platform Settings',
          subtitle: platformSettingsModule?.subtitle ?? '',
        },
        component: PlatformSettingsComponent,
      },
    ],
  },
  {
    path: 'unauthorized',
    component: UnauthorizedComponent,
  },
  {
    path: '**',
    redirectTo: '/dashboard',
  },
];
