// Management reports — `from`/`to` are the salon's local calendar days (YYYY-MM-DD, inclusive).

export interface ReportSummary {
  salesCount: number;
  collected: number;        // paid by customers, VAT inclusive
  gross: number;            // catalog value before discounts
  discounts: number;
  netSales: number;         // collected − VAT
  vat: number;
  cashCollected: number;
  cardCollected: number;
  averageTicket: number;
  servicesRevenue: number;
  productsRevenue: number;
  productCost: number;
  expenses: number;
  operatingResult: number;  // net sales − product cost − expenses
  servicesCount: number;
}

export interface SalesReport {
  from: string; to: string; days: number;
  summary: ReportSummary;
  comparison: { previousFrom: string; previousTo: string; previousNetSales: number; changePercent: number | null };
  daily: { day: string; label: string; collected: number; netSales: number; count: number }[];
  byHour: { hour: number; count: number; netSales: number }[];
  byWeekday: { weekday: number; label: string; count: number; netSales: number }[];
  byService: { label: string; quantity: number; total: number }[];
  byBarber: { barberId: string; label: string; count: number; netSales: number; commissionBase: number; averageTicket: number }[];
}

export interface ExpensesReport {
  from: string; to: string; total: number;
  items: { category: string; categoryText: string; total: number; count: number; share: number }[];
}

export interface BarberDuesReport {
  from: string; to: string; totalCommission: number;
  items: {
    barberId: string; barberName: string; active: boolean; branchNames: string[];
    salesCount: number; eligibleSales: number;
    compensationType: string | null; compensationTypeText: string;
    monthlySalary: number | null; commissionRate: number | null; estimatedCommission: number;
    chairRent: number | null; explanation: string;
  }[];
}

export interface BranchesReport {
  from: string; to: string;
  items: {
    branchId: string; branchName: string; city: string; salesCount: number; collected: number; netSales: number;
    averageTicket: number; expenses: number; productCost: number; operatingResult: number; share: number;
  }[];
}

export interface CustomersReport {
  from: string; to: string;
  summary: { identifiedVisits: number; walkInVisits: number; uniqueCustomers: number; newCustomers: number; returningCustomers: number; repeatInPeriod: number };
  items: { customerId: string; customerName: string; phone: string | null; visits: number; total: number; lastVisit: string }[];
}

export interface TaxReport {
  from: string; to: string;
  tax: { gross: number; discount: number; taxable: number; vat: number; total: number; invoices: number; nonTaxable: number };
  loyalty: { pointsEarned: number; pointsRedeemed: number };
  coupons: { code: string; uses: number; discount: number }[];
  products: { name: string; quantity: number; revenue: number; cost: number; profit: number }[];
}
