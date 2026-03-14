import { Component, OnInit, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';
import { DialogModule } from 'primeng/dialog';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { TenantManagementService, TenantDetail, TenantUser } from '../tenant-management.service';
import { ToastService } from '../../../core/ui/toast.service';

@Component({
  selector: 'app-tenant-users',
  standalone: true,
  templateUrl: './tenant-users.component.html',
  styleUrls: ['./tenant-users.component.scss'],
  imports: [CommonModule, DatePipe, ButtonModule, TagModule, DialogModule, ProgressSpinnerModule],
})
export class TenantUsersComponent implements OnInit {
  tenant = signal<TenantDetail | null>(null);
  loading = signal(false);
  showUserDialog = false;
  selectedUser = signal<TenantUser | null>(null);

  private tenantId!: string;

  constructor(
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly service: TenantManagementService,
    private readonly toast: ToastService,
  ) {}

  ngOnInit(): void {
    this.tenantId = this.route.snapshot.paramMap.get('id') ?? '';
    this.loadTenant();
  }

  goBack(): void {
    void this.router.navigate(['/host/tenants']);
  }

  openUserDetail(user: TenantUser): void {
    this.selectedUser.set(user);
    this.showUserDialog = true;
  }

  getInitials(user: TenantUser): string {
    return `${user.firstName[0] ?? ''}${user.lastName[0] ?? ''}`.toUpperCase();
  }

  private loadTenant(): void {
    if (!this.tenantId) {
      void this.router.navigate(['/host/tenants']);
      return;
    }
    this.loading.set(true);
    this.service.getTenant(this.tenantId).subscribe({
      next: (data) => {
        this.tenant.set(data);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.toast.error('Error', 'Unable to load tenant users.');
        void this.router.navigate(['/host/tenants']);
      },
    });
  }
}
