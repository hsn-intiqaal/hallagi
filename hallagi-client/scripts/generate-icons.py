"""
Renders every launcher icon and splash screen of the app from one mark
(the same geometry as public/icons/icon.svg), so a white-label build only
changes the colors below and re-runs:

    python3 scripts/generate-icons.py                 # PWA icons (public/icons)
    python3 scripts/generate-icons.py --native        # + Android/iOS icons and splash screens

Colors can also come from the environment: BRAND_BG_TOP, BRAND_BG, BRAND_ACCENT (hex).
Requires Pillow.
"""
import os
import sys
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parent.parent


def hex_rgb(value: str) -> tuple:
    value = value.lstrip('#')
    return tuple(int(value[i:i + 2], 16) for i in (0, 2, 4))


BG_TOP = hex_rgb(os.environ.get('BRAND_BG_TOP', '#1E4A6E'))
BG = hex_rgb(os.environ.get('BRAND_BG', '#173653'))
ACCENT = hex_rgb(os.environ.get('BRAND_ACCENT', '#60A5FA'))
WHITE = (255, 255, 255, 255)


def gradient(w: int, h: int) -> Image.Image:
    img = Image.new('RGBA', (w, h))
    d = ImageDraw.Draw(img)
    for y in range(h):
        t = y / max(h - 1, 1)
        d.line([(0, y), (w, y)], fill=tuple(round(BG_TOP[i] + (BG[i] - BG_TOP[i]) * t) for i in range(3)) + (255,))
    return img


def draw_mark(img: Image.Image, box: float) -> None:
    """Draws the scissors mark centered in `img`, scaled to fit a `box`-pixel square."""
    d = ImageDraw.Draw(img)
    k = box / 512
    ox, oy = (img.width - box) / 2, (img.height - box) / 2
    p = lambda x, y: (ox + x * k, oy + y * k)
    w = max(1, int(28 * k))
    for cx in (176, 336):
        x, y = p(cx, 352)
        r = 56 * k
        d.ellipse([x - r, y - r, x + r, y + r], outline=WHITE, width=w)
    for a, b in (((214, 310), (360, 120)), ((298, 310), (152, 120))):
        d.line([p(*a), p(*b)], fill=WHITE, width=w)
        for pt in (a, b):  # round caps
            x, y = p(*pt)
            d.ellipse([x - w / 2, y - w / 2, x + w / 2, y + w / 2], fill=WHITE)
    x, y = p(256, 232)
    r = 14 * k
    d.ellipse([x - r, y - r, x + r, y + r], fill=ACCENT + (255,))


def icon(size: int, shape: str = 'rounded', mark: float = 1.0) -> Image.Image:
    """shape: 'rounded' (any-purpose), 'square' (maskable / iOS), 'circle' (Android round)."""
    s = size * 4  # supersample for smooth edges
    img = gradient(s, s)
    draw_mark(img, s * mark)
    if shape != 'square':
        mask = Image.new('L', (s, s), 0)
        md = ImageDraw.Draw(mask)
        if shape == 'circle':
            md.ellipse([0, 0, s - 1, s - 1], fill=255)
        else:
            md.rounded_rectangle([0, 0, s - 1, s - 1], radius=int(s * 112 / 512), fill=255)
        img.putalpha(mask)
    return img.resize((size, size), Image.LANCZOS)


def foreground(size: int) -> Image.Image:
    """Android adaptive-icon foreground: transparent, mark inside the 66/108 safe zone."""
    s = size * 4
    img = Image.new('RGBA', (s, s), (0, 0, 0, 0))
    draw_mark(img, s * 0.5)
    return img.resize((size, size), Image.LANCZOS)


def splash(w: int, h: int) -> Image.Image:
    img = gradient(w, h)
    draw_mark(img, min(w, h) * 0.3)
    return img.convert('RGB')


def pwa() -> None:
    out = ROOT / 'public' / 'icons'
    out.mkdir(parents=True, exist_ok=True)
    for s in (72, 96, 128, 144, 152, 192, 384, 512):
        icon(s).save(out / f'icon-{s}x{s}.png')
    for s in (192, 512):
        icon(s, 'square', 0.72).save(out / f'maskable-{s}x{s}.png')
    icon(180, 'square', 0.72).convert('RGB').save(out / 'apple-touch-icon.png')
    icon(48).save(out / 'favicon-48.png')


def native() -> None:
    res = ROOT / 'android' / 'app' / 'src' / 'main' / 'res'
    if res.exists():
        for png in res.rglob('*.png'):
            w, h = Image.open(png).size
            name = png.name
            if name == 'ic_launcher.png':
                icon(w, 'rounded', 0.8).save(png)
            elif name == 'ic_launcher_round.png':
                icon(w, 'circle', 0.7).save(png)
            elif name == 'ic_launcher_foreground.png':
                foreground(w).save(png)
            elif name == 'splash.png':
                splash(w, h).save(png)
        bg = res / 'values' / 'ic_launcher_background.xml'
        bg.write_text('<?xml version="1.0" encoding="utf-8"?>\n<resources>\n'
                      f'    <color name="ic_launcher_background">#{"".join(f"{c:02X}" for c in BG)}</color>\n'
                      '</resources>\n', encoding='utf-8')
    assets = ROOT / 'ios' / 'App' / 'App' / 'Assets.xcassets'
    if assets.exists():
        for png in (assets / 'AppIcon.appiconset').glob('*.png'):
            w, _ = Image.open(png).size
            icon(w, 'square', 0.78).convert('RGB').save(png)  # iOS rejects alpha in app icons
        for png in (assets / 'Splash.imageset').glob('*.png'):
            w, h = Image.open(png).size
            splash(w, h).save(png)


if __name__ == '__main__':
    pwa()
    if '--native' in sys.argv:
        native()
    print('icons generated')
