import { AssignmentMode, MessageDirection } from '../../shared/constants/message.constants';

// ─── Backend DTOs (mirror ConversationsController records) ──────────────────

export interface ConversationThreadDto {
  id: string;
  tenantId: string;
  platform: string;
  customerIdentifier: string;
  businessIdentifier: string;
  customerDisplayName: string | null;
  lastMessagePreview: string | null;
  lastMessageDirection: MessageDirection | null;
  lastMessageAt: string | null;
  messageCount: number;
  isOpen: boolean;
  assignmentMode: AssignmentMode;
}

export interface ConversationMessageDto {
  id: string;
  conversationThreadId: string | null;
  platform: string;
  direction: MessageDirection;
  messageType: string;
  from: string;
  to: string;
  content: string;
  externalMessageId: string | null;
  deliveryStatus: string | null;
  receivedAt: string;
  sentAt: string | null;
}

// ─── Frontend View Models ────────────────────────────────────────────────────

export type Platform = 'WhatsApp' | 'Instagram' | 'Facebook';
export type ThreadStatus = 'ai-handled' | 'pending' | 'human' | 'resolved';
export type MessageSender = 'customer' | 'bot' | 'agent';

export interface Message {
  id: string;
  sender: MessageSender;
  content: string;
  time: string;
}

export interface Thread {
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
  messagesLoaded: boolean;
}

// ─── Mapping helpers ─────────────────────────────────────────────────────────

const PLATFORM_COLORS: Record<Platform, string> = {
  WhatsApp:  '#25D366',
  Instagram: '#E1306C',
  Facebook:  '#1877F2',
};

const AVATAR_PALETTE = [
  '#7C3AED', '#2563EB', '#DB2777', '#0D9488',
  '#EA580C', '#4F46E5', '#DC2626', '#059669',
];

function normalizePlatform(raw: string): Platform {
  const lower = raw.toLowerCase();
  if (lower === 'instagram') return 'Instagram';
  if (lower === 'facebook') return 'Facebook';
  return 'WhatsApp';
}

function toInitials(name: string): string {
  const parts = name.trim().split(/\s+/);
  if (parts.length >= 2) {
    return (parts[0][0] + parts[1][0]).toUpperCase();
  }
  return name.slice(0, 2).toUpperCase();
}

function avatarColor(id: string): string {
  let hash = 0;
  for (let i = 0; i < id.length; i++) {
    hash = (hash * 31 + id.charCodeAt(i)) >>> 0;
  }
  return AVATAR_PALETTE[hash % AVATAR_PALETTE.length];
}

function threadStatus(dto: ConversationThreadDto): ThreadStatus {
  if (!dto.isOpen) return 'resolved';
  if (dto.assignmentMode === AssignmentMode.Human) return 'human';
  if (dto.lastMessageDirection === MessageDirection.Incoming) return 'pending';
  return 'ai-handled';
}

export function relativeTime(isoDate: string | null): string {
  if (!isoDate) return '';
  const diffMs   = Date.now() - new Date(isoDate).getTime();
  const diffMins = Math.floor(diffMs / 60_000);
  if (diffMins < 1) return 'just now';
  if (diffMins < 60) return `${diffMins}m ago`;
  const diffHrs = Math.floor(diffMins / 60);
  if (diffHrs < 24) return `${diffHrs}h ago`;
  return `${Math.floor(diffHrs / 24)}d ago`;
}

export function mapThreadDto(dto: ConversationThreadDto): Thread {
  const displayName = dto.customerDisplayName ?? dto.customerIdentifier;
  const platform    = normalizePlatform(dto.platform);
  return {
    id:           dto.id,
    name:         displayName,
    initials:     toInitials(displayName),
    avatarColor:  avatarColor(dto.id),
    platform,
    platformColor: PLATFORM_COLORS[platform],
    lastMessage:  dto.lastMessagePreview ?? '',
    relativeTime: relativeTime(dto.lastMessageAt),
    unreadCount:  0,
    isOnline:     false,
    lastSeen:     '',
    status:       threadStatus(dto),
    messages:     [],
    messagesLoaded: false,
  };
}

export function mapMessageDto(dto: ConversationMessageDto): Message {
  const sender: MessageSender = dto.direction === MessageDirection.Incoming ? 'customer' : 'bot';
  const ts = dto.sentAt ?? dto.receivedAt;
  return {
    id:      dto.id,
    sender,
    content: dto.content,
    time:    new Date(ts).toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit' }),
  };
}
