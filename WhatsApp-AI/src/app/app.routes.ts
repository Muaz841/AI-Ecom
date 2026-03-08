import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';
import { permissionGuard } from './core/auth/permission.guard';
import { LoginComponent } from './features/auth/login.component';
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
    component: LoginComponent,
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
