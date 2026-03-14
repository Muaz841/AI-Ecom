import { Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { TooltipModule } from 'primeng/tooltip';
import { TenantManagementService, TenantSummary } from './tenant-management.service';
import { ToastService } from '../../core/ui/toast.service';

@Component({
  selector: 'app-tenant-management',
  standalone: true,
  templateUrl: './tenant-management.component.html',
  styleUrls: ['./tenant-management.component.scss'],
  imports: [
    CommonModule,
    DatePipe,
    ReactiveFormsModule,
    ButtonModule,
    TagModule,
    DialogModule,
    InputTextModule,
    PasswordModule,
    ProgressSpinnerModule,
    TooltipModule,
  ],
})
export class TenantManagementComponent implements OnInit {
  tenants = signal<TenantSummary[]>([]);
  loading = signal(false);
  creating = signal(false);
  actionBusy = signal<string | null>(null);
  showCreateDialog = false;
  wizardStep = signal(1);
  createForm: FormGroup;

  private slugManuallyEdited = false;

  readonly progressPct = computed(() => Math.round((this.wizardStep() / 3) * 100));

  get step1Valid(): boolean {
    return ['name', 'businessName'].every((k) => this.createForm.get(k)?.valid);
  }

  get step2Valid(): boolean {
    return ['adminFirstName', 'adminLastName', 'adminEmail', 'adminPassword'].every(
      (k) => this.createForm.get(k)?.valid,
    );
  }

  constructor(
    private readonly service: TenantManagementService,
    private readonly toast: ToastService,
    private readonly fb: FormBuilder,
    private readonly router: Router,
  ) {
    this.createForm = this.fb.group({
      name: ['', [Validators.required, Validators.pattern(/^[a-z0-9-]+$/)]],
      businessName: ['', Validators.required],
      adminFirstName: ['', Validators.required],
      adminLastName: ['', Validators.required],
      adminEmail: ['', [Validators.required, Validators.email]],
      adminPassword: ['', [Validators.required, Validators.minLength(8)]],
    });

    // Auto-generate slug from business name until user edits it manually
    this.createForm.get('businessName')?.valueChanges.subscribe((value: string) => {
      if (this.slugManuallyEdited || !value) return;
      const slug = value
        .toLowerCase()
        .replace(/[^a-z0-9\s]/g, '')
        .trim()
        .replace(/\s+/g, '-')
        .replace(/-+/g, '-')
        .slice(0, 40);
      this.createForm.get('name')?.setValue(slug, { emitEvent: false });
    });
  }

  ngOnInit(): void {
    this.loadTenants();
  }

  activeCount(): number {
    return this.tenants().filter((t) => t.isActive).length;
  }

  suspendedCount(): number {
    return this.tenants().filter((t) => !t.isActive).length;
  }

  totalUsers(): number {
    return this.tenants().reduce((sum, t) => sum + t.userCount, 0);
  }

  openTenantUsers(tenant: TenantSummary): void {
    void this.router.navigate(['/host/tenants', tenant.id, 'users']);
  }

  openRbac(tenant: TenantSummary): void {
    void this.router.navigate(['/admin/rbac'], { queryParams: { tenantId: tenant.id } });
  }

  openCreateDialog(): void {
    this.createForm.reset();
    this.wizardStep.set(1);
    this.slugManuallyEdited = false;
    this.showCreateDialog = true;
  }

  closeDialog(): void {
    this.showCreateDialog = false;
  }

  onDialogHide(): void {
    this.wizardStep.set(1);
    this.slugManuallyEdited = false;
  }

  onSlugInput(): void {
    this.slugManuallyEdited = true;
  }

  nextStep(): void {
    if (this.wizardStep() === 1) {
      this.createForm.get('name')?.markAsTouched();
      this.createForm.get('businessName')?.markAsTouched();
      if (!this.step1Valid) return;
    }
    if (this.wizardStep() === 2) {
      ['adminFirstName', 'adminLastName', 'adminEmail', 'adminPassword'].forEach((k) =>
        this.createForm.get(k)?.markAsTouched(),
      );
      if (!this.step2Valid) return;
    }
    this.wizardStep.update((s) => Math.min(s + 1, 3));
  }

  prevStep(): void {
    this.wizardStep.update((s) => Math.max(s - 1, 1));
  }

  submitCreate(): void {
    if (this.createForm.invalid || this.creating()) {
      return;
    }

    this.creating.set(true);
    const v = this.createForm.value;

    this.service
      .createTenant({
        name: v.name,
        businessName: v.businessName,
        adminEmail: v.adminEmail,
        adminPassword: v.adminPassword,
        adminFirstName: v.adminFirstName,
        adminLastName: v.adminLastName,
      })
      .subscribe({
        next: (result) => {
          this.creating.set(false);
          if (result.success) {
            this.toast.success('Workspace Created', `${v.businessName} has been provisioned successfully.`);
            this.showCreateDialog = false;
            this.loadTenants();
          } else {
            this.toast.error('Creation Failed', result.errorMessage ?? 'Unable to create workspace.');
          }
        },
        error: () => {
          this.creating.set(false);
          this.toast.error('Error', 'An unexpected error occurred.');
        },
      });
  }

  suspendTenant(tenant: TenantSummary): void {
    if (this.actionBusy()) return;
    this.actionBusy.set(tenant.id);

    this.service.suspendTenant(tenant.id).subscribe({
      next: () => {
        this.actionBusy.set(null);
        this.toast.warn('Workspace Suspended', `${tenant.businessName} has been suspended.`);
        this.loadTenants();
      },
      error: () => {
        this.actionBusy.set(null);
        this.toast.error('Error', 'Unable to suspend workspace.');
      },
    });
  }

  activateTenant(tenant: TenantSummary): void {
    if (this.actionBusy()) return;
    this.actionBusy.set(tenant.id);

    this.service.activateTenant(tenant.id).subscribe({
      next: () => {
        this.actionBusy.set(null);
        this.toast.success('Workspace Activated', `${tenant.businessName} is now active.`);
        this.loadTenants();
      },
      error: () => {
        this.actionBusy.set(null);
        this.toast.error('Error', 'Unable to activate workspace.');
      },
    });
  }

  private loadTenants(): void {
    this.loading.set(true);
    this.service.listTenants().subscribe({
      next: (data) => {
        this.tenants.set(data);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.toast.error('Error', 'Unable to load workspaces.');
      },
    });
  }
}
