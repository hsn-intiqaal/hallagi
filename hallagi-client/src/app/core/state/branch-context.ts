import { computed, inject, Injectable, signal } from '@angular/core';
import { Branch } from '@core/models';
import { AuthService, branchStorage } from '@core/auth/auth.service';

@Injectable({ providedIn: 'root' })
export class BranchContext {
  private auth = inject(AuthService);
  readonly branches = computed<Branch[]>(() => this.auth.me()?.branches ?? []);
  /** null = كل الفروع (owner only) */
  readonly currentBranchId = signal<string | null>(null);
  readonly currentBranch = computed<Branch | null>(() => {
    const id = this.currentBranchId();
    if (!id) return null;
    return this.branches().find(b => b.id === id) ?? null;
  });
  readonly showAllOption = computed(() => this.auth.me()?.allBranches === true);
  /** True once the session is loaded and a branch scope has been chosen — pages render only after this. */
  readonly ready = signal(false);

  init(): void {
    const me = this.auth.me();
    if (!me) return;
    this.ready.set(true);
    const stored = branchStorage.get();
    const isValid = stored && me.branchIds.includes(stored);
    const first = me.branches[0]?.id ?? null;
    this.currentBranchId.set(stored && isValid ? stored : first);
  }

  select(id: string | null): void {
    const me = this.auth.me();
    if (id !== null && me && !me.branchIds.includes(id)) return; // never allow unauthorized branches
    this.currentBranchId.set(id);
    if (id) branchStorage.set(id); else branchStorage.clear();
  }

  /** branchId to send to the API for scoped queries; null when viewing all branches */
  scopeId(): string | null {
    if (this.showAllOption()) return this.currentBranchId();
    return this.currentBranchId() ?? this.branches()[0]?.id ?? null;
  }
}
