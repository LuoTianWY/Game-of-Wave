# -*- coding: utf-8 -*-
"""
把 Assets/Sprites/Room/Props 下的道具图抠成透明背景，输出到
Assets/Sprites/Room/Cutout/（原图不动，随时可回退）。

背景是一块近似均匀的深灰（四角均色 #26~#3D），所以从四边向内做**泛洪填充**：
只吃掉与边缘连通的背景像素，道具内部的深色（音箱、金属垃圾桶）不受影响。
抠完裁到内容框并留 8px 边距，再写 meta（Single 精灵 / FullRect / PPU=1024）。

PPU 统一 1024，保证各道具的相对大小关系不被破坏（1024px = 1 世界单位），
个别道具在场景里再单独调 scale。

用法：python Tools/cutout_props.py
"""
import os
import sys
import glob
import hashlib
from collections import deque
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, 'Assets', 'Sprites', 'Room', 'Props')
DST = os.path.join(ROOT, 'Assets', 'Sprites', 'Room', 'Cutout')
META_TPL = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'cutout.meta.tpl')

MIN_THRESH = 10    # 泛洪容差下限（背景再均匀也不能低于这个，否则噪点挡住泛洪）
MIN_KEEP = 0.03    # 不透明像素占比低于此值视为「把道具也抠掉了」，收紧阈值重试
PAD = 8            # 裁剪后留的边距
ALPHA_FLOOR = 40   # 低于此透明度的像素直接判为全透明（清掉泛洪残留的雾）
MIN_BLOB = 0.02    # 连通块小于整图此比例即判为杂点丢掉（道具主体远大于 2%）
PPU = 1024


def ref_color(flat, w, h, ring=4):
    """
    取边缘 ring 像素圈的背景参考色（各通道中位数），顺带统计色散。
    用中位数而非均值：边缘偶尔会蹭到道具，中位数不受少数异常像素影响。
    """
    idx = []
    for y in range(ring):
        for x in range(w):
            idx.append((y * w + x) * 3)
            idx.append(((h - 1 - y) * w + x) * 3)
    for x in range(ring):
        for y in range(h):
            idx.append((y * w + x) * 3)
            idx.append((y * w + w - 1 - x) * 3)
    chans = [[flat[i + c] for i in idx] for c in range(3)]
    ref = tuple(sorted(c)[len(c) // 2] for c in chans)
    # 边缘像素相对参考色的曼哈顿距离分布 → 取 P90 作为基础容差
    ds = sorted(abs(flat[i] - ref[0]) + abs(flat[i + 1] - ref[1]) + abs(flat[i + 2] - ref[2])
                for i in idx)
    p90 = ds[int(len(ds) * 0.9)]
    return ref, p90


def flood(flat, w, h, ref, thresh):
    """
    从四边泛洪：与**全局参考色** ref 的曼哈顿色差 <= thresh 且连通的像素判为背景。
    与固定参考色比（而非与父像素比）是关键：逐像素比较会顺着背景渐变一路漂移，
    最终泄漏进道具内部，把暗色道具整块吃掉（v1 的 amp/chair/sofa 就是这么没的）。
    """
    bg = bytearray(w * h)
    q = deque()

    def push(p):
        if not bg[p]:
            bg[p] = 1
            q.append(p)

    for x in range(w):
        push(x); push((h - 1) * w + x)
    for y in range(h):
        push(y * w); push(y * w + w - 1)

    while q:
        p = q.popleft()
        x = p % w
        if x > 0:            _try(flat, bg, q, p - 1, ref, thresh)
        if x < w - 1:        _try(flat, bg, q, p + 1, ref, thresh)
        if p >= w:           _try(flat, bg, q, p - w, ref, thresh)
        if p < w * (h - 1):  _try(flat, bg, q, p + w, ref, thresh)
    return bg


def _try(flat, seen, q, p, ref, thresh):
    """邻像素 p 若与参考色 ref 的曼哈顿色差 <= thresh 且未访问，则并入背景。"""
    if seen[p]:
        return
    i = p * 3
    if (abs(flat[i] - ref[0]) + abs(flat[i + 1] - ref[1])
            + abs(flat[i + 2] - ref[2])) <= thresh:
        seen[p] = 1
        q.append(p)


def prune_specks(mask, min_frac=MIN_BLOB):
    """
    丢掉 mask 里孤立的小连通块，只保留主体（与泛洪同款扁平数组 BFS）。

    泛洪只吃「与画布边缘连通」的背景，所以背景里飘着的孤立色块（水印、阴影碎片、
    生成噪点）会原样留下。它们虽然不起眼，却会把 getbbox() 的包围盒撑到整幅图，
    导致裁剪框远大于道具本体（bass / coffee_table / sofa 都是这么虚胖的）。
    """
    w, h = mask.size
    # 任意非零 → 1，得到纯粹的 0/1 占据图（translate 走 C 实现，4M 字节瞬间完成）
    zero_one = bytes(1 if v else 0 for v in range(256))
    opq = bytearray(mask.tobytes().translate(zero_one))
    limit = int(w * h * min_frac)
    tocut = []

    pos = 0
    while True:
        seed = opq.find(1, pos)          # find 是 C 实现，跳过已清掉的像素很快
        if seed < 0:
            break
        opq[seed] = 0
        q = deque([seed])
        blob, over = [], False           # over=True 表示已确认是主体，不必再记像素
        while q:
            p = q.popleft()
            if not over:
                blob.append(p)
                if len(blob) > limit:
                    over = True
                    blob = None          # 主体，整块留用
            x = p % w
            if x > 0 and opq[p - 1]:           opq[p - 1] = 0; q.append(p - 1)
            if x < w - 1 and opq[p + 1]:       opq[p + 1] = 0; q.append(p + 1)
            if p >= w and opq[p - w]:          opq[p - w] = 0; q.append(p - w)
            if p < w * (h - 1) and opq[p + w]: opq[p + w] = 0; q.append(p + w)
        if not over:
            tocut.extend(blob)           # 碎块，稍后统一抹掉
        pos = seed + 1

    if not tocut:
        return mask, 0
    mb = bytearray(mask.tobytes())
    for p in tocut:
        mb[p] = 0
    return Image.frombytes('L', (w, h), bytes(mb)), len(tocut)


def flood_alpha(im, verbose=False):
    """
    抠出透明背景。返回 (RGBA 图, 背景占比, 用的阈值)。
    阈值自适应：以边缘色散 P90 为基准，先按较宽松的值试；若把道具也吃掉了
    （不透明像素太少），就收紧阈值重试，避免「抠成空图」。
    """
    w, h = im.size
    flat = im.tobytes()
    ref, p90 = ref_color(flat, w, h)

    best = None
    for thresh in (max(MIN_THRESH, p90 + 14), p90 + 6, p90, max(MIN_THRESH, p90 - 10)):
        bg = flood(flat, w, h, ref, thresh)
        keep = 1.0 - sum(bg) / (w * h)
        if verbose:
            print('      阈值 %3d → 保留 %.1f%%' % (thresh, keep * 100))
        if best is None or abs(keep - 0.30) < abs(best[2] - 0.30):
            best = (bg, thresh, keep)
        if MIN_KEEP <= keep <= 0.92:      # 合理：既抠掉了背景，也没吃掉道具
            best = (bg, thresh, keep)
            break

    bg, thresh, keep = best
    mask = Image.frombytes('L', (w, h), bytes(255 - (v * 255) for v in bg))
    mask = mask.filter(ImageFilter.GaussianBlur(0.6))   # 边缘羽化，去锯齿
    # 泛洪会在道具周围留一圈半透明的"没抠干净"的雾。不压低它，getbbox() 会按这层
    # 雾算包围盒，把裁剪框撑得远大于道具本体（v2 的 bass / coffee_table 就是这么虚胖的）。
    mask = mask.point(lambda v: 0 if v < ALPHA_FLOOR else v)
    mask, specks = prune_specks(mask)
    out = im.convert('RGBA')
    out.putalpha(mask)
    return out, 1.0 - keep, thresh, specks


def main():
    if not os.path.isdir(DST):
        os.makedirs(DST)
    if not os.path.exists(META_TPL):
        sys.exit('缺少 meta 模板：' + META_TPL)
    tpl = open(META_TPL, encoding='utf-8').read()

    files = [f for f in sorted(glob.glob(os.path.join(SRC, '*.png')))
             if 'v2' not in os.path.basename(f)]
    names = ['amp', 'bass', 'cabinet', 'chair', 'coffee_table', 'drum_kit',
             'guitar_acoustic', 'guitar_case', 'guitar_electric', 'guitar_stand',
             'instrument_case', 'keyboard', 'mic_stand', 'shelf', 'sofa',
             'stool', 'trash_bin']
    by_name = {os.path.basename(f)[:-4]: f for f in files}

    print('%-20s %-13s %-13s %-8s %s' % ('道具', '原图', '裁剪后', '阈值', '世界尺寸(单位)'))
    tiles = []
    for name in names:
        src = by_name.get(name)
        if src is None:
            print('  !! 缺素材 %s' % name)
            continue
        im = Image.open(src).convert('RGB')
        rgba, bgpct, thresh, specks = flood_alpha(im)
        bbox = rgba.getbbox()
        if bbox is None:
            print('  !! %s 抠完是空的（阈值全吃掉了）' % name)
            continue
        x0, y0, x1, y1 = bbox
        x0 = max(0, x0 - PAD); y0 = max(0, y0 - PAD)
        x1 = min(rgba.width, x1 + PAD); y1 = min(rgba.height, y1 + PAD)
        out = rgba.crop((x0, y0, x1, y1))
        op = os.path.join(DST, name + '.png')
        out.save(op)

        # meta：guid 必须是稳定的 32 位十六进制。绝不能用 hash(name) —— Python 对
        # 字符串的 hash 每个进程都随机化（PYTHONHASHSEED），那样每重跑一次脚本，
        # 17 个 GUID 全部改变，场景里已经挂好的精灵引用会集体断掉。
        guid = hashlib.md5(('WaveTeam.Room.Cutout.' + name).encode('utf-8')).hexdigest()
        with open(op + '.meta', 'w', encoding='utf-8', newline='\n') as f:
            f.write(tpl.replace('__GUID__', guid).replace('__PPU__', str(PPU)))

        print('%-20s %-13s %-13s %-8d %.2f x %.2f   (抠掉背景 %.0f%%，清杂点 %d px)' % (
            name,
            '%dx%d' % im.size,
            '%dx%d' % out.size,
            thresh,
            out.width / PPU, out.height / PPU,
            bgpct * 100, specks))
        tiles.append((name, out))

    sheet(tiles)


def sheet(tiles, cell=300, cols=6):
    """拼一张棋盘底对照图，用来肉眼检查抠图质量（不写进 Assets）。"""
    if not tiles:
        return
    rows = (len(tiles) + cols - 1) // cols
    out = Image.new('RGB', (cols * cell, rows * cell), (30, 30, 34))
    d = ImageDraw.Draw(out)
    for x in range(0, cols * cell, 24):          # 棋盘格，透明处一眼可见
        for y in range(0, rows * cell, 24):
            if (x // 24 + y // 24) % 2 == 0:
                d.rectangle([x, y, x + 23, y + 23], fill=(52, 52, 58))
    for i, (name, im) in enumerate(tiles):
        k = min(cell * 0.86 / im.width, cell * 0.86 / im.height)
        th = im.resize((max(1, int(im.width * k)), max(1, int(im.height * k))), Image.LANCZOS)
        cx = (i % cols) * cell + cell // 2
        cy = (i // cols) * cell + cell // 2
        out.paste(th, (cx - th.width // 2, cy - th.height // 2), th)
        d.text((cx - cell // 2 + 6, cy + cell // 2 - 18), name, fill=(230, 230, 230))
    p = os.path.join(os.path.dirname(os.path.abspath(__file__)), '_cutout_check.png')
    out.save(p)
    print('\n对照图 → %s' % p)


if __name__ == '__main__':
    main()
