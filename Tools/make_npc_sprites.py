# -*- coding: utf-8 -*-
"""
生成排练室里 6 个 NPC 的俯视像素小人：32×52，PPU 32，轴心在脚底。

造型按各自的立绘来：发型、上衣颜色、下装、鞋都对着立绘取色，
但不带立绘里的音波背景，只留人物本身。

输出：Assets/Resources/Characters/Npc/npc_<key>.png
用法：python Tools/make_npc_sprites.py
"""
import os
import sys

from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from make_player_sprite import Canvas  # 同一个 32×52 画布与描边逻辑

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_DIR = os.path.join(ROOT, 'Assets', 'Resources', 'Characters', 'Npc')

W, H = 32, 52
CLEAR = (0, 0, 0, 0)
OUTLINE = (43, 28, 21, 255)
EYE = (32, 21, 16, 255)
EYE_HI = (248, 240, 228, 255)
BLUSH = (222, 156, 134, 255)

HEAD_TOP = 4
CHIN = 21


def dark(c, k=0.72):
    return (int(c[0] * k), int(c[1] * k), int(c[2] * k), 255)


def light(c, k=0.18):
    return (int(c[0] + (255 - c[0]) * k),
            int(c[1] + (255 - c[1]) * k),
            int(c[2] + (255 - c[2]) * k), 255)


def draw_head(cv, c, rise=0):
    top, chin = HEAD_TOP - rise, CHIN - rise
    hair, skin, skin_d = c['hair'], c['skin'], c['skin_d']
    hair_l = light(hair, 0.10)
    style = c['hair_style']

    if style == 'hood':
        hood = c['hood']
        cv.ellipse(16, top + 7, 8, 8, hood)                 # 兜帽外轮廓
        cv.rect(8, top + 6, 23, chin + 3, hood)
        cv.ellipse(16, top + 10, 5, 6, dark(hood, 0.55))    # 帽子里的阴影
        cv.ellipse(16, top + 15, 4, 3, skin)                # 只露下半张脸
        cv.rect(13, top + 15, 18, chin, skin)
        cv.rect(13, top + 6, 18, top + 9, hair)             # 额前的头发
        cv.put(13, top + 12, EYE_HI)                        # 阴影里亮着的眼睛
        cv.put(19, top + 12, EYE_HI)
        cv.rect(15, chin - 1, 16, chin - 1, skin_d)         # 嘴
        cv.rect(13, chin + 1, 18, chin + 2, dark(hood, 0.45))  # 立起来的领子
        return

    # 头发外轮廓（所有非兜帽共用）
    cv.ellipse(16, top + 6, 8, 7, hair)
    cv.put(16, top - 1, CLEAR)                              # 削掉顶上的单颗像素

    if style == 'long':
        cv.rect(8, top + 5, 10, chin + 3, hair)
        cv.rect(21, top + 5, 23, chin + 3, hair)
    elif style == 'bob':
        cv.rect(8, top + 5, 10, chin + 1, hair)
        cv.rect(21, top + 5, 23, chin + 1, hair)
    else:                                                   # short / spiky / grey
        cv.rect(9, top + 5, 10, top + 12, hair)
        cv.rect(21, top + 5, 22, top + 12, hair)

    cv.ellipse(16, top + 11, 5, 5, skin)
    cv.rect(12, top + 15, 19, chin, skin)

    # 刘海
    if style == 'spiky':
        cv.rect(10, top + 3, 21, top + 5, hair)
        for x, y in ((10, top + 1), (12, top), (15, top), (18, top), (21, top + 1)):
            cv.rect(x, y, x, top + 2, hair)
        cv.rect(9, top + 2, 10, top + 3, hair)
        cv.rect(21, top + 2, 22, top + 3, hair)
        cv.rect(10, top + 6, 12, top + 8, hair)
        cv.rect(19, top + 6, 21, top + 8, hair)
    elif style == 'grey':
        # 发际线偏高、两侧留短鬓角：像个上了年纪的人
        cv.rect(10, top + 3, 21, top + 4, hair)
        cv.rect(10, top + 5, 11, top + 7, hair)
        cv.rect(20, top + 5, 21, top + 7, hair)
        cv.rect(15, top + 5, 16, top + 6, hair)             # 中间一小撮
    elif style == 'short':
        cv.rect(10, top + 3, 21, top + 5, hair)
        cv.rect(10, top + 6, 12, top + 8, hair)
        cv.rect(15, top + 6, 21, top + 7, hair)             # 侧分的一缕
    else:                                                   # bob / long
        cv.rect(10, top + 3, 21, top + 5, hair)
        cv.rect(10, top + 6, 12, top + 7, hair)
        cv.rect(19, top + 6, 21, top + 7, hair)

    cv.rect(13, top + 1, 17, top + 2, hair_l)               # 头顶受光
    cv.rect(12, top + 9, 13, top + 11, EYE)                 # 左眼 2×3
    cv.rect(18, top + 9, 19, top + 11, EYE)                 # 右眼 2×3
    cv.put(12, top + 9, EYE_HI)
    cv.put(18, top + 9, EYE_HI)

    if style == 'grey':
        cv.rect(12, top + 8, 13, top + 8, dark(hair, 0.8))  # 眉毛
        cv.rect(18, top + 8, 19, top + 8, dark(hair, 0.8))

    cv.rect(15, top + 14, 16, top + 14, skin_d)             # 嘴
    if c.get('blush'):
        cv.put(12, top + 13, BLUSH)
        cv.put(19, top + 13, BLUSH)

    cv.rect(15, chin + 1, 16, CHIN + 1, skin_d)             # 脖子（底端固定，吸气时变长）


def draw_arms(cv, c, rise=0):
    """手臂统一画到 33 行（大腿根），比原来多两格，上身看起来才不短。"""
    shirt = c['top']
    shirt_d = c['top_d']
    skin = c['skin']
    left = c.get('wide', False) and 7 or 8
    right = c.get('wide', False) and 24 or 23
    cv.rect(left, 23 - rise, left + 1, 32, shirt_d)
    cv.rect(right - 1, 23 - rise, right, 32, shirt_d)
    cv.rect(left, 33, left + 1, 33, skin)
    cv.rect(right - 1, 33, right, 33, skin)


def draw_torso(cv, c, rise=0):
    """上衣：按 style 画毛衣 / 连帽衫 / 夹克 / 衬衫。"""
    t = 22 - rise                                            # 肩线上移、腰线不动 → 胸腔变高
    wide = c.get('wide', False)
    x0, x1 = (10, 21) if wide else (11, 20)
    b = 31 if c['bottom_style'] == 'pants' else 30

    shirt, shirt_d = c['top'], c['top_d']
    style = c['top_style']

    cv.rect(x0, t, x1, b, shirt)
    cv.rect(x0, t, x0 + 1, b, shirt_d)                      # 左暗边
    cv.rect(x1 - 1, t, x1, b, shirt_d)                      # 右暗边

    if style == 'jacket':
        inner = c['inner']
        cv.rect(15, t, 16, b, inner)                        # 敞开的内搭（窄一点，外套占主体）
        cv.rect(13, t, 13, b, shirt)                        # 衣襟
        cv.rect(18, t, 18, b, shirt)
        cv.rect(13, t, 18, t, shirt_d)                      # 领口
        cv.rect(11, t, 12, t + 2, shirt)                    # 肩头
        cv.rect(19, t, 20, t + 2, shirt)
    elif style == 'hoodie':
        cv.rect(x0, t, x1, t + 1, shirt_d)                  # 帽子堆在肩上
        cv.rect(12, b - 3, 19, b - 2, shirt_d)              # 口袋
        cv.rect(14, t + 2, 14, t + 4, light(shirt, 0.5))    # 抽绳
        cv.rect(17, t + 2, 17, t + 4, light(shirt, 0.5))
    elif style == 'blouse':
        cv.rect(15, t + 1, 16, b - 1, shirt_d)              # 前襟
        if c.get('scarf'):
            sc = c['scarf']
            cv.rect(11, t, 20, t + 1, sc)                   # 围巾
            cv.rect(13, t + 2, 18, t + 2, dark(sc))
    elif style == 'coat':
        coat_b = 35 if c.get('long_coat') else b
        cv.rect(x0, t, x1, coat_b, shirt)
        cv.rect(14, t, 17, coat_b, c['inner'])              # 敞开的外套
        cv.rect(13, t, 13, coat_b, shirt_d)
        cv.rect(18, t, 18, coat_b, shirt_d)
        cv.rect(x0, coat_b - 1, x1, coat_b, shirt_d)        # 下摆
    else:                                                   # sweater
        cv.rect(x0, b, x1, b, shirt_d)                      # 罗纹下摆
        if wide:
            # 抱臂：两道暗缝交错，配两只手，读成交叉的前臂
            cv.rect(12, t + 3, 18, t + 3, shirt_d)
            cv.rect(14, t + 4, 20, t + 4, shirt_d)
            cv.put(12, t + 3, c['skin'])                    # 左手
            cv.put(20, t + 4, c['skin_d'])                  # 右手

def draw_lower(cv, c):
    if c['bottom_style'] == 'skirt':
        draw_skirt(cv, c)
        return

    pants, pants_d = c['bottom'], c['bottom_d']
    shoe, shoe_d = c['shoe'], c['shoe_d']

    top = 36 if c.get('long_coat') else 32
    cv.rect(12, top, 14, 45, pants)                         # 左裤腿
    cv.rect(17, top, 19, 45, pants_d)                       # 右裤腿
    cv.rect(12, top, 12, 45, light(pants, 0.14))            # 左裤受光边，两条腿才不糊成一坨
    cv.rect(17, top, 17, 45, light(pants_d, 0.12))

    for x0, col in ((12, shoe), (17, shoe)):
        cv.rect(x0 - 1, 46, x0 + 2, 49, col)                # 鞋面
        cv.rect(x0 - 1, 50, x0 + 2, 50, shoe_d)             # 平鞋底
        if c['shoe_style'] == 'sneaker':
            cv.rect(x0, 47, x0 + 1, 47, light(col, 0.35))   # 鞋带高光


def draw_skirt(cv, c):
    skirt, skirt_d = c['bottom'], c['bottom_d']
    skin, skin_d = c['skin'], c['skin_d']
    boot, boot_d = c['shoe'], c['shoe_d']

    for i, y in enumerate(range(31, 37)):
        inset = 1 if i < 2 else 0
        x0, x1 = 11 - inset, 20 + inset
        cv.rect(x0, y, x1, y, skirt)
        for x in range(x0, x1 + 1):
            if (x - x0) % 3 == 2:
                cv.put(x, y, skirt_d)
    cv.rect(11, 31, 20, 31, skirt_d)
    cv.rect(10, 36, 21, 36, skirt_d)

    cv.rect(12, 37, 14, 43, skin)                           # 左腿
    cv.rect(17, 37, 19, 43, skin_d)                         # 右腿
    for x0, s in ((12, True), (17, False)):
        cv.rect(x0 - 1, 44, x0 + 2, 49, boot)               # 靴筒
        cv.rect(x0 - 1, 44, x0 + 2, 44, dark(boot, 0.8))
        cv.rect(x0 - 1, 50, x0 + 2, 50, boot_d)             # 平鞋底


NPC = [
    dict(key='aning', name='阿宁',
         skin=(242, 198, 166), skin_d=(208, 160, 128),
         hair=(30, 28, 38), hair_style='bob',
         top=(47, 123, 219), top_d=(32, 92, 170), top_style='sweater',
         bottom=(72, 72, 86), bottom_d=(54, 54, 66), bottom_style='pants',
         shoe=(238, 238, 236), shoe_d=(74, 74, 84), shoe_style='sneaker',
         blush=True),
    dict(key='xiaochuan', name='小川',
         skin=(226, 170, 122), skin_d=(192, 138, 96),
         hair=(104, 72, 48), hair_style='short',
         top=(126, 219, 168), top_d=(92, 186, 136), top_style='hoodie',
         bottom=(64, 78, 112), bottom_d=(48, 60, 88), bottom_style='pants',
         shoe=(236, 236, 234), shoe_d=(58, 58, 66), shoe_style='sneaker'),
    dict(key='laozhou', name='老周',
         skin=(228, 178, 140), skin_d=(192, 142, 108),
         hair=(186, 182, 178), hair_style='grey',
         top=(86, 66, 98), top_d=(62, 46, 72), top_style='sweater', wide=True,
         bottom=(64, 56, 72), bottom_d=(46, 40, 52), bottom_style='pants',
         shoe=(52, 42, 52), shoe_d=(34, 28, 36), shoe_style='boot'),
    dict(key='shitou', name='石头',
         skin=(236, 186, 142), skin_d=(202, 150, 108),
         hair=(24, 20, 24), hair_style='spiky',
         top=(216, 74, 46), top_d=(174, 52, 34), top_style='jacket',
         inner=(32, 32, 38),
         bottom=(70, 88, 120), bottom_d=(54, 68, 94), bottom_style='pants',
         shoe=(214, 58, 50), shoe_d=(150, 38, 34), shoe_style='sneaker'),
    dict(key='xiaoyu', name='小雨',
         skin=(240, 196, 164), skin_d=(206, 158, 128),
         hair=(122, 66, 50), hair_style='long',
         top=(242, 230, 216), top_d=(210, 194, 176), top_style='blouse',
         scarf=(146, 216, 176),
         bottom=(74, 68, 88), bottom_d=(56, 52, 70), bottom_style='skirt',
         shoe=(58, 46, 50), shoe_d=(38, 30, 34), shoe_style='boot',
         blush=True),
    dict(key='guest', name='神秘嘉宾',
         skin=(214, 170, 138), skin_d=(178, 136, 108),
         hair=(18, 18, 24), hair_style='hood',
         hood=(46, 82, 158),
         top=(52, 92, 176), top_d=(38, 68, 138), top_style='coat',
         inner=(28, 28, 36),
         bottom=(40, 40, 50), bottom_d=(30, 30, 38), bottom_style='pants',
         shoe=(26, 24, 30), shoe_d=(18, 16, 20), shoe_style='boot', long_coat=True),
]


def render(c, rise=0):
    cv = Canvas()
    draw_lower(cv, c)
    draw_torso(cv, c, rise)
    draw_arms(cv, c, rise)
    draw_head(cv, c, rise)
    cv.outline(OUTLINE)
    return cv.image()


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    for c in NPC:
        render(c).save(os.path.join(OUT_DIR, 'npc_%s.png' % c['key']))
        render(c, rise=1).save(os.path.join(OUT_DIR, 'npc_%s_breath.png' % c['key']))
    print('已写 %d 个 NPC（站姿+吸气帧，共 %d 张，32x%d）→ %s'
          % (len(NPC), len(NPC) * 2, H, OUT_DIR))

    scale = 6
    sheet = Image.new('RGBA', (W * 3 * scale + 40, H * 2 * scale + 30), (58, 42, 31, 255))
    for i, c in enumerate(NPC):
        im = Image.open(os.path.join(OUT_DIR, 'npc_%s.png' % c['key']))
        im = im.resize((W * scale, H * scale), Image.NEAREST)
        sheet.alpha_composite(im, (10 + (i % 3) * W * scale, 10 + (i // 3) * H * scale))
    sheet.save(os.path.join(ROOT, 'Tools', '_npcsprites_preview.png'))
    print('样张 → Tools/_npcsprites_preview.png')

    # 呼吸两帧并排，方便看胸腔起伏
    breath = Image.new('RGBA', (W * 4 * scale + 50, H * 3 * scale + 40), (58, 42, 31, 255))
    for i, c in enumerate(NPC):
        col, row = i % 2, i // 2
        for k in range(2):
            name = 'npc_%s%s.png' % (c['key'], '' if k == 0 else '_breath')
            im = Image.open(os.path.join(OUT_DIR, name)).resize((W * scale, H * scale), Image.NEAREST)
            breath.alpha_composite(im, (10 + (col * 2 + k) * W * scale, 10 + row * H * scale))
    breath.save(os.path.join(ROOT, 'Tools', '_npcbreath_preview.png'))
    print('呼吸样张 → Tools/_npcbreath_preview.png')


if __name__ == '__main__':
    main()
