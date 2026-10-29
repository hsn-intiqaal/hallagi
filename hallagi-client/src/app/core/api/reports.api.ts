import { inject, Injectable } from '@angular/core';
import { ApiService } from '@core/http/api.service';
import { BarberDuesReport, BranchesReport, CustomersReport, ExpensesReport, SalesReport, TaxReport } from '@core/models';

// a type alias (not an interface) so it is assignable to the API's index-signature QueryParams
// eslint-disable-next-line @typescript-eslint/consistent-type-definitions
export type ReportQuery = { from: string; to: string; branchId: string | null };

@Injectable({ providedIn: 'root' })
export class ReportsApi {
  private api = inject(ApiService);

  sales(q: ReportQuery) { return this.api.get<SalesReport>('/reports/sales', q); }
  expenses(q: ReportQuery) { return this.api.get<ExpensesReport>('/reports/expenses', q); }
  barberDues(q: ReportQuery) { return this.api.get<BarberDuesReport>('/reports/barber-dues', q); }
  branches(q: ReportQuery) { return this.api.get<BranchesReport>('/reports/branches', { from: q.from, to: q.to }); }
  customers(q: ReportQuery) { return this.api.get<CustomersReport>('/reports/customers', q); }
  tax(q: ReportQuery) { return this.api.get<TaxReport>('/reports/tax', q); }
  exportSalesCsv(q: ReportQuery) { return this.api.download('/sales/export.csv', q, `hallagi-sales-${q.from}_${q.to}.csv`); }
}
