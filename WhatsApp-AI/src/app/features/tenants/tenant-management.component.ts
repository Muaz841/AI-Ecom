import { Component, OnInit, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
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
  template: `
    <div class="tm-page">

      <!-- Header -->
      <div class="tm-header">
        <div class="tm-header-info">
          <h1 class="tm-title">Tenant Management</h1>
          <p class="tm-subtitle">Provision and manage platform tenants. Each tenant is an isolated workspace with its own users and data.</p>
        </div>
        <button
          pButton
          label="New Tenant"
          icon="pi pi-plus"
          class="tm-create-btn"
          (click)="openCreateDialog()"
        ></button>
      </div>

      <!-- Stats bar -->
      <div class="tm-stats">
        <div class="stat-card">
          <span class="stat-value">{{ tenants().length }}</span>
          <span class="stat-label">Total Tenants</span>
        </div>
        <div class="stat-card">
          <span class="stat-value">{{ activeCount() }}</span>
          <span class="stat-label">Active</span>
        </div>
        <div class="stat-card">
          <span class="stat-value">{{ suspendedCount() }}</span>
          <span class="stat-label">Suspended</span>
        </div>
        <div class="stat-card">
          <span class="stat-value">{{ totalUsers() }}</span>
          <span class="stat-label">Total Users</span>
        </div>
      </div>

      <!-- Loading -->
      @if (loading()) {
        <div class="tm-loading">
          <p-progressSpinner strokeWidth="3" styleClass="tm-spinner"></p-progressSpinner>
          <span>Loading tenants...</span>
        </div>
      }

      <!-- Empty state -->
      @if (!loading() && tenants().length === 0) {
        <div class="tm-empty">
          <div class="empty-icon-wrap">
            <i class="pi pi-building"></i>
          </div>
          <h3>No tenants yet</h3>
          <p>Create your first tenant to get started. Each tenant gets its own isolated workspace, roles, and users.</p>
          <button pButton label="Create First Tenant" icon="pi pi-plus" (click)="openCreateDialog()"></button>
        </div>
      }

      <!-- Tenant grid -->
      @if (!loading() && tenants().length > 0) {
        <div class="tm-grid">
          @for (tenant of tenants(); track tenant.id) {
            <div class="tenant-card" [class.suspended]="!tenant.isActive">
              <div class="tc-header">
                <div class="tc-identity">
                  <div class="tc-avatar">{{ tenant.businessName[0].toUpperCase() }}</div>
                  <div class="tc-names">
                    <h3 class="tc-business">{{ tenant.businessName }}</h3>
                    <span class="tc-slug">{{ tenant.name }}</span>
                  </div>
                </div>
                <p-tag
                  [value]="tenant.isActive ? 'Active' : 'Suspended'"
                  [severity]="tenant.isActive ? 'success' : 'danger'"
                ></p-tag>
              </div>

              <div class="tc-meta">
                <div class="meta-item">
                  <i class="pi pi-users"></i>
                  <span>{{ tenant.userCount }} {{ tenant.userCount === 1 ? 'user' : 'users' }}</span>
                </div>
                <div class="meta-item">
                  <i class="pi pi-calendar"></i>
                  <span>{{ tenant.createdAt | date:'MMM d, y' }}</span>
                </div>
              </div>

              <div class="tc-actions">
                @if (tenant.isActive) {
                  <button
                    pButton
                    label="Suspend"
                    icon="pi pi-pause"
                    severity="warn"
                    outlined
                    size="small"
                    [loading]="actionBusy() === tenant.id"
                    (click)="suspendTenant(tenant)"
                    pTooltip="Prevent tenant users from logging in"
                    tooltipPosition="top"
                  ></button>
                } @else {
                  <button
                    pButton
                    label="Activate"
                    icon="pi pi-play"
                    severity="success"
                    outlined
                    size="small"
                    [loading]="actionBusy() === tenant.id"
                    (click)="activateTenant(tenant)"
                  ></button>
                }
              </div>
            </div>
          }
        </div>
      }
    </div>

    <!-- Create Tenant Dialog -->
    <p-dialog
      [(visible)]="showCreateDialog"
      [modal]="true"
      [closable]="true"
      [draggable]="false"
      [resizable]="false"
      styleClass="tm-dialog"
      header="Create New Tenant"
    >
      <form [formGroup]="createForm" (ngSubmit)="submitCreate()" class="tm-form">

        <div class="form-section-label">Tenant Information</div>

        <div class="form-row two-col">
          <div class="form-field">
            <label for="name">Tenant Slug <span class="required">*</span></label>
            <input
              id="name"
              pInputText
              formControlName="name"
              placeholder="e.g. acme-corp"
              class="w-full"
            />
            @if (createForm.get('name')?.touched && createForm.get('name')?.invalid) {
              <span class="field-error">Tenant slug is required (letters, numbers, hyphens)</span>
            }
          </div>
          <div class="form-field">
            <label for="businessName">Business Name <span class="required">*</span></label>
            <input
              id="businessName"
              pInputText
              formControlName="businessName"
              placeholder="e.g. Acme Corporation"
              class="w-full"
            />
            @if (createForm.get('businessName')?.touched && createForm.get('businessName')?.invalid) {
              <span class="field-error">Business name is required</span>
            }
          </div>
        </div>

        <div class="form-section-label">Admin Account</div>
        <p class="form-section-hint">This user will be the first admin for this tenant.</p>

        <div class="form-row two-col">
          <div class="form-field">
            <label for="adminFirstName">First Name <span class="required">*</span></label>
            <input
              id="adminFirstName"
              pInputText
              formControlName="adminFirstName"
              placeholder="John"
              class="w-full"
            />
          </div>
          <div class="form-field">
            <label for="adminLastName">Last Name <span class="required">*</span></label>
            <input
              id="adminLastName"
              pInputText
              formControlName="adminLastName"
              placeholder="Smith"
              class="w-full"
            />
          </div>
        </div>

        <div class="form-field">
          <label for="adminEmail">Admin Email <span class="required">*</span></label>
          <input
            id="adminEmail"
            pInputText
            formControlName="adminEmail"
            placeholder="admin@acmecorp.com"
            class="w-full"
          />
          @if (createForm.get('adminEmail')?.touched && createForm.get('adminEmail')?.invalid) {
            <span class="field-error">Valid email is required</span>
          }
        </div>

        <div class="form-field">
          <label for="adminPassword">Admin Password <span class="required">*</span></label>
          <p-password
            id="adminPassword"
            formControlName="adminPassword"
            placeholder="Min. 8 characters"
            [feedback]="true"
            [toggleMask]="true"
            styleClass="w-full"
            inputStyleClass="w-full"
          ></p-password>
          @if (createForm.get('adminPassword')?.touched && createForm.get('adminPassword')?.invalid) {
            <span class="field-error">Password must be at least 8 characters</span>
          }
        </div>

      </form>

      <ng-template pTemplate="footer">
        <div class="dialog-footer">
          <button pButton label="Cancel" severity="secondary" outlined (click)="showCreateDialog = false"></button>
          <button
            pButton
            label="Create Tenant"
            icon="pi pi-check"
            [loading]="creating()"
            [disabled]="createForm.invalid"
            (click)="submitCreate()"
          ></button>
        </div>
      </ng-template>
    </p-dialog>
  `,
  styles: [`
    .tm-page {
      padding: 2rem;
      max-width: 1200px;
      margin: 0 auto;
      display: flex;
      flex-direction: column;
      gap: 2rem;
    }

    /* Header */
    .tm-header {
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      gap: 1rem;
      flex-wrap: wrap;
    }

    .tm-title {
      margin: 0 0 0.35rem;
      font-size: 1.6rem;
      font-weight: 800;
      color: var(--text-primary);
      letter-spacing: -0.02em;
    }

    .tm-subtitle {
      margin: 0;
      font-size: 0.875rem;
      color: var(--text-secondary);
      max-width: 560px;
      line-height: 1.55;
    }

    .tm-create-btn {
      flex-shrink: 0;
    }

    /* Stats bar */
    .tm-stats {
      display: grid;
      grid-template-columns: repeat(4, 1fr);
      gap: 1rem;

      @media (max-width: 700px) {
        grid-template-columns: repeat(2, 1fr);
      }
    }

    .stat-card {
      background: linear-gradient(145deg, var(--surface-glass-1), var(--surface-glass-2));
      border: 1px solid var(--line);
      border-radius: 0.875rem;
      padding: 1.25rem 1.5rem;
      display: flex;
      flex-direction: column;
      gap: 0.25rem;
    }

    .stat-value {
      font-size: 2rem;
      font-weight: 800;
      color: var(--text-primary);
      line-height: 1;
    }

    .stat-label {
      font-size: 0.75rem;
      text-transform: uppercase;
      letter-spacing: 0.06em;
      color: var(--text-secondary);
      font-weight: 600;
    }

    /* Loading */
    .tm-loading {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 1rem;
      padding: 4rem;
      color: var(--text-secondary);
      font-size: 0.875rem;
    }

    ::ng-deep .tm-spinner {
      width: 2.5rem !important;
      height: 2.5rem !important;
    }

    /* Empty state */
    .tm-empty {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 1rem;
      text-align: center;
      padding: 4rem 2rem;
      background: linear-gradient(145deg, var(--surface-glass-1), var(--surface-glass-2));
      border: 1px solid var(--line);
      border-radius: 1.25rem;
    }

    .empty-icon-wrap {
      width: 5rem;
      height: 5rem;
      border-radius: 50%;
      background: rgba(148, 163, 184, 0.1);
      border: 1px solid var(--line);
      display: grid;
      place-items: center;

      i { font-size: 2rem; color: var(--text-secondary); }
    }

    .tm-empty h3 {
      margin: 0;
      font-size: 1.1rem;
      font-weight: 700;
      color: var(--text-primary);
    }

    .tm-empty p {
      margin: 0;
      font-size: 0.875rem;
      color: var(--text-secondary);
      max-width: 400px;
      line-height: 1.55;
    }

    /* Tenant grid */
    .tm-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(320px, 1fr));
      gap: 1.25rem;
    }

    .tenant-card {
      background: linear-gradient(145deg, var(--surface-glass-1), var(--surface-glass-2));
      border: 1px solid var(--line);
      border-radius: 1rem;
      padding: 1.5rem;
      display: flex;
      flex-direction: column;
      gap: 1.25rem;
      transition: border-color 200ms ease, box-shadow 200ms ease;

      &:hover {
        border-color: var(--accent);
        box-shadow: 0 8px 24px rgba(2, 6, 23, 0.2);
      }

      &.suspended {
        opacity: 0.65;
      }
    }

    /* Card header */
    .tc-header {
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      gap: 0.75rem;
    }

    .tc-identity {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      min-width: 0;
    }

    .tc-avatar {
      width: 2.75rem;
      height: 2.75rem;
      border-radius: 0.625rem;
      background: linear-gradient(135deg, var(--accent), #8b5cf6);
      display: grid;
      place-items: center;
      font-size: 1.1rem;
      font-weight: 800;
      color: #fff;
      flex-shrink: 0;
    }

    .tc-names {
      min-width: 0;
    }

    .tc-business {
      margin: 0;
      font-size: 0.975rem;
      font-weight: 700;
      color: var(--text-primary);
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }

    .tc-slug {
      font-size: 0.75rem;
      color: var(--text-secondary);
      font-family: monospace;
    }

    /* Card meta */
    .tc-meta {
      display: flex;
      gap: 1.25rem;
    }

    .meta-item {
      display: flex;
      align-items: center;
      gap: 0.4rem;
      font-size: 0.8rem;
      color: var(--text-secondary);

      i { font-size: 0.8rem; }
    }

    /* Card actions */
    .tc-actions {
      display: flex;
      gap: 0.75rem;
      padding-top: 0.25rem;
      border-top: 1px solid var(--line);
    }

    /* Dialog */
    ::ng-deep .tm-dialog {
      width: min(560px, 96vw) !important;

      .p-dialog-header {
        background: var(--surface-2);
        border-bottom: 1px solid var(--line);
        padding: 1.25rem 1.5rem;
        font-weight: 700;
        font-size: 1rem;
      }

      .p-dialog-content {
        background: var(--surface-2);
        padding: 1.5rem;
      }

      .p-dialog-footer {
        background: var(--surface-2);
        border-top: 1px solid var(--line);
        padding: 1rem 1.5rem;
      }
    }

    /* Form */
    .tm-form {
      display: flex;
      flex-direction: column;
      gap: 1.25rem;
    }

    .form-section-label {
      font-size: 0.7rem;
      text-transform: uppercase;
      letter-spacing: 0.08em;
      font-weight: 700;
      color: var(--accent);
      padding-bottom: 0.25rem;
      border-bottom: 1px solid var(--line);
    }

    .form-section-hint {
      margin: -0.75rem 0 0;
      font-size: 0.8rem;
      color: var(--text-secondary);
    }

    .form-row {
      display: flex;
      gap: 1rem;

      &.two-col > * { flex: 1; min-width: 0; }
    }

    .form-field {
      display: flex;
      flex-direction: column;
      gap: 0.4rem;

      label {
        font-size: 0.8rem;
        font-weight: 600;
        color: var(--text-secondary);
      }
    }

    .required { color: #ef4444; }

    .field-error {
      font-size: 0.75rem;
      color: #ef4444;
    }

    .dialog-footer {
      display: flex;
      justify-content: flex-end;
      gap: 0.75rem;
    }
  `],
})
export class TenantManagementComponent implements OnInit {
  tenants = signal<TenantSummary[]>([]);
  loading = signal(false);
  creating = signal(false);
  actionBusy = signal<string | null>(null);
  showCreateDialog = false;
  createForm: FormGroup;

  constructor(
    private readonly service: TenantManagementService,
    private readonly toast: ToastService,
    private readonly fb: FormBuilder,
  ) {
    this.createForm = this.fb.group({
      name: ['', [Validators.required, Validators.pattern(/^[a-z0-9-]+$/)]],
      businessName: ['', Validators.required],
      adminFirstName: ['', Validators.required],
      adminLastName: ['', Validators.required],
      adminEmail: ['', [Validators.required, Validators.email]],
      adminPassword: ['', [Validators.required, Validators.minLength(8)]],
    });
  }

  ngOnInit(): void {
    this.loadTenants();
  }

  activeCount(): number {
    return this.tenants().filter(t => t.isActive).length;
  }

  suspendedCount(): number {
    return this.tenants().filter(t => !t.isActive).length;
  }

  totalUsers(): number {
    return this.tenants().reduce((sum, t) => sum + t.userCount, 0);
  }

  openCreateDialog(): void {
    this.createForm.reset();
    this.showCreateDialog = true;
  }

  submitCreate(): void {
    if (this.createForm.invalid || this.creating()) {
      this.createForm.markAllAsTouched();
      return;
    }

    this.creating.set(true);
    const v = this.createForm.value;

    this.service.createTenant({
      name: v.name,
      businessName: v.businessName,
      adminEmail: v.adminEmail,
      adminPassword: v.adminPassword,
      adminFirstName: v.adminFirstName,
      adminLastName: v.adminLastName,
    }).subscribe({
      next: (result) => {
        this.creating.set(false);
        if (result.success) {
          this.toast.success('Tenant Created', `${v.businessName} has been provisioned successfully.`);
          this.showCreateDialog = false;
          this.loadTenants();
        } else {
          this.toast.error('Creation Failed', result.errorMessage ?? 'Unable to create tenant.');
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
        this.toast.warn('Tenant Suspended', `${tenant.businessName} has been suspended.`);
        this.loadTenants();
      },
      error: () => {
        this.actionBusy.set(null);
        this.toast.error('Error', 'Unable to suspend tenant.');
      },
    });
  }

  activateTenant(tenant: TenantSummary): void {
    if (this.actionBusy()) return;
    this.actionBusy.set(tenant.id);

    this.service.activateTenant(tenant.id).subscribe({
      next: () => {
        this.actionBusy.set(null);
        this.toast.success('Tenant Activated', `${tenant.businessName} is now active.`);
        this.loadTenants();
      },
      error: () => {
        this.actionBusy.set(null);
        this.toast.error('Error', 'Unable to activate tenant.');
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
        this.toast.error('Error', 'Unable to load tenants.');
      },
    });
  }
}
