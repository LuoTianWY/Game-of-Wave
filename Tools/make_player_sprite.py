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
输出：Assets/Resources/Characters/Player/player_<dir>_<0..3>.png      行走 4 帧
      Assets/Resources/Characters/Player/player_<dir>_idle_<0..1>.png  站立呼吸 2 帧
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
HAIR_M = (96, 63, 45, 255)       # #603F2D 中间调，做受光面，比 HAIR_L 柔和
HAIR_L = (118, 78, 55, 255)      # #764E37
SHIRT = (240, 230, 216, 255)     # #F0E6D8 奶白衬衫
SHIRT_D = (211, 195, 174, 255)   # #D3C3AE
SKIRT = (62, 91, 76, 255)        # #3E5B4C 墨绿，和房间的薄荷绿腰线同族
SKIRT_D = (46, 71, 57, 255)      # #2E4739
SKIRT_L = (80, 112, 95, 255)     # #50705F 百褶受光面
BOOT = (92, 62, 46, 255)         # #5C3E2E 短靴皮面（比描边亮，才分得开）
BOOT_D = (68, 44, 32, 255)       # #442C20 靴子暗部
BOOT_HI = (128, 90, 64, 255)     # #805A40 靴口翻边 / 鞋头高光
BOOT_SOLE = (150, 116, 84, 255)  # #967454 鞋底与跟（暖褐，别和描边同色）
EYE = (32, 21, 16, 255)
EYE_HI = (248, 240, 228, 255)    # 眼里的高光点，大头娃娃的「活气」全靠这一格
BLUSH = (222, 156, 134, 255)     # #DE9C86

# 大头娃娃比例（GBA 宝可梦那种）：头占身高约 38%，身子短、腿短。
HEAD_TOP = 4
CHIN = 21
NECK = 22
TORSO_TOP = 22
TORSO_BOT = 30
SKIRT_TOP = 31
SKIRT_BOT = 36
LEG_TOP = 37
BOOT_TOP = 44
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


def draw_head(cv, facing, bob, rise=0):
    """大头：头宽几乎盖过肩膀，眼睛用 2×3 的色块 + 一格高光，靠色块读表情而不是描细节。"""
    top = HEAD_TOP + bob - rise
    chin = CHIN + bob - rise

    if facing == 'side':
        cv.ellipse(12, top + 8, 7, 8, HAIR)                 # 后脑一整块
        cv.ellipse(17, top + 7, 4, 6, HAIR)                 # 头顶前半，别让头顶变成方角
        cv.rect(6, top + 5, 11, chin + 2, HAIR)             # 长发垂到肩
        cv.ellipse(17, top + 12, 4, 5, SKIN)                # 脸（侧脸窄一点）
        cv.rect(13, top + 5, 20, top + 6, HAIR)             # 刘海压到眉上
        cv.rect(13, top + 7, 18, top + 8, HAIR)             # 斜刘海，盖住一块额头
        cv.rect(13, top + 9, 15, top + 11, HAIR)            # 前面的长鬓角
        cv.rect(10, top + 1, 15, top + 2, HAIR_M)           # 头顶高光
        cv.rect(19, top + 10, 20, top + 12, EYE)            # 一只大眼
        cv.put(19, top + 10, EYE_HI)
        cv.put(18, chin - 4, BLUSH)                         # 腮红
        cv.put(20, chin - 3, SKIN_D)                        # 嘴
    else:
        if facing == 'down':
            cv.ellipse(16, top + 6, 8, 7, HAIR)             # 头发外轮廓
            cv.put(16, top - 1, CLEAR)                      # 削掉椭圆顶上的单颗像素，不然像天线
            cv.rect(8, top + 5, 10, chin + 3, HAIR)         # 左侧长发
            cv.rect(21, top + 5, 23, chin + 3, HAIR)        # 右侧长发
            cv.ellipse(16, top + 11, 5, 5, SKIN)            # 脸
            cv.rect(12, top + 15, 19, chin, SKIN)           # 下巴补方，别收成尖下巴
            cv.rect(10, top + 3, 21, top + 6, HAIR)         # 刘海横条（压住额头）
            cv.rect(10, top + 6, 12, top + 7, HAIR)         # 左鬓角
            cv.rect(19, top + 6, 21, top + 7, HAIR)         # 右鬓角
            cv.rect(13, top + 1, 17, top + 2, HAIR_M)       # 头顶高光
            cv.rect(12, top + 9, 13, top + 11, EYE)         # 左眼 2×3
            cv.rect(18, top + 9, 19, top + 11, EYE)         # 右眼 2×3
            cv.put(12, top + 9, EYE_HI)
            cv.put(18, top + 9, EYE_HI)
            cv.rect(15, top + 14, 16, top + 14, SKIN_D)     # 嘴
            cv.put(12, top + 13, BLUSH)
            cv.put(19, top + 13, BLUSH)
        else:
            cv.ellipse(16, top + 6, 8, 7, HAIR)             # 背面整片头发
            cv.put(16, top - 1, CLEAR)                      # 同样削掉顶上那一颗
            cv.rect(8, top + 5, 23, chin + 3, HAIR)         # 长发盖住脖子
            cv.rect(13, top + 2, 18, top + 3, HAIR_M)       # 头顶受光，跟着头的弧度走

    cv.rect(15, chin + 1, 16, NECK + bob + 1, SKIN_D)       # 脖子（底端固定，吸气时变长）


def draw_torso(cv, facing, bob, rise=0):
    t = TORSO_TOP + bob - rise                              # 肩线上移，腰线不动 → 胸腔变高
    b = TORSO_BOT + bob
    cv.rect(11, t, 20, t + 3, SHIRT)                        # 肩
    cv.rect(12, t + 4, 19, b, SHIRT)                        # 收腰
    cv.rect(11, t, 12, t + 3, SHIRT_D)
    cv.rect(19, t, 20, t + 3, SHIRT_D)
    cv.rect(12, t + 4, 13, b, SHIRT_D)
    cv.rect(18, t + 4, 19, b, SHIRT_D)
    if facing == 'down':
        cv.rect(15, t + 1, 16, t + 4, SHIRT_D)              # 前襟


def draw_skirt(cv, facing, bob):
    t = SKIRT_TOP + bob
    b = SKIRT_BOT + bob
    for i, y in enumerate(range(t, b + 1)):
        inset = 1 if i < 2 else 0                           # 腰窄摆宽
        x0, x1 = 11 - inset, 20 + inset
        cv.rect(x0, y, x1, y, SKIRT)
        # 百褶：两格受光 + 一格暗褶，随裙摆一起变宽
        for x in range(x0, x1 + 1):
            if (x - x0) % 3 == 2:
                cv.put(x, y, SKIRT_D)
        cv.put(x0, y, SKIRT_L)                              # 左侧受光边
    cv.rect(11, t, 20, t, SKIRT_D)                          # 腰头
    cv.rect(10, b, 21, b, SKIRT_D)                          # 裙摆压边


def draw_arms(cv, facing, phase, bob, rise=0):
    """手臂在躯干外侧，中间留一列透明——描边填上后手臂才是一截独立肢体。"""
    t = TORSO_TOP + bob + 1 - rise
    b = TORSO_BOT + bob + 3                                 # 手垂到大腿根，上身才不显短
    swing = (-1, 0, 1, 0)[phase]                            # -1 前 / +1 后
    cv.rect(8, t + max(0, swing), 9, b - 1 + min(0, swing), SHIRT_D)
    cv.rect(22, t + max(0, -swing), 23, b - 1 + min(0, -swing), SHIRT_D)
    cv.rect(8, b + min(0, swing), 9, b + min(0, swing), SKIN)
    cv.rect(22, b + min(0, -swing), 23, b + min(0, -swing), SKIN)


def draw_legs(cv, facing, phase, bob):
    """4 帧行走：接触（迈步）/ 过渡（抬脚）/ 反向接触 / 反向过渡。

    靴子做成「高跟踝靴」：翻边靴口 + 靴筒 + 鞋底 + 中心一格细跟。
    鞋底比靴筒外扩一格，脚才有脚感，不会像两根直筒。
    """
    t = LEG_TOP + bob
    g = FOOT_BOT + bob

    if facing == 'side':
        # 侧身：迈步帧前后分开，过渡帧收在身体下方，两腿之间始终留空列
        stride = phase in (0, 2)
        back = 12 if stride else 13
        front = 18 if stride else 17
        lift_back = -1 if phase == 3 else 0
        lift_front = -1 if phase == 1 else 0
        bt = BOOT_TOP + bob
        cv.rect(back, t, back + 2, bt + lift_back - 1, SKIN_D)
        draw_boot_side(cv, back, bt + lift_back, g - 1 + lift_back)
        cv.rect(front, t, front + 2, bt + lift_front - 1, SKIN)
        draw_boot_side(cv, front, bt + lift_front, g + lift_front)
        return

    lx, rx = 12, 17
    lift_l = -1 if phase == 1 else 0
    lift_r = -1 if phase == 3 else 0
    cv.rect(lx, t, lx + 2, BOOT_TOP + bob + lift_l - 1, SKIN)
    cv.rect(rx, t, rx + 2, BOOT_TOP + bob + lift_r - 1, SKIN_D)
    draw_boot_front(cv, lx, BOOT_TOP + bob, g, lift_l, -1)
    draw_boot_front(cv, rx, BOOT_TOP + bob, g, lift_r, +1)


def draw_boot_front(cv, x0, top, ground, lift, out):
    """正面 / 背面视角的短靴。out=±1 表示鞋底朝外侧扩一格。

    鞋底做成一整行平的：原先脚底中间多出来的一颗「跟」在游戏里就是个尖尖。
    """
    t = top + lift
    g = ground + lift
    cv.rect(x0, t, x0 + 2, t, BOOT_HI)                      # 靴口翻边
    cv.rect(x0, t + 1, x0 + 2, g - 1, BOOT)                 # 靴筒
    cv.rect(x0 + 2, t + 1, x0 + 2, g - 1, BOOT_D)           # 内侧暗部
    cv.put(x0 + 1, t + 2, BOOT_HI)                          # 皮面一点高光
    sx0 = x0 - 1 if out < 0 else x0                         # 鞋底外扩
    sx1 = x0 + 2 if out < 0 else x0 + 3
    cv.rect(sx0, g, sx1, g, BOOT_SOLE)                      # 平底


def draw_boot_side(cv, x0, top, ground):
    """侧视短靴：脚尖朝 +x 多伸两格，鞋底整行是平的。"""
    cv.rect(x0, top, x0 + 2, top, BOOT_HI)                  # 靴口翻边
    cv.rect(x0, top + 1, x0 + 2, ground - 2, BOOT)          # 靴筒
    cv.rect(x0, top + 1, x0, ground - 2, BOOT_D)            # 后侧暗部
    cv.rect(x0, ground - 1, x0 + 3, ground - 1, BOOT)       # 脚背连到脚尖
    cv.put(x0 + 3, ground - 1, BOOT_HI)                     # 鞋尖高光
    cv.rect(x0, ground, x0 + 3, ground, BOOT_SOLE)          # 平底


def render(facing, phase, rise=0):
    bob = -1 if phase in (1, 3) else 0     # 中间两帧整体上移 1px，做出起伏
    cv = Canvas()
    draw_legs(cv, facing, phase, bob)
    draw_skirt(cv, facing, bob)
    # 呼吸只让头往下沉一格（呼气），肩膀、手臂、躯干都不动
    draw_torso(cv, facing, bob)
    draw_arms(cv, facing, phase, bob)
    draw_head(cv, facing, bob, -rise)
    cv.outline()
    return cv.image()


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    for facing in ('down', 'up', 'side'):
        for phase in range(4):
            render(facing, phase).save(
                os.path.join(OUT_DIR, 'player_%s_%d.png' % (facing, phase)))
        for breathe in range(2):
            render(facing, 0, rise=breathe).save(
                os.path.join(OUT_DIR, 'player_%s_idle_%d.png' % (facing, breathe)))
    print('已写 18 帧（32x%d，行走 12 + 呼吸 6）→ %s' % (H, OUT_DIR))

    scale = 6
    sheet = Image.new('RGBA', (W * 4 * scale + 40, H * 3 * scale + 40), (58, 42, 31, 255))
    for r, facing in enumerate(('down', 'up', 'side')):
        for c in range(4):
            im = Image.open(os.path.join(OUT_DIR, 'player_%s_%d.png' % (facing, c)))
            im = im.resize((W * scale, H * scale), Image.NEAREST)
            sheet.alpha_composite(im, (10 + c * W * scale, 10 + r * H * scale))
    sheet.save(os.path.join(ROOT, 'Tools', '_playersprite_preview.png'))
    print('样张 → Tools/_playersprite_preview.png')

    # 呼吸两帧单独出一张对照图，方便看 1px 的胸腔起伏
    idle = Image.new('RGBA', (W * 2 * scale + 30, H * 3 * scale + 40), (58, 42, 31, 255))
    for r, facing in enumerate(('down', 'up', 'side')):
        for c in range(2):
            im = Image.open(os.path.join(OUT_DIR, 'player_%s_idle_%d.png' % (facing, c)))
            im = im.resize((W * scale, H * scale), Image.NEAREST)
            idle.alpha_composite(im, (10 + c * W * scale, 10 + r * H * scale))
    idle.save(os.path.join(ROOT, 'Tools', '_playeridle_preview.png'))
    print('呼吸样张 → Tools/_playeridle_preview.png')


if __name__ == '__main__':
    main()

