/**
 * Canonical realtime event contracts — single source of truth for the
 * SignalR notification pipeline.
 *
 * Every event is wrapped in a RealtimeEnvelope that carries a schemaVersion.
 * The parsers below validate required fields and normalise both camelCase
 * (current) and PascalCase (legacy) property keys in one place, so no other
 * component needs to deal with raw unknown payloads.
 */

export const REALTIME_SCHEMA_VERSION = 1;

// ─── Envelope ────────────────────────────────────────────────────────────────

export interface RealtimeEnvelope {
  schemaVersion: number;
  eventType: string;
  payload: unknown;
  createdAtUtc: string;
}

/**
 * Parses the raw value received from the 'notification' SignalR method.
 *
 * - Missing schemaVersion → treated as v1 (backward compat during rollout).
 * - Wrong schemaVersion number → returns null (unknown future format).
 * - Missing eventType → returns null.
 */
export function parseEnvelope(raw: unknown): RealtimeEnvelope | null {
  if (!raw || typeof raw !== 'object') return null;
  const r = raw as Record<string, unknown>;

  const schemaVersion =
    typeof r['schemaVersion'] === 'number' ? r['schemaVersion'] : REALTIME_SCHEMA_VERSION;

  if (schemaVersion !== REALTIME_SCHEMA_VERSION) return null;

  const eventType = str(r['eventType']);
  if (!eventType) return null;

  return {
    schemaVersion,
    eventType,
    payload:      r['payload'],
    createdAtUtc: str(r['createdAtUtc']) ?? new Date().toISOString(),
  };
}

// ─── message.received / comment.received ─────────────────────────────────────

export interface MessageReceivedEvent {
  messageId:    string;
  threadId:     string;
  tenantId:     string;
  platform:     string;
  from:         string;
  to:           string;
  content:      string;
  createdAtUtc: string;
}

/**
 * Parses and normalises a message.received / comment.received payload.
 * Returns null if threadId or messageId is missing — caller must log + discard.
 * Tolerates both camelCase (current) and PascalCase (legacy) key names.
 */
export function toMessageReceivedEvent(payload: unknown): MessageReceivedEvent | null {
  if (!payload || typeof payload !== 'object') return null;
  const p = payload as Record<string, unknown>;

  const threadId  = str(p['threadId']  ?? p['ThreadId']);
  const messageId = str(p['messageId'] ?? p['MessageId'] ?? p['id'] ?? p['Id']);

  if (!threadId || !messageId) return null;

  return {
    messageId,
    threadId,
    tenantId:     str(p['tenantId']     ?? p['TenantId'])     ?? '',
    platform:     str(p['platform']     ?? p['Platform'])     ?? '',
    from:         str(p['from']         ?? p['From'])         ?? '',
    to:           str(p['to']           ?? p['To'])           ?? '',
    content:      str(p['content']      ?? p['Content'])      ?? '',
    createdAtUtc: str(p['createdAtUtc'] ?? p['CreatedAtUtc'] ?? p['receivedAtUtc']) ?? new Date().toISOString(),
  };
}

// ─── ai.reply.sent ───────────────────────────────────────────────────────────

export interface AiReplySentEvent {
  outgoingMessageId: string;
  threadId:          string;
  tenantId:          string;
  platform:          string;
  from:              string;
  reply:             string;
  intent:            string;
  sentAtUtc:         string;
}

/**
 * Parses and normalises an ai.reply.sent payload.
 * Returns null if threadId is missing.
 */
export function toAiReplySentEvent(payload: unknown): AiReplySentEvent | null {
  if (!payload || typeof payload !== 'object') return null;
  const p = payload as Record<string, unknown>;

  const threadId = str(p['threadId'] ?? p['ThreadId']);
  if (!threadId) return null;

  return {
    outgoingMessageId: str(p['outgoingMessageId'] ?? p['OutgoingMessageId']) ?? `rt-${Date.now()}`,
    threadId,
    tenantId: str(p['tenantId'] ?? p['TenantId']) ?? '',
    platform: str(p['platform'] ?? p['Platform']) ?? '',
    from:     str(p['from']     ?? p['From'])     ?? '',
    reply:    str(p['reply']    ?? p['Reply'])     ?? '',
    intent:   str(p['intent']   ?? p['Intent'])   ?? '',
    sentAtUtc: str(p['sentAtUtc'] ?? p['SentAtUtc']) ?? new Date().toISOString(),
  };
}

// ─── Internal helper ─────────────────────────────────────────────────────────

function str(v: unknown): string | null {
  return typeof v === 'string' && v.length > 0 ? v : null;
}
