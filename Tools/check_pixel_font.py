# -*- coding: utf-8 -*-
"""
检查像素字体图集的覆盖情况：Assets/Scripts 下所有 .cs 的**字符串字面量**里出现的
每一个中日韩字符，是不是都在 Assets/Resources/PixelFont/pixel_font.json 的字表里。

漏字不会崩，只会静默留空（PixelText 会打一条 warning）。等玩到那一步才发现就晚了，
所以每次往代码里加了新中文文案，都该跑一遍这个：

    python Tools/check_pixel_font.py

有漏字就把它们加进 Tools/make_pixel_font.py 的 EXTRA，再重跑 make_pixel_font.py。
（正常情况下不必手加 —— make_pixel_font.py 自己就会扫源码；这里只是复核。）
"""
import io
import os
import re
import sys
import json
import glob

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC_DIR = os.path.join(ROOT, 'Assets', 'Scripts')
FONT_JSON = os.path.join(ROOT, 'Assets', 'Resources', 'PixelFont', 'pixel_font.json')

# 和 make_pixel_font.py 保持一致：先剥注释，再只看字符串/字符字面量
STRING_LITERAL = re.compile(r'"((?:[^"\\]|\\.)*)"')
CHAR_LITERAL = re.compile(r"'((?:[^'\\]|\\.)*)'")
CJK_RANGES = (
    (0x3000, 0x303F),
    (0x4E00, 0x9FFF),
    (0xFF00, 0xFFEF),
)
CJK_SINGLE = '·“”‘’—…、。，！？：；（）《》'


def main():
    if not os.path.exists(FONT_JSON):
        sys.exit('找不到字表：%s\n请先跑 python Tools/make_pixel_font.py' % FONT_JSON)

    data = json.load(io.open(FONT_JSON, encoding='utf-8'))
    have = set(data['chars'])

    missing = {}
    files = sorted(glob.glob(os.path.join(SRC_DIR, '**', '*.cs'), recursive=True))
    for path in files:
        src = io.open(path, encoding='utf-8', errors='replace').read()
        src = re.sub(r'/\*.*?\*/', ' ', src, flags=re.S)
        src = re.sub(r'//[^\n]*', ' ', src)
        for lit in STRING_LITERAL.findall(src) + CHAR_LITERAL.findall(src):
            for ch in lit:
                o = ord(ch)
                if not (any(lo <= o <= hi for lo, hi in CJK_RANGES) or ch in CJK_SINGLE):
                    continue
                if ch not in have:
                    missing.setdefault(ch, set()).add(os.path.basename(path))

    print('扫描 %d 个 .cs' % len(files))
    print('字表 %d 字，图集 %dx%d' % (len(data['chars']), data['atlasW'], data['atlasH']))

    if missing:
        print('\n缺 %d 个字（这些字在界面上会留空）：' % len(missing))
        for ch, where in sorted(missing.items()):
            print('    %s  U+%04X   ← %s' % (ch, ord(ch), ', '.join(sorted(where))))
        return 1

    print('字表完整，没有漏字。')
    return 0


if __name__ == '__main__':
    sys.exit(main())
