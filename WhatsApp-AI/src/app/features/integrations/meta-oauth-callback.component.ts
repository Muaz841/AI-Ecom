import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ToastService } from '../../core/ui/toast.service';

@Component({
  selector: 'app-meta-oauth-callback',
  standalone: true,
  imports: [CommonModule],
  template: `
    <section class="callback">
      <h1>Finishing Meta Connection</h1>
      <p>{{ message }}</p>
    </section>
  `,
  styles: [
    `
      .callback {
        min-height: 100vh;
        display: grid;
        place-content: center;
        gap: 0.5rem;
        text-align: center;
      }

      h1 {
        margin: 0;
        font-size: 1.5rem;
      }

      p {
        margin: 0;
        color: #94a3b8;
      }
    `,
  ],
})
export class MetaOauthCallbackComponent implements OnInit {
  message = 'Finalizing redirect...';

  constructor(
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly toastService: ToastService,
  ) {}

  ngOnInit(): void {
    const status = this.route.snapshot.queryParamMap.get('metaConnect');
    const reason = this.route.snapshot.queryParamMap.get('reason');

    if (status === 'success') {
      this.message = 'Connection successful. Redirecting to integrations.';
      this.toastService.success('Meta connected', 'Channel connected successfully.');
    } else if (status === 'error') {
      this.message = 'Connection failed. Redirecting to integrations.';
      this.toastService.error('Meta connection failed', reason ?? 'Unable to connect channel.');
    } else {
      this.message = 'Redirecting to integrations.';
    }

    setTimeout(() => {
      void this.router.navigateByUrl('/settings');
    }, 400);
  }
}
