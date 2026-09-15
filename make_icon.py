# -*- coding: utf-8 -*-
"""
从源 PNG 生成多尺寸 app.ico（Windows 图标）。

用法：
    python make_icon.py <源图片> [输出ico]

说明：
    - 生成 16/20/24/32/40/48/64/128/256 九种尺寸，覆盖托盘、任务栏、
      Alt-Tab、资源管理器大图标等全部场景；
    - 使用 LANCZOS 重采样保证缩小后边缘平滑；
    - 源图若为无透明通道的 JPEG/PNG，自动转 RGBA 并按 32 位带 Alpha 输出。
"""
import sys
import os
from PIL import Image

SIZES = [(16, 16), (20, 20), (24, 24), (32, 32), (40, 40),
         (48, 48), (64, 64), (128, 128), (256, 256)]


def build_icon(src_path, out_path):
    img = Image.open(src_path)
    if img.mode != "RGBA":
        img = img.convert("RGBA")

    side = max(img.size)
    if img.size[0] != img.size[1]:
        # 非正方形：居中裁成正方形，避免图标被拉伸变形
        left = (img.size[0] - side) // 2
        top = (img.size[1] - side) // 2
        img = img.crop((left, top, left + side, top + side))

    # 以最高目标尺寸为基准做一次高质量缩放，再交给 Pillow 逐级生成
    base = img.resize((256, 256), Image.LANCZOS)
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
