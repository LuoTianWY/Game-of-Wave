# 六边形节点贴图（音色大类）

音轨「加点/减点」处六边形节点的贴图素材，按**音色大类**一一对应。
形状统一为**尖顶朝上下**（pointy-top，与 `HexLayout.Corners()` 的方向一致）。

运行时取用：`Resources.Load<Sprite>("HexNodes/<文件名>")`

## 对应表

| 文件名 | 音色大类 | 颜色 | Hex |
|---|---|---|---|
| `percussion.png` | 打击乐 | 灰 | `#9E9E9E` |
| `strings.png` | 弦乐 | 棕 | `#8D6E63` |
| `brass.png` | 铜管 | 金 | `#D4A017` |
| `woodwind.png` | 木管 | 紫 | `#8E24AA` |
| `vocal.png` | 人声 | 绿 | `#43A047` |
| `synth.png` | 电子合成器 | 蓝 | `#1E88E5` |
| `guitar.png` | 吉他 | 橙 | `#FF8A00` |
| `piano.png` | 钢琴 | 青 | `#26C6DA` |
| `ethnic.png` | 民族乐器 | 红 | `#E53935`（占位） |

颜色值来自 `Assets/Resources/Characters/trait_music.json`（每条词条的 `color` 字段按音色大类统一）。

## 素材说明

- 每块六边形为**同色系双层边框**：外圈比主体暗约 20% 的描边，内圈比主体亮约 25% 的细高光边。
- 生成提示词见仓库根目录 `即梦六边形色块提示词.md`。
- 背景为纯白，导入时按 Sprite 使用；如需透明请自行抠图。

## 待接入

`HexWaveformRenderer` 目前是程序化画多边形（`MaskableGraphic`），尚未读取这批贴图。
接入时需要：

1. 按角色的音色大类选对应 Sprite（9 个文件名建索引）；
2. 保留原有「暗态 / 亮态」：普通格用低 alpha、节奏点用全亮（直接调 `Image.color` 的 alpha 即可，不需要两套图）；
3. 未解锁态仍可用程序化的暗灰多边形覆盖。
