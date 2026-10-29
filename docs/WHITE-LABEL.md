# استخدام النظام لأكثر من مشروع (White-label)

بناء واحد يخدم أي عميل: الاسم والشعار والألوان وعنوان الخادم تُقرأ **وقت التشغيل** من `config.json`
وليست مدمجة في الكود. لا حاجة لإعادة البناء أو لنسخة منفصلة من الكود لكل عميل.

## طبقات الهوية

| المستوى | أين يُضبط | ماذا يغيّر |
|---|---|---|
| **المنصة** (المنتج نفسه) | `config.json` ← متغيرات `APP_*` في حاوية الواجهة | اسم المنتج، الشعار في شاشة الدخول، الألوان الافتراضية، عنوان الصفحة، اسم التطبيق عند التثبيت |
| **المنشأة** (كل محل داخل المنصة) | شاشة الإعدادات أو مالك المنصة | شعار المحل وألوانه وصورة الغلاف — تُطبَّق بعد الدخول وفي صفحة الحجز العامة |

## مثال: نشر نسخة باسم «صالون النخبة»

```bash
# .env
APP_NAME="صالون النخبة"
APP_SHORT_NAME="النخبة"
APP_TAGLINE="أناقتك تبدأ هنا"
APP_PRIMARY_COLOR="#0F766E"
APP_SECONDARY_COLOR="#134E4A"
APP_LOGO_URL="https://cdn.example.com/elite-logo.png"
DOMAIN="app.elite-salon.sa"
```

```bash
docker compose --profile https up -d
```

عند تشغيل الحاوية يكتب `docker/40-runtime-config.sh`:
- `config.json` بالقيم أعلاه (مع تهريب آمن للنصوص)،
- اسم وألوان `manifest.webmanifest` (اسم التطبيق عند «إضافة للشاشة الرئيسية»)،
- عنوان الصفحة ولون شريط المتصفح في `index.html`،
- سياسة CSP تسمح بعنوان الخادم إن كان على نطاق آخر.

## أكثر من عميل على نفس الخادم
شغّل نسخة من `docker compose` لكل عميل بمجلد و`.env` و`WEB_PORT` و`DB_NAME` مختلفة،
أو استخدم منصة واحدة متعددة المنشآت (الوضع الافتراضي) إذا كان العملاء محلات داخل نفس المنتج.

## تطبيق الجوال لعميل
```bash
cd hallagi-client
# 1) الهوية وعنوان الخادم
cat > public/config.json <<'JSON'
{ "apiUrl": "https://app.elite-salon.sa/api",
  "brand": { "name": "صالون النخبة", "shortName": "النخبة", "tagline": "أناقتك تبدأ هنا",
             "primaryColor": "#0F766E", "secondaryColor": "#134E4A" } }
JSON
# 2) الأيقونات وشاشة البداية بألوان العميل (أو استبدل public/icons/icon.svg بشعاره)
BRAND_BG_TOP="#11806F" BRAND_BG="#134E4A" BRAND_ACCENT="#5EEAD4" npm run icons
# 3) معرّف واسم التطبيق في المتجر
APP_ID=sa.elitesalon.app APP_NAME="صالون النخبة" npm run build:mobile
```

> على iOS غيّر «Display Name» و«Bundle Identifier» من Xcode (‏App ← General) مرة واحدة لكل عميل.

> المعرّف (`APP_ID`) يجب أن يكون فريدًا لكل تطبيق على المتجر ولا يتغير بعد النشر.
