import { UserRole } from './auth.models';

export interface UserRow {
  id: string; profileId: string; fullName: string; email: string; phone?: string;
  role: UserRole; roleText: string; status: 'Active' | 'Disabled'; statusText: string;
  branches: string[]; branchIds: string[];
  permissions: Record<string, boolean> | null;
}

export interface TaxSettings {
  vatEnabled: boolean; vatRate: number; pricesIncludeVat: boolean;
  vatNumber: string | null; commercialRegistration: string | null; legalName: string | null; taxAddress: string | null;
}
export interface LoyaltySettings { enabled: boolean; pointsPerSar: number; pointValue: number; minRedeem: number; }
export interface BookingSettings { enabled: boolean; slug: string | null; slotMinutes: number; maxDaysAhead: number; }

export interface Settings {
  organization: {
    id: string; name: string; currency: string; timeZone: string;
    logoUrl?: string | null;
    primaryColor?: string | null;
    secondaryColor?: string | null;
    coverImageUrl?: string | null;
    slug?: string | null;
  } | null;
  settings: { enableCash: boolean; enableCard: boolean; receiptFooter?: string; receiptPhone?: string; maxManualDiscountPercent: number };
  tax: TaxSettings;
  loyalty: LoyaltySettings;
  booking: BookingSettings;
}
