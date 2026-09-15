# -*- coding: utf-8 -*-
"""
生成「托盘小图标优化版」备选图标并输出对比预览。

背景：整幅场景图在 16/20/24px 下细节全部糊掉，托盘可辨识度低。
备选方案把画面裁到猫咪主体（保头肩），小尺寸下轮廓清晰很多。

产出：
    app_tray_cat.ico        备选图标（同样是 9 个尺寸）
    preview_icon_alt.png    左=整幅场景版（当前生效）  右=猫咪特写版（备选）
"""
import os
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = r"D:\下载\下载\头像.png"
SIZES = [(16, 16), (20, 20), (24, 24), (32, 32), (40, 40),
         (48, 48), (64, 64), (128, 128), (256, 256)]

# 猫咪主体在原图中的裁剪框（左, 上, 右, 下），554x554 坐标
CROP_BOX = (158, 172, 398, 412)
SUPERSAMPLE = 4


def make_icon(img, out_path):
    base = img.resize((256, 256), Image.LANCZOS)
    base.save(out_path, format="ICO", sizes=SIZES)
    print("ICON_OK %s" % out_path)


def load_font(size):
    """优先用微软雅黑，缺失时退回 PIL 默认位图字体（标签将不含中文）。"""
    for path in (r"C:\Windows\Fonts\msyh.ttc", r"C:\Windows\Fonts\msyhbd.ttc",
                 r"C:\Windows\Fonts\simhei.ttf"):
        if os.path.exists(path):
            try:
                return ImageFont.truetype(path, size)
            except Exception:
                continue
    return ImageFont.load_default()


def render(sheet, ico_path, x0, y0, font):
    with Image.open(ico_path) as ico:
        cur = x0
        d = ImageDraw.Draw(sheet)
        for s in (16, 20, 24, 32, 48, 256):
            ico.size = (s, s)
            im = ico.copy().convert("RGBA")
            y = y0 + (256 - s)
            sheet.alpha_composite(im, (cur, y))
            d.rectangle([cur - 1, y - 1, cur + s, y + s], outline=(205, 210, 218, 255))
            d.text((cur, y0 + 262), "%dpx" % s, fill=(90, 96, 105, 255), font=font)
            cur += s + 20
    return cur


if __name__ == "__main__":
    img = Image.open(SRC).convert("RGBA")

    # 备选：先放大再裁，避免直接裁小图导致边缘锯齿
    big = img.resize((img.width * SUPERSAMPLE, img.height * SUPERSAMPLE), Image.LANCZOS)
    box = tuple(v * SUPERSAMPLE for v in CROP_BOX)
    cat = big.crop(box)
    alt_path = os.path.join(HERE, "app_tray_cat.ico")
    make_icon(cat, alt_path)

    # 也让主图标存在（首次运行本脚本时可能尚未生成）
    main_path = os.path.join(HERE, "app.ico")
    if not os.path.exists(main_path):
        make_icon(img, main_path)

    title_font = load_font(15)
    label_font = load_font(14)

    sheet = Image.new("RGBA", (1220, 340), (255, 255, 255, 255))
    d = ImageDraw.Draw(sheet)
    d.text((20, 14), "A  整幅场景版（当前生效：app.ico）", fill=(30, 34, 40, 255), font=title_font)
    render(sheet, main_path, 20, 40, label_font)
    d.text((660, 14), "B  猫咪特写版（备选：app_tray_cat.ico）", fill=(30, 34, 40, 255), font=title_font)
    render(sheet, alt_path, 660, 40, label_font)
    out = os.path.join(HERE, "preview_icon_alt.png")
    sheet.convert("RGB").save(out, quality=95)
    print("PREVIEW %s" % out)
