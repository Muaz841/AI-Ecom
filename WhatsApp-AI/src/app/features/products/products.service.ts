import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { APP_CONFIG } from '../../core/config/app-config';

// ─── Models ──────────────────────────────────────────────────────────────────

export interface ProductVariantDetail {
  id: string;
  size: string;
  color: string | null;
  stock: number;
  priceOverride: number | null;
}

export interface ProductImageDetail {
  id: string;
  url: string;
  altText: string | null;
  isPrimary: boolean;
}

export interface ProductSummary {
  id: string;
  name: string;
  description: string | null;
  basePrice: number;
  currency: string;
  totalStock: number;
  sku: string | null;
  variantCount: number;
  primaryImageUrl: string | null;
  createdAt: string;
  updatedAt: string | null;
}

export interface ProductDetail extends ProductSummary {
  externalId: string | null;
  variants: ProductVariantDetail[];
  images: ProductImageDetail[];
}

export interface ProductPageResult {
  items: ProductSummary[];
  totalCount: number;
}

// ─── Request models ───────────────────────────────────────────────────────────

export interface ProductVariantRequest {
  id?: string;
  size: string;
  color?: string | null;
  stock: number;
  priceOverride?: number | null;
}

export interface CreateProductRequest {
  name: string;
  description?: string | null;
  basePrice: number;
  currency?: string;
  sku?: string | null;
  variants?: ProductVariantRequest[];
}

export interface UpdateProductRequest {
  name: string;
  description?: string | null;
  basePrice: number;
  currency?: string;
  sku?: string | null;
  variants?: ProductVariantRequest[];
}

// ─── Service ──────────────────────────────────────────────────────────────────

@Injectable({ providedIn: 'root' })
export class ProductsService {
  private readonly http = inject(HttpClient);
  private readonly cfg  = APP_CONFIG;

  list(pageIndex = 0, pageSize = 20, search?: string): Observable<ProductPageResult> {
    let params = new HttpParams()
      .set('pageIndex', pageIndex)
      .set('pageSize', pageSize);

    if (search?.trim()) params = params.set('search', search.trim());

    return this.http.get<ProductPageResult>(
      `${this.cfg.apiBaseUrl}${this.cfg.products.list}`,
      { params }
    );
  }

  getById(id: string): Observable<ProductDetail> {
    return this.http.get<ProductDetail>(
      `${this.cfg.apiBaseUrl}${this.cfg.products.getById(id)}`
    );
  }

  create(request: CreateProductRequest): Observable<{ success: boolean; productId: string }> {
    return this.http.post<{ success: boolean; productId: string }>(
      `${this.cfg.apiBaseUrl}${this.cfg.products.upload}`,
      request
    );
  }

  update(id: string, request: UpdateProductRequest): Observable<{ success: boolean }> {
    return this.http.put<{ success: boolean }>(
      `${this.cfg.apiBaseUrl}${this.cfg.products.update(id)}`,
      request
    );
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(
      `${this.cfg.apiBaseUrl}${this.cfg.products.delete(id)}`
    );
  }

  /** Upload an image file to the product. Sends multipart/form-data. */
  uploadImage(
    productId: string,
    file: File,
    altText?: string | null,
    isPrimary = false
  ): Observable<{ success: boolean }> {
    const fd = new FormData();
    fd.append('file', file, file.name);
    if (altText?.trim()) fd.append('altText', altText.trim());
    fd.append('isPrimary', String(isPrimary));

    return this.http.post<{ success: boolean }>(
      `${this.cfg.apiBaseUrl}${this.cfg.products.addImage(productId)}`,
      fd
    );
  }

  removeImage(productId: string, imageId: string): Observable<void> {
    return this.http.delete<void>(
      `${this.cfg.apiBaseUrl}${this.cfg.products.removeImage(productId, imageId)}`
    );
  }

  setPrimaryImage(productId: string, imageId: string): Observable<{ success: boolean }> {
    return this.http.put<{ success: boolean }>(
      `${this.cfg.apiBaseUrl}${this.cfg.products.setPrimaryImage(productId, imageId)}`,
      {}
    );
  }
}
