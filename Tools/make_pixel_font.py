# -*- coding: utf-8 -*-
"""
生成「像素字体」位图图集，供 Assets/Scripts/UI/PixelText.cs 使用。

为什么要自己造：工程里没有任何字体资源（find 不到 ttf/otf/.fontsettings），而
Unity 内置的 Arial 是矢量抗锯齿字体，放大后不是像素风；中文字形又不可能手搓点阵。
所以改用系统黑体在 16px 下单色化（threshold 到 1-bit），把用到的字收进一张图集。

字形来源：黑体 SimHei。它在小字号下笔画粗、无衬线，阀值化之后方块感强，
是中文位图字体里最常用的那类。

字符集 = ASCII 可见字符 + 扫描 Assets/Scripts/**/*.cs 里**字符串字面量**中出现的中日韩
字符 + 本文件 EXTRA 里列的字。只扫字符串字面量、不扫注释，否则源码注释里的
大量中文会把图集撑大好几倍。

输出：
    Assets/Resources/PixelFont/pixel_font.png         图集（白色 RGB + 墨迹 Alpha）
    Assets/Resources/PixelFont/pixel_font.png.meta    Point 采样 / 不压缩 / 无 mipmap
    Assets/Resources/PixelFont/pixel_font.json        字表：[列, 行, 步进宽]
    Tools/_pixelfont_preview.png                      样张，不写进 Assets

用法：python Tools/make_pixel_font.py
"""
import os
import re
import sys
import json
import glob
from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC_DIR = os.path.join(ROOT, 'Assets', 'Scripts')
OUT_DIR = os.path.join(ROOT, 'Assets', 'Resources', 'PixelFont')
OUT_PNG = os.path.join(OUT_DIR, 'pixel_font.png')
OUT_JSON = os.path.join(OUT_DIR, 'pixel_font.json')

FONT_PATH = r'C:\Windows\Fonts\simhei.ttf'
SIZE = 16          # 字面 em 大小（px）。改大改小要同步重跑，图集和度量会一起变。
THRESH = 110       # 灰度阀值：>= 判为墨迹。调高笔画细，调低笔画糊。
PAD = 2            # 单元格四周留白，避免 Point 采样在边缘蹭到邻字
EXTRA_COLS = 20    # 图集列数上限（行数按需增长）

# 扫描之外的补充字：即将写进代码、但此刻源码里还没有的字，提前放进来。
EXTRA = '阿宁小川老周石头雨神秘嘉宾按互动角色信息性格等级波形任务关闭'

# 只收这些区段的非 ASCII 字符，避免把源码里混进的奇怪符号也塞进图集
CJK_RANGES = (
    (0x3000, 0x303F),   # 中文标点
    (0x4E00, 0x9FFF),   # 中日韩统一表意
    (0xFF00, 0xFFEF),   # 全角字符
)
CJK_SINGLE = '·“”‘’—…、。，！？：；（）《》'

STRING_LITERAL = re.compile(r'"((?:[^"\\]|\\.)*)"')
CHAR_LITERAL = re.compile(r"'((?:[^'\\]|\\.)*)'")


def collect_chars():
    """ASCII 可见字符 + 源码字符串字面量里的中日韩字符 + EXTRA。"""
    chars = set(chr(c) for c in range(0x20, 0x7F))
    chars.update(EXTRA)
    chars.update(CJK_SINGLE)

    files = glob.glob(os.path.join(SRC_DIR, '**', '*.cs'), recursive=True)
    for path in files:
        src = open(path, encoding='utf-8', errors='replace').read()
        # 先剥掉注释，否则注释里成片的中文会被当成 UI 文案收进来
        src = re.sub(r'/\*.*?\*/', ' ', src, flags=re.S)
        src = re.sub(r'//[^\n]*', ' ', src)
        for lit in STRING_LITERAL.findall(src) + CHAR_LITERAL.findall(src):
            for ch in lit:
                o = ord(ch)
                if any(lo <= o <= hi for lo, hi in CJK_RANGES):
                    chars.add(ch)
    print('扫描 %d 个 .cs，字符集共 %d 个字形' % (len(files), len(chars)))
    return sorted(chars)


def measure(font, chars):
    """量出统一的单元格尺寸：所有字都从同一个原点（ascender 左上）起画，
    所以只要取全体墨迹的右下边界，就能保证谁都不被裁。"""
    probe = ImageDraw.Draw(Image.new('L', (8, 8)))
    max_x = max_y = 0
    for ch in chars:
        try:
            x0, y0, x1, y1 = probe.textbbox((0, 0), ch, font=font)
        except Exception:
            continue
        max_x = max(max_x, x1)
        max_y = max(max_y, y1)
    cw, chh = max(1, int(max_x + 0.5)), max(1, int(max_y + 0.5))
    # 中文字形是满格方块，宽度不足时补到 em 宽，保证「汉字等宽、竖直对齐」
    cw = max(cw, SIZE)
    return cw, chh


def build():
    if not os.path.exists(FONT_PATH):
        sys.exit('找不到字体：' + FONT_PATH)
    chars = collect_chars()
    font = ImageFont.truetype(FONT_PATH, SIZE)
    cw, chh = measure(font, chars)
    cols = min(EXTRA_COLS, max(1, len(chars)))
    rows = (len(chars) + cols - 1) // cols
    pitch_x, pitch_y = cw + PAD, chh + PAD
    aw, ah = cols * pitch_x, rows * pitch_y

    gray = Image.new('L', (aw, ah), 0)
    draw = ImageDraw.Draw(gray)
    table = {}
    probe = ImageDraw.Draw(Image.new('L', (8, 8)))
    for i, ch in enumerate(chars):
        col, row = i % cols, i // cols
        draw.text((col * pitch_x, row * pitch_y), ch, font=font, fill=255)
        try:
            adv = probe.textlength(ch, font=font)
        except Exception:
            adv = cw
        # 步进宽取整；汉字强制等宽（满格），西文按实际墨迹宽 + 1px 间隙
        step = cw if ord(ch) > 0x2E80 else max(2, int(adv + 1.0))
        table[ch] = [col, row, step]

    # 阀值化成 1-bit：拒绝抗锯齿的灰边，这是「像素感」的关键一步
    px = gray.load()
    alpha = bytearray(aw * ah)
    for y in range(ah):
        for x in range(aw):
            if px[x, y] >= THRESH:
                alpha[y * aw + x] = 255
    rgba = Image.new('RGBA', (aw, ah), (255, 255, 255, 0))
    rgba.putalpha(Image.frombytes('L', (aw, ah), bytes(alpha)))

    if not os.path.isdir(OUT_DIR):
        os.makedirs(OUT_DIR)
    rgba.save(OUT_PNG)

    # 字表用**并行数组**而不是 {字: [...]} 字典：Unity 的 JsonUtility 不支持字典，
    # 只能反序列化成字段，所以 chars/col/row/adv 四个等长数组是它能直接吃的形状。
    meta = {
        'size': SIZE, 'cellW': cw, 'cellH': chh,
        'pitchX': pitch_x, 'pitchY': pitch_y,
        'cols': cols, 'rows': rows, 'atlasW': aw, 'atlasH': ah,
        'chars': ''.join(g[0] for g in sorted(table.items())),
        'col': [g[1][0] for g in sorted(table.items())],
        'row': [g[1][1] for g in sorted(table.items())],
        'adv': [g[1][2] for g in sorted(table.items())],
    }
    with open(OUT_JSON, 'w', encoding='utf-8', newline='\n') as f:
        json.dump(meta, f, ensure_ascii=False, sort_keys=True, separators=(',', ':'))
    write_meta()

    print('图集 %dx%d，单元格 %dx%d（含 %dpx 留白），%d 字形' % (aw, ah, cw, chh, PAD, len(chars)))
    print('已写 %s  /  %s' % (OUT_PNG, OUT_JSON))
    preview(rgba, meta, table)
    return meta


def write_meta():
    """TextureImporter meta：Default 类型（不是 Sprite），Point 采样、不压缩、无 mipmap。

    用 Default 而不是 Sprite：PixelText 是 MaskableGraphic，直接把这张图当
    material 的 _MainTex 用，不需要经过 Sprite 资源。
    """
    tpl = '''fileFormatVersion: 2
guid: 7c4a9e13b6d84f25a0e6c7b8d1f3a5e2
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 1
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 0
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 0
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {x: 0.5, y: 0.5}
  spritePixelsToUnits: 100
  spriteBorder: {x: 0, y: 0, z: 0, w: 0}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 0
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 3
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    maxPlaceholderSize: 32
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
    bones: []
    spriteID:
    internalID: 0
    vertices: []
    indices: []
    edges: []
    weights: []
    secondaryTextures: []
    nameFileIdTable: {}
  mipmapLimitGroupName:
  pSDRemoveMatte: 0
  userData:
  assetBundleName:
  assetBundleVariant:
'''
    with open(OUT_PNG + '.meta', 'w', encoding='utf-8', newline='\n') as f:
        f.write(tpl)


def preview(atlas, meta, table):
    """把图集里真实取字拼成样张，肉眼验收像素感（不写进 Assets）。"""
    samples = ['阿宁', '小川', '老周', '石头', '小雨', '神秘嘉宾',
               '按 F 互动', '性格: 急躁  等级 3', '波形: 底鼓  任务: 未指派',
               'ABCDEFGHIJKLM 0123456789']
    scale = 3
    cw, chh = meta['cellW'], meta['cellH']
    line_h = chh + 6
    w = max(sum(table.get(c, [0, 0, cw])[2] for c in s) for s in samples) * scale + 20
    h = line_h * scale * len(samples) + 20
    out = Image.new('RGB', (w, h), (18, 20, 26))
    for i, s in enumerate(samples):
        x = 10
        y = 10 + i * line_h * scale
        for ch in s:
            g = table.get(ch)
            if g is None:
                x += 4 * scale
                continue
            col, row, step = g
            cell = atlas.crop((col * meta['pitchX'], row * meta['pitchY'],
                               col * meta['pitchX'] + cw, row * meta['pitchY'] + chh))
            cell = cell.resize((cw * scale, chh * scale), Image.NEAREST)
            out.paste(cell, (x, y), cell)
            x += step * scale
    p = os.path.join(os.path.dirname(os.path.abspath(__file__)), '_pixelfont_preview.png')
    out.save(p)
    print('样张 → %s' % p)


if __name__ == '__main__':
    build()
