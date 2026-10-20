# -*- coding: utf-8 -*-
"""
生成主角的行走贴图：3 个朝向（下 / 上 / 侧）× 4 帧，每帧 32×48。

为什么用脚本画而不是让 AI 直接出序列帧：AI 出的多帧人物，四肢位置和配色每帧
都会飘，切成动画会抖。这里用同一套「身体部件」按不同姿态参数画，帧与帧之间
只有腿和手臂在动，天然对齐。

要点（第一版踩过的坑）：
  · 腿之间必须留一列透明，描边会把这一列填深色，两条腿才分得开；
  · 裤子不能太黑，否则和描边同色，仍然糊成一块 → 用暖灰褐；
  · 手臂要画在躯干外侧、用深一档的衣服色，否则轮廓合在一起看不出手。

输出：Assets/Resources/Characters/Player/player_<dir>_<0..3>.png
导入：Point 采样、PPU 32（→ 1.0×1.5 世界单位）、不压缩。

用法：python Tools/make_player_sprite.py
"""
import os

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_DIR = os.path.join(ROOT, 'Assets', 'Resources', 'Characters', 'Player')

W, H = 32, 48

CLEAR = (0, 0, 0, 0)
OUTLINE = (43, 28, 21, 255)      # #2B1C15
SKIN = (232, 185, 142, 255)      # #E8B98E
SKIN_D = (198, 145, 104, 255)    # #C69168
HAIR = (74, 47, 34, 255)         # #4A2F22
HAIR_L = (112, 74, 52, 255)      # #704A34
SHIRT = (217, 162, 74, 255)      # #D9A24A 暖金，主角的招牌色
SHIRT_D = (170, 122, 48, 255)    # #AA7A30
PANTS = (122, 98, 76, 255)       # #7A624C 暖灰褐，能和描边分开
PANTS_D = (96, 76, 58, 255)      # #604C3A
SHOE = (58, 44, 34, 255)         # #3A2C22
EYE = (32, 21, 16, 255)
BELT = (86, 64, 46, 255)

# 竖向分区（不含描边，描边是最后向外扩 1px）
HAIR_TOP = 6
CHIN = 17
NECK = 18
TORSO_TOP = 19
TORSO_BOT = 28
LEG_TOP = 30
FOOT_BOT = 45


class Canvas(object):
    def __init__(self):
        self.px = [[CLEAR] * W for _ in range(H)]

    def put(self, x, y, c):
        if 0 <= x < W and 0 <= y < H:
            self.px[y][x] = c

    def rect(self, x0, y0, x1, y1, c):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                self.put(x, y, c)

    def ellipse(self, cx, cy, rx, ry, c):
        for y in range(cy - ry, cy + ry + 1):
            for x in range(cx - rx, cx + rx + 1):
                dx = (x - cx) / float(rx) if rx else 0.0
                dy = (y - cy) / float(ry) if ry else 0.0
                if dx * dx + dy * dy <= 1.0:
                    self.put(x, y, c)

    def outline(self, c=OUTLINE):
        """只沿外轮廓扩 1px（内部缝隙自然被同一颜色填掉）。"""
        add = []
        for y in range(H):
            for x in range(W):
                if self.px[y][x] != CLEAR:
                    continue
                for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    ny, nx = y + dy, x + dx
                    if 0 <= ny < H and 0 <= nx < W and self.px[ny][nx] != CLEAR:
                        add.append((x, y))
                        break
        for x, y in add:
            self.px[y][x] = c

    def image(self):
        im = Image.new('RGBA', (W, H), CLEAR)
        im.putdata([p for row in self.px for p in row])
        return im


def draw_head(cv, facing, bob):
    top = HAIR_TOP + bob
    chin = CHIN + bob

    if facing == 'side':
        # 侧面：后脑在左、脸朝右；头身宽度和正面保持一致，才像同一个人转过去
        mid = (top + chin) // 2
        cv.ellipse(16, mid + 1, 5, 6, SKIN)
        cv.ellipse(12, top + 4, 4, 4, HAIR)                                   # 后脑头发
        cv.rect(10, top + 4, 15, chin - 2, HAIR)
        cv.rect(11, top + 1, 19, top + 3, HAIR)                               # 头顶头发
        cv.rect(11, top + 2, 14, top + 3, HAIR_L)
        cv.put(19, mid + 1, EYE)                                              # 一只眼
        cv.put(18, mid + 4, SKIN_D)                                           # 嘴角
        cv.rect(17, chin - 2, 18, chin - 2, SKIN_D)                           # 下巴阴影
    else:
        # 正面 / 背面：同一个头型，背面把脸换成头发
        cv.ellipse(16, (top + chin) // 2 + 1, 6, 6, SKIN)
        cv.ellipse(16, top + 4, 6, 3, HAIR)
        cv.rect(10, top + 4, 21, top + 6, HAIR)                               # 头发主体
        cv.rect(11, top + 2, 17, top + 3, HAIR_L)                             # 发顶高光
        if facing == 'down':
            cv.rect(10, top + 7, 12, top + 8, HAIR)                           # 左侧刘海
            cv.rect(19, top + 7, 21, top + 7, HAIR)                           # 右侧刘海
            cv.rect(12, (top + chin) // 2 + 1, 13, (top + chin) // 2 + 2, EYE)
            cv.rect(18, (top + chin) // 2 + 1, 19, (top + chin) // 2 + 2, EYE)
            cv.put(15, chin - 2, SKIN_D)                                      # 嘴
        else:
            cv.rect(10, top + 6, 21, chin - 1, HAIR)                          # 后脑全是头发
            cv.rect(12, top + 3, 19, top + 6, HAIR_L)

    # 脖子
    cv.rect(14, chin + 1, 17, NECK + bob + 1, SKIN_D)


def draw_torso(cv, facing, bob):
    t = TORSO_TOP + bob
    b = TORSO_BOT + bob
    # 三个朝向共用同一套躯干轮廓 —— 侧向只靠「头转过去 + 腿前后错位」表达，
    # 这样三个角度看起来是同一个人，而不是三种不同的体型。
    cv.rect(11, t, 20, b, SHIRT)
    cv.rect(11, t, 12, b, SHIRT_D)          # 左侧阴影
    cv.rect(19, t, 20, b, SHIRT_D)          # 右侧阴影
    cv.rect(11, b, 20, b + 1, BELT)         # 腰带
    if facing == 'down':
        cv.rect(15, t + 1, 16, t + 6, SHIRT_D)   # 前襟
    elif facing == 'side':
        cv.rect(14, t + 1, 17, t + 7, SHIRT_D)   # 侧身时衣服的走向


def draw_arms(cv, facing, phase, bob):
    """
    手臂摆在躯干外侧，中间留一列透明——描边会把它填深色，手臂才是一截独立的肢体。
    （第一版把手臂贴着躯干画，轮廓连成一片，等于没有手。）
    """
    t = TORSO_TOP + bob + 1
    b = TORSO_BOT + bob + 1
    swing = (-1, 0, 1, 0)[phase]     # -1 前 / +1 后
    cv.rect(8, t + max(0, swing), 9, b - 1 + min(0, swing), SHIRT_D)
    cv.rect(22, t + max(0, -swing), 23, b - 1 + min(0, -swing), SHIRT_D)
    cv.rect(8, b + min(0, swing), 9, b + min(0, swing), SKIN)
    cv.rect(22, b + min(0, -swing), 23, b + min(0, -swing), SKIN)


def draw_legs(cv, facing, phase, bob):
    """
    4 帧：左腿前 / 并拢 / 右腿前 / 并拢。
    两腿之间留一列透明，描边会把它填成深色，腿才分得开。
    """
    t = LEG_TOP + bob
    b = FOOT_BOT + bob
    lead = (-1, 0, 1, 0)[phase]

    if facing == 'side':
        # 侧面：后腿深、前腿浅，靠前后错位读出走路
        back = 12 - lead
        front = 16 + lead
        cv.rect(back, t, back + 3, b - 1, PANTS_D)
        cv.rect(back - 1, b - 1, back + 3, b, SHOE)
        cv.rect(front, t, front + 3, b - 2 - max(0, lead), PANTS)
        cv.rect(front - 1, b - 1, front + 4, b - max(0, lead), SHOE)
        return

    lx, rx = 12, 17
    # 4 帧各自不同：左前 / 抬左腿 / 右前 / 抬右腿
    lift_l = (-1 if lead > 0 else 0) if phase % 2 == 0 else -1
    lift_r = (-1 if lead < 0 else 0) if phase % 2 == 0 else 0
    cv.rect(lx, t, lx + 3, b - 2 + lift_l, PANTS)
    cv.rect(lx - 1, b - 1 + lift_l, lx + 3, b + lift_l, SHOE)
    cv.rect(rx, t, rx + 3, b - 2 + lift_r, PANTS_D)
    cv.rect(rx, b - 1 + lift_r, rx + 4, b + lift_r, SHOE)


def render(facing, phase):
    bob = -1 if phase in (1, 3) else 0     # 中间两帧整体上移 1px，做出起伏
    cv = Canvas()
    draw_legs(cv, facing, phase, bob)
    draw_torso(cv, facing, bob)
    draw_arms(cv, facing, phase, bob)
    draw_head(cv, facing, bob)
    cv.outline()
    return cv.image()


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    for facing in ('down', 'up', 'side'):
        for phase in range(4):
            render(facing, phase).save(
                os.path.join(OUT_DIR, 'player_%s_%d.png' % (facing, phase)))
    print('已写 12 帧 → %s' % OUT_DIR)

    scale = 6
    sheet = Image.new('RGBA', (W * 4 * scale + 40, H * 3 * scale + 40), (58, 42, 31, 255))
    for r, facing in enumerate(('down', 'up', 'side')):
        for c in range(4):
            im = Image.open(os.path.join(OUT_DIR, 'player_%s_%d.png' % (facing, c)))
            im = im.resize((W * scale, H * scale), Image.NEAREST)
            sheet.alpha_composite(im, (10 + c * W * scale, 10 + r * H * scale))
    sheet.save(os.path.join(ROOT, 'Tools', '_playersprite_preview.png'))
    print('样张 → Tools/_playersprite_preview.png')


if __name__ == '__main__':
    main()
