# -*- coding: utf-8 -*-
"""粗查 C# 源文件的括号平衡与关键结构（不做真正的语法分析，只当烟雾测试）。"""
import sys

path = sys.argv[1]
src = open(path, encoding='utf-8').read()

# 逐字符扫描，跳过字符串字面量和注释
out = []
i, n = 0, len(src)
while i < n:
    c = src[i]
    if c == '/' and i + 1 < n and src[i + 1] == '/':
        while i < n and src[i] != '\n':
            i += 1
    elif c == '/' and i + 1 < n and src[i + 1] == '*':
        i += 2
        while i + 1 < n and not (src[i] == '*' and src[i + 1] == '/'):
            i += 1
        i += 2
    elif c in '"\'':
        q, i = c, i + 1
        while i < n:
            if src[i] == '\\':
                i += 2
                continue
            if src[i] == q:
                i += 1
                break
            i += 1
        out.append('""')
    else:
        out.append(c)
        i += 1

s = ''.join(out)
pairs = [('{', '}'), ('(', ')'), ('[', ']')]
ok = True
for a, b in pairs:
    if s.count(a) != s.count(b):
        ok = False
        print('不平衡: %s=%d  %s=%d' % (a, s.count(a), b, s.count(b)))
print('括号平衡:', 'OK' if ok else '有问题')
print('行数:', src.count('\n') + 1)
for kw in ('namespace ', 'class ', 'MenuItem', 'InitializeOnLoadMethod',
           'static bool TryBuild', 'static void BuildMenu', 'void Update'):
    print('  %-28s %d' % (kw, src.count(kw)))
