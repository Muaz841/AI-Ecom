export interface MarketingConfig {
  isConfigured: boolean;
  claudeApiKeySet: boolean;
  claudeApiKeyMasked: string | null;
  claudeDecisionModel: string;
  claudeSummaryModel: string;
  metaAdsAccountId: string | null;
  metaAdsAccessTokenSet: boolean;
  metaAdsAccessTokenMasked: string | null;
  dryRun: boolean;
  maxActionsPerDay: number;
  dailySpendCapUsd: number;
  updatedAt: string | null;
}

export interface SaveMarketingConfigRequest {
  claudeApiKey: string | null;
  claudeDecisionModel: string;
  claudeSummaryModel: string;
  metaAdsAccountId: string | null;
  metaAdsAccessToken: string | null;
  dryRun: boolean;
  maxActionsPerDay: number;
  dailySpendCapUsd: number;
}

export interface KnowledgeChunk {
  id: string;
  title: string;
  content: string;
  source: string | null;
  createdAt: string;
}

export interface AddKnowledgeRequest {
  title: string;
  content: string;
  source: string | null;
}

export interface AgentDecision {
  id: string;
  runAt: string;
  actionType: string;
  actionPayload: string | null;
  status: string;
  reason: string | null;
  confidence: number;
  isDryRun: boolean;
  executedAt: string | null;
  createdAt: string;
}

export const CLAUDE_MODELS = [
  { label: 'Claude Opus 4.6 (Most capable)', value: 'claude-opus-4-6' },
  { label: 'Claude Sonnet 4.6 (Balanced)', value: 'claude-sonnet-4-6' },
  { label: 'Claude Haiku 4.5 (Fastest)', value: 'claude-haiku-4-5-20251001' },
];

type TagSeverity = 'success' | 'secondary' | 'info' | 'warn' | 'danger' | 'contrast';

export const DECISION_STATUS_LABELS: Record<string, { label: string; severity: TagSeverity }> = {
  Draft:           { label: 'Draft',            severity: 'secondary' },
  PendingApproval: { label: 'Pending Approval', severity: 'warn' },
  Approved:        { label: 'Approved',          severity: 'info' },
  Rejected:        { label: 'Rejected',          severity: 'danger' },
  Executed:        { label: 'Executed',          severity: 'success' },
  DryRun:          { label: 'Dry Run',           severity: 'secondary' },
};
