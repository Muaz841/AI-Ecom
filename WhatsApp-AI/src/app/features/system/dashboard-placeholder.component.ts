import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { map } from 'rxjs';

@Component({
  selector: 'app-dashboard-placeholder',
  standalone: true,
  imports: [CommonModule],
  template: `
    <section class="placeholder">
      <header>
        <p class="eyebrow">AutoBaat</p>
        <h1>{{ title$ | async }}</h1>
        <p>{{ subtitle$ | async }}</p>
      </header>
      <div class="grid">
        <article>
          <strong>Active conversations</strong>
          <span>0 open threads are waiting for assignment.</span>
        </article>
        <article>
          <strong>Connected channels</strong>
          <span>Manage Instagram, Facebook, and WhatsApp from Integrations.</span>
        </article>
        <article>
          <strong>Automation health</strong>
          <span>No delivery or token errors detected for this tenant.</span>
        </article>
      </div>
    </section>
  `,
  styles: [
    `
      .placeholder {
        border: 1px solid var(--line);
        border-radius: 1.1rem;
        background: rgba(15, 23, 42, 0.76);
        padding: 1.25rem;
      }

      .eyebrow {
        margin: 0;
        color: #10b981;
        font-size: 0.75rem;
        letter-spacing: 0.08em;
        text-transform: uppercase;
      }

      h1 {
        margin: 0.3rem 0 0.4rem;
        font-size: 1.6rem;
      }

      p {
        margin: 0;
        color: var(--text-secondary);
      }

      .grid {
        margin-top: 1rem;
        display: grid;
        gap: 0.7rem;
      }

      article {
        border: 1px solid var(--line);
        border-radius: 0.9rem;
        padding: 0.75rem;
        background: rgba(30, 41, 59, 0.55);
      }

      strong {
        display: block;
        margin-bottom: 0.3rem;
      }

      span {
        color: var(--text-muted);
        font-size: 0.86rem;
      }
    `,
  ],
})
export class DashboardPlaceholderComponent {
  private readonly route = inject(ActivatedRoute);
  readonly title$ = this.route.data.pipe(map((data) => (data['title'] as string) ?? 'Dashboard'));
  readonly subtitle$ = this.route.data.pipe(map((data) => (data['subtitle'] as string) ?? 'Tenant workspace overview.'));
}
