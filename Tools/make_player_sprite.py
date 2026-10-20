# -*- coding: utf-8 -*-
"""
生成主角（女生）的行走贴图：3 个朝向（下 / 上 / 侧）× 4 帧，每帧 32×52。

为什么用脚本画而不是让 AI 直接出序列帧：AI 出的多帧人物，四肢位置和配色每帧
都会飘，切成动画会抖。这里用同一套「身体部件」按不同姿态参数画，帧与帧之间
只有腿和手臂在动，天然对齐。

要点（踩过的坑）：
  · 腿之间必须留出透明列，描边会把这一列填深色，两条腿才分得开；
  · 手臂要画在躯干外侧、中间留一列透明，否则轮廓连成一片，等于没有手；
  · 三个朝向共用同一套头身轮廓，只靠「头转过去 + 腿前后错位」表达朝向，
    这样三个角度看起来是同一个人，而不是三种体型。

尺寸：32×52 @ PPU 32 → 1.0×1.625 世界单位（比 NPC 的 1.54 略高一点）。
输出：Assets/Resources/Characters/Player/player_<dir>_<0..3>.png
导入：Point 采样、PPU 32、轴心 BottomCenter、不压缩。

用法：python Tools/make_player_sprite.py
"""
import os

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_DIR = os.path.join(ROOT, 'Assets', 'Resources', 'Characters', 'Player')

W, H = 32, 52

CLEAR = (0, 0, 0, 0)
OUTLINE = (43, 28, 21, 255)      # #2B1C15
SKIN = (237, 193, 155, 255)      # #EDC19B
SKIN_D = (201, 150, 113, 255)    # #C99671
HAIR = (74, 47, 34, 255)         # #4A2F22
HAIR_L = (118, 78, 55, 255)      # #764E37
SHIRT = (240, 230, 216, 255)     # #F0E6D8 奶白衬衫
SHIRT_D = (211, 195, 174, 255)   # #D3C3AE
SKIRT = (176, 82, 60, 255)       # #B0523C 砖红半身裙
SKIRT_D = (142, 64, 48, 255)     # #8E4030
SHOE = (58, 44, 34, 255)         # #3A2C22
EYE = (32, 21, 16, 255)
BLUSH = (222, 156, 134, 255)     # #DE9C86

HAIR_TOP = 5
CHIN = 17
NECK = 18
TORSO_TOP = 19
TORSO_BOT = 29
SKIRT_TOP = 30
SKIRT_BOT = 36
LEG_TOP = 37
FOOT_BOT = 50


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
        """只沿外轮廓扩 1px（内部缝隙会被同一颜色填掉，正好当分界线）。"""
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
    """头：长发、收窄的下巴、2×2 的眼睛 + 一点点腮红。"""
    top = HAIR_TOP + bob
    chin = CHIN + bob
    mid = (top + chin) // 2

    if facing == 'side':
        cv.ellipse(16, mid + 1, 5, 6, SKIN)
        cv.ellipse(12, top + 4, 4, 4, HAIR)                 # 后脑
        cv.rect(9, top + 4, 14, chin + 5, HAIR)             # 长发垂到肩下
        cv.rect(11, top + 1, 19, top + 3, HAIR)             # 头顶
        cv.rect(11, top + 2, 14, top + 3, HAIR_L)           # 高光
        cv.rect(18, mid, 19, mid + 1, EYE)                  # 一只眼
        cv.put(19, mid + 3, BLUSH)                          # 腮红
        cv.put(18, chin - 2, SKIN_D)                        # 嘴
    else:
        cv.ellipse(16, mid + 2, 5, 5, SKIN)                 # 脸收窄
        cv.ellipse(16, top + 3, 5, 3, HAIR)     # 往上挪一格，别在眼睛那一行留一颗头发
        cv.rect(11, top + 4, 20, top + 6, HAIR)
        cv.rect(12, top + 2, 17, top + 3, HAIR_L)

        if facing == 'down':
            cv.rect(10, top + 6, 11, chin + 5, HAIR)        # 左侧长发
            cv.rect(20, top + 6, 21, chin + 5, HAIR)        # 右侧长发
            cv.put(11, top + 7, HAIR)                       # 刘海只垂一格，不压到眼睛
            cv.rect(13, chin - 5, 14, chin - 4, EYE)        # 左眼
            cv.rect(17, chin - 5, 18, chin - 4, EYE)        # 右眼
            cv.rect(15, chin - 2, 16, chin - 2, SKIN_D)     # 嘴
            cv.put(12, chin - 3, BLUSH)                     # 腮红
            cv.put(19, chin - 3, BLUSH)
        else:
            cv.rect(10, top + 4, 21, chin + 6, HAIR)        # 背面整片长发
            cv.rect(13, top + 7, 18, chin + 5, HAIR_L)      # 中间纵向亮带
            cv.rect(11, top + 2, 20, top + 4, HAIR)         # 头顶
            cv.rect(12, top + 3, 17, top + 3, HAIR_L)

    cv.rect(14, chin + 1, 17, NECK + bob + 1, SKIN_D)       # 脖子


def draw_torso(cv, facing, bob):
    t = TORSO_TOP + bob
    b = TORSO_BOT + bob
    cv.rect(11, t, 20, t + 4, SHIRT)                        # 肩
    cv.rect(12, t + 5, 19, b, SHIRT)                        # 收腰
    cv.rect(11, t, 12, t + 4, SHIRT_D)
    cv.rect(19, t, 20, t + 4, SHIRT_D)
    cv.rect(12, t + 5, 13, b, SHIRT_D)
    cv.rect(18, t + 5, 19, b, SHIRT_D)
    if facing == 'down':
        cv.rect(15, t + 1, 16, t + 5, SHIRT_D)              # 前襟


def draw_skirt(cv, facing, bob):
    t = SKIRT_TOP + bob
    b = SKIRT_BOT + bob
    for i, y in enumerate(range(t, b + 1)):
        inset = 1 if i < 2 else 0                           # 腰窄摆宽
        cv.rect(11 - inset, y, 20 + inset, y, SKIRT)
    cv.rect(11, t, 20, t, SKIRT_D)                          # 腰头
    cv.rect(10, b, 21, b, SKIRT_D)                          # 裙摆压边


def draw_arms(cv, facing, phase, bob):
    """手臂在躯干外侧，中间留一列透明——描边填上后手臂才是一截独立肢体。"""
    t = TORSO_TOP + bob + 1
    b = TORSO_BOT + bob + 1
    swing = (-1, 0, 1, 0)[phase]                            # -1 前 / +1 后
    cv.rect(8, t + max(0, swing), 9, b - 1 + min(0, swing), SHIRT_D)
    cv.rect(22, t + max(0, -swing), 23, b - 1 + min(0, -swing), SHIRT_D)
    cv.rect(8, b + min(0, swing), 9, b + min(0, swing), SKIN)
    cv.rect(22, b + min(0, -swing), 23, b + min(0, -swing), SKIN)


def draw_legs(cv, facing, phase, bob):
    """4 帧：左腿前 / 抬左腿 / 右腿前 / 抬右腿。"""
    t = LEG_TOP + bob
    b = FOOT_BOT + bob
    lead = (-1, 0, 1, 0)[phase]

    if facing == 'side':
        back = 13 - lead
        front = 16 + lead
        cv.rect(back, t, back + 2, b - 1, SKIN_D)
        cv.rect(back - 1, b - 1, back + 2, b, SHOE)
        cv.rect(front, t, front + 2, b - 2 - max(0, lead), SKIN)
        cv.rect(front - 1, b - 1, front + 3, b - max(0, lead), SHOE)
        return

    lx, rx = 12, 17
    lift_l = (-1 if lead > 0 else 0) if phase % 2 == 0 else -1
    lift_r = (-1 if lead < 0 else 0) if phase % 2 == 0 else 0
    cv.rect(lx, t, lx + 2, b - 2 + lift_l, SKIN)
    cv.rect(lx - 1, b - 1 + lift_l, lx + 2, b + lift_l, SHOE)
    cv.rect(rx, t, rx + 2, b - 2 + lift_r, SKIN_D)
    cv.rect(rx - 1, b - 1 + lift_r, rx + 3, b + lift_r, SHOE)


def render(facing, phase):
    bob = -1 if phase in (1, 3) else 0     # 中间两帧整体上移 1px，做出起伏
    cv = Canvas()
    draw_legs(cv, facing, phase, bob)
    draw_skirt(cv, facing, bob)
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
    print('已写 12 帧（32x%d）→ %s' % (H, OUT_DIR))

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
