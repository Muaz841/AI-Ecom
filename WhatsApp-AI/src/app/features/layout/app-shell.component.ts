import { CommonModule } from '@angular/common';
import { Component, ElementRef, HostListener, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { map } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { resolveVisibleSidebarModules, SIDEBAR_PIPELINE, SidebarModule } from '../../core/navigation/nav-pipeline';
import { ThemeService } from '../../core/theme/theme.service';

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app-shell.component.html',
  styleUrl: './app-shell.component.scss',
})
export class AppShellComponent {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly themeService = inject(ThemeService);
  private readonly elementRef = inject(ElementRef);

  readonly notifications = [
    { title: 'New inbound message', detail: 'WhatsApp thread is waiting for a reply.' },
    { title: 'Low stock alert', detail: '2 products dropped below threshold.' },
    { title: 'Channel health', detail: 'Meta integration token refreshed successfully.' },
  ];

  readonly visibleModules$ = this.authService.session$.pipe(
    map((session) => {
      if (!session) {
        return [] as SidebarModule[];
      }

      return resolveVisibleSidebarModules(
        SIDEBAR_PIPELINE,
        session.profile.roles,
        session.profile.permissions,
      );
    }),
  );

  readonly profile$ = this.authService.userProfile$;
  readonly currentTheme = this.themeService.currentTheme;


  
  isSidebarCollapsed = false;
  isMobileSidebarOpen = false;
  isNotificationPanelOpen = false;

  toggleSidebar(): void {
    this.isSidebarCollapsed = !this.isSidebarCollapsed;
  }

  toggleMobileSidebar(): void {
    this.isMobileSidebarOpen = !this.isMobileSidebarOpen;
  }

  closeMobileSidebar(): void {
    this.isMobileSidebarOpen = false;
  }

  toggleNotifications(): void {
    this.isNotificationPanelOpen = !this.isNotificationPanelOpen;
  }

  toggleTheme(): void {
    this.themeService.toggleTheme();
  }

  logout(): void {
    this.authService.logout();
    void this.router.navigateByUrl('/auth/login');
  }

@HostListener('document:click', ['$event.target'])
onClickOutside(target: EventTarget | null): void {
  if (!target) return;
  const clickedInside = this.elementRef.nativeElement.contains(target as HTMLElement);
  if (!clickedInside) {
    this.isNotificationPanelOpen = false;
  }
}
}
