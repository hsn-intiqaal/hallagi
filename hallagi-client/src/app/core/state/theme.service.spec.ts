import { TestBed } from '@angular/core/testing';
import { APP_CONFIG, DEFAULT_CONFIG } from '@core/config/app-config';
import { ThemeService } from './theme.service';

describe('ThemeService', () => {
  const css = (name: string) => document.documentElement.style.getPropertyValue(name);
  let theme: ThemeService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [{ provide: APP_CONFIG, useValue: { ...DEFAULT_CONFIG, brand: { ...DEFAULT_CONFIG.brand, primaryColor: '#0F766E' } } }]
    });
    theme = TestBed.inject(ThemeService);
  });

  it('starts from the deployment brand palette', () => {
    expect(css('--primary')).toBe('#0F766E');
  });

  it('applies an organization palette and ignores invalid colors', () => {
    theme.apply({ primaryColor: '#112233', secondaryColor: 'red' });
    expect(css('--primary')).toBe('#112233');
    expect(css('--secondary')).toBe(DEFAULT_CONFIG.brand.secondaryColor);
  });

  it('returns to the deployment palette on reset (sign-out)', () => {
    theme.apply({ primaryColor: '#112233' });
    theme.reset();
    expect(css('--primary')).toBe('#0F766E');
  });

  it('mixes colors toward white and black', () => {
    expect(theme.tint('#000000', 0.5)).toBe('#808080');
    expect(theme.darken('#ffffff', 0.5)).toBe('#808080');
  });
});
