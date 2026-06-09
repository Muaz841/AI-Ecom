/**
 * Canonical message direction values — must match backend MessageDirection enum (stored lowercase).
 */
export const MessageDirection = {
  Incoming: 'incoming',
  Outgoing: 'outgoing',
} as const;
export type MessageDirection = (typeof MessageDirection)[keyof typeof MessageDirection];

/**
 * Canonical message type values — must match backend MessageType enum (stored lowercase).
 */
export const MessageType = {
  Text:        'text',
  Comment:     'comment',
  Image:       'image',
  Audio:       'audio',
  Video:       'video',
  Sticker:     'sticker',
  Location:    'location',
  Reaction:    'reaction',
  Unsupported: 'unsupported',
} as const;
export type MessageType = (typeof MessageType)[keyof typeof MessageType];

/**
 * Canonical delivery status values — must match backend DeliveryStatus enum (stored lowercase).
 */
export const DeliveryStatus = {
  Sent:      'sent',
  Delivered: 'delivered',
  Read:      'read',
  Failed:    'failed',
} as const;
export type DeliveryStatus = (typeof DeliveryStatus)[keyof typeof DeliveryStatus];

/**
 * Canonical assignment mode values — must match backend AssignmentMode enum (stored lowercase).
 */
export const AssignmentMode = {
  AI:    'ai',
  Human: 'human',
} as const;
export type AssignmentMode = (typeof AssignmentMode)[keyof typeof AssignmentMode];

/**
 * AI intent codes — must match AiIntentCodes.cs in backend.
 */
export const AiIntentCodes = {
  Greeting:   'greeting',
  OrderStart: 'order_start',
  Inquiry:    'inquiry',
  Complaint:  'complaint',
  Unhandled:  'unhandled',
} as const;
export type AiIntentCode = (typeof AiIntentCodes)[keyof typeof AiIntentCodes];

/**
 * SignalR realtime event names — must match RealtimeEventNames.cs in backend.
 */
export const RealtimeEventNames = {
  MessageReceived: 'message.received',
  CommentReceived: 'comment.received',
  AiReplySent:     'ai.reply.sent',
} as const;

/**
 * Frontend-computed thread status values (derived from isOpen + assignmentMode + lastMessageDirection).
 */
export const ThreadStatusValues = {
  AiHandled: 'ai-handled',
  Pending:   'pending',
  Human:     'human',
  Resolved:  'resolved',
} as const;
export type ThreadStatus = (typeof ThreadStatusValues)[keyof typeof ThreadStatusValues];
