// ===== حلاقي — data models (mirroring the API contracts) =====

export type UserRole = 'SuperAdmin' | 'Owner' | 'BranchManager' | 'Cashier' | 'Barber';
export const UserRoleLabel: Record<UserRole, string> = {
  SuperAdmin: 'مالك المنصة',
  Owner: 'مالك',
  BranchManager: 'مدير فرع',
  Cashier: 'كاشير',
  Barber: 'حلاق'
};

export interface Branch { id: string; name: string; city: string; address?: string; status?: string; barberCount?: number; userCount?: number; hasOpenShift?: boolean; }

export interface Profile { id: string; fullName: string; email: string; phone?: string; }
export interface LoginResult {
  token: string;
  profile: Profile;
  member: { id: string | null; role: UserRole; organizationId: string | null; organizationName: string };
  branches: Branch[];
  expiresInMinutes: number;
}
export interface Me {
  profile: Profile;
  role: UserRole;
  organization: { id: string; name: string; currency: string; timeZone: string; logoUrl?: string } | null;
  branchIds: string[];
  branches: Branch[];
  allBranches: boolean;
  isSuperAdmin: boolean;
  permissions: Record<string, boolean>;
}

export interface Dashboard {
  period: string; from: string; to: string;
  branches: Branch[];
  drawer: { id: string; openingCash: number; expectedCash: number } | null;
  staleOpenShift: boolean;
  metrics: {
    totalSales: number; servicesCount: number; expensesTotal: number;
    netOperatingEstimate: number; netOperatingHint: string;
    cashSales: number; cardSales: number; salesCount: number;
  };
  comparison: { previousTotalSales: number; changePercent: number | null; changeText: string; };
  lastSales: { id: string; receiptNo: number; createdAt: string; totalAmount: number; paymentText: string; branchName: string; barberName: string; customerName: string; }[];
  topServices: { serviceName: string; quantity: number; total: number; }[];
  chart: { items: { day: string; label: string; total: number; count: number }[] };
}

export interface PosService { id: string; name: string; description?: string; price: number; status?: string; statusText?: string; basePrice?: number; hasBranchPrice?: boolean; }
export interface PosBarber { id: string; fullName: string; initials: string; branchNames: string[]; }
export interface PosCustomer { id: string; fullName: string; phone?: string; }
export interface PosData {
  branch: { id: string; name: string; city: string };
  services: PosService[];
  barbers: PosBarber[];
  customers: PosCustomer[];
  payment: { cashEnabled: boolean; cardEnabled: boolean; methods: { value: string; label: string; enabled: boolean }[] };
}

export type PaymentMethod = 'Cash' | 'Card';
export type SaleStatus = 'Completed' | 'Cancelled';

export interface SaleItem { serviceId: string; serviceName: string; unitPrice: number; quantity: number; lineTotal: number; }
export interface Sale {
  id: string; receiptNo: number; createdAt: string;
  branchId: string; branchName: string; barberId: string; barberName: string;
  customerId: string | null; customerName: string; customerPhone?: string | null;
  paymentMethod: PaymentMethod; paymentText: string;
  status: SaleStatus; statusText: string;
  totalAmount: number; items?: SaleItem[];
  servicesText?: string;
}
export interface SaleCreated { replayed: boolean; sale: Sale; shiftId: string | null; }

export interface CustomerRow { id: string; fullName: string; phone?: string; notes?: string; visitCount: number; totalSpend: number; lastVisit: string | null; }
export interface CustomerFile {
  customer: { id: string; fullName: string; phone?: string; notes?: string; createdAt: string };
  summary: { total: number; count: number; lastVisit: string | null };
  visits: { id: string; receiptNo: number; createdAt: string; totalAmount: number; statusText: string; paymentText: string; servicesText: string; barberName: string; branchName: string; }[];
}

export interface BarberRow {
  id: string; fullName: string; phone?: string; status: string; statusText: string; startDate?: string | null;
  branchNames: string[]; salesTotal: number; salesCount: number;
  compensation: { type: string; typeText: string; monthlySalary: number | null; commissionRate: number | null; chairRentAmount: number | null; notes: string | null; effectiveFrom: string } | null;
}
export interface BarberDetail {
  barber: { id: string; fullName: string; phone?: string; startDate?: string | null; status: string; statusText: string; branchNames: string[] };
  compensation: {
    type: string; typeText: string; monthlySalary: number | null; commissionRate: number | null;
    chairRentAmount: number | null; notes: string | null; effectiveFrom: string;
    estimatedCommission: number | null; explanation: string | null;
  } | null;
  summary: { total: number; count: number; servicesCount: number };
  from: string; to: string;
  sales: { id: string; receiptNo: number; createdAt: string; totalAmount: number; paymentText: string; servicesText: string; customerName: string; branchName: string; }[];
}

export type ExpenseCategory = 'Rent' | 'ElectricityWater' | 'Supplies' | 'Maintenance' | 'Salaries' | 'Commissions' | 'Other';
export type ExpenseSource = 'CashDrawer' | 'Bank' | 'Other';
export const ExpenseCategoryLabel: Record<ExpenseCategory, string> = {
  Rent: 'إيجار', ElectricityWater: 'كهرباء ومياه', Supplies: 'مستلزمات',
  Maintenance: 'صيانة', Salaries: 'رواتب', Commissions: 'عمولات', Other: 'أخرى'
};
export const ExpenseSourceLabel: Record<ExpenseSource, string> = {
  CashDrawer: 'درج النقدية', Bank: 'حساب بنكي', Other: 'مصدر آخر'
};
export interface ExpenseRow {
  id: string; expenseDate: string; category: ExpenseCategory; categoryText: string;
  amount: number; description?: string; source: ExpenseSource; sourceText: string;
  status: string; statusText: string; branchName: string; createdByName: string;
}
export interface TreasuryState {
  branch: { id: string; name: string; city: string };
  hasOpenShift: boolean;
  current: {
    id: string; openingCash: number; openedAt: string; autoOpened: boolean; openedByName: string;
    cashSales: number; cardSales: number; cashExpenses: number; movementsIn: number; movementsOut: number; expectedCash: number;
  } | null;
  drawerBalanceWhenClosed: number;
  lastClosedAt: string | null;
  openStale: boolean;
  staleOpenShift?: boolean;
}
export interface ShiftState {
  id: string; branchId: string; branchName: string; openingCash: number; openedAt: string; closedAt: string | null;
  status: 'Open' | 'Closed'; countedCash: number | null; expectedCash: number | null; difference: number | null;
  note: string | null; autoOpened: boolean;
  cashSales: number; cardSales: number; cashExpenses: number; movementsIn: number; movementsOut: number;
  differenceText: string;
}
export interface ShiftRow {
  id: string; openedAt: string; closedAt: string | null; status: 'Open' | 'Closed'; statusText: string;
  openingCash: number; expectedCash: number | null; countedCash: number | null; difference: number | null;
  branchName: string; openedByName: string;
}

export interface UserRow {
  id: string; profileId: string; fullName: string; email: string; phone?: string;
  role: UserRole; roleText: string; status: 'Active' | 'Disabled'; statusText: string;
  branches: string[]; branchIds: string[];
  permissions: Record<string, boolean> | null;
}

export interface Settings {
  organization: { id: string; name: string; currency: string; timeZone: string; logoUrl?: string } | null;
  settings: { enableCash: boolean; enableCard: boolean; receiptFooter?: string; receiptPhone?: string };
}

export interface ApiError { error: { code: string; message: string } }
