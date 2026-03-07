import { Injectable } from '@angular/core';
import {
  HttpErrorResponse,
  HttpEvent,
  HttpHandler,
  HttpInterceptor,
  HttpRequest,
} from '@angular/common/http';
import { Observable, catchError, switchMap, throwError } from 'rxjs';
import { APP_CONFIG } from '../config/app-config';
import { AuthService } from '../auth/auth.service';
import { TokenStore } from '../auth/token.store';

@Injectable()
export class AuthInterceptor implements HttpInterceptor {
  constructor(
    private readonly tokenStore: TokenStore,
    private readonly authService: AuthService,
  ) {}

  intercept(request: HttpRequest<unknown>, next: HttpHandler): Observable<HttpEvent<unknown>> {
    const isApiCall = request.url.startsWith(APP_CONFIG.apiBaseUrl) || request.url.startsWith('/');
    if (!isApiCall || this.isAuthEndpoint(request.url)) {
      return next.handle(request);
    }

    const token = this.tokenStore.getAccessToken();
    const authorizedRequest = token
      ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
      : request;

    return next.handle(authorizedRequest).pipe(
      catchError((error: unknown) => {
        if (!(error instanceof HttpErrorResponse) || error.status !== 401) {
          return throwError(() => error);
        }

        return this.authService.refreshSession().pipe(
          switchMap((session) => {
            if (!session?.accessToken) {
              this.authService.logout(false);
              return throwError(() => error);
            }

            const retryRequest = request.clone({
              setHeaders: { Authorization: `Bearer ${session.accessToken}` },
            });

            return next.handle(retryRequest);
          }),
          catchError((refreshError) => {
            this.authService.logout(false);
            return throwError(() => refreshError);
          }),
        );
      }),
    );
  }

  private isAuthEndpoint(url: string): boolean {
    const endpoints = APP_CONFIG.auth;
    return [endpoints.login, endpoints.register, endpoints.refresh, endpoints.forgotPassword, endpoints.resetPassword]
      .some((path) => url.includes(path));
  }
}
