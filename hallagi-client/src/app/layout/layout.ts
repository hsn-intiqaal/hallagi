import { NgIf, NgFor } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../core/auth.service';
import { BranchContext } from '../core/branch-context';
import { ToastService } from '../core/toast.service';
import { UserRoleLabel } from '../core/models';
import { Icon } from '../shared/icon';

interface NavItem {
  path: string;
  label: string;
  icon: string; // lucide icon name key
  permission?: string;
  roles?: string[];
}

@Component({
  selector: 'app-layout',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, Icon, NgIf, NgFor],
  templateUrl: './layout.html',
  styleUrl: './layout.scss'
})
export class Layout implements OnInit {
  readonly auth = inject(AuthService);
  readonly branches = inject(BranchContext);
  private toast = inject(ToastService);
  private router = inject(Router);

  readonly menuOpen = signal(false);
  readonly roleLabel = computed(() => (this.auth.role() ? UserRoleLabel[this.auth.role()!] : ''));

  navItems: NavItem[] = [
    { path: 'dashboard', label: 'الرئيسية', icon: 'home', permission: undefined, roles: ['Owner', 'BranchManager', 'Cashier'] },
    { path: 'pos', label: 'بيع جديد', icon: 'scissors', permission: 'sale.create' },
    { path: 'sales', label: 'المبيعات', icon: 'receipt', permission: 'sale.view' },
    { path: 'customers', label: 'العملاء', icon: 'users', permission: 'customer.manage' },
    { path: 'barbers', label: 'الحلاقون', icon: 'user', permission: 'barber.view' },
    { path: 'services', label: 'الخدمات والأسعار', icon: 'tag', permission: 'service.manage', roles: ['Owner'] },
    { path: 'expenses', label: 'المصروفات', icon: 'wallet', permission: 'expense.view' },
    { path: 'treasury', label: 'الخزينة والوردية', icon: 'safe', permission: 'shift.view' },
    { path: 'reports', label: 'التقارير', icon: 'chart', permission: 'report.view', roles: ['Owner', 'BranchManager'] },
    { path: 'finance/settlements', label: 'التسويات المالية', icon: 'receipt', permission: 'settlement.preview' },
    { path: 'finance/handovers', label: 'التسليمات النقدية', icon: 'cash', permission: 'handover.create' },
    { path: 'branches', label: 'الفروع', icon: 'building', permission: 'branch.manage' },
    { path: 'devices', label: 'أجهزة POS', icon: 'card', permission: 'device.manage' },
    { path: 'users', label: 'المستخدمون والصلاحيات', icon: 'shield', permission: 'user.manage' },
    { path: 'settings', label: 'الإعدادات', icon: 'settings', permission: 'settings.manage' },
    { path: 'me', label: 'ملخصي', icon: 'user', roles: ['Barber'] }
  ];

  ngOnInit(): void {
    this.auth.loadMe().subscribe({ next: () => this.branches.init() });
  }

  visibleItems(): NavItem[] {
    const role = this.auth.role();
    return this.navItems.filter(item => {
      if (item.roles && !item.roles.includes(role ?? '')) return false;
      if (item.permission && !this.auth.can(item.permission)) return false;
      return true;
    });
  }

  /** Mobile bottom navigation: fewer items + a prominent "بيع جديد" */
  bottomItems(): NavItem[] {
    const role = this.auth.role();
    if (role === 'Barber') return this.visibleItems().slice(0, 4);
    return this.visibleItems()
      .filter(i => ['dashboard', 'pos', 'sales', 'customers', 'treasury'].includes(i.path))
      .slice(0, 4);
  }

  initials(name: string): string {
    return (name ?? 'ح').trim().charAt(0);
  }

  logout(): void {
    this.auth.logout();
  }

  toggleMenu(): void {
    this.menuOpen.update(v => !v);
  }

  selectBranch(id: string | null): void {
    this.branches.select(id);
    this.menuOpen.set(false);
  }

  allBranches(): boolean {
    return this.branches.showAllOption();
  }
}
