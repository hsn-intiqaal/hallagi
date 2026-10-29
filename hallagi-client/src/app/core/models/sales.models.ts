// POS, sales and tax invoices

export interface PosService {
  id: string; name: string; description?: string; price: number;
  status?: string; statusText?: string; basePrice?: number; hasBranchPrice?: boolean;
  durationMinutes?: number;
}
export interface PosProduct { id: string; name: string; price: number; barcode?: string | null; stock: number; }
export interface PosBarber { id: string; fullName: string; initials: string; branchNames: string[]; }
export interface PosCustomer { id: string; fullName: string; phone?: string; loyaltyPoints?: number; }

export interface PosPricing {
  vatEnabled: boolean;
  vatRate: number;
  pricesIncludeVat: boolean;
  canDiscount: boolean;
  maxManualDiscountPercent: number;
  loyaltyEnabled: boolean;
  loyaltyPointValue: number;
  loyaltyPointsPerSar: number;
  loyaltyMinRedeem: number;
}

export interface PosData {
  branch: { id: string; name: string; city: string };
  services: PosService[];
  products: PosProduct[];
  barbers: PosBarber[];
  customers: PosCustomer[];
  pricing: PosPricing;
  payment: { cashEnabled: boolean; cardEnabled: boolean; methods: { value: string; label: string; enabled: boolean }[] };
}

export type PaymentMethod = 'Cash' | 'Card';
export type SaleStatus = 'Completed' | 'Cancelled';
export type SaleItemType = 'Service' | 'Product';

export interface SaleItem {
  itemType?: SaleItemType;
  serviceId: string | null;
  productId?: string | null;
  serviceName: string;
  unitPrice: number;
  quantity: number;
  lineTotal: number;
}

export interface InvoiceInfo {
  title: string;
  sellerName: string;
  organizationName: string;
  logoUrl?: string | null;
  vatNumber?: string | null;
  commercialRegistration?: string | null;
  address?: string | null;
  phone?: string | null;
  footer?: string | null;
  zatcaQr?: string | null;
}

export interface Sale {
  id: string; receiptNo: number; createdAt: string;
  branchId: string; branchName: string; barberId: string; barberName: string;
  customerId: string | null; customerName: string; customerPhone?: string | null; customerPoints?: number | null;
  paymentMethod: PaymentMethod; paymentText: string;
  status: SaleStatus; statusText: string; cancelReason?: string | null;
  grossAmount?: number; discountAmount?: number; subtotalAmount?: number;
  vatRate?: number; vatAmount?: number; totalAmount: number;
  couponCode?: string | null; pointsRedeemed?: number; pointsEarned?: number;
  appointmentId?: string | null;
  isTaxInvoice?: boolean;
  invoice?: InvoiceInfo;
  items?: SaleItem[];
  servicesText?: string;
}
export interface SaleCreated { replayed: boolean; sale: Sale; shiftId: string | null; }

export interface CreateSaleRequest {
  branchId: string;
  barberId: string;
  customerId: string | null;
  paymentMethod: PaymentMethod;
  idempotencyKey: string;
  items: { serviceId: string | null; productId: string | null; quantity: number }[];
  couponCode?: string | null;
  manualDiscount?: number | null;
  redeemPoints?: number;
  appointmentId?: string | null;
}
