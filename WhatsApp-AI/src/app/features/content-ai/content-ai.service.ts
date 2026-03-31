import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { APP_CONFIG } from '../../core/config/app-config';

export interface PoseSummary {
  id: string;
  name: string;
  referenceImagePath: string;
  createdAt: string;
}

export interface GenerateFromNewPoseResponse {
  generatedImageBase64: string;
  generatedImageMimeType: string;
  poseScript: string;
  sessionToken: string;
}

export interface GenerateFromSavedPoseResponse {
  generatedImageBase64: string;
  generatedImageMimeType: string;
}

export interface SavePoseResponse {
  id: string;
  name: string;
  thumbnailPath: string;
  createdAt: string;
}

@Injectable({ providedIn: 'root' })
export class ContentAiService {
  private readonly http = inject(HttpClient);
  private readonly cfg = APP_CONFIG;

  listPoses(): Observable<PoseSummary[]> {
    return this.http.get<PoseSummary[]>(`${this.cfg.apiBaseUrl}${this.cfg.imagePipeline.poses}`);
  }

  generateFromNewPose(poseImage: File, dressImage: File): Observable<GenerateFromNewPoseResponse> {
    const form = new FormData();
    form.append('poseImage', poseImage);
    form.append('dressImage', dressImage);
    return this.http.post<GenerateFromNewPoseResponse>(
      `${this.cfg.apiBaseUrl}${this.cfg.imagePipeline.generate}`,
      form
    );
  }

  generateFromSavedPose(poseId: string, dressImage: File): Observable<GenerateFromSavedPoseResponse> {
    const form = new FormData();
    form.append('poseId', poseId);
    form.append('dressImage', dressImage);
    return this.http.post<GenerateFromSavedPoseResponse>(
      `${this.cfg.apiBaseUrl}${this.cfg.imagePipeline.generateFromPose}`,
      form
    );
  }

  savePose(sessionToken: string, name: string, referenceImage: File): Observable<SavePoseResponse> {
    const form = new FormData();
    form.append('sessionToken', sessionToken);
    form.append('name', name);
    form.append('referenceImage', referenceImage);
    return this.http.post<SavePoseResponse>(
      `${this.cfg.apiBaseUrl}${this.cfg.imagePipeline.poses}`,
      form
    );
  }

  deletePose(id: string): Observable<void> {
    return this.http.delete<void>(`${this.cfg.apiBaseUrl}${this.cfg.imagePipeline.poseById(id)}`);
  }
}
