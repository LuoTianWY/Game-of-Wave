# CutoutV2 - 新画风道具素材

按新背景（暖木色地板 + 奶油米墙面 + 墨绿/砖红/藏蓝点缀）重画的一整套道具，
细描边、平涂两层明暗。旧的 `../Cutout/` 和 `../Props/` 两套是旧画风，暂时保留没动。

共 17 张，全部为带 alpha 通道的透明 PNG，文件名和旧素材一一对应：

乐器（9）：`amp` `bass` `drum_kit` `guitar_acoustic` `guitar_case` `guitar_electric`
`guitar_stand` `keyboard` `mic_stand`

家具（8）：`cabinet` `chair` `coffee_table` `instrument_case` `shelf` `sofa` `stool` `trash_bin`

## 处理说明

出图背景不统一（品红底和白底混着），抠图时分了两套逻辑：品红底走色键，白底走局部背景
归一化。之后又做了四步：去掉物体外围的浅灰描边、去掉底下的地面投影、去掉生成器水印、
边缘颜色外扩（避免贴到深色物体上出现白边）。最后按内容裁边，每边留 4px。

## 导入设置

统一 `spritePixelsToUnits = 750`（Single 精灵模式、FullRect、不压缩、最大 2048）。
750 是按「世界尺寸和旧素材基本一致」倒推的：新图像素尺寸平均约为旧图的 0.73 倍，
所以 PPU 用 1024 x 0.73 约等于 750，换过去时摆放位置和缩放不用大改。

## 已知问题

- `coffee_table`：桌腿和桌面的接缝处还残留几个像素大小的蓝色碎点（原来是投影，去得不彻底）。
- `chair`：坐垫是粉色，和整体配色不太一致。
- `amp` `guitar_case`：物体外围有很淡的一圈浅灰描边，是生成时画进图里的。

## 怎么用

`Assets/Editor/BuildRehearsalRoom.cs` 里的 `CutoutDir` 常量指向 `Assets/Sprites/Room/Cutout/`，
改成 `Assets/Sprites/Room/CutoutV2/` 再跑一次菜单「波形小队/重建排练室场景」即可整套换成新素材。