#!/usr/bin/env python3
"""Draw thunderstore/icon.png (256x256): a cracked lunar crown ringed by six shards, one per reclaimed boss.

Procedural on purpose: no game assets are redistributed. Rerun after changing the palette.
"""
import math
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

S = 1024  # draw at 4x, downsample for clean edges
OUT = Path(__file__).resolve().parent.parent / "thunderstore" / "icon.png"

# Mithrix, Voidling, Echo of Providence, Anima Harvester, Solus Wing, Solus Heart
SHARDS = [(110, 190, 255), (196, 92, 255), (255, 214, 120), (120, 255, 170), (255, 140, 70), (255, 70, 110)]


def radial_bg():
    y, x = np.mgrid[0:S, 0:S].astype(np.float32)
    d = np.hypot(x - S / 2, y - S / 2) / (S / 2)
    inner, outer = np.array([34, 38, 70]), np.array([6, 6, 14])
    t = np.clip(d, 0, 1)[..., None] ** 1.4
    img = (inner * (1 - t) + outer * t).astype(np.uint8)
    rng = np.random.default_rng(7)
    stars = rng.random((S, S)) > 0.9985
    img[stars] = [210, 220, 255]
    return Image.fromarray(img, "RGB")


def main():
    img = radial_bg().convert("RGBA")
    glow = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    g = ImageDraw.Draw(glow)
    c = S / 2
    # orbit ring
    g.ellipse([c - 380, c - 380, c + 380, c + 380], outline=(150, 200, 255, 150), width=10)
    # six shards on the ring
    for i, col in enumerate(SHARDS):
        a = -math.pi / 2 + i * math.tau / 6
        px, py = c + 380 * math.cos(a), c + 380 * math.sin(a)
        r = 70
        pts = [(px + r * math.cos(a), py + r * math.sin(a)),
               (px + r * 0.45 * math.cos(a + math.pi / 2), py + r * 0.45 * math.sin(a + math.pi / 2)),
               (px - r * 0.8 * math.cos(a), py - r * 0.8 * math.sin(a)),
               (px + r * 0.45 * math.cos(a - math.pi / 2), py + r * 0.45 * math.sin(a - math.pi / 2))]
        g.polygon(pts, fill=col + (255,))
    # crown: five spires over a band, split by a crack
    band_y, band_h, w = c + 110, 90, 520
    spires = []
    for i in range(5):
        x0 = c - w / 2 + i * w / 5
        h = [210, 300, 400, 300, 210][i]
        spires.append([(x0, band_y), (x0 + w / 10, band_y - h), (x0 + w / 5, band_y)])
    crown = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    cd = ImageDraw.Draw(crown)
    for sp in spires:
        cd.polygon(sp, fill=(200, 230, 255, 255))
    cd.rectangle([c - w / 2, band_y, c + w / 2, band_y + band_h], fill=(200, 230, 255, 255))
    # the cyclopean eye on the band
    cd.ellipse([c - 70, band_y + 10, c + 70, band_y + band_h - 10], fill=(20, 40, 90, 255))
    cd.ellipse([c - 28, band_y + 25, c + 28, band_y + band_h - 25], fill=(120, 220, 255, 255))
    # crack (reclaimed: broken and remade)
    crack = [(c + 10, band_y - 400), (c - 30, band_y - 230), (c + 25, band_y - 90), (c - 15, band_y + 40),
             (c + 20, band_y + band_h)]
    cd.line(crack, fill=(0, 0, 0, 0), width=26)
    cd.line(crack, fill=(255, 225, 140, 255), width=10)
    glow.alpha_composite(crown)
    halo = glow.filter(ImageFilter.GaussianBlur(28))
    img.alpha_composite(halo)
    img.alpha_composite(glow)
    img = img.resize((256, 256), Image.LANCZOS).convert("RGB")
    OUT.parent.mkdir(parents=True, exist_ok=True)
    img.save(OUT, optimize=True)
    print(f"wrote {OUT}")


if __name__ == "__main__":
    main()
