# -*- coding: utf-8 -*-
"""
图标集成校验 + 预览图生成。

1) 校验 app.ico 是否完整（9 个尺寸）；
2) 校验 EXE 是否已嵌入图标（/win32icon）与 .NET 资源（/resource,AppIcon.ico）；
3) 生成 preview_icon.png：各尺寸实际显示效果对照表，便于人工确认托盘小图标是否清晰。
"""
import os
import sys
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
ICO = os.path.join(HERE, "app.ico")
EXE = os.path.join(HERE, "GameAudioSwitcher.exe")
PREVIEW = os.path.join(HERE, "preview_icon.png")

SIZES = [16, 20, 24, 32, 40, 48, 64, 128, 256]


def check_ico():
    with Image.open(ICO) as ico:
        got = sorted(ico.info.get("sizes", set()))
    missing = [s for s in SIZES if (s, s) not in got]
    print("[1] app.ico 尺寸 %s" % (got,))
    print("    缺失尺寸：%s" % (missing if missing else "无"))
    return not missing


def check_exe():
    data = open(EXE, "rb").read()
    png_count = data.count(b"\x89PNG\r\n\x1a\n")
    has_res_name = b"AppIcon.ico" in data
    # ICON 资源目录特征：RT_ICON 的 BMP 头（BITMAPINFOHEADER biHeight 双倍高度）
    print("[2] GameAudioSwitcher.exe %d 字节" % len(data))
    print("    内嵌 PNG 帧数量：%d（ICO 中 256x256 帧为 PNG 编码，win32icon + resource 各一份）" % png_count)
    print("    含资源名 \"AppIcon.ico\"：%s" % has_res_name)
    return png_count > 0 and has_res_name


def build_preview():
    with Image.open(ICO) as ico:
        frames = {}
        for s in SIZES:
            try:
                ico.size = (s, s)
                frames[s] = ico.copy().convert("RGBA")
            except Exception:
                pass

    pad, gap, label_h = 24, 26, 22
    cell_h = max(frames.keys()) + label_h
    total_w = pad * 2 + sum(s for s in frames) + gap * (len(frames) - 1)
    total_h = pad * 2 + cell_h + 30

    sheet = Image.new("RGBA", (total_w, total_h), (255, 255, 255, 255))
    d = ImageDraw.Draw(sheet)

    x = pad
    for s, im in frames.items():
        # 每个尺寸垂直居中于最大尺寸所在基线
        y = pad + (max(frames.keys()) - s)
        sheet.alpha_composite(im, (x, y))
        d.rectangle([x - 1, y - 1, x + s, y + s], outline=(210, 214, 220, 255))
        d.text((x, pad + max(frames.keys()) + 4), "%dpx" % s, fill=(90, 96, 105, 255))
        x += s + gap

    d.text((pad, total_h - 22),
           "GameAudioSwitcher app.ico - 9 sizes (tray 16/20/24, taskbar 32/40/48, explorer 256)",
           fill=(120, 126, 136, 255))

    sheet.convert("RGB").save(PREVIEW, quality=95)
    print("[3] 预览图已生成：%s (%dx%d)" % (PREVIEW, total_w, total_h))


if __name__ == "__main__":
    ok = check_ico()
    ok = check_exe() and ok
    build_preview()
    print("VERIFY_%s" % ("OK" if ok else "FAIL"))
    sys.exit(0 if ok else 1)
