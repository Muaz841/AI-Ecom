import { CommonModule } from '@angular/common';
import {
  AfterViewChecked,
  Component,
  DestroyRef,
  ElementRef,
  OnInit,
  ViewChild,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { take } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { ThreadStatusValues } from '../../shared/constants/message.constants';
import {
  Message,
  MessageSender,
  Platform,
  Thread,
  ThreadStatus,
  mapMessageDto,
  mapThreadDto,
  relativeTime,
} from './conversation.models';
import { InboxService } from './inbox.service';

const STATUS_LABELS: Record<ThreadStatus, string> = {
  [ThreadStatusValues.AiHandled]: 'AI',
  [ThreadStatusValues.Pending]:   'Pending',
  [ThreadStatusValues.Human]:     'Human',
  [ThreadStatusValues.Resolved]: 'Done',
};

const STATUS_CLASSES: Record<ThreadStatus, string> = {
  [ThreadStatusValues.AiHandled]: 'status-ai',
  [ThreadStatusValues.Pending]:   'status-pending',
  [ThreadStatusValues.Human]:     'status-human',
  [ThreadStatusValues.Resolved]:  'status-resolved',
};

@Component({
  selector: 'app-inbox',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './inbox.component.html',
  styleUrl: './inbox.component.scss',
})
export class InboxComponent implements OnInit, AfterViewChecked {
  @ViewChild('messagesArea') private readonly messagesArea?: ElementRef<HTMLElement>;

  private readonly inboxService = inject(InboxService);
  private readonly authService = inject(AuthService);
  private readonly destroyRef = inject(DestroyRef);

  private tenantId: string | null = null;

  readonly filters = [
    { id: 'all', label: 'All' },
    { id: 'unread', label: 'Unread' },
    { id: ThreadStatusValues.AiHandled, label: 'AI Handled' },
    { id: ThreadStatusValues.Pending,   label: 'Pending' },
  ];

  readonly platformList = [
    { id: 'WhatsApp' as Platform, label: 'WhatsApp', color: '#25D366' },
    { id: 'Instagram' as Platform, label: 'Instagram', color: '#E1306C' },
    { id: 'Facebook' as Platform, label: 'Facebook', color: '#1877F2' },
  ];

  readonly aiSuggestions = [
    'Thank you for reaching out! Let me check that for you right away.',
    "I'll escalate this to our support team who will assist you shortly.",
    'Your order will be delivered within 2–3 business days. Is there anything else I can help with?',
  ];

  // ─── UI state ───────────────────────────────────────────────────────────────
  readonly activeFilter = signal('all');
  readonly searchQuery = signal('');
  readonly activePlatforms = signal<Platform[]>([]);
  readonly selectedThread = signal<Thread | null>(null);
  readonly messageInput = signal('');
  readonly showSuggestions = signal(false);

  // ─── Data + async state ─────────────────────────────────────────────────────
  readonly loadingThreads = signal(false);
  readonly loadingMessages = signal(false);
  readonly threadsError = signal<string | null>(null);
  readonly messagesError = signal<string | null>(null);

  private readonly threads = signal<Thread[]>([]);

  // ─── Derived ────────────────────────────────────────────────────────────────
  readonly filteredThreads = computed(() => {
    const query = this.searchQuery().toLowerCase();
    const filter = this.activeFilter();
    const platforms = this.activePlatforms();

    return this.threads().filter((t) => {
      if (query && !t.name.toLowerCase().includes(query) && !t.lastMessage.toLowerCase().includes(query)) {
        return false;
      }
      if (filter === 'unread' && t.unreadCount === 0) return false;
      if (filter === ThreadStatusValues.AiHandled && t.status !== ThreadStatusValues.AiHandled) return false;
      if (filter === ThreadStatusValues.Pending   && t.status !== ThreadStatusValues.Pending)   return false;
      if (platforms.length > 0 && !platforms.includes(t.platform)) return false;
      return true;
    });
  });

  readonly currentMessages = computed(() => this.selectedThread()?.messages ?? []);

  private shouldScrollToBottom = false;

  // ─── Lifecycle ───────────────────────────────────────────────────────────────
  ngOnInit(): void {
    this.authService.tenantId$.pipe(take(1), takeUntilDestroyed(this.destroyRef)).subscribe((id) => {
      this.tenantId = id;
      if (id) {
        this.loadThreads();
      }
    });
  }

  // ─── Data loading ────────────────────────────────────────────────────────────
  loadThreads(): void {
    if (!this.tenantId) return;

    this.loadingThreads.set(true);
    this.threadsError.set(null);

    this.inboxService
      .listThreads(this.tenantId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (dtos) => {
          this.threads.set(dtos.map(mapThreadDto));
          this.loadingThreads.set(false);
        },
        error: () => {
          this.threadsError.set('Failed to load conversations. Check your connection and try again.');
          this.loadingThreads.set(false);
        },
      });
  }

  private loadMessagesForThread(thread: Thread): void {
    if (!this.tenantId || thread.messagesLoaded) return;

    this.loadingMessages.set(true);
    this.messagesError.set(null);

    this.inboxService
      .getMessages(this.tenantId, thread.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (dtos) => {
          const messages = dtos.map(mapMessageDto);
          this.threads.update((prev) =>
            prev.map((t) => (t.id === thread.id ? { ...t, messages, messagesLoaded: true } : t)),
          );
          this.selectedThread.update((t) =>
            t?.id === thread.id ? { ...t, messages, messagesLoaded: true } : t,
          );
          this.loadingMessages.set(false);
          this.shouldScrollToBottom = true;
        },
        error: () => {
          this.messagesError.set('Failed to load messages.');
          this.loadingMessages.set(false);
        },
      });
  }

  // ─── Actions ─────────────────────────────────────────────────────────────────
  setFilter(id: string): void {
    this.activeFilter.set(id);
  }

  setSearch(event: Event): void {
    this.searchQuery.set((event.target as HTMLInputElement).value);
  }

  togglePlatform(id: Platform): void {
    this.activePlatforms.update((prev) =>
      prev.includes(id) ? prev.filter((p) => p !== id) : [...prev, id],
    );
  }

  selectThread(thread: Thread | null): void {
    this.selectedThread.set(thread);

    if (thread) {
      if (thread.unreadCount > 0) {
        this.threads.update((prev) => prev.map((t) => (t.id === thread.id ? { ...t, unreadCount: 0 } : t)));
        this.selectedThread.update((t) => (t ? { ...t, unreadCount: 0 } : t));
      }
      this.loadMessagesForThread(thread);
    }

    this.showSuggestions.set(false);
    this.shouldScrollToBottom = true;
  }

  setMessageInput(event: Event): void {
    this.messageInput.set((event.target as HTMLTextAreaElement).value);
  }

  sendMessage(event?: Event): void {
    if (event) {
      const ke = event as KeyboardEvent;
      if (ke.shiftKey) return;
      event.preventDefault();
    }

    const content = this.messageInput().trim();
    if (!content || !this.selectedThread()) return;

    const newMsg: Message = {
      id: `msg-${Date.now()}`,
      sender: 'agent' as MessageSender,
      content,
      time: new Date().toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit' }),
    };

    const threadId = this.selectedThread()!.id;
    const now = new Date().toISOString();

    this.threads.update((prev) =>
      prev.map((t) =>
        t.id === threadId
          ? { ...t, messages: [...t.messages, newMsg], lastMessage: content, relativeTime: relativeTime(now) }
          : t,
      ),
    );
    this.selectedThread.update((t) =>
      t ? { ...t, messages: [...t.messages, newMsg], lastMessage: content, relativeTime: relativeTime(now) } : t,
    );

    this.messageInput.set('');
    this.showSuggestions.set(false);
    this.shouldScrollToBottom = true;
  }

  applySuggestion(text: string): void {
    this.messageInput.set(text);
    this.showSuggestions.set(false);
  }

  toggleSuggestions(): void {
    this.showSuggestions.update((v) => !v);
  }

  statusLabel(status: ThreadStatus): string {
    return STATUS_LABELS[status];
  }

  statusClass(status: ThreadStatus): string {
    return STATUS_CLASSES[status];
  }

  ngAfterViewChecked(): void {
    if (this.shouldScrollToBottom && this.messagesArea) {
      const el = this.messagesArea.nativeElement;
      el.scrollTop = el.scrollHeight;
      this.shouldScrollToBottom = false;
    }
  }
}
