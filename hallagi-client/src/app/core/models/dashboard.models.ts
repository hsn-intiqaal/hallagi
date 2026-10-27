import { Branch } from './auth.models';

export interface Dashboard {
  period: string; from: string; to: string; today: string;
  branches: Branch[];
  drawer: { id: string; openingCash: number; expectedCash: number } | null;
  staleOpenShift: boolean;
  metrics: {
    totalSales: number;           // collected, VAT inclusive
    netSales: number;             // excluding VAT
    vat: number; discounts: number;
    servicesCount: number; salesCount: number; averageTicket: number;
    expensesTotal: number; productCost: number;
    netOperatingEstimate: number; netOperatingHint: string;
    cashSales: number; cardSales: number;
  };
  comparison: { previousNetSales: number; changePercent: number | null; changeText: string };
  signals: { appointmentsToday: number; appointmentsRemaining: number; waiting: number; lowStockProducts: number };
  lastSales: { id: string; receiptNo: number; createdAt: string; totalAmount: number; paymentText: string; branchName: string; barberName: string; customerName: string }[];
  topServices: { serviceName: string; quantity: number; total: number }[];
  chart: { items: { day: string; label: string; isToday: boolean; total: number; count: number }[] };
}
