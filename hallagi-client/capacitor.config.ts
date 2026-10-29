import type { CapacitorConfig } from '@capacitor/cli';

/**
 * Native shells (Android / iOS) wrap the same production web build.
 * For a white-label app, override the id and name per client:
 *   APP_ID=sa.example.salon APP_NAME="صالون المثال" npx cap sync
 * and point `public/config.json` (apiUrl) at that client's API before building.
 */
const config: CapacitorConfig = {
  appId: process.env['APP_ID'] ?? 'sa.hallagi.app',
  appName: process.env['APP_NAME'] ?? 'حلاقي',
  webDir: 'dist/hallagi-client/browser',
  android: { allowMixedContent: false },
  ios: { contentInset: 'never' },
  plugins: {
    SplashScreen: { launchShowDuration: 1200, backgroundColor: '#173653', showSpinner: false },
    StatusBar: { style: 'DARK', backgroundColor: '#173653', overlaysWebView: false }
  }
};

export default config;
