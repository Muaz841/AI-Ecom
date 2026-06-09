import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { APP_CONFIG } from '../../core/config/app-config';
import {
  MarketingConfig,
  SaveMarketingConfigRequest,
  KnowledgeChunk,
  AddKnowledgeRequest,
  AgentDecision,
} from './meta-ads.models';

@Injectable({ providedIn: 'root' })
export class MetaAdsService {
  private readonly http   = inject(HttpClient);
  private readonly config = APP_CONFIG;

  // ── Settings ────────────────────────────────────────────────────────────────

  getConfig(): Observable<MarketingConfig> {
    return this.http.get<MarketingConfig>(this.config.metaAds.settings.get);
  }

  saveConfig(request: SaveMarketingConfigRequest): Observable<MarketingConfig> {
    return this.http.put<MarketingConfig>(this.config.metaAds.settings.save, request);
  }

  // ── Knowledge ────────────────────────────────────────────────────────────────

  getKnowledge(): Observable<KnowledgeChunk[]> {
    return this.http.get<KnowledgeChunk[]>(this.config.metaAds.knowledge.list);
  }

  addKnowledge(request: AddKnowledgeRequest): Observable<KnowledgeChunk> {
    return this.http.post<KnowledgeChunk>(this.config.metaAds.knowledge.add, request);
  }

  updateKnowledge(id: string, request: AddKnowledgeRequest): Observable<KnowledgeChunk> {
    return this.http.put<KnowledgeChunk>(this.config.metaAds.knowledge.update(id), request);
  }

  deleteKnowledge(id: string): Observable<void> {
    return this.http.delete<void>(this.config.metaAds.knowledge.delete(id));
  }

  // ── Decisions ────────────────────────────────────────────────────────────────

  getDecisions(count = 20): Observable<AgentDecision[]> {
    return this.http.get<AgentDecision[]>(`${this.config.metaAds.decisions.list}?count=${count}`);
  }

  approveDecision(id: string): Observable<void> {
    return this.http.post<void>(this.config.metaAds.decisions.approve(id), {});
  }

  rejectDecision(id: string): Observable<void> {
    return this.http.post<void>(this.config.metaAds.decisions.reject(id), {});
  }
}
