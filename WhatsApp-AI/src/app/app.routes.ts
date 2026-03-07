import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';
import { permissionGuard } from './core/auth/permission.guard';
import { AuthPlaceholderComponent } from './features/auth/auth-placeholder.component';
import { DashboardPlaceholderComponent } from './features/system/dashboard-placeholder.component';
import { UnauthorizedComponent } from './features/system/unauthorized.component';

export const routes: Routes = [
  {
    path: '',
    canActivate: [authGuard],
    component: DashboardPlaceholderComponent,
  },
  {
    path: 'auth/login',
    component: AuthPlaceholderComponent,
  },
  {
    path: 'admin/rbac',
    canActivate: [authGuard, permissionGuard],
    data: { permissions: ['roles.manage'] },
    component: DashboardPlaceholderComponent,
  },
  {
    path: 'unauthorized',
    component: UnauthorizedComponent,
  },
  {
    path: '**',
    redirectTo: '',
  },
];
