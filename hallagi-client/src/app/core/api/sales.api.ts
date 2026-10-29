import { inject, Injectable } from '@angular/core';
import { ApiService } from '@core/http/api.service';
import { CouponPreview, CreateSaleRequest, PosData, Sale, SaleCreated } from '@core/models';

@Injectable({ providedIn: 'root' })
export class SalesApi {
  private api = inject(ApiService);

  posData(branchId: string) { return this.api.get<PosData>('/pos/data', { branchId }); }
  create(request: CreateSaleRequest) { return this.api.post<SaleCreated>('/sales', request); }
  get(id: string) { return this.api.get<Sale>(`/sales/${id}`); }
  cancel(id: string, reason: string) { return this.api.post<{ message: string }>(`/sales/${id}/cancel`, { reason }); }
  previewCoupon(code: string, gross: number) { return this.api.get<CouponPreview>('/coupons/preview', { code, gross }); }
}
