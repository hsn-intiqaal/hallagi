# حلاقي — الواجهة (Angular 20)

```bash
npm ci
npx ng serve --port 4300   # يتصل بالخادم على http://localhost:5095 (environment.development.ts)
npm run lint               # ESLint (TypeScript + قوالب + a11y)
npm run test:ci            # اختبارات الوحدة (CHROME_BIN لمسار Chromium إن لزم)
npx playwright test        # E2E (الخادم والواجهة يعملان)
npm run build:mobile       # بناء + مزامنة android/ و ios/
npm run icons              # توليد الأيقونات وشاشات البداية من الشعار
```

| الملف | الغرض |
|---|---|
| `public/config.json` | إعداد وقت التشغيل: عنوان الخادم والهوية — يُكتب من متغيرات البيئة في حاوية الإنتاج |
| `ngsw-config.json` | سياسة الـ Service Worker (التخزين المؤقت والتحديثات) |
| `capacitor.config.ts` | إعداد التطبيق الأصلي |
| `docker/40-runtime-config.sh` | يكتب `config.json` و manifest و CSP عند بدء الحاوية |

الوثائق: [النشر](../docs/DEPLOYMENT.md) · [الجوال](../docs/MOBILE.md) · [White-label](../docs/WHITE-LABEL.md)
