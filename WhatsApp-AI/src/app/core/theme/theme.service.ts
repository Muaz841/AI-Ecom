import { Inject, Injectable, PLATFORM_ID, signal } from '@angular/core';
import { DOCUMENT, isPlatformBrowser } from '@angular/common';

export type AppTheme = 'light' | 'dark';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly themeStorageKey = 'autobaat_theme';
  private readonly isBrowser: boolean;
  private readonly currentThemeSignal = signal<AppTheme>('dark');

  readonly currentTheme = this.currentThemeSignal.asReadonly();

  constructor(
    @Inject(DOCUMENT) private readonly document: Document,
    @Inject(PLATFORM_ID) platformId: object,
  ) {
    this.isBrowser = isPlatformBrowser(platformId);
  }

  initialize(): void {
    const theme = this.resolveInitialTheme();
    this.applyTheme(theme);
  }

  toggleTheme(): void {
    this.applyTheme(this.currentThemeSignal() === 'dark' ? 'light' : 'dark');
  }

  setTheme(theme: AppTheme): void {
    this.applyTheme(theme);
  }

  private resolveInitialTheme(): AppTheme {
    if (!this.isBrowser) {
      return 'dark';
    }

    const savedTheme = localStorage.getItem(this.themeStorageKey);
    if (savedTheme === 'light' || savedTheme === 'dark') {
      return savedTheme;
    }

    return window.matchMedia('(prefers-color-scheme: light)').matches ? 'light' : 'dark';
  }

  private applyTheme(theme: AppTheme): void {
    this.currentThemeSignal.set(theme);
    this.document.documentElement.setAttribute('data-theme', theme);

    if (this.isBrowser) {
      localStorage.setItem(this.themeStorageKey, theme);
    }
  }
}
