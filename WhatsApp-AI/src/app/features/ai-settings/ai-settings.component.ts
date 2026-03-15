import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { SelectModule } from 'primeng/select';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
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
  { label: 'Mock (Debug)', value: 'Mock' },
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
      openAIModel: ['gpt-4o-mini', Validators.required],
      openAIApiKey: [''],
      geminiModel: ['gemini-1.5-flash', Validators.required],
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
  get isMock(): boolean  { return this.activeProvider === 'Mock'; }

  loadModels(provider: string, refresh = false): void {
    if (provider === 'Mock' || provider === 'Ollama') { this.models.set([]); return; }
    this.loadingModels.set(true);
    this.service.getModels(provider, refresh).subscribe({
      next: (result) => {
        this.models.set(result.models);
        this.loadingModels.set(false);
      },
      error: () => {
        this.models.set([]);
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

    this.service.saveConfig({
      activeProvider: v.activeProvider,
      debugModeEnabled: v.debugModeEnabled,
      ollamaEndpoint: v.ollamaEndpoint?.trim(),
      ollamaModel: v.ollamaModel?.trim(),
      openAIModel: v.openAIModel?.trim(),
      openAIApiKey: v.openAIApiKey?.trim() || null,
      geminiModel: v.geminiModel?.trim(),
      geminiApiKey: v.geminiApiKey?.trim() || null,
      requestTimeoutSeconds: v.requestTimeoutSeconds,
      enableToolCalling: v.enableToolCalling ?? false,
      enableStructuredOutput: v.enableStructuredOutput ?? false,
      temperature: v.temperature ?? null,
      topP: v.topP ?? null,
      maxTokens: v.maxTokens ?? null,
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
      openAIModel: cfg.openAIModel,
      openAIApiKey: '',
      geminiModel: cfg.geminiModel,
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
