// Barber-related model types (kept local to the barbers feature)
export interface BarberRow {
  id: string;
  fullName: string;
  phone?: string;
  status: string;
  statusText: string;
  startDate?: string | null;
  branchNames: string[];
  salesTotal: number;
  salesCount: number;
  compensation: {
    type: string;
    typeText: string;
    monthlySalary: number | null;
    commissionRate: number | null;
    chairRentAmount: number | null;
    notes: string | null;
    effectiveFrom: string;
  } | null;
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

export type CompensationType = 'FixedSalary' | 'Commission' | 'FixedPlusCommission' | 'ChairRental';

export const CompensationTypeLabel: Record<CompensationType, string> = {
  FixedSalary: 'راتب ثابت',
  Commission: 'عمولة',
  FixedPlusCommission: 'راتب ثابت + عمولة',
  ChairRental: 'تأجير كرسي'
};

export interface BarberFormModel {
  id?: string;
  fullName: string;
  phone: string;
  startDate: string;
  active: boolean;
  branchIds: string[];
  compensation: {
    type: CompensationType;
    monthlySalary: number | null;
    commissionRate: number | null;
    chairRentAmount: number | null;
    notes: string;
  } | null;
}
