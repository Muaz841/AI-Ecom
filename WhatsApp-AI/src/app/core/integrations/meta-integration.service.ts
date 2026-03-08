import { Injectable } from '@angular/core';
import { HttpHeaders } from '@angular/common/http';
import { map, Observable } from 'rxjs';
import { ApiClientService } from '../api/api-client.service';
import { APP_CONFIG } from '../config/app-config';
import { MetaChannel, MetaConnection, MetaConnectionView, MetaConnectStartResponse } from './meta-integration.models';

@Injectable({ providedIn: 'root' })
export class MetaIntegrationService {
  private readonly silentToastHeaders = new HttpHeaders().set(
    ApiClientService.skipGlobalErrorToastHeader,
    'true',
  );

  constructor(private readonly apiClient: ApiClientService) {}

  startConnect(channel: MetaChannel, returnUrl: string): Observable<string> {
    const path = APP_CONFIG.integrations.start(channel);
    return this.apiClient
      .post<MetaConnectStartResponse>(path, { returnUrl }, this.silentToastHeaders)
      .pipe(
        map((response) => {
          if (!response.success || !response.authorizationUrl) {
            throw new Error(response.errorMessage ?? 'Unable to start Meta connection.');
          }

          return response.authorizationUrl;
        }),
      );
  }

  listConnections(): Observable<MetaConnection[]> {
    return this.apiClient.get<MetaConnection[]>(APP_CONFIG.integrations.list, undefined, this.silentToastHeaders);
  }

  listConnectionsView(): Observable<MetaConnectionView[]> {
    return this.listConnections().pipe(
      map((rows) =>
        rows
          .map((row) => this.toView(row))
          .sort((a, b) => new Date(b.connectedAtUtc).getTime() - new Date(a.connectedAtUtc).getTime()),
      ),
    );
  }

  disconnect(connectionId: string): Observable<void> {
    return this.apiClient.delete<void>(APP_CONFIG.integrations.disconnect(connectionId), undefined, this.silentToastHeaders);
  }

  private toView(row: MetaConnection): MetaConnectionView {
    const platform = normalizePlatform(row.channel);
    const status = normalizeStatus(row.status, row.lastError);

    return {
      id: row.id,
      platform,
      platformLabel: platformLabel(platform),
      accountName: row.externalAccountId || row.externalBusinessId || 'N/A',
      businessId: row.externalBusinessId || row.externalAccountId || 'N/A',
      status,
      statusLabel: statusLabel(status),
      statusTagSeverity: statusSeverity(status),
      connectedDateDisplay: formatDate(row.connectedAtUtc),
      connectedAtUtc: row.connectedAtUtc,
    };
  }
}

function normalizePlatform(channel: string): MetaChannel {
  const normalized = channel.trim().toLowerCase();
  if (normalized === 'instagram' || normalized === 'facebook' || normalized === 'whatsapp') {
    return normalized;
  }

  return 'facebook';
}

function normalizeStatus(status: string, lastError: string | null): 'connected' | 'expired' {
  const normalized = status.trim().toLowerCase();
  if (normalized.includes('error') || normalized.includes('expired') || lastError) {
    return 'expired';
  }

  return 'connected';
}

function platformLabel(platform: MetaChannel): string {
  switch (platform) {
    case 'instagram':
      return 'Instagram';
    case 'whatsapp':
      return 'WhatsApp';
    default:
      return 'Facebook';
  }
}

function statusLabel(status: 'connected' | 'expired'): string {
  return status === 'connected' ? 'Connected' : 'Expired';
}

function statusSeverity(status: 'connected' | 'expired'): 'success' | 'danger' {
  return status === 'connected' ? 'success' : 'danger';
}

function formatDate(value: string): string {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return date.toLocaleString();
}
