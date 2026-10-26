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
