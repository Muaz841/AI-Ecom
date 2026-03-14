import { Component, OnInit, computed, signal } from '@angular/core';
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

      <!-- ── Header ──────────────────────────────────────────── -->
      <div class="tm-header">
        <div class="tm-header-info">
          <h1 class="tm-title">Tenant Management</h1>
          <p class="tm-subtitle">Provision and manage platform tenants. Each tenant is an isolated workspace with its own users and data.</p>
        </div>
        <button pButton label="New Workspace" icon="pi pi-plus" class="tm-create-btn" (click)="openCreateDialog()"></button>
      </div>

      <!-- ── Stats ───────────────────────────────────────────── -->
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

      <!-- ── Loading ─────────────────────────────────────────── -->
      @if (loading()) {
        <div class="tm-loading">
          <p-progressSpinner strokeWidth="3" styleClass="tm-spinner"></p-progressSpinner>
          <span>Loading tenants...</span>
        </div>
      }

      <!-- ── Empty state ─────────────────────────────────────── -->
      @if (!loading() && tenants().length === 0) {
        <div class="tm-empty">
          <div class="empty-icon-wrap"><i class="pi pi-building"></i></div>
          <h3>No workspaces yet</h3>
          <p>Create your first tenant workspace to get started. Each workspace is fully isolated with its own users, roles, and data.</p>
          <button pButton label="Create First Workspace" icon="pi pi-plus" (click)="openCreateDialog()"></button>
        </div>
      }

      <!-- ── Tenant grid ─────────────────────────────────────── -->
      @if (!loading() && tenants().length > 0) {
        <div class="tm-grid">
          @for (tenant of tenants(); track tenant.id) {
            <div class="tenant-card" [class.suspended]="!tenant.isActive">
              <div class="tc-header">
                <div class="tc-identity">
                  <div class="tc-avatar">{{ tenant.businessName[0].toUpperCase() }}</div>
                  <div class="tc-names">
                    <h3 class="tc-business">{{ tenant.businessName }}</h3>
                    <span class="tc-slug">{{ tenant.name }}.autobaat.com</span>
                  </div>
                </div>
                <p-tag [value]="tenant.isActive ? 'Active' : 'Suspended'" [severity]="tenant.isActive ? 'success' : 'danger'"></p-tag>
              </div>
              <div class="tc-meta">
                <div class="meta-item"><i class="pi pi-users"></i><span>{{ tenant.userCount }} {{ tenant.userCount === 1 ? 'user' : 'users' }}</span></div>
                <div class="meta-item"><i class="pi pi-calendar"></i><span>{{ tenant.createdAt | date:'MMM d, y' }}</span></div>
              </div>
              <div class="tc-actions">
                @if (tenant.isActive) {
                  <button pButton label="Suspend" icon="pi pi-pause" severity="warn" outlined size="small"
                    [loading]="actionBusy() === tenant.id" (click)="suspendTenant(tenant)"
                    pTooltip="Prevent tenant users from logging in" tooltipPosition="top"></button>
                } @else {
                  <button pButton label="Activate" icon="pi pi-play" severity="success" outlined size="small"
                    [loading]="actionBusy() === tenant.id" (click)="activateTenant(tenant)"></button>
                }
              </div>
            </div>
          }
        </div>
      }

    </div>

    <!-- ════════════════════════════════════════════════════════
         CREATE WORKSPACE — 3-STEP WIZARD DIALOG
    ════════════════════════════════════════════════════════ -->
    <p-dialog
      [(visible)]="showCreateDialog"
      [modal]="true"
      [closable]="true"
      [draggable]="false"
      [resizable]="false"
      styleClass="wiz-dialog"
      header="New Workspace"
      (onHide)="onDialogHide()"
    >

      <div class="wizard-wrap" [formGroup]="createForm">

        <!-- ── Step indicator ──────────────────────────────── -->
        <div class="step-indicator">
          <div class="step-track">
            <div class="step-node" [class.done]="wizardStep() > 1" [class.current]="wizardStep() === 1">
              @if (wizardStep() > 1) { <i class="pi pi-check"></i> } @else { <span>1</span> }
            </div>
            <div class="step-line" [class.filled]="wizardStep() > 1"></div>
            <div class="step-node" [class.done]="wizardStep() > 2" [class.current]="wizardStep() === 2">
              @if (wizardStep() > 2) { <i class="pi pi-check"></i> } @else { <span>2</span> }
            </div>
            <div class="step-line" [class.filled]="wizardStep() > 2"></div>
            <div class="step-node" [class.current]="wizardStep() === 3"><span>3</span></div>
          </div>
          <div class="step-labels">
            <span [class.active-label]="wizardStep() === 1">Workspace</span>
            <span [class.active-label]="wizardStep() === 2">Admin</span>
            <span [class.active-label]="wizardStep() === 3">Review</span>
          </div>
          <div class="progress-track">
            <div class="progress-fill" [style.width.%]="progressPct()"></div>
          </div>
        </div>

        <!-- ── Step 1: Workspace ───────────────────────────── -->
        @if (wizardStep() === 1) {
          <div class="step-content">
            <div class="step-heading">
              <h2 class="wiz-step-title">Workspace Information</h2>
              <p class="wiz-step-sub">Set up your organization's workspace identity</p>
            </div>

            <div class="form-field">
              <label class="field-label">Business Name <span class="required">*</span></label>
              <input
                pInputText
                formControlName="businessName"
                placeholder="e.g. Acme Corporation"
                class="wiz-input"
                autocomplete="organization"
              />
              @if (createForm.get('businessName')?.touched && createForm.get('businessName')?.invalid) {
                <span class="field-error"><i class="pi pi-exclamation-circle"></i> Business name is required</span>
              }
            </div>

            <div class="form-field">
              <label class="field-label">Workspace URL <span class="required">*</span></label>
              <div class="slug-wrap">
                <input
                  pInputText
                  formControlName="name"
                  placeholder="acme-corp"
                  class="wiz-input slug-input"
                  (input)="onSlugInput()"
                  autocomplete="off"
                />
                <span class="slug-suffix">.autobaat.com</span>
              </div>
              @if (createForm.get('name')?.touched && createForm.get('name')?.invalid) {
                <span class="field-error"><i class="pi pi-exclamation-circle"></i> URL must use lowercase letters, numbers, and hyphens only</span>
              }
              @if (createForm.get('name')?.valid && createForm.get('name')?.value) {
                <span class="field-hint"><i class="pi pi-link"></i> {{ createForm.get('name')?.value }}.autobaat.com</span>
              }
            </div>
          </div>
        }

        <!-- ── Step 2: Admin Account ───────────────────────── -->
        @if (wizardStep() === 2) {
          <div class="step-content">
            <div class="step-heading">
              <h2 class="wiz-step-title">Admin Account</h2>
              <p class="wiz-step-sub">This user will be the first admin for <strong>{{ createForm.get('businessName')?.value }}</strong></p>
            </div>

            <div class="form-row two-col">
              <div class="form-field">
                <label class="field-label">First Name <span class="required">*</span></label>
                <input pInputText formControlName="adminFirstName" placeholder="John" class="wiz-input" autocomplete="given-name" />
                @if (createForm.get('adminFirstName')?.touched && createForm.get('adminFirstName')?.invalid) {
                  <span class="field-error"><i class="pi pi-exclamation-circle"></i> Required</span>
                }
              </div>
              <div class="form-field">
                <label class="field-label">Last Name <span class="required">*</span></label>
                <input pInputText formControlName="adminLastName" placeholder="Smith" class="wiz-input" autocomplete="family-name" />
                @if (createForm.get('adminLastName')?.touched && createForm.get('adminLastName')?.invalid) {
                  <span class="field-error"><i class="pi pi-exclamation-circle"></i> Required</span>
                }
              </div>
            </div>

            <div class="form-field">
              <label class="field-label">Email <span class="required">*</span></label>
              <input pInputText formControlName="adminEmail" placeholder="admin@acmecorp.com" class="wiz-input" type="email" autocomplete="email" />
              @if (createForm.get('adminEmail')?.touched && createForm.get('adminEmail')?.invalid) {
                <span class="field-error"><i class="pi pi-exclamation-circle"></i> Valid email address is required</span>
              }
            </div>

            <div class="form-field">
              <label class="field-label">Password <span class="required">*</span></label>
              <p-password
                formControlName="adminPassword"
                placeholder="Min. 8 characters"
                [feedback]="true"
                [toggleMask]="true"
                class="w-full"
                inputStyleClass="wiz-input w-full"
              ></p-password>
              @if (createForm.get('adminPassword')?.touched && createForm.get('adminPassword')?.invalid) {
                <span class="field-error"><i class="pi pi-exclamation-circle"></i> Password must be at least 8 characters</span>
              }
            </div>
          </div>
        }

        <!-- ── Step 3: Review & Confirm ───────────────────── -->
        @if (wizardStep() === 3) {
          <div class="step-content">
            <div class="step-heading">
              <h2 class="wiz-step-title">Review & Create</h2>
              <p class="wiz-step-sub">Double-check your workspace setup before creating</p>
            </div>

            <div class="review-card">
              <div class="review-section">
                <div class="review-section-label">
                  <i class="pi pi-building"></i> Workspace
                </div>
                <div class="review-row">
                  <span class="rk">Business Name</span>
                  <span class="rv">{{ createForm.get('businessName')?.value }}</span>
                </div>
                <div class="review-row">
                  <span class="rk">Workspace URL</span>
                  <span class="rv rv-url">
                    <i class="pi pi-link"></i>
                    {{ createForm.get('name')?.value }}.autobaat.com
                  </span>
                </div>
              </div>

              <div class="review-divider"></div>

              <div class="review-section">
                <div class="review-section-label">
                  <i class="pi pi-user"></i> Admin Account
                </div>
                <div class="review-row">
                  <span class="rk">Name</span>
                  <span class="rv">{{ createForm.get('adminFirstName')?.value }} {{ createForm.get('adminLastName')?.value }}</span>
                </div>
                <div class="review-row">
                  <span class="rk">Email</span>
                  <span class="rv">{{ createForm.get('adminEmail')?.value }}</span>
                </div>
                <div class="review-row">
                  <span class="rk">Password</span>
                  <span class="rv rv-password">••••••••</span>
                </div>
              </div>
            </div>

            <p class="review-note">
              <i class="pi pi-info-circle"></i>
              3 default roles (tenant_admin, manager, agent) will be seeded automatically.
            </p>
          </div>
        }

      </div>

      <!-- ── Wizard Footer ──────────────────────────────────── -->
      <ng-template pTemplate="footer">
        <div class="wiz-footer">

          <span class="step-counter">Step {{ wizardStep() }} of 3</span>

          <div class="wiz-footer-actions">
            @if (wizardStep() === 1) {
              <button pButton label="Cancel" severity="secondary" outlined (click)="closeDialog()"></button>
              <button pButton label="Continue" icon="pi pi-arrow-right" iconPos="right"
                (click)="nextStep()"></button>
            }
            @if (wizardStep() === 2) {
              <button pButton label="Back" icon="pi pi-arrow-left" severity="secondary" outlined
                (click)="prevStep()"></button>
              <button pButton label="Continue" icon="pi pi-arrow-right" iconPos="right"
                (click)="nextStep()"></button>
            }
            @if (wizardStep() === 3) {
              <button pButton label="Back" icon="pi pi-arrow-left" severity="secondary" outlined
                (click)="prevStep()" [disabled]="creating()"></button>
              <button pButton label="Create Workspace" icon="pi pi-check"
                [loading]="creating()" (click)="submitCreate()"></button>
            }
          </div>

        </div>
      </ng-template>

    </p-dialog>
  `,
  styles: [`
    /* ── Page ──────────────────────────────────────────────── */
    .tm-page {
      padding: 2rem;
      max-width: 1200px;
      margin: 0 auto;
      display: flex;
      flex-direction: column;
      gap: 2rem;
    }

    /* ── Header ────────────────────────────────────────────── */
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

    .tm-create-btn { flex-shrink: 0; }

    /* ── Stats ─────────────────────────────────────────────── */
    .tm-stats {
      display: grid;
      grid-template-columns: repeat(4, 1fr);
      gap: 1rem;

      @media (max-width: 700px) { grid-template-columns: repeat(2, 1fr); }
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

    /* ── Loading ───────────────────────────────────────────── */
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

    /* ── Empty state ───────────────────────────────────────── */
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

    .tm-empty h3 { margin: 0; font-size: 1.1rem; font-weight: 700; color: var(--text-primary); }
    .tm-empty p  { margin: 0; font-size: 0.875rem; color: var(--text-secondary); max-width: 400px; line-height: 1.55; }

    /* ── Tenant grid ───────────────────────────────────────── */
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

      &:hover { border-color: var(--accent); box-shadow: 0 8px 24px rgba(2, 6, 23, 0.2); }
      &.suspended { opacity: 0.65; }
    }

    .tc-header { display: flex; align-items: flex-start; justify-content: space-between; gap: 0.75rem; }
    .tc-identity { display: flex; align-items: center; gap: 0.75rem; min-width: 0; }

    .tc-avatar {
      width: 2.75rem; height: 2.75rem; border-radius: 0.625rem;
      background: linear-gradient(135deg, var(--accent), #8b5cf6);
      display: grid; place-items: center;
      font-size: 1.1rem; font-weight: 800; color: #fff; flex-shrink: 0;
    }

    .tc-names { min-width: 0; }

    .tc-business {
      margin: 0; font-size: 0.975rem; font-weight: 700; color: var(--text-primary);
      white-space: nowrap; overflow: hidden; text-overflow: ellipsis;
    }

    .tc-slug { font-size: 0.72rem; color: var(--text-secondary); font-family: monospace; }
    .tc-meta { display: flex; gap: 1.25rem; }

    .meta-item {
      display: flex; align-items: center; gap: 0.4rem;
      font-size: 0.8rem; color: var(--text-secondary);
      i { font-size: 0.8rem; }
    }

    .tc-actions { display: flex; gap: 0.75rem; padding-top: 0.25rem; border-top: 1px solid var(--line); }

    /* ════════════════════════════════════════════════════════
       WIZARD DIALOG
    ════════════════════════════════════════════════════════ */

    ::ng-deep .wiz-dialog {
      width: min(640px, 96vw) !important;

      .p-dialog-header {
        background: var(--surface);
        border-bottom: 1px solid var(--line);
        padding: 1.25rem 1.75rem;
        font-weight: 700;
        font-size: 1rem;
        letter-spacing: -0.01em;
      }

      .p-dialog-content {
        background: var(--surface);
        padding: 0;
      }

      .p-dialog-footer {
        background: var(--surface);
        border-top: 1px solid var(--line);
        padding: 0;
      }
    }

    /* ── Wizard layout ─────────────────────────────────────── */
    .wizard-wrap {
      display: flex;
      flex-direction: column;
    }

    /* ── Step indicator ────────────────────────────────────── */
    .step-indicator {
      padding: 1.5rem 1.75rem 0;
      display: flex;
      flex-direction: column;
      gap: 0.6rem;
    }

    .step-track {
      display: flex;
      align-items: center;
      gap: 0;
    }

    .step-node {
      width: 2rem;
      height: 2rem;
      border-radius: 50%;
      border: 2px solid var(--line);
      background: var(--surface-2);
      display: grid;
      place-items: center;
      font-size: 0.75rem;
      font-weight: 700;
      color: var(--text-muted);
      flex-shrink: 0;
      transition: all 250ms ease;
      position: relative;
      z-index: 1;

      span { line-height: 1; }

      &.current {
        border-color: var(--accent);
        background: color-mix(in srgb, var(--accent) 12%, transparent);
        color: var(--accent);
        box-shadow: 0 0 0 4px color-mix(in srgb, var(--accent) 10%, transparent);
      }

      &.done {
        border-color: var(--accent);
        background: var(--accent);
        color: #fff;
        i { font-size: 0.7rem; font-weight: 700; }
      }
    }

    .step-line {
      flex: 1;
      height: 2px;
      background: var(--line);
      transition: background 300ms ease;

      &.filled { background: var(--accent); }
    }

    .step-labels {
      display: flex;
      justify-content: space-between;
      padding: 0 0.2rem;

      span {
        font-size: 0.72rem;
        font-weight: 500;
        color: var(--text-muted);
        letter-spacing: 0.02em;
        flex: 1;
        text-align: center;

        &:first-child { text-align: left; }
        &:last-child  { text-align: right; }

        &.active-label {
          color: var(--accent);
          font-weight: 700;
        }
      }
    }

    .progress-track {
      height: 3px;
      border-radius: 999px;
      background: var(--line);
      overflow: hidden;
    }

    .progress-fill {
      height: 100%;
      border-radius: 999px;
      background: linear-gradient(90deg, var(--accent), color-mix(in srgb, var(--accent) 70%, #8b5cf6));
      transition: width 400ms cubic-bezier(0.4, 0, 0.2, 1);
    }

    /* ── Step content area ─────────────────────────────────── */
    .step-content {
      padding: 1.75rem;
      display: flex;
      flex-direction: column;
      gap: 1.35rem;
      min-height: 320px;
      animation: stepFadeIn 220ms ease forwards;
    }

    @keyframes stepFadeIn {
      from { opacity: 0; transform: translateX(16px); }
      to   { opacity: 1; transform: translateX(0); }
    }

    .step-heading { display: flex; flex-direction: column; gap: 0.3rem; }

    .wiz-step-title {
      margin: 0;
      font-size: 1.15rem;
      font-weight: 800;
      color: var(--text-primary);
      letter-spacing: -0.02em;
    }

    .wiz-step-sub {
      margin: 0;
      font-size: 0.85rem;
      color: var(--text-secondary);
      line-height: 1.5;

      strong { color: var(--text-primary); }
    }

    /* ── Form fields ───────────────────────────────────────── */
    .form-row {
      display: flex;
      gap: 1rem;

      &.two-col > * { flex: 1; min-width: 0; }
    }

    .form-field {
      display: flex;
      flex-direction: column;
      gap: 0.45rem;
    }

    .field-label {
      font-size: 0.8rem;
      font-weight: 600;
      color: var(--text-secondary);
      letter-spacing: 0.01em;
    }

    .required { color: #ef4444; margin-left: 2px; }

    .wiz-input {
      height: 44px;
      border-radius: 0.625rem !important;
      font-size: 0.9rem;
    }

    /* Slug row */
    .slug-wrap {
      display: flex;
      align-items: center;
      gap: 0;
      border: 1px solid var(--line);
      border-radius: 0.625rem;
      background: var(--surface-soft, var(--surface-2));
      overflow: hidden;
      transition: border-color 200ms ease;

      &:focus-within { border-color: var(--accent); }

      .slug-input {
        flex: 1;
        border: none !important;
        border-radius: 0 !important;
        background: transparent;
        height: 44px;
        padding-right: 0.5rem;
        font-family: 'JetBrains Mono', 'Courier New', monospace;
        font-size: 0.85rem;
      }
    }

    .slug-suffix {
      padding: 0 0.85rem 0 0.25rem;
      font-size: 0.8rem;
      color: var(--text-muted);
      font-family: 'JetBrains Mono', 'Courier New', monospace;
      white-space: nowrap;
      flex-shrink: 0;
    }

    .field-error {
      display: flex;
      align-items: center;
      gap: 0.3rem;
      font-size: 0.75rem;
      color: #ef4444;
      i { font-size: 0.7rem; }
    }

    .field-hint {
      display: flex;
      align-items: center;
      gap: 0.35rem;
      font-size: 0.75rem;
      color: var(--accent);
      font-family: monospace;
      i { font-size: 0.7rem; }
    }

    /* ── Review card ───────────────────────────────────────── */
    .review-card {
      border: 1px solid var(--line);
      border-radius: 0.875rem;
      background: var(--surface-2);
      overflow: hidden;
    }

    .review-section {
      padding: 1.1rem 1.25rem;
      display: flex;
      flex-direction: column;
      gap: 0.65rem;
    }

    .review-section-label {
      display: flex;
      align-items: center;
      gap: 0.45rem;
      font-size: 0.7rem;
      text-transform: uppercase;
      letter-spacing: 0.08em;
      font-weight: 700;
      color: var(--accent);
      i { font-size: 0.72rem; }
    }

    .review-divider {
      height: 1px;
      background: var(--line);
    }

    .review-row {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 1rem;
    }

    .rk {
      font-size: 0.8rem;
      color: var(--text-secondary);
      flex-shrink: 0;
    }

    .rv {
      font-size: 0.85rem;
      font-weight: 600;
      color: var(--text-primary);
      text-align: right;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .rv-url {
      display: flex;
      align-items: center;
      gap: 0.35rem;
      color: var(--accent);
      font-family: monospace;
      font-weight: 500;
      i { font-size: 0.7rem; flex-shrink: 0; }
    }

    .rv-password { letter-spacing: 0.1em; color: var(--text-secondary); }

    .review-note {
      display: flex;
      align-items: flex-start;
      gap: 0.5rem;
      margin: 0;
      font-size: 0.78rem;
      color: var(--text-muted);
      line-height: 1.5;
      i { font-size: 0.78rem; flex-shrink: 0; margin-top: 1px; }
    }

    /* ── Wizard footer ─────────────────────────────────────── */
    .wiz-footer {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 1rem 1.75rem;
      gap: 1rem;
    }

    .step-counter {
      font-size: 0.78rem;
      color: var(--text-muted);
      font-weight: 500;
    }

    .wiz-footer-actions {
      display: flex;
      gap: 0.75rem;
      align-items: center;
    }
  `],
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
