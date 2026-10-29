// Retail products, stock, coupons and loyalty

export type StockMovementType = 'Purchase' | 'Sale' | 'SaleVoid' | 'Adjustment';
export type DiscountType = 'Percent' | 'Fixed';

export interface ProductBranchStock { branchId: string; branchName: string; quantity: number; lowStockThreshold: number; }
export interface ProductRow {
  id: string; name: string; sku?: string | null; barcode?: string | null;
  price: number; cost: number; margin: number;
  status: 'Active' | 'Inactive'; statusText: string;
  quantity: number; lowStock: boolean;
  branches: ProductBranchStock[];
}
export interface ProductList {
  items: ProductRow[];
  summary: { count: number; lowStockCount: number; stockValue: number; retailValue: number };
}
export interface ProductInput { name: string; sku: string | null; barcode: string | null; price: number; cost: number; active: boolean; }
export interface StockAdjustInput {
  branchId: string; type: StockMovementType; quantity: number;
  unitCost: number | null; note: string | null; lowStockThreshold: number | null;
}
export interface StockMovementRow {
  id: string; createdAt: string; type: StockMovementType; typeText: string;
  quantity: number; balanceAfter: number; unitCost?: number | null; note?: string | null;
  branchName: string; byName?: string | null;
}

export interface CouponRow {
  id: string; code: string; description?: string | null;
  type: DiscountType; value: number; typeText: string;
  minSubtotal?: number | null; maxDiscount?: number | null;
  maxUses?: number | null; usedCount: number;
  validFrom?: string | null; validTo?: string | null;
  status: 'Active' | 'Inactive'; statusText: string;
}
export interface CouponInput {
  code: string; description: string | null; type: DiscountType; value: number;
  minSubtotal: number | null; maxDiscount: number | null; maxUses: number | null;
  validFrom: string | null; validTo: string | null; active: boolean;
}
export interface CouponPreview { code: string; description?: string | null; discount: number; }

export interface LoyaltyHistory {
  balance: number; balanceValue: number; enabled: boolean;
  items: { id: string; createdAt: string; type: string; typeText: string; points: number; balanceAfter: number; note?: string | null; saleId?: string | null }[];
}
