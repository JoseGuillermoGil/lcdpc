import { HttpClient } from '@angular/common/http';
import { Inject, Injectable } from '@angular/core';
import { Observable, map } from 'rxjs';
import { API_BASE_URL } from '../../pages/auth-page/auth-api-go.service';
import { Bundle, CreateBundleRequest } from '../models/bundle.model';

interface JsendEnvelope<T> {
  status: 'success' | 'fail' | 'error';
  data: T;
  message?: string;
}

interface BundleGoData {
  bundle_id: string;
  code: string;
  name: string;
  status: string;
  enabled_branch_ids: string[];
  total_price: number;
  total_price_currency: string;
  promotional_price: number | null;
  promotional_price_currency: string | null;
  items: BundleItemGoData[] | null;
  img: string | null;
  category_id: string | null;
}

interface BundleItemGoData {
  id: string;
  bundle_id: string;
  product_id: string;
  quantity: number;
}

@Injectable({ providedIn: 'root' })
export class BundleApiService {
  private readonly baseUrl: string;

  constructor(
    private readonly http: HttpClient,
    @Inject(API_BASE_URL) apiBaseUrl: string
  ) {
    this.baseUrl = apiBaseUrl.replace(/\/$/, '');
  }

  list(): Observable<Bundle[]> {
    return this.http
      .get<JsendEnvelope<BundleGoData[]>>(`${this.baseUrl}/api/v1/bundles/`)
      .pipe(map((res) => res.data.map((b) => this.map(b))));
  }

  getById(id: string): Observable<Bundle> {
    return this.http
      .get<JsendEnvelope<BundleGoData>>(`${this.baseUrl}/api/v1/bundles/${id}`)
      .pipe(map((res) => this.map(res.data)));
  }

  create(req: CreateBundleRequest): Observable<Bundle> {
    return this.http
      .post<JsendEnvelope<BundleGoData>>(`${this.baseUrl}/api/v1/bundles/`, req, {
        withCredentials: true,
      })
      .pipe(map((res) => this.map(res.data)));
  }

  update(id: string, req: CreateBundleRequest): Observable<Bundle> {
    return this.http
      .put<JsendEnvelope<BundleGoData>>(`${this.baseUrl}/api/v1/bundles/${id}`, req, {
        withCredentials: true,
      })
      .pipe(map((res) => this.map(res.data)));
  }

  delete(id: string): Observable<void> {
    return this.http
      .delete<JsendEnvelope<{ status: string }>>(`${this.baseUrl}/api/v1/bundles/${id}`, {
        withCredentials: true,
      })
      .pipe(map(() => undefined));
  }

  publish(id: string): Observable<Bundle> {
    return this.http
      .post<JsendEnvelope<BundleGoData>>(
        `${this.baseUrl}/api/v1/bundles/${id}/publish`,
        {},
        { withCredentials: true }
      )
      .pipe(map((res) => this.map(res.data)));
  }

  pause(id: string): Observable<Bundle> {
    return this.http
      .post<JsendEnvelope<BundleGoData>>(
        `${this.baseUrl}/api/v1/bundles/${id}/pause`,
        {},
        { withCredentials: true }
      )
      .pipe(map((res) => this.map(res.data)));
  }

  updateImage(id: string, file: File): Observable<{ img: string }> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http
      .put<JsendEnvelope<{ img: string }>>(
        `${this.baseUrl}/api/v1/bundles/${id}/image`,
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

  private map(raw: BundleGoData): Bundle {
    return {
      bundleId: raw.bundle_id,
      code: raw.code,
      name: raw.name,
      status: raw.status,
      enabledBranchIds: raw.enabled_branch_ids,
      totalPrice: raw.total_price,
      totalPriceCurrency: raw.total_price_currency,
      promotionalPrice: raw.promotional_price,
      promotionalPriceCurrency: raw.promotional_price_currency,
      items: raw.items
        ? raw.items.map((i) => ({
            id: i.id,
            bundleId: i.bundle_id,
            productId: i.product_id,
            quantity: i.quantity,
          }))
        : null,
      img: raw.img,
      categoryId: raw.category_id,
    };
  }
}
