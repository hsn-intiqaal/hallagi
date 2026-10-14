import { test } from '@playwright/test';

const APP = 'http://localhost:4300';
const API = 'http://localhost:5095';

const CREDENTIALS: Record<string, any> = {
  owner: { email: 'owner@hallagi.sa', password: 'Hallagi@123' },
  cashier: { email: 'cashier@hallagi.sa', password: 'Hallagi@123' },
  barber: { email: 'barber@hallagi.sa', password: 'Hallagi@123' },
  admin: { email: 'admin@hallagi.sa', password: 'Hallagi@123' }
};

const ROUTES = [
  { path: '/login', expect: 'حلاقي', role: 'none' },
  { path: '/platform', expect: 'لوحة منصة حلاقي', role: 'admin' },
  { path: '/dashboard', expect: 'مرحبًا', role: 'owner' },
  { path: '/pos', expect: 'بيع جديد', role: 'cashier' },
  { path: '/pos', expect: 'أنت تسجّل المبيعات على نفسك', role: 'barber' },
  { path: '/sales', expect: 'سجل المبيعات', role: 'cashier' },
  { path: '/services', expect: 'الخدمات والأسعار', role: 'owner' },
  { path: '/barbers', expect: 'الحلاقون', role: 'owner' },
  { path: '/barbers/new', expect: 'إضافة حلاق جديد', role: 'owner' },
  { path: '/customers', expect: 'العملاء', role: 'cashier' },
  { path: '/expenses', expect: 'المصروفات', role: 'owner' },
  { path: '/treasury', expect: 'الخزينة والوردية', role: 'owner' },
  { path: '/reports', expect: 'التقارير', role: 'owner' },
  { path: '/branches', expect: 'الفروع', role: 'owner' },
  { path: '/users', expect: 'المستخدمون والصلاحيات', role: 'owner' },
  { path: '/settings', expect: 'الإعدادات', role: 'owner' },
  { path: '/finance/settlements', expect: 'التسويات المالية', role: 'owner' },
  { path: '/finance/handovers', expect: 'التسليمات النقدية', role: 'owner' },
  { path: '/devices', expect: 'أجهزة POS', role: 'owner' },
  { path: '/me', expect: 'ملخصي', role: 'barber' },
  { path: '/onboarding', expect: 'إعداد سريع', role: 'owner' }
];

test('كل الصفحات تُفتح بدون أخطاء', async ({ page }) => {
  test.setTimeout(300000);
  const report: string[] = [];

  const tokens: Record<string, string> = {};
  for (const role of Object.keys(CREDENTIALS)) {
    const res = await page.request.post(`${API}/api/auth/login`, { data: CREDENTIALS[role] });
    tokens[role] = (await res.json()).token;
  }

  for (const route of ROUTES) {
    const errors: string[] = [];
    const onPageError = (err: any) => errors.push('PAGEERROR: ' + err.message.substring(0, 160) + ' @ ' + (err.stack ?? '').split('\n').slice(0, 4).join(' | '));
    const onConsole = (msg: any) => {
      if (msg.type() === 'error') {
        const loc = msg.location ? ` @${msg.location().url?.split('/').pop()}:${msg.location().lineNumber}` : '';
        errors.push('CONSOLE: ' + msg.text().substring(0, 160) + loc);
      }
    };
    const onResponse = (res: any) => {
      const u = res.url();
      if (u.includes('/api/') && res.status() >= 400) errors.push(`HTTP ${res.status()}: ${u.replace('http://localhost:5095', '')}`);
    };
    page.on('pageerror', onPageError);
    page.on('console', onConsole);
    page.on('response', onResponse);

    try {
      await page.goto(`${APP}/login`, { waitUntil: 'domcontentloaded' });
      if (route.role !== 'none') {
        await page.evaluate((t: string) => localStorage.setItem('hallagi.token', t), tokens[route.role]);
      } else {
        await page.evaluate(() => localStorage.removeItem('hallagi.token'));
      }
      await page.goto(`${APP}${route.path}`, { waitUntil: 'domcontentloaded' });
      await page.waitForTimeout(2800);

      const bodyText = await page.evaluate(() => document.body.innerText || '');
      const found = bodyText.includes(route.expect);
      const url = await page.evaluate(() => location.pathname);
      report.push(
        `${found && errors.length === 0 ? 'PASS' : 'FAIL'} | ${route.path} (${route.role})` +
        (found ? '' : ` | MISSING: "${route.expect}"`) +
        (errors.length ? ` | ${[...new Set(errors)].slice(0, 2).join(' ;; ')}` : '') +
        (url !== route.path ? ` | redirected to ${url}` : '')
      );
    } catch (e: any) {
      report.push(`FAIL | ${route.path} | EXCEPTION ${e.message.substring(0, 160)}`);
    } finally {
      page.off('pageerror', onPageError);
      page.off('console', onConsole);
      page.off('response', onResponse);
    }
  }

  console.log('\n===== PAGE REPORT =====\n' + report.join('\n'));
});
