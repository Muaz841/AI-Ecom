import { CommonModule } from '@angular/common';
import { AfterViewChecked, Component, ElementRef, ViewChild, computed, signal } from '@angular/core';

type Platform = 'WhatsApp' | 'Instagram' | 'Facebook';
type ThreadStatus = 'ai-handled' | 'pending' | 'human' | 'resolved';
type MessageSender = 'customer' | 'bot' | 'agent';

interface Message {
  id: string;
  sender: MessageSender;
  content: string;
  time: string;
}

interface Thread {
  id: string;
  name: string;
  initials: string;
  avatarColor: string;
  platform: Platform;
  platformColor: string;
  lastMessage: string;
  relativeTime: string;
  unreadCount: number;
  isOnline: boolean;
  lastSeen: string;
  status: ThreadStatus;
  messages: Message[];
}

const PLATFORM_COLORS: Record<Platform, string> = {
  WhatsApp: '#25D366',
  Instagram: '#E1306C',
  Facebook: '#1877F2',
};

const MOCK_THREADS: Thread[] = [
  {
    id: '1',
    name: 'Sara Malik',
    initials: 'SM',
    avatarColor: '#7C3AED',
    platform: 'WhatsApp',
    platformColor: PLATFORM_COLORS.WhatsApp,
    lastMessage: 'Can you help me track my order #1042?',
    relativeTime: '2m ago',
    unreadCount: 3,
    isOnline: true,
    lastSeen: '',
    status: 'pending',
    messages: [
      { id: 'm1', sender: 'bot', content: 'Hello Sara! How can I help you today?', time: '10:40 AM' },
      { id: 'm2', sender: 'customer', content: "Hi, I placed an order yesterday but haven't received a confirmation.", time: '10:42 AM' },
      { id: 'm3', sender: 'bot', content: "I found your order #1042 placed on March 10. It's being processed and will ship within 24 hours. You'll receive a tracking link via SMS.", time: '10:42 AM' },
      { id: 'm4', sender: 'customer', content: "That's great! Can I change the delivery address?", time: '10:45 AM' },
      { id: 'm5', sender: 'bot', content: "Of course! Please share the new address and I'll update it right away.", time: '10:45 AM' },
      { id: 'm6', sender: 'customer', content: 'Can you help me track my order #1042?', time: '10:58 AM' },
    ],
  },
  {
    id: '2',
    name: 'Ahmed Khan',
    initials: 'AK',
    avatarColor: '#2563EB',
    platform: 'Instagram',
    platformColor: PLATFORM_COLORS.Instagram,
    lastMessage: 'Is this jacket available in blue?',
    relativeTime: '14m ago',
    unreadCount: 1,
    isOnline: false,
    lastSeen: '12 min ago',
    status: 'pending',
    messages: [
      { id: 'm1', sender: 'customer', content: 'I saw your ad for the premium jacket. Is it available in blue?', time: '10:30 AM' },
      { id: 'm2', sender: 'bot', content: 'Hi Ahmed! Yes, the jacket is available in Blue, Black, and Olive. Would you like to place an order?', time: '10:30 AM' },
      { id: 'm3', sender: 'customer', content: 'Is this jacket available in blue?', time: '10:46 AM' },
    ],
  },
  {
    id: '3',
    name: 'Fatima Noor',
    initials: 'FN',
    avatarColor: '#DB2777',
    platform: 'Facebook',
    platformColor: PLATFORM_COLORS.Facebook,
    lastMessage: 'AI handled: Provided full product catalog.',
    relativeTime: '1h ago',
    unreadCount: 0,
    isOnline: false,
    lastSeen: '58 min ago',
    status: 'ai-handled',
    messages: [
      { id: 'm1', sender: 'customer', content: 'Do you have a catalog of your products?', time: '09:15 AM' },
      { id: 'm2', sender: 'bot', content: "Absolutely, Fatima! Here's our latest product catalog. Feel free to browse and let me know if you need help with any item.", time: '09:15 AM' },
      { id: 'm3', sender: 'customer', content: "Thanks! I'll check it out.", time: '09:20 AM' },
      { id: 'm4', sender: 'bot', content: "Great! I'm here whenever you're ready to order. Have a wonderful day!", time: '09:20 AM' },
    ],
  },
  {
    id: '4',
    name: 'Bilal Rafiq',
    initials: 'BR',
    avatarColor: '#0D9488',
    platform: 'WhatsApp',
    platformColor: PLATFORM_COLORS.WhatsApp,
    lastMessage: 'Please take a photo of the damaged item.',
    relativeTime: '2h ago',
    unreadCount: 0,
    isOnline: true,
    lastSeen: '',
    status: 'human',
    messages: [
      { id: 'm1', sender: 'customer', content: 'I received a damaged item. What are your return policies?', time: '08:50 AM' },
      { id: 'm2', sender: 'bot', content: "I'm sorry to hear that, Bilal! We have a 14-day hassle-free return policy. I'm escalating this to a human agent who will assist you shortly.", time: '08:50 AM' },
      { id: 'm3', sender: 'agent', content: "Hi Bilal! I'm Zara from support. I've reviewed your case. Please take a photo of the damaged item and we'll process your refund immediately.", time: '09:05 AM' },
      { id: 'm4', sender: 'customer', content: 'What are your return policies?', time: '09:10 AM' },
    ],
  },
  {
    id: '5',
    name: 'Mariam Qureshi',
    initials: 'MQ',
    avatarColor: '#EA580C',
    platform: 'Instagram',
    platformColor: PLATFORM_COLORS.Instagram,
    lastMessage: 'Order #2081 confirmed! Delivery in 2–3 days.',
    relativeTime: '3h ago',
    unreadCount: 0,
    isOnline: false,
    lastSeen: '2h ago',
    status: 'resolved',
    messages: [
      { id: 'm1', sender: 'customer', content: 'I want to order the floral dress in size M.', time: '07:30 AM' },
      { id: 'm2', sender: 'bot', content: 'Perfect choice, Mariam! The floral dress in size M is available. Total: PKR 2,450. Shall I place the order?', time: '07:30 AM' },
      { id: 'm3', sender: 'customer', content: 'Yes please! Cash on delivery.', time: '07:32 AM' },
      { id: 'm4', sender: 'bot', content: 'Order #2081 confirmed! Expected delivery: 2–3 business days. Thank you for shopping with us!', time: '07:32 AM' },
    ],
  },
  {
    id: '6',
    name: 'Hassan Ali',
    initials: 'HA',
    avatarColor: '#4F46E5',
    platform: 'Facebook',
    platformColor: PLATFORM_COLORS.Facebook,
    lastMessage: 'Free delivery on orders above PKR 3,000!',
    relativeTime: '5h ago',
    unreadCount: 0,
    isOnline: false,
    lastSeen: '4h ago',
    status: 'ai-handled',
    messages: [
      { id: 'm1', sender: 'customer', content: 'Do you deliver to Lahore?', time: '06:00 AM' },
      { id: 'm2', sender: 'bot', content: 'Yes, Hassan! We deliver to all major cities including Lahore. Standard delivery takes 2–4 business days. Express (next-day) is also available.', time: '06:00 AM' },
      { id: 'm3', sender: 'customer', content: "What's the delivery charge?", time: '06:02 AM' },
      { id: 'm4', sender: 'bot', content: 'Standard delivery is PKR 200 for orders below PKR 3,000. Free delivery on orders above PKR 3,000!', time: '06:02 AM' },
    ],
  },
];

const STATUS_LABELS: Record<ThreadStatus, string> = {
  'ai-handled': 'AI',
  pending: 'Pending',
  human: 'Human',
  resolved: 'Done',
};

const STATUS_CLASSES: Record<ThreadStatus, string> = {
  'ai-handled': 'status-ai',
  pending: 'status-pending',
  human: 'status-human',
  resolved: 'status-resolved',
};

@Component({
  selector: 'app-inbox',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './inbox.component.html',
  styleUrl: './inbox.component.scss',
})
export class InboxComponent implements AfterViewChecked {
  @ViewChild('messagesArea') private readonly messagesArea?: ElementRef<HTMLElement>;

  readonly filters = [
    { id: 'all', label: 'All' },
    { id: 'unread', label: 'Unread' },
    { id: 'ai-handled', label: 'AI Handled' },
    { id: 'pending', label: 'Pending' },
  ];

  readonly platformList = [
    { id: 'WhatsApp' as Platform, label: 'WhatsApp', color: PLATFORM_COLORS.WhatsApp },
    { id: 'Instagram' as Platform, label: 'Instagram', color: PLATFORM_COLORS.Instagram },
    { id: 'Facebook' as Platform, label: 'Facebook', color: PLATFORM_COLORS.Facebook },
  ];

  readonly aiSuggestions = [
    'Thank you for reaching out! Let me check that for you right away.',
    "I'll escalate this to our support team who will assist you shortly.",
    'Your order will be delivered within 2–3 business days. Is there anything else I can help with?',
  ];

  readonly activeFilter = signal('all');
  readonly searchQuery = signal('');
  readonly activePlatforms = signal<Platform[]>([]);
  readonly selectedThread = signal<Thread | null>(null);
  readonly messageInput = signal('');
  readonly showSuggestions = signal(false);

  private readonly threads = signal<Thread[]>(MOCK_THREADS);

  readonly filteredThreads = computed(() => {
    const query = this.searchQuery().toLowerCase();
    const filter = this.activeFilter();
    const platforms = this.activePlatforms();

    return this.threads().filter((t) => {
      if (query && !t.name.toLowerCase().includes(query) && !t.lastMessage.toLowerCase().includes(query)) {
        return false;
      }
      if (filter === 'unread' && t.unreadCount === 0) return false;
      if (filter === 'ai-handled' && t.status !== 'ai-handled') return false;
      if (filter === 'pending' && t.status !== 'pending') return false;
      if (platforms.length > 0 && !platforms.includes(t.platform)) return false;
      return true;
    });
  });

  readonly currentMessages = computed(() => this.selectedThread()?.messages ?? []);

  private shouldScrollToBottom = false;

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
    if (thread?.unreadCount) {
      this.threads.update((prev) =>
        prev.map((t) => (t.id === thread.id ? { ...t, unreadCount: 0 } : t)),
      );
      this.selectedThread.update((t) => (t ? { ...t, unreadCount: 0 } : t));
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
      sender: 'agent',
      content,
      time: new Date().toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit' }),
    };

    const threadId = this.selectedThread()!.id;
    this.threads.update((prev) =>
      prev.map((t) =>
        t.id === threadId
          ? { ...t, messages: [...t.messages, newMsg], lastMessage: content, relativeTime: 'just now' }
          : t,
      ),
    );
    this.selectedThread.update((t) =>
      t ? { ...t, messages: [...t.messages, newMsg], lastMessage: content, relativeTime: 'just now' } : t,
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
