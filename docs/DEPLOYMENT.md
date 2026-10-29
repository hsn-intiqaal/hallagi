# النشر في الإنتاج

## المتطلبات
- خادم Linux عليه Docker وDocker Compose (‏2 vCPU و4GB RAM كحد أدنى لـ SQL Server).
- نطاق (Domain) يشير سجله `A` إلى عنوان الخادم إذا أردت HTTPS تلقائيًا.

## الخطوات

```bash
git clone <repo> /opt/hallagi && cd /opt/hallagi
cp .env.example .env          # عدّل القيم — انظر الجدول أدناه
docker compose --profile https up -d --build
```

- بدون `--profile https` تعمل الواجهة على `http://<server>:8080` (ضع موزع حمل بشهادة TLS أمامها).
- مع `--profile https` يحصل Caddy على شهادة Let's Encrypt للنطاق `DOMAIN` ويجددها تلقائيًا، ويضيف HSTS.
- عند أول تشغيل تُطبَّق الهجرات ويُنشأ حساب مالك المنصة من `ADMIN_EMAIL` / `ADMIN_PASSWORD`. لا توجد أي بيانات عرض.

### استخدام صور جاهزة بدل البناء
كل وسم `vX.Y.Z` ينشر صورتين في GitHub Container Registry عبر `.github/workflows/release.yml`:

```bash
API_IMAGE=ghcr.io/<owner>/hallagi-api:1.2.0
WEB_IMAGE=ghcr.io/<owner>/hallagi-web:1.2.0
docker compose --profile https up -d --no-build
```

## المتغيرات الأساسية

| المتغير | الوصف |
|---|---|
| `DB_PASSWORD` | كلمة مرور `sa` لـ SQL Server (قوية) |
| `JWT_KEY` | سر التوقيع — 32 حرفًا عشوائيًا على الأقل (الخادم يرفض الافتراضي) |
| `ADMIN_EMAIL` / `ADMIN_PASSWORD` | أول مالك منصة (12 حرفًا على الأقل) |
| `PUBLIC_URL` | رابط الواجهة العام (CORS) |
| `DOMAIN` / `ACME_EMAIL` | للـ HTTPS التلقائي |
| `APP_*` | الهوية التجارية — انظر [WHITE-LABEL.md](WHITE-LABEL.md) |

## الأمان المطبَّق
- **الواجهة (nginx):** ‏Content-Security-Policy صارمة (`script-src 'self'` بدون أي سكربت مضمَّن)، ‏`X-Frame-Options: DENY`، ‏`nosniff`، ‏Permissions-Policy، إخفاء إصدار الخادم.
- **الخادم:** توكن وصول قصير العمر + Refresh Token دوّار مع كشف إعادة الاستخدام، حدود محاولات لكل IP، ترويسات أمان، سجلات JSON.
- **التخزين المؤقت:** الملفات ذات البصمة تُخزَّن سنة، و`index.html` و`config.json` و`ngsw.json` لا تُخزَّن أبدًا — فكل إصدار جديد يصل فورًا، ويظهر للمستخدم شريط «يتوفر إصدار جديد».

## النسخ الاحتياطي والاستعادة

```bash
./deploy/backup.sh                                   # نسخة مضغوطة مع CHECKSUM في ./backups (يحتفظ بآخر 14)
./deploy/restore.sh backups/Hallagi_20260101T031500Z.bak
```

جدولة يومية (crontab):

```
15 3 * * * cd /opt/hallagi && ./deploy/backup.sh >> backups/backup.log 2>&1
```

انسخ مجلد `backups/` إلى تخزين خارجي (S3 / Backblaze / خادم آخر) — النسخة على نفس الخادم لا تحمي من فقدانه.

## المراقبة
- `GET /health/live` — العملية تعمل.
- `GET /health/ready` — قاعدة البيانات متاحة (مناسب لموزع الحمل و`docker compose ps`).
- السجلات بصيغة JSON على stdout: `docker compose logs -f api`.

## التحديث

```bash
git pull && docker compose --profile https up -d --build
```

الهجرات تُطبَّق تلقائيًا عند بدء الخادم. خذ نسخة احتياطية قبل كل تحديث.
