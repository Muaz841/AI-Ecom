import { Inject, Injectable, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';

@Injectable({ providedIn: 'root' })
export class TokenStore {
  private readonly accessTokenKey = 'wa_ai_access_token';
  private readonly refreshTokenKey = 'wa_ai_refresh_token';
  private readonly expiresAtKey = 'wa_ai_access_expires';
  private readonly isBrowser: boolean;

  constructor(@Inject(PLATFORM_ID) platformId: object) {
    this.isBrowser = isPlatformBrowser(platformId);
  }

  getAccessToken(): string | null {
    return this.read(this.accessTokenKey);
  }

  getRefreshToken(): string | null {
    return this.read(this.refreshTokenKey);
  }

  getAccessTokenExpiresAtUtc(): string | null {
    return this.read(this.expiresAtKey);
  }

  setTokens(accessToken: string, refreshToken: string, accessTokenExpiresAtUtc: string | null): void {
    this.write(this.accessTokenKey, accessToken);
    this.write(this.refreshTokenKey, refreshToken);
    if (accessTokenExpiresAtUtc) {
      this.write(this.expiresAtKey, accessTokenExpiresAtUtc);
    } else {
      this.remove(this.expiresAtKey);
    }
  }

  clear(): void {
    this.remove(this.accessTokenKey);
    this.remove(this.refreshTokenKey);
    this.remove(this.expiresAtKey);
  }

  private read(key: string): string | null {
    if (!this.isBrowser) {
      return null;
    }

    return localStorage.getItem(key);
  }

  private write(key: string, value: string): void {
    if (!this.isBrowser) {
      return;
    }

    localStorage.setItem(key, value);
  }

  private remove(key: string): void {
    if (!this.isBrowser) {
      return;
    }

    localStorage.removeItem(key);
  }
}
