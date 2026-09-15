# -*- coding: utf-8 -*-
"""
从源 PNG 生成多尺寸 app.ico（Windows 图标）。

用法：
    python make_icon.py <源图片> [输出ico]

说明：
    - 生成 16/20/24/32/40/48/64/128/256 九种尺寸，覆盖托盘、任务栏、
      Alt-Tab、资源管理器大图标等全部场景；
    - 预处理三步：① 按 Alpha 通道裁掉四周透明留白 → ② 等比缩放使长边
      占满画布的 92% → ③ 居中贴到 256x256 透明画布（上下/左右不对称留白
      会被拉平）。这样小尺寸下主体尽量大，避免 16px 托盘图标糊成一团；
    - 等比缩放，不裁剪不拉伸，非正方形图案原样保留（留白由透明画布补足）；
    - 使用 LANCZOS 重采样保证缩小后边缘平滑；
    - 源图若为无透明通道的 JPEG/PNG，自动转 RGBA 并按 32 位带 Alpha 输出。
"""
import sys
import os
from PIL import Image

SIZES = [(16, 16), (20, 20), (24, 24), (32, 32), (40, 40),
         (48, 48), (64, 64), (128, 128), (256, 256)]

CANVAS = 256      # ICO 最大帧边长
MARGIN = 0.04     # 四周各留 4% 边距（长边占画布 92%）


def trim_alpha(img):
    """裁掉四周全透明留白。返回 (处理后的图, 裁剪框或 None)。"""
    bbox = img.getbbox()
    if bbox and bbox != (0, 0, img.size[0], img.size[1]):
        return img.crop(bbox), bbox
    return img, None


def fit_square(img, canvas=CANVAS, margin=MARGIN):
    """等比缩放到 canvas 画布内并居中，四周留 margin 比例透明边距。"""
    inner = int(round(canvas * (1 - 2 * margin)))
    w, h = img.size
    k = float(inner) / max(w, h)
    new = (max(1, int(round(w * k))), max(1, int(round(h * k))))
    scaled = img.resize(new, Image.LANCZOS)
    base = Image.new("RGBA", (canvas, canvas), (0, 0, 0, 0))
    base.paste(scaled, ((canvas - new[0]) // 2, (canvas - new[1]) // 2), scaled)
    return base, new


def build_icon(src_path, out_path):
    img = Image.open(src_path)
    if img.mode != "RGBA":
        img = img.convert("RGBA")

    img, cut = trim_alpha(img)
    if cut:
        print("TRIM %s -> %dx%d" % (str(cut), img.size[0], img.size[1]))

    base, placed = fit_square(img)
    print("FIT art=%dx%d in %dx%d" % (placed[0], placed[1], CANVAS, CANVAS))

    base.save(out_path, format="ICO", sizes=SIZES)

    size_kb = os.path.getsize(out_path) / 1024.0
    print("ICON_OK %s  (%d entries, %.1f KB)" % (out_path, len(SIZES), size_kb))

    # 回读校验：确认每个尺寸都真实存在
    with Image.open(out_path) as chk:
        got = sorted(chk.info.get("sizes", set()))
        print("SIZES %s" % (got,))


if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("usage: python make_icon.py <source.png> [out.ico]")
        sys.exit(1)
    src = sys.argv[1]
    out = sys.argv[2] if len(sys.argv) > 2 else os.path.join(
        os.path.dirname(os.path.abspath(__file__)), "app.ico")
    build_icon(src, out)
