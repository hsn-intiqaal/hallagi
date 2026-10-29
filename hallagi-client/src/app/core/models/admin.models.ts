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

export interface Device {
  id: string; branchId: string; branchName: string; name: string; deviceCode: string;
  status: 'Active' | 'Disabled'; statusText: string; openShifts: number;
}

export interface SubscriptionPlan {
  id: string; name: string; description: string | null; maxBranches: number; maxUsers: number; maxBarbers: number;
  allowAdvancedReports: boolean; allowSettlements: boolean; monthlyPrice: number;
}
export interface CurrentSubscription {
  hasSubscription: boolean; status?: string; periodStart?: string; periodEnd?: string | null; trialEndsAt?: string | null;
  plan?: SubscriptionPlan;
}

export interface PlatformStats {
  organizationsCount: number; branchesCount: number; usersCount: number; barbersCount: number;
  salesCount: number; salesTotal: number; activeSubscriptions: number;
}
