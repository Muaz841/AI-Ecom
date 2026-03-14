import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { map } from 'rxjs';

@Component({
  selector: 'app-dashboard-placeholder',
  standalone: true,
  templateUrl: './dashboard-placeholder.component.html',
  styleUrls: ['./dashboard-placeholder.component.scss'],
  imports: [CommonModule],
})
export class DashboardPlaceholderComponent {
  private readonly route = inject(ActivatedRoute);
  readonly title$ = this.route.data.pipe(map((data) => (data['title'] as string) ?? 'Dashboard'));
  readonly subtitle$ = this.route.data.pipe(map((data) => (data['subtitle'] as string) ?? 'Tenant workspace overview.'));
}
