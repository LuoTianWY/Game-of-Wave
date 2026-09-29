# -*- coding: utf-8 -*-
"""
把 Assets/Sprites/Room/Tiles 下的「透视房间壳」素材拼成一张整房间背景图。

输出 Assets/Sprites/Room/RoomBackdrop.png（+ .meta），
尺寸 2160x1440 = 18x12 世界单位 @ 120 px/单位，与排练室场景现有房间矩形完全对齐：
    世界 x  -9.5 .. 8.5   → 像素 x    0 .. 2160
    世界 y   5.5 ..-6.5   → 像素 y    0 .. 1440   （像素 y 向下，世界 y 向上）
场景里把这张图挂在 (−0.5, −0.5)，世界尺寸 18x12，即与房间矩形严丝合缝。

拼法：以 wall_bottom（后墙 + 木地板，本身就是一张完整室内图）为底板铺满，
再用「透明羽化」把 wall_left / wall_right / wall_top 贴到左右上三边融合，
最后压暗四角做暗角。所有贴合边都走 alpha 渐变，不留硬缝。

用法：python Tools/compose_room.py
"""
import os
import sys
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TILES = os.path.join(ROOT, 'Assets', 'Sprites', 'Room', 'Tiles')
OUT_PNG = os.path.join(ROOT, 'Assets', 'Sprites', 'Room', 'RoomBackdrop.png')
OUT_META = OUT_PNG + '.meta'

UNIT = 120
ROOM_W, H = 18 * UNIT, 12 * UNIT     # 2160 x 1440 —— 房间本体，与场景里的房间矩形严格对齐
# 输出宽度 = 2560，即 21.33 单位宽 x 12 单位高 = 16:9。相机正交半高 6 时，
# 16:9 屏幕横向可见 12 * 16/9 = 21.33 单位，正好被这张图铺满，不再留边。
# 房间本体仍然只占中间 2160px，左右各镜像延伸 200px —— 这样墙脚线（wy 0.96）
# 的位置不变，道具坐标和碰撞布局全都不用重测。
W = 2560
EXTEND_BLEND = 160                   # 镜像接缝处的横向渐变宽度，抹掉镜像"折痕"

SIDE_W = 520                         # 左右延伸带宽度（≈4.3 单位）
TOP_H = 260                          # 天花板带高度（≈2.2 单位）
VIGNETTE = 0.45                      # 暗角强度 0=不压 1=全黑


def load(name):
    p = os.path.join(TILES, name + '.png')
    if not os.path.exists(p):
        sys.exit('缺少素材：' + p)
    return Image.open(p).convert('RGB')


def cover(img, w, h, anchor='center'):
    """等比缩放充满 w x h，多余部分裁掉（不拉伸变形）。"""
    sw, sh = img.size
    k = max(w / sw, h / sh)
    nw, nh = max(w, int(round(sw * k))), max(h, int(round(sh * k)))
    img = img.resize((nw, nh), Image.LANCZOS)
    oy = 0 if anchor == 'top' else (nh - h if anchor == 'bottom' else (nh - h) // 2)
    ox = (nw - w) // 2
    return img.crop((ox, oy, ox + w, oy + h))


def fade_mask(size, sides, depth):
    """生成方向性 alpha 渐变遮罩：边沿 edge_a，往里 depth 像素线性到 inner_a。"""
    w, h = size
    mask = Image.new('L', size, 255)
    px = mask.load()
    for side, (edge_a, inner_a) in sides.items():
        for i in range(depth):
            t = i / max(1, depth - 1)
            v = int(round(255 * (edge_a + (inner_a - edge_a) * t)))
            if side == 'bottom':
                for x in range(w):
                    px[x, h - 1 - i] = min(px[x, h - 1 - i], v)
            elif side == 'top':
                for x in range(w):
                    px[x, i] = min(px[x, i], v)
            elif side == 'left':
                for y in range(h):
                    px[i, y] = min(px[i, y], v)
            elif side == 'right':
                for y in range(h):
                    px[w - 1 - i, y] = min(px[w - 1 - i, y], v)
    return mask


def paste_fade(canvas, img, pos, sides, depth):
    """把 img 以透明羽化边贴到 canvas 上（sides 指定哪些边渐隐）。"""
    canvas.paste(img, pos, fade_mask(img.size, sides, depth))


def fill_sides(canvas, target_w, sample=(700, 220), blend=EXTEND_BLEND):
    """
    把画布左右各扩 (target_w - 宽)/2，用房间**中段一块空白墙+地板**铺开。

    不能用边缘镜像：底图 wall_bottom 左端就画着那台音箱柜，镜像等于把它复制一份，
    屏幕边上会冒出第二个对称的音箱（试过，一眼假）。
    所以改从 px 700~920 取样——那段只有空墙和木地板，不含任何画好的道具——
    再横向镜像平铺（镜像处取值连续，不会出现硬边），最后在接缝做渐变把纹理差异化开。

    取样条贯穿整幅高度，墙脚线（wy 0.96）在延伸区里自然对齐，
    房间本体一个像素都没动，道具坐标和碰撞布局全都不用重测。
    """
    w, h = canvas.size
    extra = target_w - w
    if extra <= 0:
        return canvas.crop((0, 0, target_w, h))
    left = extra // 2
    right = extra - left

    sx, sw = sample
    strip = canvas.crop((sx, 0, sx + sw, h))

    def band(width):
        out = Image.new('RGB', (width, h))
        x, flip = 0, False
        while x < width:
            out.paste(strip if not flip else strip.transpose(Image.FLIP_LEFT_RIGHT), (x, 0))
            x += sw
            flip = not flip
        return out.crop((0, 0, width, h))

    out = Image.new('RGB', (target_w, h))
    out.paste(band(left), (0, 0))
    out.paste(canvas, (left, 0))
    out.paste(band(right), (left + w, 0))

    # 接缝渐变：紧贴缝的那一列**完全取房间本体的值**（保证连续），往外逐列回到延伸内容。
    # 方向很关键——反过来写（缝上混得最少）等于没混，缝会是一条硬边，之前就是这么翻车的。
    half = max(1, min(blend // 2, left, w, right))
    px = out.load()
    for d in range(half):
        t = 1.0 - (d + 0.5) / half                 # 1 → 0，由缝向外
        for xo, xi in ((left - 1 - d, left + d), (left + w - 1 - d, left + w + d)):
            for y in range(h):
                a, b = px[xo, y], px[xi, y]
                px[xo, y] = tuple(int(round(a[c] + (b[c] - a[c]) * t)) for c in range(3))
    return out


def edge_color(canvas, band=6):
    """取最外侧 band 列的平均色 —— 相机清屏色用它，背景图边缘就能无缝融进屏幕底色。"""
    w, h = canvas.size
    px = canvas.load()
    cols = list(range(band)) + list(range(w - band, w))
    rs = gs = bs = n = 0
    for x in cols:
        for y in range(0, h, 4):
            r, g, b = px[x, y]
            rs += r; gs += g; bs += b; n += 1
    return (rs // n, gs // n, bs // n)


def vignette(canvas, strength, depth=320):
    """四周压暗做暗角。"""
    w, h = canvas.size
    m = Image.new('L', (w, h), 0)          # 0=全亮，255=全黑
    px = m.load()
    for y in range(h):
        for x in range(w):
            d = min(x, y, w - 1 - x, h - 1 - y)
            if d < depth:
                px[x, y] = int(255 * strength * (1 - d / depth) ** 1.6)
    dark = Image.new('RGB', (w, h), (0, 0, 0))
    canvas.paste(dark, (0, 0), m)


def main():
    # ---- 1) 房间本体：宽高都用 ROOM_W / H，与之前完全一致，所以墙脚线不动 ----
    #      wall_bottom 本身就是一张完整室内图，等比放大铺满即可，不必再拼侧带
    #      （侧带会留下竖直硬缝）。anchor='bottom' 取偏下的一段，让画面上半是
    #      后墙、下半留出足够地板摆道具。
    canvas = cover(load('wall_bottom'), ROOM_W, H, anchor='bottom')

    # 天花板：贴在顶边，底边渐隐融进后墙
    top = cover(load('wall_top'), ROOM_W, TOP_H, anchor='center')
    paste_fade(canvas, top, (0, 0), {'bottom': (0, 255)}, 200)

    # 注：不再贴 wall_left / wall_right 侧带。这两张素材各自带一截地板、跨度又
    # 顶天立地，压上去必然遮住底板的地面并在两侧留下竖直硬缝（已试过两版）。

    # ---- 2) 取中段空白墙+地板向两侧铺开，补到 16:9 满屏 ----
    canvas = fill_sides(canvas, W)

    # ---- 3) 暗角：放在延伸之后，新边缘一起压暗，接缝进一步隐形 ----
    vignette(canvas, VIGNETTE)

    canvas.save(OUT_PNG)
    rgb = edge_color(canvas)
    print('已生成 %s %s  (%.2f x %.2f 单位)' % (
        OUT_PNG, canvas.size, canvas.width / UNIT, canvas.height / UNIT))
    print('边缘平均色 = rgb(%d, %d, %d)  → 相机 Clear Flags 用 Solid Color 时填这个值' % rgb)
    write_meta()


def write_meta():
    """写 TextureImporter meta：Single 精灵、PPU=120、FullRect、不透明。"""
    tpl = open(os.path.join(os.path.dirname(os.path.abspath(__file__)),
                            'backdrop.meta.tpl'), encoding='utf-8').read()
    with open(OUT_META, 'w', encoding='utf-8', newline='\n') as f:
        f.write(tpl)
    print('已写 %s' % OUT_META)


if __name__ == '__main__':
    main()
