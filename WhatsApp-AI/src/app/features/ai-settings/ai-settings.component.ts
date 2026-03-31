import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { SelectModule } from 'primeng/select';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { CardModule } from 'primeng/card';
import { DividerModule } from 'primeng/divider';
import { TagModule } from 'primeng/tag';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { TooltipModule } from 'primeng/tooltip';
import { BadgeModule } from 'primeng/badge';
import { AiSettingsService, AiConfigResult, AiModelInfo } from './ai-settings.service';
import { ToastService } from '../../core/ui/toast.service';

const AI_PROVIDERS = [
  { label: 'Ollama (Local)', value: 'Ollama' },
  { label: 'OpenAI', value: 'OpenAI' },
  { label: 'Gemini', value: 'Gemini' },
];

@Component({
  selector: 'app-ai-settings',
  standalone: true,
  templateUrl: './ai-settings.component.html',
  styleUrls: ['./ai-settings.component.scss'],
  imports: [
    CommonModule,
    ReactiveFormsModule,
    ButtonModule,
    InputTextModule,
    InputNumberModule,
    SelectModule,
    ToggleSwitchModule,
    CardModule,
    DividerModule,
    TagModule,
    ProgressSpinnerModule,
    TooltipModule,
    BadgeModule,
  ],
})
export class AiSettingsComponent implements OnInit {
  private readonly service: AiSettingsService;
  private readonly toast: ToastService;
  private readonly fb: FormBuilder;

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly loadingModels = signal(false);
  readonly modelsLoadFailed = signal(false);
  readonly config = signal<AiConfigResult | null>(null);
  readonly models = signal<AiModelInfo[]>([]);
  readonly showOpenAIKey = signal(false);
  readonly showGeminiKey = signal(false);

  readonly providerOptions = AI_PROVIDERS;

  /** Model options formatted for p-select */
  readonly modelOptions = computed(() =>
    this.models().map(m => ({
      label: m.label + (m.isPreview ? ' (preview)' : ''),
      value: m.name,
      model: m,
    }))
  );

  /** Currently selected model's capability info */
  readonly selectedModelInfo = computed<AiModelInfo | null>(() => {
    const provider = this.form?.get('activeProvider')?.value;
    const modelName = provider === 'OpenAI'
      ? this.form?.get('openAIModel')?.value
      : provider === 'Gemini'
        ? this.form?.get('geminiModel')?.value
        : null;
    if (!modelName) return null;
    return this.models().find(m => m.name === modelName) ?? null;
  });

  form: FormGroup;

  constructor(service: AiSettingsService, toast: ToastService, fb: FormBuilder) {
    this.service = service;
    this.toast = toast;
    this.fb = fb;

    this.form = this.fb.group({
      activeProvider: ['Ollama', Validators.required],
      debugModeEnabled: [true],
      ollamaEndpoint: ['http://localhost:11434', Validators.required],
      ollamaModel: ['llama3.1:8b', Validators.required],
      openAIModel: [null],
      openAIApiKey: [''],
      geminiModel: [null],
      geminiApiKey: [''],
      requestTimeoutSeconds: [60, [Validators.required, Validators.min(10), Validators.max(300)]],
      enableToolCalling: [false],
      enableStructuredOutput: [false],
      temperature: [null],
      topP: [null],
      maxTokens: [null],
    });

    // Reload model list whenever the provider changes
    this.form.get('activeProvider')!.valueChanges.subscribe(provider => {
      this.loadModels(provider);
    });
  }

  ngOnInit(): void {
    this.service.getConfig().subscribe({
      next: (cfg) => {
        this.config.set(cfg);
        this.patchForm(cfg);
        this.loading.set(false);
        this.loadModels(cfg.activeProvider);
      },
      error: () => {
        this.toast.error('Load Failed', 'Unable to load AI provider configuration.');
        this.loading.set(false);
      },
    });
  }

  get activeProvider(): string {
    return this.form.get('activeProvider')?.value ?? 'Ollama';
  }

  get isOllama(): boolean { return this.activeProvider === 'Ollama'; }
  get isOpenAI(): boolean { return this.activeProvider === 'OpenAI'; }
  get isGemini(): boolean { return this.activeProvider === 'Gemini'; }

  loadModels(provider: string, refresh = false): void {
    if (provider === 'Ollama') {
      this.models.set([]);
      this.modelsLoadFailed.set(false);
      return;
    }

    this.loadingModels.set(true);
    this.modelsLoadFailed.set(false);

    this.service.getModels(provider, refresh).subscribe({
      next: (result) => {
        let models = [...result.models];

        // If the saved model is not in the loaded list (e.g. renamed / removed by provider),
        // prepend it so the dropdown always shows the current saved value.
        const savedModel = provider === 'OpenAI'
          ? (this.form.get('openAIModel')?.value as string | null)
          : (this.form.get('geminiModel')?.value as string | null);

        if (savedModel && models.length > 0 && !models.some(m => m.name === savedModel)) {
          models.unshift({
            name: savedModel,
            label: `${savedModel} (saved — not in current list)`,
            supportsToolCalling: false,
            supportsStructuredOutput: false,
            contextWindow: 0,
            isPreview: false,
          });
        }

        this.models.set(models);
        this.loadingModels.set(false);
      },
      error: () => {
        this.models.set([]);
        this.modelsLoadFailed.set(true);
        this.loadingModels.set(false);
      },
    });
  }

  refreshModels(): void {
    this.loadModels(this.activeProvider, true);
  }

  save(): void {
    if (this.saving()) return;
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    const v = this.form.value;
    const provider: string = v.activeProvider;

    this.service.saveConfig({
      activeProvider: provider,
      debugModeEnabled: v.debugModeEnabled,
      // Only send fields for the active provider; null tells the backend to preserve existing values.
      ollamaEndpoint:  provider === 'Ollama'  ? (v.ollamaEndpoint?.trim()  ?? null) : null,
      ollamaModel:     provider === 'Ollama'  ? (v.ollamaModel?.trim()     ?? null) : null,
      openAIModel:     provider === 'OpenAI'  ? (v.openAIModel?.trim()     ?? null) : null,
      openAIApiKey:    provider === 'OpenAI'  ? (v.openAIApiKey?.trim()    || null) : null,
      geminiModel:     provider === 'Gemini'  ? (v.geminiModel?.trim()     ?? null) : null,
      geminiApiKey:    provider === 'Gemini'  ? (v.geminiApiKey?.trim()    || null) : null,
      requestTimeoutSeconds: v.requestTimeoutSeconds,
      enableToolCalling: v.enableToolCalling ?? false,
      enableStructuredOutput: v.enableStructuredOutput ?? false,
      temperature: v.temperature ?? null,
      topP: v.topP ?? null,
      maxTokens: v.maxTokens ?? null,
      // preserve model overrides managed via Platform Settings
      visionModelName: this.config()?.visionModelName ?? null,
      imageGenerationModelName: this.config()?.imageGenerationModelName ?? null,
      messagingModelName: this.config()?.messagingModelName ?? null,
    }).subscribe({
      next: (saved) => {
        this.config.set(saved);
        this.patchForm(saved);
        this.saving.set(false);
        this.toast.success('Saved', 'AI provider configuration updated successfully.');
      },
      error: (err) => {
        this.saving.set(false);
        const detail = err?.error?.detail ?? 'Unable to save AI configuration. Check your input and try again.';
        this.toast.error('Save Failed', detail);
      },
    });
  }

  resetForm(): void {
    const cfg = this.config();
    if (!cfg) return;
    this.patchForm(cfg);
    this.form.markAsPristine();
    this.form.markAsUntouched();
  }

  formatDate(iso: string): string {
    return new Date(iso).toLocaleString('en-US', {
      month: 'short', day: 'numeric', year: 'numeric',
      hour: '2-digit', minute: '2-digit',
    });
  }

  private patchForm(cfg: AiConfigResult): void {
    this.form.patchValue({
      activeProvider: cfg.activeProvider,
      debugModeEnabled: cfg.debugModeEnabled,
      ollamaEndpoint: cfg.ollamaEndpoint,
      ollamaModel: cfg.ollamaModel,
      openAIModel: cfg.openAIModel ?? null,
      openAIApiKey: '',
      geminiModel: cfg.geminiModel ?? null,
      geminiApiKey: '',
      requestTimeoutSeconds: cfg.requestTimeoutSeconds,
      enableToolCalling: cfg.enableToolCalling,
      enableStructuredOutput: cfg.enableStructuredOutput,
      temperature: cfg.temperature,
      topP: cfg.topP,
      maxTokens: cfg.maxTokens,
    });
  }
}
