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

const dashboardModule = SIDEBAR_PIPELINE.find((module) => module.id === 'dashboard');
const messagingModule = SIDEBAR_PIPELINE.find((module) => module.id === 'messaging');
const contentModule = SIDEBAR_PIPELINE.find((module) => module.id === 'content');
const productsModule = SIDEBAR_PIPELINE.find((module) => module.id === 'products');
const schedulingModule = SIDEBAR_PIPELINE.find((module) => module.id === 'scheduling');
const settingsModule = SIDEBAR_PIPELINE.find((module) => module.id === 'settings');
const rbacModule = SIDEBAR_PIPELINE.find((module) => module.id === 'rbac');

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
        component: DashboardPlaceholderComponent,
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
          roles: rbacModule?.requiredRoles ?? ['super_admin'],
          roleMatchMode: rbacModule?.roleMatchMode ?? 'any',
          title: rbacModule?.label ?? 'RBAC',
          subtitle: rbacModule?.subtitle ?? 'Tenant role, permission, and assignment management.',
        },
        component: DashboardPlaceholderComponent,
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
