import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { APP_CONFIG } from '../../core/config/app-config';

export interface TenantAIProfileResult {
  id: string;
  tenantId: string;
  systemPrompt: string;
  tone: string | null;
  language: string | null;
  brandRules: string | null;
  forbiddenTopics: string | null;
  defaultResponseStyle: string | null;
  aiCallsPerHourLimit: number;
  version: number;
  createdAt: string;
  updatedAt: string | null;
}

export interface SaveAIProfileRequest {
  systemPrompt: string;
  tone: string | null;
  language: string | null;
  brandRules: string | null;
  forbiddenTopics: string | null;
  defaultResponseStyle: string | null;
  aiCallsPerHourLimit: number;
}

@Injectable({ providedIn: 'root' })
export class AiProfileService {
  private readonly http = inject(HttpClient);
  private readonly cfg = APP_CONFIG;

  getProfile(): Observable<TenantAIProfileResult> {
    return this.http.get<TenantAIProfileResult>(
      `${this.cfg.apiBaseUrl}${this.cfg.aiProfile.get}`
    );
  }

  saveProfile(request: SaveAIProfileRequest): Observable<TenantAIProfileResult> {
    return this.http.put<TenantAIProfileResult>(
      `${this.cfg.apiBaseUrl}${this.cfg.aiProfile.save}`,
      request
    );
  }
}
