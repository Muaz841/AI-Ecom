export type MetaChannel = 'instagram' | 'facebook' | 'whatsapp';
export type IntegrationStatus = 'connected' | 'active' | 'not_connected' | 'expired';

export interface MetaConnectStartResponse {
  success: boolean;
  authorizationUrl: string | null;
  state: string | null;
  errorMessage: string | null;
}

export interface MetaConnection {
  id: string;
  channel: string;
  status: string;
  externalBusinessId: string | null;
  externalAccountId: string | null;
  connectedAtUtc: string;
  accessTokenExpiresAtUtc: string | null;
  lastValidatedAtUtc: string | null;
  lastError: string | null;
}

export interface MetaConnectionView {
  id: string;
  platform: MetaChannel;
  platformLabel: string;
  accountName: string;
  businessId: string;
  status: IntegrationStatus;
  statusLabel: string;
  statusTagSeverity: 'success' | 'warn' | 'danger';
  connectedDateDisplay: string;
  connectedAtUtc: string;
}
