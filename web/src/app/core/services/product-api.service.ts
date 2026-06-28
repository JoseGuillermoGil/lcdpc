import { HttpClient } from '@angular/common/http';
import { Inject, Injectable } from '@angular/core';
import { Observable, map } from 'rxjs';
import { API_BASE_URL } from '../../pages/auth-page/auth-api-go.service';
import { CreateProductRequest, Product } from '../models/product.model';

interface JsendEnvelope<T> {
  status: 'success' | 'fail' | 'error';
  data: T;
  message?: string;
}

interface ProductGoData {
  product_id: string;
  name: string;
  sku: string;
  base_measure_type: string;
  wholesale_commercial_type: string;
  units_per_box: number | null;
  units_per_bundle: number | null;
  is_active: boolean;
  img: string | null;
  category_id: string | null;
}

@Injectable({ providedIn: 'root' })
export class ProductApiService {
  private readonly baseUrl: string;

  constructor(
    private readonly http: HttpClient,
    @Inject(API_BASE_URL) apiBaseUrl: string
  ) {
    this.baseUrl = apiBaseUrl.replace(/\/$/, '');
  }

  list(): Observable<Product[]> {
    return this.http
      .get<JsendEnvelope<ProductGoData[]>>(`${this.baseUrl}/api/v1/products/`)
      .pipe(map((res) => res.data.map((p) => this.map(p))));
  }

  getById(id: string): Observable<Product> {
    return this.http
      .get<JsendEnvelope<ProductGoData>>(`${this.baseUrl}/api/v1/products/${id}`)
      .pipe(map((res) => this.map(res.data)));
  }

  create(req: CreateProductRequest): Observable<Product> {
    return this.http
      .post<JsendEnvelope<ProductGoData>>(`${this.baseUrl}/api/v1/products/`, req, {
        withCredentials: true,
      })
      .pipe(map((res) => this.map(res.data)));
  }

  update(id: string, req: CreateProductRequest): Observable<Product> {
    return this.http
      .put<JsendEnvelope<ProductGoData>>(`${this.baseUrl}/api/v1/products/${id}`, req, {
        withCredentials: true,
      })
      .pipe(map((res) => this.map(res.data)));
  }

  delete(id: string): Observable<void> {
    return this.http
      .delete<JsendEnvelope<{ status: string }>>(`${this.baseUrl}/api/v1/products/${id}`, {
        withCredentials: true,
      })
      .pipe(map(() => undefined));
  }

  updateImage(id: string, file: File): Observable<{ img: string }> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http
      .put<JsendEnvelope<{ img: string }>>(
        `${this.baseUrl}/api/v1/products/${id}/image`,
        formData,
        { withCredentials: true }
      )
      .pipe(map((res) => res.data));
  }

  resolveImageUrl(img: string | null): string | null {
    if (!img) return null;
    if (img.startsWith('http://') || img.startsWith('https://')) return img;
    return `${this.baseUrl}${img}`;
  }

  private map(raw: ProductGoData): Product {
    return {
      productId: raw.product_id,
      name: raw.name,
      sku: raw.sku,
      baseMeasureType: raw.base_measure_type,
      wholesaleCommercialType: raw.wholesale_commercial_type,
      unitsPerBox: raw.units_per_box,
      unitsPerBundle: raw.units_per_bundle,
      isActive: raw.is_active,
      img: raw.img,
      categoryId: raw.category_id,
    };
  }
}
