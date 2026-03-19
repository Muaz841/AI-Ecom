import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClientService } from '../../../core/api/api-client.service';
import { APP_CONFIG } from '../../../core/config/app-config';

export interface WebhookTestRequest {
  platform: string;
  from: string;
  to: string;
  message: string;
  messageType?: string;
  rawPayloadJson?: string | null;
  useRawPayload?: boolean;
}

export interface WebhookTestResponse {
  requestPayload: string;
  statusCode: number;
  success: boolean;
  resultMessage: string;
  processedCount: number;
  detectedIntent: string | null;
  replySent: string | null;
  errorMessage: string | null;
  toolCallsMade: string[];
  inputTokens: number;
  outputTokens: number;
  aiProvider: string;
  aiModel: string;
}

@Injectable({ providedIn: 'root' })
export class WebhookTesterService {
  constructor(private readonly apiClient: ApiClientService) {}

  sendTest(request: WebhookTestRequest): Observable<WebhookTestResponse> {
    return this.apiClient.post<WebhookTestResponse>(APP_CONFIG.dev.webhookTest, request);
  }
}
