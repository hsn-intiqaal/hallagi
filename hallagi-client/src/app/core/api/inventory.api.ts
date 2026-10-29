import { inject, Injectable } from '@angular/core';
import { ApiService } from '@core/http/api.service';
import { CouponInput, CouponRow, LoyaltyHistory, ProductInput, ProductList, StockAdjustInput, StockMovementRow } from '@core/models';

@Injectable({ providedIn: 'root' })
export class ProductsApi {
  private api = inject(ApiService);

  list(branchId: string | null, search?: string, lowStock = false) {
    return this.api.get<ProductList>('/products', { branchId, search, lowStock: lowStock || null });
  }
  create(input: ProductInput) { return this.api.post<{ id: string; message: string }>('/products', input); }
  update(id: string, input: ProductInput) { return this.api.put<{ message: string }>(`/products/${id}`, input); }
  adjustStock(id: string, input: StockAdjustInput) { return this.api.post<{ message: string }>(`/products/${id}/stock`, input); }
  movements(id: string, branchId?: string | null) { return this.api.get<{ items: StockMovementRow[] }>(`/products/${id}/movements`, { branchId }); }
}

@Injectable({ providedIn: 'root' })
export class CouponsApi {
  private api = inject(ApiService);

  list() { return this.api.get<{ items: CouponRow[] }>('/coupons'); }
  create(input: CouponInput) { return this.api.post<{ id: string; message: string }>('/coupons', input); }
  update(id: string, input: CouponInput) { return this.api.put<{ message: string }>(`/coupons/${id}`, input); }
}

@Injectable({ providedIn: 'root' })
export class LoyaltyApi {
  private api = inject(ApiService);

  history(customerId: string) { return this.api.get<LoyaltyHistory>(`/customers/${customerId}/loyalty`); }
  adjust(customerId: string, points: number, note: string | null) {
    return this.api.post<{ message: string }>(`/customers/${customerId}/loyalty/adjust`, { points, note });
  }
}
