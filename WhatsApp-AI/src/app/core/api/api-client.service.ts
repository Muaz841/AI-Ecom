import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { APP_CONFIG } from '../config/app-config';

@Injectable({ providedIn: 'root' })
export class ApiClientService {
  constructor(private readonly http: HttpClient) {}

  get<T>(path: string, query?: Record<string, unknown>): Observable<T> {
    return this.http.get<T>(this.buildUrl(path), { params: this.toParams(query) });
  }

  post<T>(path: string, body: unknown, headers?: HttpHeaders): Observable<T> {
    return this.http.post<T>(this.buildUrl(path), body, { headers });
  }

  put<T>(path: string, body: unknown): Observable<T> {
    return this.http.put<T>(this.buildUrl(path), body);
  }

  delete<T>(path: string, query?: Record<string, unknown>): Observable<T> {
    return this.http.delete<T>(this.buildUrl(path), { params: this.toParams(query) });
  }

  buildUrl(path: string): string {
    if (/^https?:\/\//i.test(path)) {
      return path;
    }

    if (!path.startsWith('/')) {
      return `${APP_CONFIG.apiBaseUrl}/${path}`;
    }

    return `${APP_CONFIG.apiBaseUrl}${path}`;
  }

  private toParams(query?: Record<string, unknown>): HttpParams {
    let params = new HttpParams();
    if (!query) {
      return params;
    }

    for (const [key, value] of Object.entries(query)) {
      if (value === null || value === undefined) {
        continue;
      }

      params = params.set(key, String(value));
    }

    return params;
  }
}
