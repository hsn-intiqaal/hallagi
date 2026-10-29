import { test, expect } from '@playwright/test';

const API = 'http://localhost:5095';
const APP = 'http://localhost:4300';

const OWNER = { email: 'owner@hallagi.sa', password: 'Hallagi@123' };
const CASHIER = { email: 'cashier@hallagi.sa', password: 'Hallagi@123' };
const BARBER = { email: 'barber@hallagi.sa', password: 'Hallagi@123' };
const ADMIN = { email: 'admin@hallagi.sa', password: 'Hallagi@123' };

async function tokenFor(page: any, user: { email: string; password: string }): Promise<string> {
  const res = await page.request.post(`${API}/api/auth/login`, { data: user });
  const body = await res.json();
  return body.token as string;
}

test('شاشة الدخول عربية RTL مع شعار حلاقي', async ({ page }) => {
  await page.goto(APP);
  await expect(page.locator('h1', { hasText: 'حلاقي' }).first()).toBeVisible();
  await expect(page.getByText('محلك في يدك')).toBeVisible();
  const dir = await page.evaluate(() => document.documentElement.dir);
  expect(dir).toBe('rtl');
  const lang = await page.evaluate(() => document.documentElement.lang);
  expect(lang).toBe('ar');
});

test('دخول المالك يفتح لوحة الرئيسية مع مؤشرات المبيعات', async ({ page }) => {
  const token = await tokenFor(page, OWNER);
  await page.addInitScript(`localStorage.setItem('hallagi.token', '${token}')`);
  await page.goto(`${APP}/dashboard`);
  await expect(page.getByRole('heading', { name: /مرحبًا/ })).toBeVisible({ timeout: 20000 });
  await expect(page.locator('.stat-label', { hasText: 'صافي مبيعات اليوم' })).toBeVisible();
  await expect(page.locator('.stat-label', { hasText: 'نتيجة التشغيل' })).toBeVisible();
  // icons must actually render (Angular's sanitizer used to strip their SVG paths)
  expect(await page.locator('.sidebar app-icon svg path').count()).toBeGreaterThan(10);
  await expect(page.locator('.money-split')).toBeVisible();
});

test('رحلة بيع كاملة: خدمة + حلاق + حفظ + شاشة تأكيد', async ({ page }) => {
  const token = await tokenFor(page, CASHIER);
  await page.addInitScript(`localStorage.setItem('hallagi.token', '${token}')`);
  await page.goto(`${APP}/pos`);
  await expect(page.getByRole('heading', { name: 'بيع جديد' })).toBeVisible({ timeout: 20000 });

  const serviceCards = page.locator('.svc-card');
  await expect(serviceCards.first()).toBeVisible();
  await serviceCards.first().click();
  await expect(page.locator('.svc-card .count').first()).toHaveText('1');

  await page.locator('.barber-chip').first().click();
  await expect(page.locator('.total-bar .v')).toContainText('ر.س');

  await page.getByRole('button', { name: /تأكيد البيع وحفظه/ }).click();

  await expect(page.locator('.success h1', { hasText: 'تم تسجيل البيع بنجاح' })).toBeVisible({ timeout: 20000 });
  await expect(page.getByText('رقم العملية')).toBeVisible();
  await expect(page.getByRole('button', { name: 'بيع جديد' })).toBeVisible();
  // the demo salon is VAT-registered, so every sale is a simplified tax invoice
  await expect(page.getByRole('button', { name: /عرض الفاتورة|عرض الإيصال/ })).toBeVisible();
});

test('شاشة التأكيد تعرض فاتورة ضريبية مبسطة مع رمز QR', async ({ page }) => {
  const token = await tokenFor(page, CASHIER);
  await page.addInitScript(`localStorage.setItem('hallagi.token', '${token}')`);
  await page.goto(`${APP}/pos`);

  const serviceCards = page.locator('.svc-card');
  await expect(serviceCards.first()).toBeVisible({ timeout: 20000 });
  await serviceCards.first().click();
  await page.locator('.barber-chip').first().click();
  await page.getByRole('button', { name: /تأكيد البيع وحفظه/ }).click();
  await expect(page.locator('.success h1', { hasText: 'تم تسجيل البيع بنجاح' })).toBeVisible({ timeout: 20000 });

  await page.getByRole('button', { name: /عرض الفاتورة/ }).click();
  await expect(page.locator('.receipt')).toBeVisible({ timeout: 20000 });
  await expect(page.getByText('فاتورة ضريبية مبسطة')).toBeVisible();
  await expect(page.getByText('الرقم الضريبي')).toBeVisible();
  await expect(page.getByText(/ضريبة القيمة المضافة/)).toBeVisible();
  // ZATCA QR rendered as an image data URL
  await expect(page.locator('.r-qr img')).toHaveAttribute('src', /^data:image\/png;base64,/, { timeout: 10000 });
});

test('الحلاق يرى معلوماته فقط: لا مصروفات ولا خزينة في القائمة', async ({ page }) => {
  const token = await tokenFor(page, BARBER);
  await page.addInitScript(`localStorage.setItem('hallagi.token', '${token}')`);
  await page.goto(`${APP}/dashboard`);
  await page.waitForURL(/\/me/, { timeout: 20000 });
  await expect(page.getByRole('heading', { name: 'ملخصي' })).toBeVisible();

  const sideLinks = await page.locator('.side-link').allTextContents();
  for (const label of ['المصروفات', 'الخزينة والوردية', 'التقارير', 'المستخدمون والصلاحيات', 'الفروع']) {
    expect(sideLinks.join(' ')).not.toContain(label);
  }
  await expect(page.getByText('معلوماتك الشخصية المصرح بها فقط')).toBeVisible();
});

test('الوصول المباشر للخزينة كحلاق يُرفض', async ({ page }) => {
  const token = await tokenFor(page, BARBER);
  await page.addInitScript(`localStorage.setItem('hallagi.token', '${token}')`);
  await page.goto(`${APP}/treasury`);
  await page.waitForURL(/\/me|\/dashboard/, { timeout: 20000 });
  await expect(page.getByText('ليس لديك صلاحية')).toBeVisible();
});

test('تبديل الفرع من الشريط العلوي يعمل للمالك', async ({ page }) => {
  const token = await tokenFor(page, OWNER);
  await page.addInitScript(`localStorage.setItem('hallagi.token', '${token}')`);
  await page.goto(`${APP}/dashboard`);
  await expect(page.locator('.branch-btn')).toBeVisible({ timeout: 20000 });
  const firstBranch = await page.locator('.branch-btn span').first().textContent();
  await page.locator('.branch-btn').click();
  await page.locator('.branch-menu button').nth(1).click();
  await expect(page.locator('.branch-menu')).toBeHidden();
});

test('قائمة المستخدم تفتح وتعرض الدور والبريد', async ({ page }) => {
  const token = await tokenFor(page, OWNER);
  await page.addInitScript(`localStorage.setItem('hallagi.token', '${token}')`);
  await page.goto(`${APP}/dashboard`);
  await expect(page.locator('.user-btn')).toBeVisible({ timeout: 20000 });

  await page.locator('.user-btn').click();
  const dropdown = page.locator('.user-dropdown');
  await expect(dropdown).toBeVisible();
  await expect(dropdown.locator('.user-card-email')).toHaveText('owner@hallagi.sa');
  await expect(dropdown.getByText('مالك', { exact: true })).toBeVisible();
  await expect(dropdown.locator('.menu-item-danger')).toBeVisible();

  // النقر خارج القائمة يغلقها
  await page.locator('h1').first().click();
  await expect(dropdown).toBeHidden();
});

test('تسجيل الخروج يتطلب تأكيدًا ثم يعيد لصفحة الدخول', async ({ page }) => {
  const token = await tokenFor(page, OWNER);
  await page.addInitScript(`localStorage.setItem('hallagi.token', '${token}')`);
  await page.goto(`${APP}/dashboard`);
  await expect(page.locator('.user-btn')).toBeVisible({ timeout: 20000 });

  // طلب الخروج من قائمة المستخدم يفتح نافذة التأكيد
  await page.locator('.user-btn').click();
  await page.locator('.menu-item-danger').click();
  const modal = page.locator('.modal');
  await expect(modal).toBeVisible();
  await expect(modal.getByText('هل أنت متأكد من رغبتك في تسجيل الخروج؟')).toBeVisible();

  // الإلغاء يُبقي الجلسة
  await modal.locator('.btn-ghost').click();
  await expect(modal).toBeHidden();
  expect(page.url()).toContain('/dashboard');

  // التأكيد يسجّل الخروج وينظف التوكن ويعيد لصفحة الدخول
  await page.locator('.user-btn').click();
  await page.locator('.menu-item-danger').click();
  await modal.locator('.btn-danger').click();
  await page.waitForURL(/\/login/, { timeout: 20000 });
  const tokenAfter = await page.evaluate(() => localStorage.getItem('hallagi.token'));
  expect(tokenAfter).toBeNull();
});

test('سجل المبيعات يعرض العمليات المحفوظة', async ({ page }) => {
  const token = await tokenFor(page, CASHIER);
  await page.addInitScript(`localStorage.setItem('hallagi.token', '${token}')`);
  await page.goto(`${APP}/sales`);
  await expect(page.getByRole('heading', { name: 'سجل المبيعات' })).toBeVisible({ timeout: 20000 });
  await expect(page.locator('.row-item').first()).toBeVisible({ timeout: 20000 });
});

// ===== Platform owner: full-screen organization creation =====

test('شاشة إنشاء منشأة جديدة كاملة مع الهوية البصرية', async ({ page }) => {
  const token = await tokenFor(page, ADMIN);
  await page.addInitScript(`localStorage.setItem('hallagi.token', '${token}')`);
  await page.goto(`${APP}/platform/new`);

  // الشاشة الكاملة: العنوان والأقسام الأربعة
  await expect(page.getByRole('heading', { name: 'إنشاء منشأة جديدة' })).toBeVisible({ timeout: 20000 });
  for (const section of ['بيانات المنشأة', 'الهوية البصرية', 'حساب مالك المنشأة', 'الخطة']) {
    await expect(page.getByRole('heading', { name: section })).toBeVisible();
  }
  // المعاينة الحية
  await expect(page.locator('.preview-title')).toContainText('معاينة حية');
  await expect(page.locator('.device-topbar')).toBeVisible();

  // اختيار لوحة ألوان جاهزة يحدّث الحقول والمعاينة
  await page.getByRole('button', { name: /زمردي/ }).click();
  await expect(page.locator('.hex-input').first()).toHaveValue('#0E9F6E');
  await expect(page.locator('.hex-input').nth(1)).toHaveValue('#0B3B2E');

  // تعبئة البيانات وإنشاء المنشأة
  const stamp = Date.now();
  const orgName = `صالون E2E ${stamp}`;
  const ownerEmail = `e2e.${stamp}@hallagi.sa`;
  await page.getByPlaceholder('مثال: صالون الأمانة للحلاقة').fill(orgName);
  await page.getByPlaceholder('مثال: عبدالله الدوسري').fill('مالك تجريبي');
  await page.getByPlaceholder('owner@example.com').fill(ownerEmail);
  await page.getByPlaceholder('6 أحرف على الأقل').fill('Hallagi@123');
  await expect(page.locator('.device-org')).toHaveText(orgName);

  await page.getByRole('button', { name: 'إنشاء المنشأة' }).click();
  await page.waitForURL(/\/platform/, { timeout: 20000 });
  await expect(page.getByText(orgName)).toBeVisible({ timeout: 20000 });
});

test('ألوان المنشأة تُطبّق على الواجهة بعد تسجيل دخول المالك', async ({ page }) => {
  // إنشاء منشأة بألوان محددة عبر API
  const adminToken = await tokenFor(page, ADMIN);
  const stamp = Date.now();
  const ownerEmail = `colors.${stamp}@hallagi.sa`;
  const created = await page.request.post(`${API}/api/platform/organizations`, {
    headers: { Authorization: `Bearer ${adminToken}` },
    data: {
      name: `صالون ألوان ${stamp}`, city: 'الرياض',
      ownerName: 'مالك الألوان', ownerEmail,
      ownerPassword: 'Hallagi@123', planId: null,
      primaryColor: '#0E9F6E', secondaryColor: '#0B3B2E'
    }
  });
  expect(created.ok()).toBeTruthy();

  // دخول المالك والتحقق من تطبيق الألوان على متغيرات الواجهة
  const ownerLogin = await page.request.post(`${API}/api/auth/login`, {
    data: { email: ownerEmail, password: 'Hallagi@123' }
  });
  const ownerToken = (await ownerLogin.json()).token;
  await page.addInitScript(`localStorage.setItem('hallagi.token', '${ownerToken}')`);
  await page.goto(`${APP}/dashboard`);
  await expect(page.getByRole('heading', { name: /مرحبًا/ })).toBeVisible({ timeout: 20000 });

  const colors = await page.evaluate(() => {
    const s = getComputedStyle(document.documentElement);
    return {
      blue: s.getPropertyValue('--blue').trim(),
      navy: s.getPropertyValue('--navy').trim()
    };
  });
  expect(colors.blue.toLowerCase()).toBe('#0e9f6e');
  expect(colors.navy.toLowerCase()).toBe('#0b3b2e');
});
