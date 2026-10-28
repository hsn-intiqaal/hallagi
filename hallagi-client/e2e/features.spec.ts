import { expect, Page, test } from '@playwright/test';

const APP = 'http://localhost:4300';
const API = 'http://localhost:5095';
const OWNER = { email: 'owner@hallagi.sa', password: 'Hallagi@123' };
const CASHIER = { email: 'cashier@hallagi.sa', password: 'Hallagi@123' };

async function signIn(page: Page, creds = OWNER): Promise<void> {
  const res = await page.request.post(`${API}/api/auth/login`, { data: creds });
  expect(res.ok()).toBeTruthy();
  const body = await res.json();
  await page.addInitScript(([t, r]) => {
    localStorage.setItem('hallagi.token', t);
    localStorage.setItem('hallagi.refresh', r);
  }, [body.token, body.refreshToken]);
}

/** A day on the salon's calendar (Riyadh), not the test machine's — they differ late in the UTC evening. */
const daysFromNow = (days: number): string => {
  const today = new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Riyadh' }).format(new Date());
  return new Date(Date.parse(today + 'T00:00:00Z') + days * 86_400_000).toISOString().slice(0, 10);
};

test('بيع خدمة + منتج مع كود خصم يُصدر فاتورة ضريبية ويخصم المخزون', async ({ page }) => {
  // the cashier works in one branch (the owner defaults to "all branches", which POS rejects)
  await signIn(page, CASHIER);
  await page.goto(`${APP}/pos`);

  await expect(page.locator('.svc-card').first()).toBeVisible({ timeout: 20000 });
  await page.locator('.svc-card').first().click();

  await page.getByRole('button', { name: /المنتجات/ }).click();
  // pick the in-stock product with the most units so repeated runs never sell it out
  const cards = page.locator('.svc-card:not(.out)');
  const stocks = await cards.locator('.add-hint').allInnerTexts();
  const counts = stocks.map(t => Number(t.match(/\d+/)?.[0] ?? 0));
  const index = counts.indexOf(Math.max(...counts));
  const productName = (await cards.nth(index).locator('.n').innerText()).trim();
  const stockBefore = counts[index];
  await cards.nth(index).click();

  await page.locator('.barber-chip').first().click();
  await page.getByRole('button', { name: /خصم \/ كود \/ نقاط/ }).click();
  await page.getByPlaceholder('WELCOME10').fill('WELCOME10');
  await page.getByRole('button', { name: 'تطبيق' }).click();
  await expect(page.getByText(/تم تطبيق الكود/)).toBeVisible();
  await expect(page.getByText(/ضريبة القيمة المضافة/).first()).toBeVisible();

  await page.getByRole('button', { name: /تأكيد البيع وحفظه/ }).click();
  await expect(page.locator('.success h1')).toHaveText('تم تسجيل البيع بنجاح', { timeout: 20000 });
  await expect(page.locator('.success').getByText('WELCOME10')).toBeVisible();

  // stock went down by one after the sale (POS reloads its data)
  await page.getByRole('button', { name: /بيع جديد/ }).click();
  await page.getByRole('button', { name: /المنتجات/ }).click();
  await expect(page.locator('.svc-card', { hasText: productName }).locator('.add-hint')).toContainText(`المتوفر: ${stockBefore - 1}`);
});

test('العميل يحجز من صفحة الحجز العامة ويظهر الموعد في جدول المحل', async ({ page, browser }) => {
  const customer = await browser.newPage();
  await customer.goto(`${APP}/book/elite`);
  await expect(customer.getByText('احجز موعدك')).toBeVisible({ timeout: 20000 });

  // first branch, first service
  if (await customer.getByText('اختر الفرع').isVisible()) await customer.locator('.pick').first().click();
  await customer.locator('.pick.service').first().click();
  await customer.getByRole('button', { name: 'اختر الموعد' }).click();

  // spread bookings over the next two weeks so repeated runs never fill a day
  const offset = 1 + Math.floor(Math.random() * 13);
  const day = daysFromNow(offset);
  const slotsLoaded = customer.waitForResponse(r => r.url().includes('/slots') && r.url().includes(`date=${day}`));
  await customer.locator('.day').nth(offset).click();
  await slotsLoaded;
  const slots = customer.locator('.slot');
  await slots.first().waitFor({ timeout: 20000 });
  const pick = Math.floor(Math.random() * (await slots.count()));
  const slot = (await slots.nth(pick).innerText()).trim();
  await slots.nth(pick).click();

  const name = `زائر ${Date.now() % 100000}`;
  await customer.getByPlaceholder('اسمك الكريم').fill(name);
  await customer.getByPlaceholder('05XXXXXXXX').first().fill(`05${String(Date.now()).slice(-8)}`);
  await customer.getByRole('button', { name: /تأكيد الحجز/ }).click();
  await expect(customer.getByText('تم تأكيد حجزك')).toBeVisible({ timeout: 20000 });
  const reference = (await customer.locator('.ref b').innerText()).trim();
  expect(reference).toMatch(/^[A-Z0-9]{6}$/);

  // the salon sees it on that day's schedule
  await signIn(page);
  await page.goto(`${APP}/appointments`);
  await page.getByRole('button', { name: 'قائمة' }).click();
  await page.locator('input[type="date"]').first().fill(day);
  await page.locator('input[type="date"]').first().dispatchEvent('change');
  const row = page.locator('.list-item', { hasText: name });
  await expect(row).toBeVisible({ timeout: 20000 });
  await expect(row).toContainText(slot);
});

test('عميل حضور مباشر: انتظار ← بدء الخدمة ← تحصيل من نقطة البيع', async ({ page }) => {
  await signIn(page);
  await page.goto(`${APP}/appointments`);
  await expect(page.getByRole('heading', { name: 'المواعيد' })).toBeVisible({ timeout: 20000 });

  const name = `حضور ${Date.now() % 100000}`;
  await page.getByRole('button', { name: /إضافة للانتظار/ }).first().click();
  await page.getByPlaceholder('ابحث باسم عميل مسجل أو اكتب اسمًا جديدًا').fill(name);
  await page.locator('app-modal .chip').first().click();
  await page.locator('app-modal').getByRole('button', { name: 'إضافة للانتظار' }).click();

  const waiting = page.locator('.wait-item', { hasText: name });
  await expect(waiting).toBeVisible({ timeout: 20000 });
  await waiting.getByRole('button', { name: /ابدأ/ }).click();
  await page.locator('app-modal .chip').first().click();
  await page.getByRole('button', { name: 'بدء الخدمة' }).click();

  await page.getByRole('button', { name: 'قائمة' }).click();
  await page.locator('.list-item', { hasText: name }).click();
  await page.getByRole('button', { name: /تحصيل/ }).click();

  await expect(page.getByText(/تحصيل موعد/)).toBeVisible({ timeout: 20000 });
  await page.getByRole('button', { name: /تأكيد البيع وحفظه/ }).click();
  await expect(page.locator('.success h1')).toHaveText('تم تسجيل البيع بنجاح', { timeout: 20000 });
});

test('تجديد الجلسة تلقائيًا عند انتهاء التوكن', async ({ page }) => {
  const res = await page.request.post(`${API}/api/auth/login`, { data: OWNER });
  const { refreshToken } = await res.json();
  // an invalid / expired access token + a valid refresh token
  await page.addInitScript(r => {
    localStorage.setItem('hallagi.token', 'expired.token.value');
    localStorage.setItem('hallagi.refresh', r);
  }, refreshToken);
  await page.goto(`${APP}/sales`);
  await expect(page.getByText('سجل المبيعات')).toBeVisible({ timeout: 20000 });
  await expect(page).toHaveURL(/\/sales/);
});

test('تبديل الفرع يعيد تحميل بيانات الصفحة الحالية', async ({ page }) => {
  await signIn(page);
  await page.goto(`${APP}/sales`);
  await page.locator('.branch-btn').click();
  await page.locator('.branch-menu button', { hasText: 'فرع النرجس' }).click();
  const rows = page.locator('.row-item');
  await expect(rows.first()).toBeVisible({ timeout: 20000 });
  await expect(rows.first()).toContainText('فرع النرجس');

  await page.locator('.branch-btn').click();
  await page.locator('.branch-menu button', { hasText: 'فرع الياسمين' }).click();
  await expect(rows.first()).toContainText('فرع الياسمين', { timeout: 20000 });
  // no row from the previous branch survives the switch
  await expect(page.locator('.row-item', { hasText: 'فرع النرجس' })).toHaveCount(0);
});

test('أرقام شاشة التقارير تطابق حسابات الخادم', async ({ page }) => {
  const login = await (await page.request.post(`${API}/api/auth/login`, { data: OWNER })).json();
  await signIn(page);
  await page.goto(`${APP}/reports`);
  await page.locator('.branch-btn').click();
  await page.locator('.branch-menu button', { hasText: 'كل الفروع' }).click();
  await page.getByRole('button', { name: 'آخر 7 أيام' }).click();

  const statValue = (label: string) =>
    page.locator('app-stat-card').filter({ has: page.locator('.stat-label', { hasText: new RegExp(`^${label}$`) }) }).locator('.stat-value');
  const card = statValue('صافي المبيعات');
  await expect(card).not.toHaveText('', { timeout: 20000 });
  const today = await page.evaluate(() => new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Riyadh' }).format(new Date()));
  const from = new Date(Date.parse(today + 'T00:00:00Z') - 6 * 86_400_000).toISOString().slice(0, 10);
  const api = await (await page.request.get(`${API}/api/reports/sales?from=${from}&to=${today}`, {
    headers: { Authorization: `Bearer ${login.token}` }
  })).json();

  // "4,391.76 ر.س" → 4391.76 (the currency symbol contains a dot too)
  const shown = Number((await card.innerText()).match(/[\d,]+\.\d{2}/)![0].replace(/,/g, ''));
  expect(shown).toBeCloseTo(api.summary.netSales, 2);
  const count = Number((await statValue('عدد العمليات').innerText()).trim());
  expect(count).toBe(api.summary.salesCount);
});
