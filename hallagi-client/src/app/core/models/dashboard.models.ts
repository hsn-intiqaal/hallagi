import { Branch } from './auth.models';

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
