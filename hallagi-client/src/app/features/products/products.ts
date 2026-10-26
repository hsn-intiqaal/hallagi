import { NgFor, NgIf } from '@angular/common';
import { Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ProductsApi } from '@core/api/inventory.api';
import { AuthService } from '@core/auth/auth.service';
import { ApiFailure } from '@core/http/api.service';
import { ProductList, ProductRow, StockMovementRow, StockMovementType } from '@core/models';
import { BranchContext } from '@core/state/branch-context';
import { ToastService } from '@core/state/toast.service';
import { DateTimeArPipe, SarPipe } from '@shared/pipes/format.pipes';
import { Icon } from '@shared/ui/icon';
import { EmptyState, LoadingRow, Modal, StatCard } from '@shared/ui/ui';

interface ProductForm { id: string | null; name: string; sku: string; barcode: string; price: number | null; cost: number | null; active: boolean; }
interface StockForm { product: ProductRow; branchId: string; type: StockMovementType; quantity: number | null; unitCost: number | null; note: string; lowStockThreshold: number | null; }

@Component({
  selector: 'app-products',
  standalone: true,
  imports: [NgIf, NgFor, FormsModule, Icon, SarPipe, DateTimeArPipe, LoadingRow, Modal, EmptyState, StatCard],
  templateUrl: './products.html',
  styleUrl: './products.scss'
})
export class Products {
  private productsApi = inject(ProductsApi);
  private branches = inject(BranchContext);
  private toast = inject(ToastService);
  readonly auth = inject(AuthService);

  readonly list = signal<ProductList | null>(null);
  readonly loading = signal(true);
  readonly lowStockOnly = signal(false);
  readonly form = signal<ProductForm | null>(null);
  readonly stockForm = signal<StockForm | null>(null);
  readonly history = signal<{ product: ProductRow; items: StockMovementRow[] } | null>(null);
  readonly saving = signal(false);
  search = '';

  readonly canManage = computed(() => this.auth.can('product.manage'));
  readonly canAdjust = computed(() => this.auth.can('inventory.manage'));
  readonly branchOptions = computed(() => this.branches.branches());

  constructor() {
    effect(() => {
      this.branches.currentBranchId();
      untracked(() => this.load());
    });
  }

  load(): void {
    this.loading.set(true);
    this.productsApi.list(this.branches.scopeId(), this.search.trim() || undefined, this.lowStockOnly()).subscribe({
      next: res => { this.list.set(res); this.loading.set(false); },
      error: (e: ApiFailure) => { this.toast.error(e.message); this.loading.set(false); }
    });
  }

  toggleLowStock(): void {
    this.lowStockOnly.update(v => !v);
    this.load();
  }

  margin(p: ProductRow): string {
    return `${p.margin}%`;
  }

  // ===== product form =====

  openAdd(): void {
    this.form.set({ id: null, name: '', sku: '', barcode: '', price: null, cost: null, active: true });
  }

  openEdit(p: ProductRow): void {
    this.form.set({ id: p.id, name: p.name, sku: p.sku ?? '', barcode: p.barcode ?? '', price: p.price, cost: p.cost, active: p.status === 'Active' });
  }

  saveProduct(): void {
    const f = this.form();
    if (!f || this.saving()) return;
    if (!f.name.trim()) { this.toast.error('اسم المنتج مطلوب'); return; }
    if (f.price === null || f.price < 0) { this.toast.error('سعر البيع غير صحيح'); return; }

    const body = {
      name: f.name.trim(), sku: f.sku.trim() || null, barcode: f.barcode.trim() || null,
      price: f.price, cost: f.cost ?? 0, active: f.active
    };
    this.saving.set(true);
    (f.id ? this.productsApi.update(f.id, body) : this.productsApi.create(body)).subscribe({
      next: res => { this.saving.set(false); this.toast.success(res.message); this.form.set(null); this.load(); },
      error: (e: ApiFailure) => { this.saving.set(false); this.toast.error(e.message); }
    });
  }

  // ===== stock =====

  openStock(p: ProductRow, type: StockMovementType): void {
    const branchId = this.branches.scopeId() ?? p.branches[0]?.branchId ?? this.branchOptions()[0]?.id ?? '';
    const row = p.branches.find(b => b.branchId === branchId);
    this.stockForm.set({ product: p, branchId, type, quantity: null, unitCost: type === 'Purchase' ? p.cost : null, note: '', lowStockThreshold: row?.lowStockThreshold ?? 5 });
  }

  patchStock(patch: Partial<StockForm>): void {
    this.stockForm.update(f => (f ? { ...f, ...patch } : f));
  }

  currentQty(f: StockForm): number {
    return f.product.branches.find(b => b.branchId === f.branchId)?.quantity ?? 0;
  }

  saveStock(): void {
    const f = this.stockForm();
    if (!f || this.saving()) return;
    if (!f.quantity && f.lowStockThreshold === null) { this.toast.error('أدخل الكمية'); return; }

    // Adjustment is entered as the counted quantity; the server records the signed difference
    const quantity = f.type === 'Adjustment' ? (f.quantity ?? this.currentQty(f)) - this.currentQty(f) : (f.quantity ?? 0);
    this.saving.set(true);
    this.productsApi.adjustStock(f.product.id, {
      branchId: f.branchId, type: f.type, quantity,
      unitCost: f.type === 'Purchase' ? f.unitCost : null,
      note: f.note.trim() || null, lowStockThreshold: f.lowStockThreshold
    }).subscribe({
      next: res => { this.saving.set(false); this.toast.success(res.message); this.stockForm.set(null); this.load(); },
      error: (e: ApiFailure) => { this.saving.set(false); this.toast.error(e.message); }
    });
  }

  openHistory(p: ProductRow): void {
    this.productsApi.movements(p.id, this.branches.scopeId()).subscribe({
      next: res => this.history.set({ product: p, items: res.items }),
      error: (e: ApiFailure) => this.toast.error(e.message)
    });
  }
}
