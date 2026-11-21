# -*- coding: utf-8 -*-
"""程序化生成水彩感背景：教室 / 走廊。
柔和大色块 + 光晕 + 纸纹颗粒，模拟水彩的晕染感。"""
import numpy as np
from pathlib import Path
from PIL import Image

OUT = Path(__file__).parent / "Assets" / "Art"
OUT.mkdir(parents=True, exist_ok=True)
W, H = 1920, 1080


def canvas(base):
    y, x = np.mgrid[0:H, 0:W].astype(np.float32)
    return x, y, np.zeros((H, W, 3), np.float32) + np.array(base, np.float32)


def blob(img, x, y, cx, cy, r, color, alpha=0.5, soft=3.0):
    d = np.sqrt((x - cx) ** 2 + (y - cy) ** 2) / r
    m = np.clip(1 - d, 0, 1) ** soft * alpha
    for i in range(3):
        img[:, :, i] = img[:, :, i] * (1 - m) + color[i] * m


def paper_grain(img, seed=7):
    rng = np.random.default_rng(seed)
    n = rng.normal(0, 1.5, (H, W)).astype(np.float32)
    fib = rng.normal(0, 2.5, (H // 16 + 1, W // 16 + 1))
    fib = np.kron(fib, np.ones((16, 16)))[:H, :W]
    img += (n + fib)[:, :, None]
    return np.clip(img, 0, 255)


def ink_lines(img, segs, a=45):
    """沿给定的归一化线段画淡淡的勾线，模拟水彩画中铅笔底稿。"""
    for (x0, y0, x1, y1) in segs:
        steps = int(np.hypot((x1 - x0) * W, (y1 - y0) * H))
        for s in np.linspace(0, 1, max(steps, 2)):
            px = int((x0 + (x1 - x0) * s) * W)
            py = int((y0 + (y1 - y0) * s) * H)
            r = 3
            region = img[max(0, py - r):py + r, max(0, px - r):px + r]
            img[max(0, py - r):py + r, max(0, px - r):px + r] = region * (1 - a / 255.0)
    return img


def save(img, name):
    Image.fromarray(img.astype(np.uint8)).save(OUT / name)
    print("saved", name)


# ---------- 教室：暖黄夕光 ----------
x, y, img = canvas((214, 196, 172))
# 透光的窗
blob(img, x, y, W * 0.62, H * 0.30, 620, (250, 236, 200), 0.85, 2.2)
blob(img, x, y, W * 0.75, H * 0.42, 480, (252, 242, 214), 0.7, 2.5)
# 光斜切进来的暖带
blob(img, x, y, W * 0.5, H * 0.62, 900, (238, 214, 168), 0.5, 2.8)
# 课桌的暗色块（模糊的横条）
for i, cy in enumerate([H * 0.72, H * 0.82, H * 0.92]):
    blob(img, x, y, W * 0.35 + i * 260, cy, 320, (156, 132, 108), 0.4, 1.6)
blob(img, x, y, W * 0.15, H * 0.35, 420, (182, 168, 150), 0.5, 2.0)
# 窗框与桌沿的勾勒线（弱化水彩晕染的形状感）
img = ink_lines(img, [(0.55, 0.10, 0.55, 0.62), (0.70, 0.10, 0.70, 0.62),
                      (0.55, 0.10, 0.85, 0.10), (0.55, 0.62, 0.85, 0.62)])
save(paper_grain(img), "bg_classroom.png")

# ---------- 走廊：偏冷清晨 ----------
x, y, img = canvas((186, 194, 200))
blob(img, x, y, W * 0.28, H * 0.35, 640, (222, 232, 238), 0.85, 2.2)
blob(img, x, y, W * 0.5, H * 0.55, 800, (206, 218, 226), 0.5, 2.6)
# 地面反光
blob(img, x, y, W * 0.6, H * 0.9, 700, (168, 180, 190), 0.6, 1.8)
# 远端的光点
blob(img, x, y, W * 0.88, H * 0.45, 260, (245, 248, 250), 0.8, 2.4)
# 走廊两侧的立柱线
for fx in (0.12, 0.30, 0.52, 0.74):
    img = ink_lines(img, [(fx, 0.05, fx, 0.95)], a=22)
save(paper_grain(img, 11), "bg_corridor.png")

print("done")
