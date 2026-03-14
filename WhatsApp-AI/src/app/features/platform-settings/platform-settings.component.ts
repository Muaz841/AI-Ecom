import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { TooltipModule } from 'primeng/tooltip';
import { PlatformSettingsService, PlatformMetaConfigDto } from './platform-settings.service';
import { ToastService } from '../../core/ui/toast.service';

const GRAPH_VERSIONS = ['25.0', '21.0', '20.0', '19.0', '18.0'];

@Component({
  selector: 'app-platform-settings',
  standalone: true,
  templateUrl: './platform-settings.component.html',
  styleUrls: ['./platform-settings.component.scss'],
  imports: [CommonModule, ReactiveFormsModule, ButtonModule, InputTextModule, ProgressSpinnerModule, TooltipModule],
})
export class PlatformSettingsComponent implements OnInit {
  private readonly service = new PlatformSettingsService();
  private readonly toast: ToastService;
  private readonly fb: FormBuilder;

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly config = signal<PlatformMetaConfigDto | null>(null);
  readonly showSecret = signal(false);

  readonly graphVersions = GRAPH_VERSIONS;

  form: FormGroup;

  constructor(toast: ToastService, fb: FormBuilder) {
    this.toast = toast;
    this.fb = fb;

    this.form = this.fb.group({
      appId: ['', Validators.required],
      loginConfigurationId: [''],
      appSecret: [''],
      graphVersion: ['25.0', Validators.required],
      callbackBaseUrl: ['', Validators.required],
    });
  }

  ngOnInit(): void {
    this.service.getMetaConfig().subscribe({
      next: (cfg) => {
        this.config.set(cfg);
        this.form.patchValue({
          appId: cfg.appId,
          loginConfigurationId: cfg.loginConfigurationId ?? '',
          appSecret: '',
          graphVersion: cfg.graphVersion || '25.0',
          callbackBaseUrl: cfg.callbackBaseUrl,
        });
        this.loading.set(false);
      },
      error: () => {
        this.toast.error('Load Failed', 'Unable to load platform configuration.');
        this.loading.set(false);
      },
    });
  }

  callbackPreview(): string {
    const base = this.form.get('callbackBaseUrl')?.value?.trim();
    return base ? `${base.replace(/\/$/, '')}/api/integrations/meta/callback` : '';
  }

  isPartialUpdateValid(): boolean {
    const cfg = this.config();
    if (!cfg?.isConfigured) return false;
    const appId = this.form.get('appId')?.value?.trim();
    const graphVersion = this.form.get('graphVersion')?.value?.trim();
    const callbackBaseUrl = this.form.get('callbackBaseUrl')?.value?.trim();
    return !!appId && !!graphVersion && !!callbackBaseUrl;
  }

  save(): void {
    if (this.saving()) return;

    const cfg = this.config();
    const appSecret = this.form.get('appSecret')?.value?.trim() || null;

    if (!cfg?.isConfigured && !appSecret) {
      this.form.get('appSecret')?.setErrors({ required: true });
      this.form.get('appSecret')?.markAsTouched();
      return;
    }

    if (this.form.invalid && !this.isPartialUpdateValid()) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    const v = this.form.value;

    this.service
      .saveMetaConfig({
        appId: v.appId.trim(),
        appSecret: appSecret,
        loginConfigurationId: v.loginConfigurationId?.trim() || null,
        graphVersion: v.graphVersion,
        callbackBaseUrl: v.callbackBaseUrl.trim(),
      })
      .subscribe({
        next: (saved) => {
          this.config.set(saved);
          this.form.patchValue({ appSecret: '' });
          this.saving.set(false);
          this.toast.success('Saved', 'Meta app configuration updated successfully.');
        },
        error: () => {
          this.saving.set(false);
          this.toast.error('Save Failed', 'Unable to save configuration. Check your input and try again.');
        },
      });
  }

  resetForm(): void {
    const cfg = this.config();
    if (!cfg) return;
    this.form.patchValue({
      appId: cfg.appId,
      loginConfigurationId: cfg.loginConfigurationId ?? '',
      appSecret: '',
      graphVersion: cfg.graphVersion || '25.0',
      callbackBaseUrl: cfg.callbackBaseUrl,
    });
    this.form.markAsPristine();
    this.form.markAsUntouched();
  }

  formatDate(iso: string): string {
    return new Date(iso).toLocaleString('en-US', {
      month: 'short', day: 'numeric', year: 'numeric',
      hour: '2-digit', minute: '2-digit',
    });
  }
}
