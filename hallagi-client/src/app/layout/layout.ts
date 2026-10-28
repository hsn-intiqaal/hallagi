import { NgIf, NgFor } from '@angular/common';
import { Component, HostListener, OnInit, computed, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '@core/auth/auth.service';
import { BranchContext } from '@core/state/branch-context';
import { ToastService } from '@core/state/toast.service';
import { UserRoleLabel } from '@core/models';
import { Icon } from '@shared/ui/icon';
import { LoadingRow, Modal } from '@shared/ui/ui';
import { APP_CONFIG } from '@core/config/app-config';

interface NavItem {
  group: NavGroup;
  path: string;
  label: string;
  icon: string; // lucide icon name key
  permission?: string;
  roles?: string[];
}

type NavGroup = 'daily' | 'catalog' | 'money' | 'admin';

const NAV_GROUPS: { id: NavGroup; label: string }[] = [
  { id: 'daily', label: 'اليوم' },
  { id: 'catalog', label: 'الفريق والكتالوج' },
  { id: 'money', label: 'المالية والتقارير' },
  { id: 'admin', label: 'الإدارة' }
];

@Component({
  selector: 'app-layout',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, Icon, NgIf, NgFor, Modal, LoadingRow],
  templateUrl: './layout.html',
  styleUrl: './layout.scss'
})
export class Layout implements OnInit {
  readonly auth = inject(AuthService);
  readonly brand = inject(APP_CONFIG).brand;
  readonly branches = inject(BranchContext);
  private toast = inject(ToastService);
  private router = inject(Router);

  readonly menuOpen = signal(false);
  readonly userMenuOpen = signal(false);
  readonly logoutConfirmOpen = signal(false);
  readonly roleLabel = computed(() => (this.auth.role() ? UserRoleLabel[this.auth.role()!] : ''));

  navItems: NavItem[] = [
    { group: 'daily', path: 'dashboard', label: 'الرئيسية', icon: 'home', permission: undefined, roles: ['Owner', 'BranchManager', 'Cashier'] },
    { group: 'daily', path: 'pos', label: 'بيع جديد', icon: 'scissors', permission: 'sale.create' },
    { group: 'daily', path: 'appointments', label: 'المواعيد', icon: 'calendar', permission: 'appointment.view' },
    { group: 'daily', path: 'sales', label: 'المبيعات', icon: 'receipt', permission: 'sale.view' },
    { group: 'daily', path: 'customers', label: 'العملاء', icon: 'users', permission: 'customer.manage' },
    { group: 'catalog', path: 'barbers', label: 'الحلاقون', icon: 'user', permission: 'barber.view' },
    { group: 'catalog', path: 'services', label: 'الخدمات والأسعار', icon: 'tag', permission: 'service.manage', roles: ['Owner'] },
    { group: 'catalog', path: 'products', label: 'المنتجات والمخزون', icon: 'package', permission: 'product.view', roles: ['Owner', 'BranchManager', 'Cashier'] },
    { group: 'catalog', path: 'coupons', label: 'أكواد الخصم', icon: 'percent', permission: 'coupon.manage' },
    { group: 'money', path: 'expenses', label: 'المصروفات', icon: 'wallet', permission: 'expense.view' },
    { group: 'money', path: 'treasury', label: 'الخزينة والوردية', icon: 'safe', permission: 'shift.view' },
    { group: 'money', path: 'reports', label: 'التقارير', icon: 'chart', permission: 'report.view', roles: ['Owner', 'BranchManager'] },
    { group: 'money', path: 'finance/settlements', label: 'التسويات المالية', icon: 'receipt', permission: 'settlement.preview' },
    { group: 'money', path: 'finance/handovers', label: 'التسليمات النقدية', icon: 'cash', permission: 'handover.create' },
    { group: 'admin', path: 'branches', label: 'الفروع', icon: 'building', permission: 'branch.manage' },
    { group: 'admin', path: 'devices', label: 'أجهزة POS', icon: 'card', permission: 'device.manage' },
    { group: 'admin', path: 'users', label: 'المستخدمون والصلاحيات', icon: 'shield', permission: 'user.manage' },
    { group: 'admin', path: 'settings', label: 'الإعدادات', icon: 'settings', permission: 'settings.manage' },
    { group: 'daily', path: 'me', label: 'ملخصي', icon: 'user', roles: ['Barber'] }
  ];

  ngOnInit(): void {
    this.auth.loadMe().subscribe({ next: () => this.branches.init() });
  }

  /**
   * Sidebar sections with only the items this user may open. Memoized: returning fresh
   * objects on every change-detection pass would make *ngFor rebuild the links endlessly.
   */
  readonly visibleGroups = computed(() => {
    const items = this.visibleItems();
    return NAV_GROUPS
      .map(g => ({ label: g.label, items: items.filter(i => i.group === g.id) }))
      .filter(g => g.items.length > 0);
  });

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
      .filter(i => ['dashboard', 'pos', 'appointments', 'sales', 'customers'].includes(i.path))
      .slice(0, 4);
  }

  initials(name: string): string {
    return (name ?? 'ح').trim().charAt(0);
  }

  hasNavItem(path: string): boolean {
    return this.visibleItems().some(i => i.path === path);
  }

  /** Toggle the branch dropdown (closes the user menu). */
  toggleMenu(event: Event): void {
    event.stopPropagation();
    this.userMenuOpen.set(false);
    this.menuOpen.update(v => !v);
  }

  /** Toggle the user dropdown (closes the branch menu). */
  toggleUserMenu(event: Event): void {
    event.stopPropagation();
    this.menuOpen.set(false);
    this.userMenuOpen.update(v => !v);
  }

  selectBranch(id: string | null): void {
    this.branches.select(id);
    this.menuOpen.set(false);
  }

  allBranches(): boolean {
    return this.branches.showAllOption();
  }

  /** Close the user menu after activating one of its links. */
  closeUserMenu(): void {
    this.userMenuOpen.set(false);
  }

  /** Step 1 of sign-out: ask for confirmation before leaving. */
  requestLogout(): void {
    this.userMenuOpen.set(false);
    this.menuOpen.set(false);
    this.logoutConfirmOpen.set(true);
  }

  cancelLogout(): void {
    this.logoutConfirmOpen.set(false);
  }

  /** Step 2 of sign-out: clear the session and return to login. */
  confirmLogout(): void {
    this.logoutConfirmOpen.set(false);
    this.toast.info('تم تسجيل الخروج بنجاح');
    this.auth.logout();
  }

  /** Close open dropdowns when clicking anywhere outside them. */
  @HostListener('document:click', ['$event'])
  onDocumentClick(event: Event): void {
    const target = event.target as HTMLElement | null;
    if (!target?.closest) return;
    if (target.closest('.branch-switch') || target.closest('.user-menu')) return;
    this.menuOpen.set(false);
    this.userMenuOpen.set(false);
  }

  /** Escape closes dropdowns first, then the confirmation dialog. */
  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.menuOpen() || this.userMenuOpen()) {
      this.menuOpen.set(false);
      this.userMenuOpen.set(false);
      return;
    }
    this.logoutConfirmOpen.set(false);
  }
}
