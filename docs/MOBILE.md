# تطبيق الجوال (Android / iOS)

التطبيق نفسه يعمل بثلاث طرق، من نفس الكود ونفس البناء:

| الطريقة | متى تناسب |
|---|---|
| **المتصفح** | أي جهاز، بدون تثبيت |
| **PWA (تثبيت من المتصفح)** | «إضافة إلى الشاشة الرئيسية» على Android/iOS/Windows — أيقونة، ملء الشاشة، يفتح بدون إنترنت، تحديث تلقائي |
| **تطبيق أصلي (Capacitor)** | النشر على Google Play وApp Store |

## ما تم تجهيزه
- `capacitor.config.ts` + مشروعا `android/` و`ios/` جاهزان.
- أيقونات التطبيق وشاشة البداية مولّدة من شعار واحد (`npm run icons`).
- زر الرجوع في Android يعود للصفحة السابقة ثم يغلق التطبيق، وشريط الحالة بلون الهوية.
- دعم الحواف الآمنة (النوتش وشريط الإيماءات) في كل الشاشات.
- شريط «لا يوجد اتصال» عند انقطاع الإنترنت.
- الخادم يقبل طلبات التطبيق الأصلي (`https://localhost` و`capacitor://localhost` في CORS).

## البناء محليًا

```bash
cd hallagi-client
# 1) اجعل التطبيق يتصل بخادمك (التطبيق الأصلي لا يشارك النطاق مع الخادم)
#    عدّل public/config.json وأضف: "apiUrl": "https://app.your-domain.sa/api"
npm ci
npm run build:mobile        # بناء الإنتاج + نسخه إلى android/ و ios/
npm run android             # يفتح Android Studio → Run أو Build > Generate Signed Bundle
npm run ios                 # يفتح Xcode (على macOS) → Product > Archive
```

المتطلبات: Android Studio (JDK 21، SDK 35) — وXcode 16 على macOS لـ iOS.

## البناء الآلي
- **كل Push/PR:** وظيفة `android` في CI تبني APK للتجربة (Artifacts).
- **عند وسم إصدار `vX.Y.Z`:** ‏`release.yml` يبني APK وAAB ويرفقهما بصفحة الإصدار.
  - ضع رابط الخادم في متغير المستودع `MOBILE_API_URL`.
  - للتوقيع ضع الأسرار: `ANDROID_KEYSTORE_BASE64` و`ANDROID_KEYSTORE_PASSWORD` و`ANDROID_KEY_ALIAS` و`ANDROID_KEY_PASSWORD`.

إنشاء مفتاح التوقيع (مرة واحدة — احفظه خارج المستودع، فقدانه يمنع تحديث التطبيق على المتجر):

```bash
keytool -genkey -v -keystore release.jks -alias hallagi -keyalg RSA -keysize 2048 -validity 10000
base64 -w0 release.jks   # القيمة لسر ANDROID_KEYSTORE_BASE64
```

## تطبيق باسم عميل آخر
انظر [WHITE-LABEL.md](WHITE-LABEL.md) — المعرّف والاسم ورقم الإصدار تُمرَّر بمتغيرات بيئة:
`APP_ID=sa.example.salon APP_NAME="صالون المثال" VERSION_CODE=3 VERSION_NAME=1.0.2`.
