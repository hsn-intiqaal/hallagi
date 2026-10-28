import { Capacitor } from '@capacitor/core';

/** True inside the Android / iOS shell (Capacitor), false in a browser or installed PWA. */
export const isNativeApp = (): boolean => Capacitor.isNativePlatform();

/** Native-shell integration: status bar colors, splash, and the Android hardware back button. */
export async function initNativeShell(onBack: () => void, statusBarColor: string): Promise<void> {
  if (!isNativeApp()) return;
  const [{ App }, { StatusBar, Style }, { SplashScreen }] = await Promise.all([
    import('@capacitor/app'),
    import('@capacitor/status-bar'),
    import('@capacitor/splash-screen')
  ]);
  await StatusBar.setStyle({ style: Style.Dark }).catch(() => undefined);
  if (Capacitor.getPlatform() === 'android') {
    await StatusBar.setBackgroundColor({ color: statusBarColor }).catch(() => undefined);
  }
  await App.addListener('backButton', ({ canGoBack }) => (canGoBack ? onBack() : App.exitApp()));
  await SplashScreen.hide().catch(() => undefined);
}
