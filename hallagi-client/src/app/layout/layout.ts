import { NgIf, NgFor } from '@angular/common';
import { Component, HostListener, OnInit, computed, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '@core/auth/auth.service';
import { BranchContext } from '@core/state/branch-context';
import { ToastService } from '@core/state/toast.service';
import { UserRoleLabel } from '@core/models';
import { Icon } from '@shared/ui/icon';
import { Modal } from '@shared/ui/ui';

interface NavItem {
  path: string;
  label: string;
  icon: string; // lucide icon name key
  permission?: string;
  roles?: string[];
}

@Component({
  selector: 'app-layout',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, Icon, NgIf, NgFor, Modal],
  templateUrl: './layout.html',
  styleUrl: './layout.scss'
})
export class Layout implements OnInit {
  readonly auth = inject(AuthService);
  readonly branches = inject(BranchContext);
  private toast = inject(ToastService);
  private router = inject(Router);

  readonly menuOpen = signal(false);
  readonly userMenuOpen = signal(false);
  readonly logoutConfirmOpen = signal(false);
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
