import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClientService } from '../../core/api/api-client.service';
import { APP_CONFIG } from '../../core/config/app-config';

export interface PermissionDto {
  id: string;
  name: string;
  code: string;
  description: string | null;
  isSystem: boolean;
}

export interface RoleDto {
  id: string;
  name: string;
  code: string;
  description: string | null;
  isSystem: boolean;
  permissions: PermissionDto[];
}

export interface RbacUserDto {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  isActive: boolean;
  createdAt: string;
  roles: string[];
}

@Injectable({ providedIn: 'root' })
export class RbacService {
  constructor(private readonly apiClient: ApiClientService) {}

  listPermissions(): Observable<PermissionDto[]> {
    return this.apiClient.get<PermissionDto[]>(APP_CONFIG.rbac.permissions);
  }

  listRoles(tenantId: string): Observable<RoleDto[]> {
    return this.apiClient.get<RoleDto[]>(APP_CONFIG.rbac.roles, { tenantId });
  }

  createRole(tenantId: string, name: string, code: string, description?: string): Observable<RoleDto> {
    return this.apiClient.post<RoleDto>(APP_CONFIG.rbac.roles, { tenantId, name, code, description: description ?? null });
  }

  updateRole(tenantId: string, roleId: string, name: string, code: string, description?: string): Observable<RoleDto> {
    return this.apiClient.put<RoleDto>(APP_CONFIG.rbac.roleById(roleId), { tenantId, name, code, description: description ?? null });
  }

  deleteRole(tenantId: string, roleId: string): Observable<void> {
    return this.apiClient.delete<void>(APP_CONFIG.rbac.roleById(roleId), { tenantId });
  }

  setRolePermissions(tenantId: string, roleId: string, permissionIds: string[]): Observable<RoleDto> {
    return this.apiClient.put<RoleDto>(APP_CONFIG.rbac.rolePermissions(roleId), { tenantId, permissionIds });
  }

  listUsers(tenantId: string): Observable<RbacUserDto[]> {
    return this.apiClient.get<RbacUserDto[]>(APP_CONFIG.rbac.users, { tenantId });
  }

  assignRole(tenantId: string, userId: string, roleId: string): Observable<void> {
    return this.apiClient.put<void>(APP_CONFIG.rbac.userRole(userId, roleId), { tenantId });
  }

  removeRole(tenantId: string, userId: string, roleId: string): Observable<void> {
    return this.apiClient.delete<void>(APP_CONFIG.rbac.userRole(userId, roleId), { tenantId });
  }
}
