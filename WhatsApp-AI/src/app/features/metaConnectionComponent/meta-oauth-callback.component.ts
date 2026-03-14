import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ProgressSpinnerModule } from 'primeng/progressspinner';

type CallbackState = 'loading' | 'success' | 'error';

@Component({
  selector: 'app-meta-oauth-callback',
  standalone: true,
  templateUrl: './meta-oauth-callback.component.html',
  styleUrls: ['./meta-oauth-callback.component.scss'],
  imports: [CommonModule, ProgressSpinnerModule],
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
