import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClientService } from '../../core/api/api-client.service';
import { APP_CONFIG } from '../../core/config/app-config';

export interface TenantSummary {
  id: string;
  name: string;
  businessName: string;
  isActive: boolean;
  createdAt: string;
  userCount: number;
}

export interface TenantDetail {
  id: string;
  name: string;
  businessName: string;
  isActive: boolean;
  createdAt: string;
  users: TenantUser[];
}

export interface TenantUser {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  role: string;
  isActive: boolean;
  createdAt: string;
}

export interface CreateTenantRequest {
  name: string;
  businessName: string;
  adminEmail: string;
  adminPassword: string;
  adminFirstName: string;
  adminLastName: string;
}

export interface TenantProvisionResult {
  success: boolean;
  tenantId: string | null;
  adminUserId: string | null;
  errorMessage: string | null;
}

@Injectable({ providedIn: 'root' })
export class TenantManagementService {
  constructor(private readonly apiClient: ApiClientService) {}

  listTenants(): Observable<TenantSummary[]> {
    return this.apiClient.get<TenantSummary[]>(APP_CONFIG.tenants.list);
  }

  getTenant(id: string): Observable<TenantDetail> {
    return this.apiClient.get<TenantDetail>(APP_CONFIG.tenants.get(id));
  }

  createTenant(request: CreateTenantRequest): Observable<TenantProvisionResult> {
    return this.apiClient.post<TenantProvisionResult>(APP_CONFIG.tenants.create, request);
  }

  suspendTenant(id: string): Observable<void> {
    return this.apiClient.post<void>(APP_CONFIG.tenants.suspend(id), {});
  }

  activateTenant(id: string): Observable<void> {
    return this.apiClient.post<void>(APP_CONFIG.tenants.activate(id), {});
  }
}
