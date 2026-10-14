import { test, expect } from '@playwright/test';

const API = 'http://localhost:5095';
const APP = 'http://localhost:4300';

const OWNER = { email: 'owner@hallagi.sa', password: 'Hallagi@123' };
const CASHIER = { email: 'cashier@hallagi.sa', password: 'Hallagi@123' };
const BARBER = { email: 'barber@hallagi.sa', password: 'Hallagi@123' };

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
  await expect(page.getByText('مبيعات اليوم').first()).toBeVisible();
  await expect(page.locator('.stat-label', { hasText: 'صافي التشغيل التقديري' })).toBeVisible();
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
  await expect(page.getByRole('button', { name: /عرض الإيصال/ })).toBeVisible();
});

test('شاشة التأكيد تعرض إيصالاً قابلًا للطباعة', async ({ page }) => {
  const token = await tokenFor(page, CASHIER);
  await page.addInitScript(`localStorage.setItem('hallagi.token', '${token}')`);
  await page.goto(`${APP}/pos`);

  const serviceCards = page.locator('.svc-card');
  await expect(serviceCards.first()).toBeVisible({ timeout: 20000 });
  await serviceCards.first().click();
  await page.locator('.barber-chip').first().click();
  await page.getByRole('button', { name: /تأكيد البيع وحفظه/ }).click();
  await expect(page.locator('.success h1', { hasText: 'تم تسجيل البيع بنجاح' })).toBeVisible({ timeout: 20000 });

  await page.getByRole('button', { name: /عرض الإيصال/ }).click();
  await expect(page.locator('.receipt')).toBeVisible({ timeout: 20000 });
  await expect(page.getByText('الإجمالي', { exact: false }).first()).toBeVisible();
  await expect(page.getByText('إيصال داخلي غير ضريبي')).toBeVisible();
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

test('سجل المبيعات يعرض العمليات المحفوظة', async ({ page }) => {
  const token = await tokenFor(page, CASHIER);
  await page.addInitScript(`localStorage.setItem('hallagi.token', '${token}')`);
  await page.goto(`${APP}/sales`);
  await expect(page.getByRole('heading', { name: 'سجل المبيعات' })).toBeVisible({ timeout: 20000 });
  await expect(page.locator('.row-item').first()).toBeVisible({ timeout: 20000 });
});
