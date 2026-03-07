import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable, catchError, map, of, shareReplay, tap } from 'rxjs';
import { ApiClientService } from '../api/api-client.service';
import { APP_CONFIG } from '../config/app-config';
import { AuthResponse, AuthSession, LoginRequest, UserProfile } from './auth.models';
import { decodeJwtPayload, getTokenExpiryUtcMs } from './jwt.utils';
import { TokenStore } from './token.store';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly sessionSubject = new BehaviorSubject<AuthSession | null>(null);
  private refreshInFlight$?: Observable<AuthSession | null>;
  private autoLogoutTimerId: ReturnType<typeof setTimeout> | null = null;

  readonly session$ = this.sessionSubject.asObservable();
  readonly isAuthenticated$ = this.session$.pipe(map((s) => !!s));

  constructor(
    private readonly apiClient: ApiClientService,
    private readonly tokenStore: TokenStore,
  ) {}

  login(request: LoginRequest): Observable<AuthSession> {
    return this.apiClient.post<AuthResponse>(APP_CONFIG.auth.login, request).pipe(
      map((response) => this.mapAuthResponse(response)),
      tap((session) => this.setSession(session)),
    );
  }

  refreshSession(): Observable<AuthSession | null> {
    if (this.refreshInFlight$) {
      return this.refreshInFlight$;
    }

    const refreshToken = this.tokenStore.getRefreshToken();
    if (!refreshToken) {
      return of(null);
    }

    this.refreshInFlight$ = this.apiClient
      .post<AuthResponse>(APP_CONFIG.auth.refresh, { refreshToken })
      .pipe(
        map((response) => this.mapAuthResponse(response)),
        tap((session) => this.setSession(session)),
        catchError(() => {
          this.clearSession();
          return of(null);
        }),
        tap(() => {
          this.refreshInFlight$ = undefined;
        }),
        shareReplay(1),
      );

    return this.refreshInFlight$;
  }

  logout(callServer = true): void {
    const refreshToken = this.tokenStore.getRefreshToken();
    if (callServer && refreshToken) {
      this.apiClient.post<void>(APP_CONFIG.auth.logout, { refreshToken }).subscribe({
        next: () => undefined,
        error: () => undefined,
      });
    }

    this.clearSession();
  }

  initializeSession(): Promise<void> {
    const accessToken = this.tokenStore.getAccessToken();
    const refreshToken = this.tokenStore.getRefreshToken();
    const expiresAt = this.tokenStore.getAccessTokenExpiresAtUtc();

    if (!accessToken || !refreshToken) {
      this.clearSession();
      return Promise.resolve();
    }

    const profile = this.createProfileFromToken(accessToken);
    if (!profile) {
      this.clearSession();
      return Promise.resolve();
    }

    this.setSession({
      accessToken,
      refreshToken,
      accessTokenExpiresAtUtc: expiresAt,
      profile,
    });

    const expiryMs = getTokenExpiryUtcMs(accessToken);
    if (expiryMs && expiryMs <= Date.now() + 10_000) {
      return new Promise((resolve) => {
        this.refreshSession().subscribe(() => resolve());
      });
    }

    return Promise.resolve();
  }

  hasPermission(permissionCode: string): boolean {
    const current = this.sessionSubject.value;
    if (!current) {
      return false;
    }

    return current.profile.permissions.includes(permissionCode);
  }

  private setSession(session: AuthSession): void {
    this.tokenStore.setTokens(session.accessToken, session.refreshToken, session.accessTokenExpiresAtUtc);
    this.sessionSubject.next(session);
    this.scheduleAutoLogout(session.accessToken);
  }

  private clearSession(): void {
    this.tokenStore.clear();
    this.sessionSubject.next(null);
    this.clearAutoLogoutTimer();
  }

  private mapAuthResponse(response: AuthResponse): AuthSession {
    if (!response.success || !response.accessToken || !response.refreshToken) {
      throw new Error(response.errorMessage || 'Authentication failed.');
    }

    const profile = this.createProfileFromToken(response.accessToken);
    if (!profile) {
      throw new Error('Invalid access token payload.');
    }

    return {
      accessToken: response.accessToken,
      refreshToken: response.refreshToken,
      accessTokenExpiresAtUtc: response.accessTokenExpiresAtUtc,
      profile,
    };
  }

  private createProfileFromToken(accessToken: string): UserProfile | null {
    const payload = decodeJwtPayload(accessToken);
    if (!payload?.sub || !payload.email || (!payload.client_id && !payload.tenant_id)) {
      return null;
    }

    const roleClaims = normalizeClaimValues(payload.role);
    const permissionClaims = normalizeClaimValues(payload.permission);

    return {
      userId: payload.sub,
      email: payload.email,
      tenantId: payload.client_id || payload.tenant_id || '',
      roles: roleClaims,
      permissions: permissionClaims,
    };
  }

  private scheduleAutoLogout(accessToken: string): void {
    this.clearAutoLogoutTimer();
    const expiryMs = getTokenExpiryUtcMs(accessToken);
    if (!expiryMs) {
      return;
    }

    const msUntilLogout = expiryMs - Date.now();
    if (msUntilLogout <= 0) {
      this.logout(false);
      return;
    }

    this.autoLogoutTimerId = setTimeout(() => this.logout(false), msUntilLogout);
  }

  private clearAutoLogoutTimer(): void {
    if (this.autoLogoutTimerId) {
      clearTimeout(this.autoLogoutTimerId);
      this.autoLogoutTimerId = null;
    }
  }
}

function normalizeClaimValues(value: string | string[] | undefined): string[] {
  if (!value) {
    return [];
  }

  if (Array.isArray(value)) {
    return value.filter(Boolean);
  }

  return [value];
}
