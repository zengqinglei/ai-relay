import { inject } from '@angular/core';
import { toObservable } from '@angular/core/rxjs-interop';
import { CanActivateFn, Router } from '@angular/router';
import { filter, map, take } from 'rxjs/operators';

import { AuthService } from '../services/auth-service';
import { StartupService } from '../services/startup-service';

export const roleGuard: CanActivateFn = (route, _state) => {
  const authService = inject(AuthService);
  const startupService = inject(StartupService);
  const router = inject(Router);

  // Wait for startup service to complete loading user data
  return toObservable(startupService.status).pipe(
    filter(status => status !== 'loading'),
    take(1),
    map(() => {
      const requiredRole = route.data['role'];

      if (!requiredRole || authService.hasRole(requiredRole)) {
        return true;
      }

      // Redirect to an unauthorized page or home
      // For now, redirect to workspace
      return router.parseUrl('/workspace');
    })
  );
};
