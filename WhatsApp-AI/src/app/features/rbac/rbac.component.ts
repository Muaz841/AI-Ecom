import { Component, OnInit, signal, DestroyRef, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { combineLatest } from 'rxjs';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule, FormsModule } from '@angular/forms';
import { trigger, transition, style, animate } from '@angular/animations';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { TextareaModule } from 'primeng/textarea';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { TooltipModule } from 'primeng/tooltip';
import { TagModule } from 'primeng/tag';
import { RbacService, RoleDto, PermissionDto, RbacUserDto } from './rbac.service';
import { AuthService } from '../../core/auth/auth.service';
import { ToastService } from '../../core/ui/toast.service';

type Tab = 'roles' | 'permissions' | 'users';

@Component({
  selector: 'app-rbac',
  standalone: true,
  templateUrl: './rbac.component.html',
  styleUrls: ['./rbac.component.scss'],
  imports: [
    CommonModule,
    ReactiveFormsModule,
    FormsModule,
    ButtonModule,
    DialogModule,
    InputTextModule,
    TextareaModule,
    ToggleSwitchModule,
    ProgressSpinnerModule,
    TooltipModule,
    TagModule,
  ],
  animations: [
    trigger('fadeSlide', [
      transition(':enter', [
        style({ opacity: 0, transform: 'translateY(8px)' }),
        animate('200ms ease', style({ opacity: 1, transform: 'translateY(0)' })),
      ]),
    ]),
  ],
})
export class RbacComponent implements OnInit {
  activeTab = signal<Tab>('roles');
  loading = signal(false);

  roles = signal<RoleDto[]>([]);
  permissions = signal<PermissionDto[]>([]);
  users = signal<RbacUserDto[]>([]);

  // Role dialog
  showRoleDialog = false;
  editingRole = signal<RoleDto | null>(null);
  savingRole = signal(false);
  roleForm: FormGroup;
  private slugManuallyEdited = false;

  // Permissions dialog
  showPermissionsDialog = false;
  permDialogRole = signal<RoleDto | null>(null);
  selectedPermIds = signal<Set<string>>(new Set());
  savingPerms = signal(false);

  // User roles dialog
  showUserRolesDialog = false;
  selectedUser = signal<RbacUserDto | null>(null);
  togglingRole = signal<string | null>(null);

  // Delete confirm
  showDeleteConfirm = false;
  roleToDelete = signal<RoleDto | null>(null);
  deletingRoleId = signal<string | null>(null);

  noTenantSelected = signal(false);

  private tenantId = '';
  private readonly destroyRef = inject(DestroyRef);

  constructor(
    private readonly rbacService: RbacService,
    private readonly authService: AuthService,
    private readonly toast: ToastService,
    private readonly fb: FormBuilder,
    private readonly route: ActivatedRoute,
  ) {
    this.roleForm = this.fb.group({
      name: ['', Validators.required],
      code: ['', [Validators.required, Validators.pattern(/^[a-z0-9_]+$/)]],
      description: [''],
    });
  }

  ngOnInit(): void {
    // Permissions are global — load once independently
    this.loadPermissions();

    // Effective tenantId = query param (host navigating for a specific tenant) OR profile tenantId
    combineLatest([this.route.queryParamMap, this.authService.userProfile$])
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(([params, profile]) => {
        const qp = params.get('tenantId') ?? '';
        const profileId = profile?.tenantId ?? '';
        const effectiveId = qp || profileId;
        if (effectiveId !== this.tenantId) {
          this.tenantId = effectiveId;
          if (effectiveId) {
            this.loadRolesAndUsers();
          } else {
            this.noTenantSelected.set(true);
            this.loading.set(false);
          }
        }
      });
  }

  setTab(tab: Tab): void {
    this.activeTab.set(tab);
  }

  // ── Role helpers ──────────────────────────────────────

  openRoleDialog(role?: RoleDto): void {
    this.editingRole.set(role ?? null);
    this.slugManuallyEdited = !!role;
    this.roleForm.reset({
      name: role?.name ?? '',
      code: role?.code ?? '',
      description: role?.description ?? '',
    });
    this.showRoleDialog = true;
  }

  onRoleNameInput(): void {
    if (this.slugManuallyEdited) return;
    const name: string = this.roleForm.get('name')?.value ?? '';
    const code = name.toLowerCase().replace(/[^a-z0-9\s]/g, '').trim().replace(/\s+/g, '_');
    this.roleForm.get('code')?.setValue(code, { emitEvent: false });
  }

  resetRoleForm(): void {
    this.slugManuallyEdited = false;
    this.editingRole.set(null);
  }

  submitRole(): void {
    this.roleForm.markAllAsTouched();
    if (this.roleForm.invalid || this.savingRole()) return;

    const v = this.roleForm.value;
    this.savingRole.set(true);
    const editing = this.editingRole();

    const req$ = editing
      ? this.rbacService.updateRole(this.tenantId, editing.id, v.name, v.code, v.description || undefined)
      : this.rbacService.createRole(this.tenantId, v.name, v.code, v.description || undefined);

    req$.subscribe({
      next: (role) => {
        this.savingRole.set(false);
        this.showRoleDialog = false;
        if (editing) {
          this.roles.update((list) => list.map((r) => (r.id === role.id ? { ...role, permissions: r.permissions } : r)));
          this.toast.success('Updated', `Role "${role.name}" updated.`);
        } else {
          this.roles.update((list) => [...list, role]);
          this.toast.success('Created', `Role "${role.name}" created.`);
        }
      },
      error: () => {
        this.savingRole.set(false);
        this.toast.error('Error', 'Unable to save role.');
      },
    });
  }

  confirmDeleteRole(role: RoleDto): void {
    this.roleToDelete.set(role);
    this.showDeleteConfirm = true;
  }

  deleteRole(): void {
    const role = this.roleToDelete();
    if (!role || this.deletingRoleId()) return;
    this.deletingRoleId.set(role.id);

    this.rbacService.deleteRole(this.tenantId, role.id).subscribe({
      next: () => {
        this.deletingRoleId.set(null);
        this.showDeleteConfirm = false;
        this.roles.update((list) => list.filter((r) => r.id !== role.id));
        this.toast.warn('Deleted', `Role "${role.name}" removed.`);
      },
      error: () => {
        this.deletingRoleId.set(null);
        this.toast.error('Error', 'Unable to delete role.');
      },
    });
  }

  // ── Permissions dialog ───────────────────────────────

  openPermissionsDialog(role: RoleDto): void {
    this.permDialogRole.set(role);
    this.selectedPermIds.set(new Set(role.permissions.map((p) => p.id)));
    this.showPermissionsDialog = true;
  }

  isPermSelected(permId: string): boolean {
    return this.selectedPermIds().has(permId);
  }

  togglePerm(permId: string, selected: boolean): void {
    const set = new Set(this.selectedPermIds());
    if (selected) { set.add(permId); } else { set.delete(permId); }
    this.selectedPermIds.set(set);
  }

  savePermissions(): void {
    const role = this.permDialogRole();
    if (!role || this.savingPerms()) return;
    this.savingPerms.set(true);
    const ids = [...this.selectedPermIds()];

    this.rbacService.setRolePermissions(this.tenantId, role.id, ids).subscribe({
      next: (updated) => {
        this.savingPerms.set(false);
        this.showPermissionsDialog = false;
        this.roles.update((list) => list.map((r) => (r.id === updated.id ? updated : r)));
        this.toast.success('Saved', `Permissions updated for "${role.name}".`);
      },
      error: () => {
        this.savingPerms.set(false);
        this.toast.error('Error', 'Unable to save permissions.');
      },
    });
  }

  // ── User roles dialog ────────────────────────────────

  openUserRolesDialog(user: RbacUserDto): void {
    this.selectedUser.set(user);
    this.showUserRolesDialog = true;
  }

  userHasRole(user: RbacUserDto, roleName: string): boolean {
    return user.roles.includes(roleName);
  }

  toggleUserRole(user: RbacUserDto, role: RoleDto, assign: boolean): void {
    if (this.togglingRole()) return;
    this.togglingRole.set(role.id);

    const req$ = assign
      ? this.rbacService.assignRole(this.tenantId, user.id, role.id)
      : this.rbacService.removeRole(this.tenantId, user.id, role.id);

    req$.subscribe({
      next: () => {
        this.togglingRole.set(null);
        this.users.update((list) =>
          list.map((u) => {
            if (u.id !== user.id) return u;
            const updated = assign
              ? [...new Set([...u.roles, role.name])]
              : u.roles.filter((r) => r !== role.name);
            return { ...u, roles: updated };
          }),
        );
        this.selectedUser.update((u) => {
          if (!u || u.id !== user.id) return u;
          const updated = assign
            ? [...new Set([...u.roles, role.name])]
            : u.roles.filter((r) => r !== role.name);
          return { ...u, roles: updated };
        });
      },
      error: () => {
        this.togglingRole.set(null);
        this.toast.error('Error', 'Unable to update user role.');
      },
    });
  }

  // ── Utils ─────────────────────────────────────────────

  getInitials(user: RbacUserDto): string {
    return `${user.firstName[0] ?? ''}${user.lastName[0] ?? ''}`.toUpperCase();
  }

  // ── Data loading ──────────────────────────────────────

  private loadPermissions(): void {
    this.rbacService.listPermissions().subscribe({
      next: (data) => { this.permissions.set(data); },
      error: () => { this.toast.error('Error', 'Unable to load permissions.'); },
    });
  }

  private loadRolesAndUsers(): void {
    this.noTenantSelected.set(false);
    this.loading.set(true);
    let done = 0;
    const check = () => { if (++done === 2) this.loading.set(false); };

    this.rbacService.listRoles(this.tenantId).subscribe({
      next: (data) => { this.roles.set(data); check(); },
      error: () => { check(); this.toast.error('Error', 'Unable to load roles.'); },
    });

    this.rbacService.listUsers(this.tenantId).subscribe({
      next: (data) => { this.users.set(data); check(); },
      error: () => { check(); this.toast.error('Error', 'Unable to load users.'); },
    });
  }
}
