import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ProgressSpinnerModule } from 'primeng/progressspinner';

type CallbackState = 'loading' | 'success' | 'error';

@Component({
  selector: 'app-meta-oauth-callback',
  standalone: true,
  imports: [CommonModule, ProgressSpinnerModule],
  template: `
    <div class="callback-page">
      <div class="callback-card">

        @if (state === 'loading') {
          <div class="icon-wrap loading">
            <p-progressSpinner strokeWidth="3" styleClass="callback-spinner"></p-progressSpinner>
          </div>
          <h2>Finalizing connection...</h2>
          <p>Please wait while we complete the Meta authorization.</p>
        }

        @if (state === 'success') {
          <div class="icon-wrap success">
            <i class="pi pi-check-circle"></i>
          </div>
          <h2>Channel Connected</h2>
          <p>Your Meta channel was connected successfully. Redirecting you back...</p>
        }

        @if (state === 'error') {
          <div class="icon-wrap error">
            <i class="pi pi-times-circle"></i>
          </div>
          <h2>Connection Failed</h2>
          <p class="error-reason">{{ reason }}</p>
          <button class="retry-btn" (click)="goToIntegrations()">
            <i class="pi pi-arrow-left"></i>
            Back to Integrations
          </button>
        }

      </div>
    </div>
  `,
  styles: [`
    .callback-page {
      min-height: 100vh;
      display: grid;
      place-items: center;
      background: var(--bg);
      padding: 2rem;
    }

    .callback-card {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 1rem;
      text-align: center;
      background: linear-gradient(145deg, var(--surface-glass-1), var(--surface-glass-2));
      border: 1px solid var(--line);
      border-radius: 1.25rem;
      padding: 3rem 2.5rem;
      max-width: 26rem;
      width: 100%;
      box-shadow: 0 24px 48px rgba(2, 6, 23, 0.3);
    }

    .icon-wrap {
      width: 4.5rem;
      height: 4.5rem;
      border-radius: 50%;
      display: grid;
      place-items: center;
      margin-bottom: 0.5rem;

      i { font-size: 2.2rem; }

      &.success {
        background: rgba(16, 185, 129, 0.12);
        border: 1px solid rgba(16, 185, 129, 0.3);
        i { color: #10b981; }
      }

      &.error {
        background: rgba(239, 68, 68, 0.1);
        border: 1px solid rgba(239, 68, 68, 0.3);
        i { color: #ef4444; }
      }

      &.loading {
        background: rgba(148, 163, 184, 0.08);
        border: 1px solid var(--line);
      }
    }

    h2 {
      margin: 0;
      font-size: 1.25rem;
      font-weight: 700;
      color: var(--text-primary);
    }

    p {
      margin: 0;
      font-size: 0.875rem;
      color: var(--text-secondary);
      line-height: 1.55;
    }

    .error-reason {
      color: #ef4444;
      font-size: 0.825rem;
    }

    .retry-btn {
      margin-top: 0.5rem;
      display: inline-flex;
      align-items: center;
      gap: 0.5rem;
      padding: 0.6rem 1.4rem;
      border-radius: 0.7rem;
      border: 1px solid var(--line);
      background: var(--surface-2);
      color: var(--text-primary);
      font-size: 0.875rem;
      font-weight: 600;
      cursor: pointer;
      transition: background 180ms ease, border-color 180ms ease;

      &:hover {
        background: var(--hover-soft);
        border-color: var(--accent);
      }
    }

    ::ng-deep .callback-spinner {
      width: 2.8rem !important;
      height: 2.8rem !important;
    }
  `],
})
export class MetaOauthCallbackComponent implements OnInit {
  state: CallbackState = 'loading';
  reason = 'Unable to connect channel. Please try again.';

  private redirectTimer: ReturnType<typeof setTimeout> | null = null;

  constructor(
    private readonly route: ActivatedRoute,
    private readonly router: Router,
  ) {}

  ngOnInit(): void {
    const status = this.route.snapshot.queryParamMap.get('metaConnect');
    const rawReason = this.route.snapshot.queryParamMap.get('reason');

    if (status === 'success') {
      this.state = 'success';
      this.redirectTimer = setTimeout(() => this.goToIntegrations(), 2500);
    } else if (status === 'error') {
      this.state = 'error';
      this.reason = rawReason ?? this.reason;
    } else {
      this.redirectTimer = setTimeout(() => this.goToIntegrations(), 1500);
    }
  }

  goToIntegrations(): void {
    if (this.redirectTimer) {
      clearTimeout(this.redirectTimer);
    }
    void this.router.navigateByUrl('/settings');
  }
}
