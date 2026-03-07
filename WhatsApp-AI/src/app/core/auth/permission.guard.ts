import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map, take } from 'rxjs/operators';
import { AuthService } from './auth.service';

export const permissionGuard: CanActivateFn = (route) => {
  const authService = inject(AuthService);
  const router = inject(Router);
  const requiredPermissions = (route.data?.['permissions'] as string[] | undefined) ?? [];

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

      return hasAllPermissions ? true : router.createUrlTree(['/unauthorized']);
    }),
  );
};
