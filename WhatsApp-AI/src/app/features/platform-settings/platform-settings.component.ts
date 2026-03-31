import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { SelectModule } from 'primeng/select';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { TagModule } from 'primeng/tag';
import { DividerModule } from 'primeng/divider';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { TooltipModule } from 'primeng/tooltip';
import { ToastModule } from 'primeng/toast';
import { PlatformSettingsService, PlatformMetaConfigDto } from './platform-settings.service';
import { AiSettingsService, AiConfigResult, AiModelInfo, SaveAiConfigRequest } from '../ai-settings/ai-settings.service';
import { ToastService } from '../../core/ui/toast.service';

const GRAPH_VERSIONS = ['25.0', '21.0', '20.0', '19.0', '18.0'];

@Component({
  selector: 'app-platform-settings',
  standalone: true,
  templateUrl: './platform-settings.component.html',
  styleUrls: ['./platform-settings.component.scss'],
  imports: [
    CommonModule,
    ReactiveFormsModule,
    ButtonModule,
    DialogModule,
    InputTextModule,
    InputNumberModule,
    SelectModule,
    ToggleSwitchModule,
    TagModule,
    DividerModule,
    ProgressSpinnerModule,
    TooltipModule,
    ToastModule,
  ],
})
export class PlatformSettingsComponent implements OnInit {
  private readonly metaService = inject(PlatformSettingsService);
  private readonly aiService = inject(AiSettingsService);
  private readonly toast = inject(ToastService);
  private readonly fb = inject(FormBuilder);

  // ── Page state ────────────────────────────────────────────────────────────
  readonly loading = signal(true);
  readonly aiConfig = signal<AiConfigResult | null>(null);
  readonly metaConfig = signal<PlatformMetaConfigDto | null>(null);

  // ── Model lists ───────────────────────────────────────────────────────────
  readonly models = signal<AiModelInfo[]>([]);
  readonly loadingModels = signal(false);
  readonly modelOptions = computed(() =>
    this.models().map(m => ({
      label: m.label + (m.isPreview ? ' (preview)' : ''),
      value: m.name,
    }))
  );

  // ── Dialogs ───────────────────────────────────────────────────────────────
  readonly showAiDialog = signal(false);
  readonly showMessagingDialog = signal(false);
  readonly showImagePipelineDialog = signal(false);
  readonly showMetaDialog = signal(false);

  // ── Saving ────────────────────────────────────────────────────────────────
  readonly savingAi = signal(false);
  readonly savingMessaging = signal(false);
  readonly savingImagePipeline = signal(false);
  readonly savingMeta = signal(false);

  // ── Secret visibility ─────────────────────────────────────────────────────
  readonly showGeminiKey = signal(false);
  readonly showMetaSecret = signal(false);

  readonly graphVersions = GRAPH_VERSIONS;

  // ── Forms ─────────────────────────────────────────────────────────────────
  aiForm!: FormGroup;
  messagingForm!: FormGroup;
  imagePipelineForm!: FormGroup;
  metaForm!: FormGroup;

  constructor() {
    this.aiForm = this.fb.group({
      geminiModel: [null],
      geminiApiKey: [''],
      debugModeEnabled: [true],
      requestTimeoutSeconds: [60, [Validators.required, Validators.min(10), Validators.max(300)]],
      enableToolCalling: [false],
      enableStructuredOutput: [false],
      temperature: [null],
      topP: [null],
      maxTokens: [null],
    });

    this.messagingForm = this.fb.group({
      messagingModelName: [null],
    });

    this.imagePipelineForm = this.fb.group({
      visionModelName: [null],
      imageGenerationModelName: [null],
    });

    this.metaForm = this.fb.group({
      appId: ['', Validators.required],
      loginConfigurationId: [''],
      appSecret: [''],
      graphVersion: ['25.0', Validators.required],
      callbackBaseUrl: ['', Validators.required],
    });
  }

  ngOnInit(): void {
    this.loadAll();
  }

  // ── Card status helpers ────────────────────────────────────────────────────
  get aiConfigured(): boolean { return this.aiConfig()?.isConfigured ?? false; }
  get aiStatusLabel(): string {
    const cfg = this.aiConfig();
    if (!cfg?.isConfigured) return 'Not configured';
    const key = cfg.geminiApiKeySet ? 'API key set' : 'No API key';
    const model = cfg.geminiModel ?? 'No base model';
    return `${model} · ${key}`;
  }

  get messagingModelLabel(): string {
    const m = this.aiConfig()?.messagingModelName;
    return m ? m : 'Using base model (fallback)';
  }
  get messagingConfigured(): boolean { return !!this.aiConfig()?.messagingModelName; }

  get imagePipelineLabel(): string {
    const cfg = this.aiConfig();
    if (!cfg?.isConfigured) return 'Not configured';
    const parts: string[] = [];
    if (cfg.visionModelName) parts.push('Vision: ' + cfg.visionModelName);
    if (cfg.imageGenerationModelName) parts.push('Gen: ' + cfg.imageGenerationModelName);
    return parts.length ? parts.join(' · ') : 'Default models';
  }
  get imagePipelineConfigured(): boolean {
    const cfg = this.aiConfig();
    return !!(cfg?.visionModelName || cfg?.imageGenerationModelName);
  }

  get metaConfigured(): boolean { return this.metaConfig()?.isConfigured ?? false; }
  get metaStatusLabel(): string {
    const cfg = this.metaConfig();
    if (!cfg?.isConfigured) return 'Not configured';
    return `App ID: ${cfg.appId}`;
  }

  // ── Dialog openers ─────────────────────────────────────────────────────────
  openAiDialog(): void {
    const cfg = this.aiConfig();
    this.aiForm.patchValue({
      geminiModel: cfg?.geminiModel ?? null,
      geminiApiKey: '',
      debugModeEnabled: cfg?.debugModeEnabled ?? true,
      requestTimeoutSeconds: cfg?.requestTimeoutSeconds ?? 60,
      enableToolCalling: cfg?.enableToolCalling ?? false,
      enableStructuredOutput: cfg?.enableStructuredOutput ?? false,
      temperature: cfg?.temperature ?? null,
      topP: cfg?.topP ?? null,
      maxTokens: cfg?.maxTokens ?? null,
    });
    this.ensureModelsLoaded();
    this.showAiDialog.set(true);
  }

  openMessagingDialog(): void {
    this.messagingForm.patchValue({ messagingModelName: this.aiConfig()?.messagingModelName ?? null });
    this.ensureModelsLoaded();
    this.showMessagingDialog.set(true);
  }

  openImagePipelineDialog(): void {
    const cfg = this.aiConfig();
    this.imagePipelineForm.patchValue({
      visionModelName: cfg?.visionModelName ?? null,
      imageGenerationModelName: cfg?.imageGenerationModelName ?? null,
    });
    this.ensureModelsLoaded();
    this.showImagePipelineDialog.set(true);
  }

  openMetaDialog(): void {
    const cfg = this.metaConfig();
    this.metaForm.patchValue({
      appId: cfg?.appId ?? '',
      loginConfigurationId: cfg?.loginConfigurationId ?? '',
      appSecret: '',
      graphVersion: cfg?.graphVersion ?? '25.0',
      callbackBaseUrl: cfg?.callbackBaseUrl ?? '',
    });
    this.showMetaDialog.set(true);
  }

  // ── Save methods ───────────────────────────────────────────────────────────
  saveAiProvider(): void {
    if (this.savingAi()) return;
    const existing = this.aiConfig();
    const v = this.aiForm.value;
    this.savingAi.set(true);
    this.aiService.saveConfig({
      activeProvider: 'Gemini',
      debugModeEnabled: v.debugModeEnabled,
      ollamaEndpoint: null,
      ollamaModel: null,
      openAIModel: null,
      openAIApiKey: null,
      geminiModel: v.geminiModel ?? null,
      geminiApiKey: v.geminiApiKey?.trim() || null,
      requestTimeoutSeconds: v.requestTimeoutSeconds,
      enableToolCalling: v.enableToolCalling ?? false,
      enableStructuredOutput: v.enableStructuredOutput ?? false,
      temperature: v.temperature ?? null,
      topP: v.topP ?? null,
      maxTokens: v.maxTokens ?? null,
      visionModelName: existing?.visionModelName ?? null,
      imageGenerationModelName: existing?.imageGenerationModelName ?? null,
      messagingModelName: existing?.messagingModelName ?? null,
    }).subscribe({
      next: (saved) => {
        this.aiConfig.set(saved);
        this.savingAi.set(false);
        this.showAiDialog.set(false);
        this.toast.success('Saved', 'AI provider settings updated.');
      },
      error: (err) => {
        this.savingAi.set(false);
        this.toast.error('Save Failed', err?.error?.detail ?? 'Unable to save AI configuration.');
      },
    });
  }

  saveMessaging(): void {
    if (this.savingMessaging()) return;
    const v = this.messagingForm.value;
    this.savingMessaging.set(true);
    this.aiService.saveConfig(this.mergeRequest({ messagingModelName: v.messagingModelName || null }))
      .subscribe({
        next: (saved) => {
          this.aiConfig.set(saved);
          this.savingMessaging.set(false);
          this.showMessagingDialog.set(false);
          this.toast.success('Saved', 'Messaging model updated.');
        },
        error: () => {
          this.savingMessaging.set(false);
          this.toast.error('Save Failed', 'Unable to save messaging settings.');
        },
      });
  }

  saveImagePipeline(): void {
    if (this.savingImagePipeline()) return;
    const v = this.imagePipelineForm.value;
    this.savingImagePipeline.set(true);
    this.aiService.saveConfig(this.mergeRequest({
      visionModelName: v.visionModelName || null,
      imageGenerationModelName: v.imageGenerationModelName || null,
    })).subscribe({
      next: (saved) => {
        this.aiConfig.set(saved);
        this.savingImagePipeline.set(false);
        this.showImagePipelineDialog.set(false);
        this.toast.success('Saved', 'Image pipeline models updated.');
      },
      error: () => {
        this.savingImagePipeline.set(false);
        this.toast.error('Save Failed', 'Unable to save image pipeline settings.');
      },
    });
  }

  saveMeta(): void {
    if (this.savingMeta()) return;
    const cfg = this.metaConfig();
    const v = this.metaForm.value;
    const appSecret = v.appSecret?.trim() || null;

    if (!cfg?.isConfigured && !appSecret) {
      this.metaForm.get('appSecret')?.setErrors({ required: true });
      this.metaForm.get('appSecret')?.markAsTouched();
      return;
    }
    if (this.metaForm.invalid) { this.metaForm.markAllAsTouched(); return; }

    this.savingMeta.set(true);
    this.metaService.saveMetaConfig({
      appId: v.appId.trim(),
      appSecret,
      loginConfigurationId: v.loginConfigurationId?.trim() || null,
      graphVersion: v.graphVersion,
      callbackBaseUrl: v.callbackBaseUrl.trim(),
    }).subscribe({
      next: (saved) => {
        this.metaConfig.set(saved);
        this.metaForm.patchValue({ appSecret: '' });
        this.savingMeta.set(false);
        this.showMetaDialog.set(false);
        this.toast.success('Saved', 'Meta app credentials updated.');
      },
      error: () => {
        this.savingMeta.set(false);
        this.toast.error('Save Failed', 'Unable to save Meta configuration.');
      },
    });
  }

  refreshModels(): void {
    this.loadingModels.set(true);
    this.aiService.getModels('Gemini', true).subscribe({
      next: (r) => { this.models.set(r.models); this.loadingModels.set(false); },
      error: () => this.loadingModels.set(false),
    });
  }

  callbackPreview(): string {
    const base = this.metaForm.get('callbackBaseUrl')?.value?.trim();
    return base ? `${base.replace(/\/$/, '')}/api/integrations/meta/callback` : '';
  }

  formatDate(iso: string): string {
    return new Date(iso).toLocaleString('en-US', {
      month: 'short', day: 'numeric', year: 'numeric',
      hour: '2-digit', minute: '2-digit',
    });
  }

  // ── Helpers ────────────────────────────────────────────────────────────────
  private loadAll(): void {
    this.loading.set(true);
    let aiDone = false, metaDone = false;
    const tryDone = () => { if (aiDone && metaDone) this.loading.set(false); };

    this.aiService.getConfig().subscribe({
      next: (cfg) => { this.aiConfig.set(cfg); aiDone = true; tryDone(); },
      error: () => { aiDone = true; tryDone(); },
    });
    this.metaService.getMetaConfig().subscribe({
      next: (cfg) => { this.metaConfig.set(cfg); metaDone = true; tryDone(); },
      error: () => { metaDone = true; tryDone(); },
    });
  }

  private ensureModelsLoaded(): void {
    if (this.models().length > 0) return;
    this.loadingModels.set(true);
    this.aiService.getModels('Gemini').subscribe({
      next: (r) => { this.models.set(r.models); this.loadingModels.set(false); },
      error: () => this.loadingModels.set(false),
    });
  }

  private mergeRequest(overrides: Partial<SaveAiConfigRequest>): SaveAiConfigRequest {
    const e = this.aiConfig();
    return {
      activeProvider: e?.activeProvider ?? 'Gemini',
      debugModeEnabled: e?.debugModeEnabled ?? true,
      ollamaEndpoint: null,
      ollamaModel: null,
      openAIModel: null,
      openAIApiKey: null,
      geminiModel: e?.geminiModel ?? null,
      geminiApiKey: null,
      requestTimeoutSeconds: e?.requestTimeoutSeconds ?? 60,
      enableToolCalling: e?.enableToolCalling ?? false,
      enableStructuredOutput: e?.enableStructuredOutput ?? false,
      temperature: e?.temperature ?? null,
      topP: e?.topP ?? null,
      maxTokens: e?.maxTokens ?? null,
      visionModelName: e?.visionModelName ?? null,
      imageGenerationModelName: e?.imageGenerationModelName ?? null,
      messagingModelName: e?.messagingModelName ?? null,
      ...overrides,
    };
  }
}
