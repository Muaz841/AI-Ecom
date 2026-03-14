import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClientService } from '../../core/api/api-client.service';
import { APP_CONFIG } from '../../core/config/app-config';

export interface PlatformMetaConfigDto {
  isConfigured: boolean;
  appId: string;
  appSecretMasked: string;
  loginConfigurationId: string | null;
  graphVersion: string;
  callbackBaseUrl: string;
  updatedAt: string | null;
}

export interface SavePlatformMetaConfigRequest {
  appId: string;
  appSecret: string | null;
  loginConfigurationId: string | null;
  graphVersion: string;
  callbackBaseUrl: string;
}

@Injectable({ providedIn: 'root' })
export class PlatformSettingsService {
  private readonly api = inject(ApiClientService);

  getMetaConfig(): Observable<PlatformMetaConfigDto> {
    return this.api.get<PlatformMetaConfigDto>(APP_CONFIG.platformSettings.meta);
  }

  saveMetaConfig(request: SavePlatformMetaConfigRequest): Observable<PlatformMetaConfigDto> {
    return this.api.put<PlatformMetaConfigDto>(APP_CONFIG.platformSettings.meta, request);
  }
}
