import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map, take } from 'rxjs/operators';
import { AuthService } from './auth.service';

export const permissionGuard: CanActivateFn = (route) => {
  const authService = inject(AuthService);
  const router = inject(Router);
  const requiredPermissions = (route.data?.['permissions'] as string[] | undefined) ?? [];
  const requiredRoles = (route.data?.['roles'] as string[] | undefined) ?? [];
  const roleMatchMode = (route.data?.['roleMatchMode'] as 'any' | 'all' | undefined) ?? 'any';

  return authService.session$.pipe(
    take(1),
    map((session) => {
      if (!session) {
        return router.createUrlTree(['/auth/login']);
      }

      if (requiredPermissions.length === 0) {
        return true;
      }

      const hasAllPermissions = requiredPermissions.every((permission) =>
        session.profile.permissions.includes(permission),
      );

      const hasRoles =
        requiredRoles.length === 0 ||
        (roleMatchMode === 'all'
          ? requiredRoles.every((role) => session.profile.roles.includes(role))
          : requiredRoles.some((role) => session.profile.roles.includes(role)));

      return hasAllPermissions && hasRoles ? true : router.createUrlTree(['/unauthorized']);
    }),
  );
};
