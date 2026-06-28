import { HttpClient } from '@angular/common/http';
import { Inject, Injectable } from '@angular/core';
import { Observable, map } from 'rxjs';
import { API_BASE_URL } from '../../pages/auth-page/auth-api-go.service';
import { CreatePriceRequest, ProductBranchPrice } from '../models/price.model';

interface JsendEnvelope<T> {
  status: 'success' | 'fail' | 'error';
  data: T;
  message?: string;
}

interface PriceGoData {
  id: string;
  product_id: string;
  branch_id: string;
  price1_unit: number;
  price1_currency: string;
  price2_box_bundle_piece: number;
  price2_currency: string;
  price3_wholesale_from2: number;
  price3_currency: string;
  price4_wholesale: number | null;
  price4_currency: string | null;
  price4_requires_agreement: boolean;
  valid_from: string;
  valid_until: string | null;
}

@Injectable({ providedIn: 'root' })
export class PriceApiService {
  private readonly baseUrl: string;

  constructor(
    private readonly http: HttpClient,
    @Inject(API_BASE_URL) apiBaseUrl: string
  ) {
    this.baseUrl = apiBaseUrl.replace(/\/$/, '');
  }

  getById(id: string): Observable<ProductBranchPrice> {
    return this.http
      .get<JsendEnvelope<PriceGoData>>(`${this.baseUrl}/api/v1/prices/${id}`)
      .pipe(map((res) => this.map(res.data)));
  }

  listByProductId(productId: string): Observable<ProductBranchPrice[]> {
    return this.http
      .get<JsendEnvelope<PriceGoData[]>>(
        `${this.baseUrl}/api/v1/prices/product/${productId}`
      )
      .pipe(map((res) => res.data.map((p) => this.map(p))));
  }

  listByBranchId(branchId: string): Observable<ProductBranchPrice[]> {
    return this.http
      .get<JsendEnvelope<PriceGoData[]>>(
        `${this.baseUrl}/api/v1/prices/branch/${branchId}`
      )
      .pipe(map((res) => res.data.map((p) => this.map(p))));
  }

  create(req: CreatePriceRequest): Observable<ProductBranchPrice> {
    return this.http
      .post<JsendEnvelope<PriceGoData>>(`${this.baseUrl}/api/v1/prices/`, req, {
        withCredentials: true,
      })
      .pipe(map((res) => this.map(res.data)));
  }

  update(id: string, req: CreatePriceRequest): Observable<ProductBranchPrice> {
    return this.http
      .put<JsendEnvelope<PriceGoData>>(`${this.baseUrl}/api/v1/prices/${id}`, req, {
        withCredentials: true,
      })
      .pipe(map((res) => this.map(res.data)));
  }

  delete(id: string): Observable<void> {
    return this.http
      .delete<JsendEnvelope<{ status: string }>>(`${this.baseUrl}/api/v1/prices/${id}`, {
        withCredentials: true,
      })
      .pipe(map(() => undefined));
  }

  private map(raw: PriceGoData): ProductBranchPrice {
    return {
      id: raw.id,
      productId: raw.product_id,
      branchId: raw.branch_id,
      price1Unit: raw.price1_unit,
      price1Currency: raw.price1_currency,
      price2BoxBundlePiece: raw.price2_box_bundle_piece,
      price2Currency: raw.price2_currency,
      price3WholesaleFrom2: raw.price3_wholesale_from2,
      price3Currency: raw.price3_currency,
      price4Wholesale: raw.price4_wholesale,
      price4Currency: raw.price4_currency,
      price4RequiresAgreement: raw.price4_requires_agreement,
      validFrom: raw.valid_from,
      validUntil: raw.valid_until,
    };
  }
}
