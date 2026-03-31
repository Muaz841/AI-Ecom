import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClientService } from '../../core/api/api-client.service';
import { APP_CONFIG } from '../../core/config/app-config';

export interface AiModelInfo {
  name: string;
  label: string;
  supportsToolCalling: boolean;
  supportsStructuredOutput: boolean;
  contextWindow: number;
  isPreview: boolean;
}

export interface AiModelListResult {
  provider: string;
  models: AiModelInfo[];
  isCached: boolean;
}

export interface AiConfigResult {
  isConfigured: boolean;
  activeProvider: string;
  debugModeEnabled: boolean;
  ollamaEndpoint: string;
  ollamaModel: string;
  openAIModel: string;
  openAIApiKeySet: boolean;
  openAIApiKeyMasked: string | null;
  geminiModel: string;
  geminiApiKeySet: boolean;
  geminiApiKeyMasked: string | null;
  requestTimeoutSeconds: number;
  enableToolCalling: boolean;
  enableStructuredOutput: boolean;
  temperature: number | null;
  topP: number | null;
  maxTokens: number | null;
  updatedAt: string | null;
  visionModelName: string | null;
  imageGenerationModelName: string | null;
  messagingModelName: string | null;
}

export interface SaveAiConfigRequest {
  activeProvider: string;
  debugModeEnabled: boolean;
  ollamaEndpoint: string | null;
  ollamaModel: string | null;
  openAIModel: string | null;
  openAIApiKey: string | null;
  geminiModel: string | null;
  geminiApiKey: string | null;
  requestTimeoutSeconds: number;
  enableToolCalling: boolean;
  enableStructuredOutput: boolean;
  temperature: number | null;
  topP: number | null;
  maxTokens: number | null;
  visionModelName: string | null;
  imageGenerationModelName: string | null;
  messagingModelName: string | null;
}

@Injectable({ providedIn: 'root' })
export class AiSettingsService {
  private readonly apiClient = inject(ApiClientService);

  getConfig(): Observable<AiConfigResult> {
    return this.apiClient.get<AiConfigResult>(APP_CONFIG.aiSettings.get);
  }

  saveConfig(request: SaveAiConfigRequest): Observable<AiConfigResult> {
    return this.apiClient.put<AiConfigResult>(APP_CONFIG.aiSettings.save, request);
  }

  getModels(provider: string, refresh = false): Observable<AiModelListResult> {
    const url = refresh
      ? `${APP_CONFIG.aiSettings.models(provider)}&refresh=true`
      : APP_CONFIG.aiSettings.models(provider);
    return this.apiClient.get<AiModelListResult>(url);
  }
}
