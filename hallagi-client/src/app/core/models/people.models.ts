export interface CustomerRow { id: string; fullName: string; phone?: string; notes?: string; loyaltyPoints?: number; visitCount: number; totalSpend: number; lastVisit: string | null; }
export interface CustomerFile {
  customer: { id: string; fullName: string; phone?: string; notes?: string; createdAt: string; loyaltyPoints: number };
  appointments: { id: string; startAt: string; status: string; reference: string; branchName: string }[];
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
