import { expect, test } from '@playwright/test';

const APP = 'http://localhost:4300';
const API = 'http://localhost:5095';

test('الهوية التجارية تُقرأ من config.json وقت التشغيل (White-label)', async ({ page }) => {
  await page.route('**/config.json', route => route.fulfill({
    json: { brand: { name: 'صالون الاختبار', shortName: 'الاختبار', tagline: 'هوية مخصصة', primaryColor: '#0F766E', secondaryColor: '#134E4A' } }
  }));
  await page.goto(`${APP}/login`);

  await expect(page.locator('.brand h1')).toHaveText('صالون الاختبار');
  await expect(page.locator('.brand .tagline')).toHaveText('هوية مخصصة');
  await expect(page).toHaveTitle('صالون الاختبار — هوية مخصصة');
  const primary = await page.evaluate(() => getComputedStyle(document.documentElement).getPropertyValue('--primary').trim());
  expect(primary.toLowerCase()).toBe('#0f766e');
});

test('التطبيق يعمل بالإعدادات الافتراضية إذا تعذّر تحميل config.json', async ({ page }) => {
  await page.route('**/config.json', route => route.abort());
  await page.goto(`${APP}/login`);
  await expect(page.locator('.brand h1')).toHaveText('حلاقي');
});

test('ملف PWA manifest صالح وأيقوناته متاحة', async ({ request }) => {
  const manifest = await (await request.get(`${APP}/manifest.webmanifest`)).json();
  expect(manifest.display).toBe('standalone');
  expect(manifest.dir).toBe('rtl');
  expect(manifest.icons.some((i: { purpose: string }) => i.purpose === 'maskable')).toBeTruthy();
  for (const icon of manifest.icons) {
    const res = await request.get(`${APP}/${icon.src}`);
    expect(res.ok(), icon.src).toBeTruthy();
    expect(res.headers()['content-type']).toContain('image/png');
  }
});

test('شريط انقطاع الاتصال يظهر ويختفي', async ({ page, context }) => {
  await page.goto(`${APP}/login`);
  await context.setOffline(true);
  await expect(page.getByText('لا يوجد اتصال بالإنترنت')).toBeVisible();
  await context.setOffline(false);
  await expect(page.getByText('لا يوجد اتصال بالإنترنت')).toBeHidden();
});

test.describe('واجهة الجوال', () => {
  test.use({ viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true });

  test('التنقل السفلي ونقطة البيع على شاشة الجوال', async ({ page }) => {
    const login = await (await page.request.post(`${API}/api/auth/login`, { data: { email: 'cashier@hallagi.sa', password: 'Hallagi@123' } })).json();
    await page.addInitScript(([t, r]) => {
      localStorage.setItem('hallagi.token', t);
      localStorage.setItem('hallagi.refresh', r);
    }, [login.token, login.refreshToken]);

    await page.goto(`${APP}/pos`);
    await expect(page.locator('.bottom-nav')).toBeVisible({ timeout: 20000 });
    await expect(page.locator('.sidebar')).toBeHidden();
    await expect(page.locator('.svc-card').first()).toBeVisible({ timeout: 20000 });
    await page.locator('.svc-card').first().click();
    // the floating checkout bar appears once the cart has an item
    await expect(page.locator('.mobile-checkout')).toBeVisible();
    const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
    expect(overflow).toBeLessThanOrEqual(1);
  });
});

test.describe('لا تمرير أفقي على شاشة الجوال', () => {
  test.use({ viewport: { width: 390, height: 844 } });

  const ROUTES: [string, string][] = [
    ['owner', '/dashboard'], ['owner', '/appointments'], ['cashier', '/pos'], ['owner', '/sales'],
    ['owner', '/customers'], ['owner', '/barbers'], ['owner', '/barbers/new'], ['owner', '/services'],
    ['owner', '/products'], ['owner', '/coupons'], ['owner', '/expenses'], ['owner', '/treasury'],
    ['owner', '/reports'], ['owner', '/finance/settlements'], ['owner', '/finance/handovers'],
    ['owner', '/branches'], ['owner', '/devices'], ['owner', '/users'], ['owner', '/settings'],
    ['barber', '/me'], ['admin', '/platform'], ['none', '/login'], ['none', '/book/elite']
  ];

  test('كل الشاشات تتسع لعرض 390px', async ({ page }) => {
    test.setTimeout(180000);
    const tokens: Record<string, string> = {};
    for (const role of ['owner', 'cashier', 'barber', 'admin']) {
      const res = await page.request.post(`${API}/api/auth/login`, { data: { email: `${role}@hallagi.sa`, password: 'Hallagi@123' } });
      tokens[role] = (await res.json()).token;
    }
    const failures: string[] = [];
    for (const [role, path] of ROUTES) {
      await page.goto(`${APP}/login`);
      await page.evaluate(t => (t ? localStorage.setItem('hallagi.token', t) : localStorage.removeItem('hallagi.token')), tokens[role] ?? '');
      await page.goto(`${APP}${path}`, { waitUntil: 'networkidle' });
      await page.waitForTimeout(500);
      const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
      if (overflow > 1) failures.push(`${path} (${role}): ${overflow}px`);
    }
    expect(failures).toEqual([]);
  });
});
