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
  imports: [CommonModule, ReactiveFormsModule, ButtonModule, InputTextModule, ProgressSpinnerModule, TooltipModule],
  template: `
    <div class="ps-page">

      <!-- ── Header ──────────────────────────────────────────── -->
      <div class="ps-header">
        <div>
          <h1 class="ps-title">Platform Settings</h1>
          <p class="ps-subtitle">Host-level configuration applied across all tenants. Changes take effect immediately for new OAuth flows.</p>
        </div>
      </div>

      <!-- ── Loading ─────────────────────────────────────────── -->
      @if (loading()) {
        <div class="ps-loading">
          <p-progressSpinner strokeWidth="3" styleClass="ps-spinner"></p-progressSpinner>
          <span>Loading configuration…</span>
        </div>
      }

      @if (!loading()) {
        <!-- ── Status banner ─────────────────────────────────── -->
        <div class="status-banner" [class.configured]="config()?.isConfigured" [class.unconfigured]="!config()?.isConfigured">
          <div class="status-icon">
            <i [class]="config()?.isConfigured ? 'pi pi-check-circle' : 'pi pi-exclamation-triangle'"></i>
          </div>
          <div class="status-body">
            <strong>{{ config()?.isConfigured ? 'Meta App Configured' : 'Meta App Not Configured' }}</strong>
            <span>
              @if (config()?.isConfigured) {
                App credentials are active. Tenant OAuth flows will use these settings.
                @if (config()?.updatedAt) {
                  Last updated {{ formatDate(config()!.updatedAt!) }}.
                }
              } @else {
                No credentials stored. OAuth flows will fall back to appsettings.json values.
              }
            </span>
          </div>
        </div>

        <!-- ── Meta App Config card ──────────────────────────── -->
        <div class="ps-card">
          <div class="card-header">
            <div class="card-title-row">
              <div class="card-icon"><i class="pi pi-meta"></i></div>
              <div>
                <h2 class="card-title">Meta App Credentials</h2>
                <p class="card-sub">Used for WhatsApp, Instagram, and Facebook OAuth token exchange and API calls.</p>
              </div>
            </div>
          </div>

          <form [formGroup]="form" (ngSubmit)="save()" class="ps-form">

            <!-- App ID -->
            <div class="form-field">
              <label class="field-label">
                App ID <span class="required">*</span>
                <span class="field-badge">Public</span>
              </label>
              <input
                pInputText
                formControlName="appId"
                placeholder="e.g. 1304702521713674"
                class="ps-input"
                autocomplete="off"
              />
              <span class="field-hint">Your Meta App ID from the Meta Developer Portal.</span>
              @if (form.get('appId')?.touched && form.get('appId')?.invalid) {
                <span class="field-error"><i class="pi pi-exclamation-circle"></i> App ID is required</span>
              }
            </div>

            <!-- Login Configuration ID -->
            <div class="form-field">
              <label class="field-label">
                Login Configuration ID
                <span class="field-badge">Optional</span>
              </label>
              <input
                pInputText
                formControlName="loginConfigurationId"
                placeholder="e.g. 123456789012345"
                class="ps-input"
                autocomplete="off"
              />
              <span class="field-hint">
                Use this only if your app uses Facebook Login for Business. Found in Meta â†’ Login for Business â†’ Configurations.
              </span>
            </div>

            <!-- App Secret -->
            <div class="form-field">
              <label class="field-label">
                App Secret <span class="required">*</span>
                <span class="field-badge field-badge-secret">Encrypted at rest</span>
              </label>
              <div class="secret-wrap">
                <input
                  [type]="showSecret() ? 'text' : 'password'"
                  pInputText
                  formControlName="appSecret"
                  [placeholder]="config()?.isConfigured ? 'Leave empty to keep existing secret' : 'Enter your Meta App Secret'"
                  class="ps-input secret-input"
                  autocomplete="new-password"
                />
                <button
                  type="button"
                  class="secret-toggle"
                  (click)="showSecret.set(!showSecret())"
                  [pTooltip]="showSecret() ? 'Hide' : 'Show'"
                  tooltipPosition="top"
                >
                  <i [class]="showSecret() ? 'pi pi-eye-slash' : 'pi pi-eye'"></i>
                </button>
              </div>
              <span class="field-hint">
                @if (config()?.isConfigured) {
                  <i class="pi pi-lock"></i> Currently set. Leave blank to keep unchanged. Enter a new value to rotate the secret.
                } @else {
                  Find this in Meta Developer Portal → Your App → App Settings → Basic.
                }
              </span>
              @if (form.get('appSecret')?.touched && form.get('appSecret')?.invalid) {
                <span class="field-error"><i class="pi pi-exclamation-circle"></i> App Secret is required for initial setup</span>
              }
            </div>

            <!-- Graph API Version + Callback URL in a two-col row -->
            <div class="form-row two-col">
              <div class="form-field">
                <label class="field-label">Graph API Version <span class="required">*</span></label>
                <div class="version-wrap">
                  <span class="version-prefix">v</span>
                  <select formControlName="graphVersion" class="ps-select">
                    @for (v of graphVersions; track v) {
                      <option [value]="v">{{ v }}</option>
                    }
                  </select>
                </div>
                <span class="field-hint">Use the latest stable version supported by your app.</span>
              </div>

              <div class="form-field">
                <label class="field-label">Callback Base URL <span class="required">*</span></label>
                <input
                  pInputText
                  formControlName="callbackBaseUrl"
                  placeholder="https://api.yourapp.com"
                  class="ps-input"
                  autocomplete="off"
                />
                @if (callbackPreview()) {
                  <span class="field-hint">
                    <i class="pi pi-link"></i> {{ callbackPreview() }}
                  </span>
                }
                @if (form.get('callbackBaseUrl')?.touched && form.get('callbackBaseUrl')?.invalid) {
                  <span class="field-error"><i class="pi pi-exclamation-circle"></i> Callback URL is required</span>
                }
              </div>
            </div>

            <!-- Save -->
            <div class="form-actions">
              <button
                type="submit"
                pButton
                label="Save Configuration"
                icon="pi pi-check"
                [loading]="saving()"
                [disabled]="form.invalid && !isPartialUpdateValid()"
              ></button>
              <button type="button" pButton label="Reset" severity="secondary" outlined (click)="resetForm()"></button>
            </div>

          </form>
        </div>

        <!-- ── Info card ─────────────────────────────────────── -->
        <div class="info-card">
          <div class="info-row"><i class="pi pi-info-circle"></i>
            <span>The App Secret is <strong>never returned to the UI</strong> after saving. Only a masked placeholder is shown. A new value must be entered to rotate it.</span>
          </div>
          <div class="info-row"><i class="pi pi-info-circle"></i>
            <span>If no DB config exists, the system falls back to <code>MetaOAuth</code> values in <code>appsettings.json</code> automatically.</span>
          </div>
          <div class="info-row"><i class="pi pi-info-circle"></i>
            <span>The <strong>Callback Base URL</strong> must match the redirect URI registered in your Meta App Dashboard exactly.</span>
          </div>
        </div>
      }

    </div>
  `,
  styles: [`
    .ps-page {
      padding: 2rem;
      max-width: 860px;
      margin: 0 auto;
      display: flex;
      flex-direction: column;
      gap: 1.75rem;
    }

    /* ── Header ────────────────────────────────────────────── */
    .ps-title {
      margin: 0 0 0.35rem;
      font-size: 1.6rem;
      font-weight: 800;
      color: var(--text-primary);
      letter-spacing: -0.02em;
    }

    .ps-subtitle {
      margin: 0;
      font-size: 0.875rem;
      color: var(--text-secondary);
      line-height: 1.55;
    }

    /* ── Loading ───────────────────────────────────────────── */
    .ps-loading {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 1rem;
      padding: 4rem;
      color: var(--text-secondary);
      font-size: 0.875rem;
    }

    ::ng-deep .ps-spinner {
      width: 2.5rem !important;
      height: 2.5rem !important;
    }

    /* ── Status banner ─────────────────────────────────────── */
    .status-banner {
      display: flex;
      align-items: flex-start;
      gap: 1rem;
      padding: 1rem 1.25rem;
      border-radius: 0.875rem;
      border: 1px solid var(--line);
    }

    .status-banner.configured {
      background: rgba(16, 185, 129, 0.06);
      border-color: rgba(16, 185, 129, 0.3);
    }

    .status-banner.unconfigured {
      background: rgba(245, 158, 11, 0.06);
      border-color: rgba(245, 158, 11, 0.3);
    }

    .status-icon {
      flex-shrink: 0;
      width: 2rem;
      height: 2rem;
      border-radius: 50%;
      display: grid;
      place-items: center;

      i { font-size: 1.1rem; }
    }

    .configured .status-icon i { color: #10b981; }
    .unconfigured .status-icon i { color: #f59e0b; }

    .status-body {
      display: flex;
      flex-direction: column;
      gap: 0.2rem;

      strong { font-size: 0.9rem; color: var(--text-primary); }
      span   { font-size: 0.8rem; color: var(--text-secondary); line-height: 1.5; }
    }

    /* ── Card ──────────────────────────────────────────────── */
    .ps-card {
      background: linear-gradient(145deg, var(--surface-glass-1, var(--surface)), var(--surface-glass-2, var(--surface-2)));
      border: 1px solid var(--line);
      border-radius: 1rem;
      overflow: hidden;
    }

    .card-header {
      padding: 1.35rem 1.75rem;
      border-bottom: 1px solid var(--line);
    }

    .card-title-row {
      display: flex;
      align-items: center;
      gap: 1rem;
    }

    .card-icon {
      width: 2.75rem;
      height: 2.75rem;
      border-radius: 0.75rem;
      background: linear-gradient(135deg, #1877F2, #0052cc);
      display: grid;
      place-items: center;
      flex-shrink: 0;

      i { font-size: 1.15rem; color: #fff; }
    }

    .card-title {
      margin: 0 0 0.2rem;
      font-size: 1rem;
      font-weight: 700;
      color: var(--text-primary);
    }

    .card-sub {
      margin: 0;
      font-size: 0.8rem;
      color: var(--text-secondary);
    }

    /* ── Form ──────────────────────────────────────────────── */
    .ps-form {
      padding: 1.75rem;
      display: flex;
      flex-direction: column;
      gap: 1.5rem;
    }

    .form-row {
      display: flex;
      gap: 1.25rem;

      &.two-col > * { flex: 1; min-width: 0; }
    }

    .form-field {
      display: flex;
      flex-direction: column;
      gap: 0.45rem;
    }

    .field-label {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      font-size: 0.82rem;
      font-weight: 600;
      color: var(--text-secondary);
    }

    .required { color: #ef4444; }

    .field-badge {
      display: inline-flex;
      align-items: center;
      padding: 0.1rem 0.45rem;
      border-radius: 999px;
      font-size: 0.65rem;
      font-weight: 600;
      letter-spacing: 0.04em;
      text-transform: uppercase;
      background: rgba(148, 163, 184, 0.12);
      color: var(--text-muted);
    }

    .field-badge-secret {
      background: rgba(16, 185, 129, 0.1);
      color: var(--accent);
    }

    .ps-input {
      height: 44px;
      border-radius: 0.625rem !important;
      font-size: 0.9rem;
      width: 100%;
    }

    /* Secret input row */
    .secret-wrap {
      display: flex;
      align-items: stretch;
      gap: 0.5rem;

      .secret-input { flex: 1; }
    }

    .secret-toggle {
      flex-shrink: 0;
      width: 44px;
      height: 44px;
      border-radius: 0.625rem;
      border: 1px solid var(--line);
      background: var(--surface-2);
      color: var(--text-secondary);
      cursor: pointer;
      display: grid;
      place-items: center;
      transition: all 150ms ease;

      &:hover { color: var(--text-primary); border-color: var(--accent); }
    }

    /* Version selector */
    .version-wrap {
      display: flex;
      align-items: center;
      border: 1px solid var(--line);
      border-radius: 0.625rem;
      background: var(--surface-2);
      overflow: hidden;
      height: 44px;
      transition: border-color 200ms ease;

      &:focus-within { border-color: var(--accent); }
    }

    .version-prefix {
      padding: 0 0.6rem 0 0.9rem;
      font-size: 0.9rem;
      color: var(--text-muted);
      font-family: monospace;
      flex-shrink: 0;
    }

    .ps-select {
      flex: 1;
      border: none;
      outline: none;
      background: transparent;
      color: var(--text-primary);
      font-size: 0.9rem;
      padding-right: 0.75rem;
      cursor: pointer;
    }

    .field-hint {
      display: flex;
      align-items: center;
      gap: 0.35rem;
      font-size: 0.75rem;
      color: var(--text-muted);
      line-height: 1.45;

      i { font-size: 0.72rem; flex-shrink: 0; }

      code {
        background: var(--surface-2);
        padding: 0.05rem 0.35rem;
        border-radius: 0.25rem;
        font-size: 0.72rem;
        font-family: monospace;
        color: var(--accent);
      }
    }

    .field-error {
      display: flex;
      align-items: center;
      gap: 0.3rem;
      font-size: 0.75rem;
      color: #ef4444;
      i { font-size: 0.7rem; }
    }

    .form-actions {
      display: flex;
      gap: 0.75rem;
      padding-top: 0.5rem;
      border-top: 1px solid var(--line);
    }

    /* ── Info card ─────────────────────────────────────────── */
    .info-card {
      border: 1px solid var(--line);
      border-radius: 0.875rem;
      padding: 1.1rem 1.35rem;
      background: rgba(148, 163, 184, 0.04);
      display: flex;
      flex-direction: column;
      gap: 0.7rem;
    }

    .info-row {
      display: flex;
      align-items: flex-start;
      gap: 0.6rem;
      font-size: 0.8rem;
      color: var(--text-secondary);
      line-height: 1.5;

      i { font-size: 0.8rem; flex-shrink: 0; margin-top: 2px; color: var(--text-muted); }

      strong { color: var(--text-primary); }

      code {
        background: var(--surface-2);
        padding: 0.05rem 0.35rem;
        border-radius: 0.25rem;
        font-size: 0.73rem;
        font-family: monospace;
        color: var(--accent);
      }
    }
  `],
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
    // Allow save when existing config is set and appSecret is intentionally left blank.
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

    // Require a secret if this is the first-time save.
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
