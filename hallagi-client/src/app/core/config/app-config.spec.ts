import { DEFAULT_CONFIG, loadAppConfig } from './app-config';

describe('loadAppConfig', () => {
  const respond = (body: unknown, ok = true) =>
    spyOn(window, 'fetch').and.resolveTo(new Response(JSON.stringify(body), { status: ok ? 200 : 404 }));

  it('merges the deployment brand over the defaults', async () => {
    respond({ apiUrl: 'https://api.salon.sa/api/', brand: { name: 'صالون النخبة', primaryColor: '#0F766E' } });
    const config = await loadAppConfig();
    expect(config.apiUrl).toBe('https://api.salon.sa/api');
    expect(config.brand.name).toBe('صالون النخبة');
    expect(config.brand.primaryColor).toBe('#0F766E');
    expect(config.brand.tagline).toBe(DEFAULT_CONFIG.brand.tagline);
  });

  it('keeps the build API address when the file sets none', async () => {
    respond({ brand: { name: 'X' } });
    expect((await loadAppConfig()).apiUrl).toBe(DEFAULT_CONFIG.apiUrl);
  });

  it('falls back to the defaults when the file is missing or the device is offline', async () => {
    respond({}, false);
    expect(await loadAppConfig()).toEqual(DEFAULT_CONFIG);
    (window.fetch as jasmine.Spy).and.rejectWith(new TypeError('offline'));
    expect(await loadAppConfig()).toEqual(DEFAULT_CONFIG);
  });
});
