import { Component, OnInit, inject, signal, computed, DestroyRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { ButtonModule }    from 'primeng/button';
import { CardModule }      from 'primeng/card';
import { DialogModule }    from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule }  from 'primeng/password';
import { SelectModule }    from 'primeng/select';
import { TableModule }     from 'primeng/table';
import { TagModule }       from 'primeng/tag';
import { TooltipModule }   from 'primeng/tooltip';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { TextareaModule }  from 'primeng/textarea';
import { DividerModule }   from 'primeng/divider';
import { ProgressSpinnerModule } from 'primeng/progressspinner';

import { ToastService } from '../../core/ui/toast.service';
import { MetaAdsService } from './meta-ads.service';
import {
  MarketingConfig,
  KnowledgeChunk,
  AgentDecision,
  CLAUDE_MODELS,
  DECISION_STATUS_LABELS,
} from './meta-ads.models';

@Component({
  selector: 'app-meta-ads',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    ButtonModule,
    CardModule,
    DialogModule,
    InputTextModule,
    PasswordModule,
    SelectModule,
    TableModule,
    TagModule,
    TooltipModule,
    ToggleSwitchModule,
    TextareaModule,
    DividerModule,
    ProgressSpinnerModule,
  ],
  templateUrl: './meta-ads.component.html',
  styleUrls: ['./meta-ads.component.scss'],
})
export class MetaAdsComponent implements OnInit {
  private readonly service    = inject(MetaAdsService);
  private readonly toast      = inject(ToastService);
  private readonly fb         = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);

  // ── State signals ─────────────────────────────────────────────────────────
  config       = signal<MarketingConfig | null>(null);
  knowledge    = signal<KnowledgeChunk[]>([]);
  decisions    = signal<AgentDecision[]>([]);

  loadingConfig    = signal(false);
  loadingKnowledge = signal(false);
  loadingDecisions = signal(false);
  savingConfig     = signal(false);
  savingKnowledge  = signal(false);

  settingsVisible  = signal(false);
  knowledgeVisible = signal(false);
  editingChunk     = signal<KnowledgeChunk | null>(null);

  // ── Computed ──────────────────────────────────────────────────────────────
  pendingDecisions = computed(() => this.decisions().filter(d => d.status === 'PendingApproval'));
  hasPending       = computed(() => this.pendingDecisions().length > 0);
  isConfigured     = computed(() => this.config()?.isConfigured ?? false);

  readonly claudeModels   = CLAUDE_MODELS;
  readonly statusLabels   = DECISION_STATUS_LABELS;
  readonly decisionModels = CLAUDE_MODELS.filter(m => m.value !== 'claude-haiku-4-5-20251001');
  readonly summaryModels  = CLAUDE_MODELS;

  // ── Forms ─────────────────────────────────────────────────────────────────
  settingsForm!:  FormGroup;
  knowledgeForm!: FormGroup;

  ngOnInit(): void {
    this.initForms();
    this.loadAll();
  }

  private initForms(): void {
    this.settingsForm = this.fb.group({
      claudeApiKey:        [null],
      claudeDecisionModel: ['claude-opus-4-6', Validators.required],
      claudeSummaryModel:  ['claude-haiku-4-5-20251001', Validators.required],
      metaAdsAccountId:    [null],
      metaAdsAccessToken:  [null],
      dryRun:              [true],
      maxActionsPerDay:    [10, [Validators.required, Validators.min(1), Validators.max(50)]],
      dailySpendCapUsd:    [100, [Validators.required, Validators.min(1)]],
    });

    this.knowledgeForm = this.fb.group({
      title:   ['', [Validators.required, Validators.minLength(2), Validators.maxLength(300)]],
      content: ['', [Validators.required, Validators.minLength(10)]],
      source:  [null],
    });
  }

  private loadAll(): void {
    this.loadConfig();
    this.loadKnowledge();
    this.loadDecisions();
  }

  // ── Config ────────────────────────────────────────────────────────────────

  loadConfig(): void {
    this.loadingConfig.set(true);
    this.service.getConfig()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (cfg) => {
          this.config.set(cfg);
          this.patchSettingsForm(cfg);
          this.loadingConfig.set(false);
        },
        error: () => {
          this.loadingConfig.set(false);
        },
      });
  }

  private patchSettingsForm(cfg: MarketingConfig): void {
    this.settingsForm.patchValue({
      claudeApiKey:        cfg.claudeApiKeyMasked ?? null,
      claudeDecisionModel: cfg.claudeDecisionModel,
      claudeSummaryModel:  cfg.claudeSummaryModel,
      metaAdsAccountId:    cfg.metaAdsAccountId ?? null,
      metaAdsAccessToken:  cfg.metaAdsAccessTokenMasked ?? null,
      dryRun:              cfg.dryRun,
      maxActionsPerDay:    cfg.maxActionsPerDay,
      dailySpendCapUsd:    cfg.dailySpendCapUsd,
    });
  }

  openSettings(): void {
    this.settingsVisible.set(true);
  }

  saveSettings(): void {
    if (this.settingsForm.invalid) return;
    this.savingConfig.set(true);
    const v = this.settingsForm.value;
    this.service.saveConfig({
      claudeApiKey:        v.claudeApiKey,
      claudeDecisionModel: v.claudeDecisionModel,
      claudeSummaryModel:  v.claudeSummaryModel,
      metaAdsAccountId:    v.metaAdsAccountId,
      metaAdsAccessToken:  v.metaAdsAccessToken,
      dryRun:              v.dryRun,
      maxActionsPerDay:    v.maxActionsPerDay,
      dailySpendCapUsd:    v.dailySpendCapUsd,
    }).pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (cfg) => {
          this.config.set(cfg);
          this.patchSettingsForm(cfg);
          this.savingConfig.set(false);
          this.settingsVisible.set(false);
          this.toast.success('Settings saved', 'Marketing Engine configuration updated.');
        },
        error: () => {
          this.savingConfig.set(false);
          this.toast.error('Save failed', 'Could not save settings. Please try again.');
        },
      });
  }

  // ── Knowledge ─────────────────────────────────────────────────────────────

  loadKnowledge(): void {
    this.loadingKnowledge.set(true);
    this.service.getKnowledge()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (chunks) => {
          this.knowledge.set(chunks);
          this.loadingKnowledge.set(false);
        },
        error: () => this.loadingKnowledge.set(false),
      });
  }

  openAddKnowledge(): void {
    this.editingChunk.set(null);
    this.knowledgeForm.reset({ title: '', content: '', source: null });
    this.knowledgeVisible.set(true);
  }

  openEditKnowledge(chunk: KnowledgeChunk): void {
    this.editingChunk.set(chunk);
    this.knowledgeForm.patchValue({
      title:   chunk.title,
      content: chunk.content,
      source:  chunk.source,
    });
    this.knowledgeVisible.set(true);
  }

  saveKnowledge(): void {
    if (this.knowledgeForm.invalid) return;
    this.savingKnowledge.set(true);
    const v = this.knowledgeForm.value;
    const editing = this.editingChunk();

    const request = { title: v.title, content: v.content, source: v.source || null };
    const call$ = editing
      ? this.service.updateKnowledge(editing.id, request)
      : this.service.addKnowledge(request);

    call$.pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.loadKnowledge();
          this.savingKnowledge.set(false);
          this.knowledgeVisible.set(false);
          this.toast.success('Saved', editing ? 'Knowledge updated.' : 'Knowledge added to the engine.');
        },
        error: () => {
          this.savingKnowledge.set(false);
          this.toast.error('Save failed', 'Could not save knowledge. Please try again.');
        },
      });
  }

  deleteKnowledge(chunk: KnowledgeChunk): void {
    this.service.deleteKnowledge(chunk.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.knowledge.update(list => list.filter(c => c.id !== chunk.id));
          this.toast.success('Deleted', 'Knowledge chunk removed.');
        },
        error: () => this.toast.error('Delete failed', 'Could not remove knowledge chunk.'),
      });
  }

  // ── Decisions ─────────────────────────────────────────────────────────────

  loadDecisions(): void {
    this.loadingDecisions.set(true);
    this.service.getDecisions()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (d) => {
          this.decisions.set(d);
          this.loadingDecisions.set(false);
        },
        error: () => this.loadingDecisions.set(false),
      });
  }

  approveDecision(decision: AgentDecision): void {
    this.service.approveDecision(decision.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.loadDecisions();
          this.toast.success('Approved', 'Decision approved and queued for execution.');
        },
        error: () => this.toast.error('Failed', 'Could not approve decision.'),
      });
  }

  rejectDecision(decision: AgentDecision): void {
    this.service.rejectDecision(decision.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.loadDecisions();
          this.toast.success('Rejected', 'Decision rejected.');
        },
        error: () => this.toast.error('Failed', 'Could not reject decision.'),
      });
  }

  // ── Helpers ──────────────────────────────────────────────────────────────

  getStatusLabel(status: string): { label: string; severity: 'success' | 'secondary' | 'info' | 'warn' | 'danger' | 'contrast' } {
    return this.statusLabels[status] ?? { label: status, severity: 'secondary' as const };
  }

  confidenceClass(confidence: number): string {
    if (confidence >= 0.8) return 'conf-high';
    if (confidence >= 0.6) return 'conf-mid';
    return 'conf-low';
  }

  formatDate(dateStr: string | null): string {
    if (!dateStr) return '—';
    return new Date(dateStr).toLocaleString('en-PK', { dateStyle: 'medium', timeStyle: 'short' });
  }
}
