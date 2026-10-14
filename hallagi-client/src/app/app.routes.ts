import { Routes } from '@angular/router';
import { authGuard, permissionGuard, roleGuard, superAdminGuard } from './core/guards';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
  {
    path: 'login',
    loadComponent: () => import('./features/auth/login').then(m => m.Login)
  },
  {
    path: 'platform',
    canActivate: [superAdminGuard],
    loadComponent: () => import('./features/platform/platform').then(m => m.Platform)
  },
  {
    path: 'onboarding',
    canActivate: [authGuard],
    loadComponent: () => import('./features/onboarding/onboarding').then(m => m.Onboarding)
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./layout/layout').then(m => m.Layout),
    children: [
      { path: 'dashboard', canActivate: [roleGuard(['Owner', 'BranchManager', 'Cashier'])], loadComponent: () => import('./features/dashboard/dashboard').then(m => m.Dashboard) },

      // ===== POS =====
      { path: 'pos', canActivate: [permissionGuard('sale.create')], loadComponent: () => import('./features/pos/pos').then(m => m.Pos) },
      { path: 'sales', canActivate: [permissionGuard('sale.view')], loadComponent: () => import('./features/sales/sales-list').then(m => m.SalesList) },
      { path: 'sales/:id', canActivate: [permissionGuard('sale.view')], loadComponent: () => import('./features/sales/sale-detail').then(m => m.SaleDetail) },

      // ===== People =====
      { path: 'customers', canActivate: [permissionGuard('customer.manage')], loadComponent: () => import('./features/customers/customers-list').then(m => m.CustomersList) },
      { path: 'customers/:id', canActivate: [permissionGuard('customer.manage')], loadComponent: () => import('./features/customers/customer-profile').then(m => m.CustomerProfile) },
      { path: 'barbers', canActivate: [permissionGuard('barber.view')], loadComponent: () => import('./features/barbers/barbers-list').then(m => m.BarbersList) },
      { path: 'barbers/new', canActivate: [permissionGuard('barber.manage')], loadComponent: () => import('./features/barbers/barber-form').then(m => m.BarberForm) },
      { path: 'barbers/:id/edit', canActivate: [permissionGuard('barber.manage')], loadComponent: () => import('./features/barbers/barber-form').then(m => m.BarberForm) },
      { path: 'barbers/:id', canActivate: [permissionGuard('barber.view')], loadComponent: () => import('./features/barbers/barber-detail').then(m => m.BarberDetailPage) },

      // ===== Catalog =====
      { path: 'services', canActivate: [permissionGuard('service.view')], loadComponent: () => import('./features/services/services').then(m => m.Services) },

      // ===== Money =====
      { path: 'expenses', canActivate: [permissionGuard('expense.view')], loadComponent: () => import('./features/expenses/expenses').then(m => m.Expenses) },
      { path: 'treasury', canActivate: [permissionGuard('shift.view')], loadComponent: () => import('./features/treasury/treasury').then(m => m.Treasury) },
      { path: 'reports', canActivate: [permissionGuard('report.view')], loadComponent: () => import('./features/reports/reports').then(m => m.Reports) },

      // ===== Financial management =====
      { path: 'finance/settlements', canActivate: [permissionGuard('settlement.preview')], loadComponent: () => import('./features/finance/settlements').then(m => m.Settlements) },
      { path: 'finance/handovers', canActivate: [permissionGuard('handover.create')], loadComponent: () => import('./features/finance/handovers').then(m => m.Handovers) },
      { path: 'finance/ledger/:id', canActivate: [permissionGuard('ledger.view')], loadComponent: () => import('./features/finance/ledger').then(m => m.BarberLedger) },
      { path: 'devices', canActivate: [permissionGuard('device.manage')], loadComponent: () => import('./features/devices/devices').then(m => m.Devices) },

      // ===== Admin =====
      { path: 'branches', canActivate: [permissionGuard('branch.manage')], loadComponent: () => import('./features/branches/branches').then(m => m.Branches) },
      { path: 'users', canActivate: [permissionGuard('user.manage')], loadComponent: () => import('./features/users/users').then(m => m.Users) },
      { path: 'settings', loadComponent: () => import('./features/settings/settings').then(m => m.Settings) },

      // ===== Barber self-service =====
      { path: 'me', canActivate: [roleGuard(['Barber'])], loadComponent: () => import('./features/me/my-summary').then(m => m.MySummary) }
    ]
  },
  { path: '**', redirectTo: 'dashboard' }
];
