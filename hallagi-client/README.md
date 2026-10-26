# حلاقي — الواجهة (Angular 20)

```bash
npm ci
npx ng serve --port 4300         # يتصل بالخادم على http://localhost:5095 (environment.development.ts)
npx ng build                     # بناء الإنتاج → dist/hallagi-client/browser
npx playwright test              # E2E (الخادم والواجهة يعملان) — PW_CHROMIUM_PATH لاستخدام Chromium مثبت مسبقًا
```

## التنظيم

| المجلد | المحتوى |
|---|---|
| `src/app/core/auth` | الجلسة، تخزين التوكنات، interceptor التجديد التلقائي، الحمايات |
| `src/app/core/http` | `ApiService` وتوحيد رسائل الأخطاء العربية (`ApiFailure`) |
| `src/app/core/api` | عملاء API مطبوعة لكل مجال (sales, appointments, booking, inventory) |
| `src/app/core/models` | عقود البيانات مقسمة حسب المجال |
| `src/app/core/state` | الفرع الحالي، الثيم، الإشعارات |
| `src/app/core/pricing` | مرآة لقواعد التسعير والضريبة للعرض الفوري |
| `src/app/shared` | مكونات وأنابيب مشتركة (Modal, StatCard, QR…) |
| `src/app/features` | الشاشات — كل شاشة مستقلة وتُحمّل عند الطلب |

الاستيراد عبر الأسماء المستعارة: `@core/*`, `@shared/*`, `@features/*`, `@env/*`.
