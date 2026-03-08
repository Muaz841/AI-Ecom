import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';
import { permissionGuard } from './core/auth/permission.guard';
import { LoginComponent } from './features/auth/login.component';
import { DashboardPlaceholderComponent } from './features/system/dashboard-placeholder.component';
import { UnauthorizedComponent } from './features/system/unauthorized.component';
import { AppShellComponent } from './features/layout/app-shell.component';

export const routes: Routes = [
  {
    path: 'auth/login',
    component: LoginComponent,
  },
  {
    path: '',
    canActivate: [authGuard],
    component: AppShellComponent,
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      { path: 'dashboard', component: DashboardPlaceholderComponent, data: { title: 'Dashboard', subtitle: 'Live operational pulse of your tenant.' } },
      { path: 'messaging', component: DashboardPlaceholderComponent, data: { title: 'Messaging', subtitle: 'Unified inbox with AI-assisted handling flow.' } },
      { path: 'content', component: DashboardPlaceholderComponent, data: { title: 'Content AI', subtitle: 'Generate conversion-first captions and campaign copy.' } },
      { path: 'products', component: DashboardPlaceholderComponent, data: { title: 'Products', subtitle: 'Catalog intelligence, stock visibility, and variant control.' } },
      { path: 'scheduling', component: DashboardPlaceholderComponent, data: { title: 'Scheduling', subtitle: 'Plan and publish channel content with confidence.' } },
      { path: 'settings', component: DashboardPlaceholderComponent, data: { title: 'Settings', subtitle: 'Integrations, policies, notification and profile controls.' } },
      {
        path: 'admin/rbac',
        canActivate: [permissionGuard],
        data: { permissions: ['roles.manage'], title: 'RBAC', subtitle: 'Tenant role, permission, and assignment management.' },
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
