import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { map } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { SIDEBAR_PIPELINE, SidebarModule } from '../../core/navigation/nav-pipeline';

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

      const roles = session.profile.roles;
      const permissions = session.profile.permissions;

      return SIDEBAR_PIPELINE
        .filter((module) => this.canAccessModule(module, roles, permissions))
        .sort((a, b) => a.order - b.order);
    }),
  );

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

  logout(): void {
    this.authService.logout();
    void this.router.navigateByUrl('/auth/login');
  }

  private canAccessModule(module: SidebarModule, roles: string[], permissions: string[]): boolean {
    const permissionPass =
      !module.requiredPermissions || module.requiredPermissions.every((permission) => permissions.includes(permission));
    const rolePass = !module.requiredRoles || module.requiredRoles.some((role) => roles.includes(role));
    return permissionPass && rolePass;
  }
}
