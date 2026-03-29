import { CommonModule } from '@angular/common';
import { Component, ElementRef, HostListener, inject, DestroyRef, OnInit, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { map } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AuthService } from '../../core/auth/auth.service';
import { resolveVisibleSidebarModules, SIDEBAR_PIPELINE, SidebarModule } from '../../core/navigation/nav-pipeline';
import { ThemeService } from '../../core/theme/theme.service';
import { SignalrRealtimeService } from '../../core/realtime/signalr-realtime.service';
import { parseEnvelope, RealtimeEnvelope } from '../../core/realtime/realtime-events';
import { RealtimeEventNames } from '../../shared/constants/message.constants';

type NotificationItem = {
  id: string;
  title: string;
  detail: string;
  receivedAt: string;
};

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app-shell.component.html',
  styleUrl: './app-shell.component.scss',
})
export class AppShellComponent implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly themeService = inject(ThemeService);
  private readonly elementRef = inject(ElementRef);
  private readonly realtime = inject(SignalrRealtimeService);
  private readonly destroyRef = inject(DestroyRef);

  readonly notifications = signal<NotificationItem[]>([]);

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

  ngOnInit(): void {
    this.bootstrapRealtime();
  }

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

  private bootstrapRealtime(): void {
    this.authService.session$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((session) => {
        if (!session) {
          this.realtime.disconnect().catch(() => undefined);
          return;
        }

        this.realtime.connect().catch((err) =>
          console.error('[Realtime] Connection failed', err));
        this.realtime.on<RealtimeEnvelope>('notification');
      });

    this.realtime.events$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(({ event, payload }) => {
        if (event !== 'notification') return;

        const envelope = parseEnvelope(payload);
        if (!envelope) {
          console.warn('[Realtime/Shell] Dropped: invalid envelope or unknown schema version', payload);
          return;
        }
        const item = this.mapNotification(envelope);
        if (!item) return;

        this.notifications.update(prev => {
          const next = [item, ...prev];
          return next.length > 25 ? next.slice(0, 25) : next;
        });
      });
  }

  private mapNotification(envelope: RealtimeEnvelope): NotificationItem | null {
    const createdAt = new Date(envelope.createdAtUtc).toLocaleTimeString();
    const id = typeof crypto !== 'undefined' && 'randomUUID' in crypto
      ? crypto.randomUUID()
      : `${Date.now()}-${Math.random().toString(16).slice(2)}`;

    // payload is `unknown` — cast once to a loose record for property access.
    const p = (envelope.payload && typeof envelope.payload === 'object')
      ? (envelope.payload as Record<string, unknown>)
      : {};

    switch (envelope.eventType) {
      case RealtimeEventNames.CommentReceived: {
        const platform = (p['platform'] as string) ?? 'instagram';
        const content  = (p['content']  as string) ?? 'New comment received.';
        return { id, title: `New ${platform} comment`, detail: content, receivedAt: createdAt };
      }
      case RealtimeEventNames.MessageReceived: {
        const platform = (p['platform'] as string) ?? 'whatsapp';
        const content  = (p['content']  as string) ?? 'New message received.';
        return { id, title: `New ${platform} message`, detail: content, receivedAt: createdAt };
      }
      case RealtimeEventNames.AiReplySent: {
        const platform = (p['platform'] as string) ?? 'whatsapp';
        const reply    = (p['reply']    as string) ?? 'AI replied to a message.';
        const intent   = (p['intent']   as string) ?? '';
        return { id, title: `AI replied on ${platform}`, detail: intent ? `[${intent}] ${reply}` : reply, receivedAt: createdAt };
      }
      default:
        return null;
    }
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
