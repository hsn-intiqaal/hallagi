import { Component, OnInit, inject, signal } from '@angular/core';
import { NgIf, NgFor } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ApiService } from '@core/http/api.service';
import { ToastService } from '@core/state/toast.service';
import { ThemeService } from '@core/state/theme.service';
import { SarPipe } from '@shared/pipes/format.pipes';
import { Icon } from '@shared/ui/icon';
import { resizeImageFile } from '@shared/ui/image-upload';

interface Palette { name: string; primary: string; secondary: string; }
interface Plan { id: string; name: string; description: string; maxBranches: number; maxUsers: number; maxBarbers: number; monthlyPrice: number; }

const DEFAULT_PRIMARY = '#2563EB';
const DEFAULT_SECONDARY = '#173653';

@Component({
  selector: 'app-org-create',
  standalone: true,
  imports: [NgIf, NgFor, FormsModule, RouterLink, Icon, SarPipe],
  templateUrl: './org-create.html',
  styleUrl: './org-create.scss'
})
export class OrgCreate implements OnInit {
  private api = inject(ApiService);
  private toast = inject(ToastService);
  private router = inject(Router);
  private theme = inject(ThemeService);

  readonly plans = signal<Plan[]>([]);
  readonly creating = signal(false);
  readonly logoUrl = signal<string | null>(null);
  readonly coverUrl = signal<string | null>(null);

  form = {
    name: '', city: 'الرياض', branchName: 'الفرع الرئيسي', address: '',
    ownerName: '', ownerEmail: '', ownerPhone: '', ownerPassword: '',
    planId: '', primaryColor: DEFAULT_PRIMARY, secondaryColor: DEFAULT_SECONDARY
  };

  readonly palettes: Palette[] = [
    { name: 'كحلي كلاسيكي', primary: '#2563EB', secondary: '#173653' },
    { name: 'ذهبي ملكي', primary: '#C79A3B', secondary: '#2A2113' },
    { name: 'زمردي', primary: '#0E9F6E', secondary: '#0B3B2E' },
    { name: 'عنابي', primary: '#BE123C', secondary: '#3F0D1F' },
    { name: 'برتقالي', primary: '#EA580C', secondary: '#431407' },
    { name: 'بنفسجي', primary: '#7C3AED', secondary: '#2E1065' },
    { name: 'فيروزي', primary: '#0D9488', secondary: '#134E4A' }
  ];

  ngOnInit(): void {
    this.api.get<Plan[]>('/platform/plans').subscribe({
      next: (res: any) => {
        this.plans.set(res ?? []);
        this.form.planId = res?.[0]?.id ?? '';
      },
      error: () => this.plans.set([])
    });
  }

  applyPreset(p: Palette): void {
    this.form.primaryColor = p.primary;
    this.form.secondaryColor = p.secondary;
  }

  generatePassword(): void {
    const chars = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789';
    let s = '';
    for (let i = 0; i < 10; i++) s += chars[Math.floor(Math.random() * chars.length)];
    this.form.ownerPassword = `${s}7#`;
  }

  removeLogo(): void { this.logoUrl.set(null); }
  removeCover(): void { this.coverUrl.set(null); }

  /** Read an image file, downscale it on a canvas, and keep the compressed data URL. */
  async pickImage(kind: 'logo' | 'cover', event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    if (!file.type.startsWith('image/')) { this.toast.error('الملف المختار ليس صورة'); return; }
    try {
      const dataUrl = await resizeImageFile(file, kind === 'logo' ? 256 : 1280, kind === 'logo' ? 0.85 : 0.75);
      if (kind === 'logo') this.logoUrl.set(dataUrl);
      else this.coverUrl.set(dataUrl);
    } catch {
      this.toast.error('تعذّر معالجة الصورة، جرّب صورة أخرى');
    }
  }

  create(): void {
    if (this.creating()) return;
    if (!this.form.name.trim()) { this.toast.error('اسم المنشأة مطلوب'); return; }
    if (!this.form.city.trim()) { this.toast.error('المدينة مطلوبة'); return; }
    if (!this.form.ownerEmail.trim()) { this.toast.error('بريد المالك مطلوب'); return; }
    if (this.form.ownerPassword.length < 6) { this.toast.error('كلمة مرور المالك يجب أن تكون 6 أحرف على الأقل'); return; }

    this.creating.set(true);
    this.api.post<any>('/platform/organizations', {
      name: this.form.name.trim(),
      city: this.form.city.trim(),
      branchName: this.form.branchName.trim() || 'الفرع الرئيسي',
      address: this.form.address.trim() || null,
      ownerName: this.form.ownerName.trim() || 'مالك المنشأة',
      ownerEmail: this.form.ownerEmail.trim(),
      ownerPhone: this.form.ownerPhone.trim() || null,
      ownerPassword: this.form.ownerPassword,
      planId: this.form.planId || null,
      logoUrl: this.logoUrl(),
      coverImageUrl: this.coverUrl(),
      primaryColor: this.form.primaryColor,
      secondaryColor: this.form.secondaryColor
    }).subscribe({
      next: () => {
        this.creating.set(false);
        this.toast.success('تم إنشاء المنشأة وحساب مالكها');
        this.router.navigate(['/platform']);
      },
      error: (e: any) => { this.creating.set(false); this.toast.error(e?.message ?? 'تعذّر إنشاء المنشأة'); }
    });
  }

  // ===== Live preview helpers =====

  private safe(hex: string | null | undefined, fallback: string): string {
    return hex && /^#([0-9a-fA-F]{6})$/.test(hex) ? hex : fallback;
  }

  previewTopbar(): string {
    const sec = this.safe(this.form.secondaryColor, DEFAULT_SECONDARY);
    return `linear-gradient(180deg, ${this.theme.lighten(sec, 0.14)}, ${sec})`;
  }

  heroBg(): string {
    const sec = this.safe(this.form.secondaryColor, DEFAULT_SECONDARY);
    return `linear-gradient(135deg, ${sec}, ${this.theme.lighten(sec, 0.22)})`;
  }

  coverGradient(): string {
    const pri = this.safe(this.form.primaryColor, DEFAULT_PRIMARY);
    return `linear-gradient(135deg, ${pri}, ${this.theme.darken(pri, 0.3)})`;
  }

  tintPrimary(): string {
    return this.theme.tint(this.safe(this.form.primaryColor, DEFAULT_PRIMARY), 0.9);
  }

  safePrimary(): string { return this.safe(this.form.primaryColor, DEFAULT_PRIMARY); }
  safeSecondary(): string { return this.safe(this.form.secondaryColor, DEFAULT_SECONDARY); }
}
