# UI 素材表（导入对照）

> **用途**：把「需要美术素材的 UI 元素」映射到具体文件目录，方便后续导入真实 UI。
> **导入规则**：PNG 放到 `Assets/Resources/<路径>`，导入设置选 **Sprite (2D and UI)**、透明底、格式 PNG、**Point 采样**、不压缩；带边框/圆角的面板用 **9-slice**（`Image.Type.Sliced`）避免拉伸变形。
> **占位说明**：没素材也能跑。风格层现在是**运行时生成的 16×16 硬边像素九宫格**（凸面 `UIStyle.FrameRaised` / 凹槽 `UIStyle.FrameWell`），所以缺图时界面本身就是像素风，不是纯色块。
> **提示词**：每条路径对应的即梦提示词见《UI素材提示词清单.md》。

## 一、素材对照表

| UI 元素 | 代码位置 | 无素材时 | Resources 路径 | 建议尺寸(px) | 9-slice | 状态 |
|---|---|---|---|---|---|---|
| 画布底色 | `GameBootstrap.BuildBoard` | 纯色 `UIStyle.BgDeep` | `UI/bg_canvas` | 1920×1080 | 否 | ✅ 已接线 |
| 鼓轨底板 | `DrumTrackBoard.Build` | `UIStyle.Stage` + 程序化木纹 | `UI/baseplate` | 512×512 无缝平铺 | 否 | ✅ 已接线 |
| 顶栏 / 底栏木条 | `GameBootstrap.BuildPlayback` / `BuildBottomBar` | 凸面像素边框 | `UI/header` | 拉长用，四角纯色 | 是 | ✅ 已接线 |
| 格槽（16 格） | `DrumTrackBoard.DrawTrack` | 凹槽像素边框 `FrameWell` | `UI/cell_slot` | 拉长用，四角纯色 | 是 | ✅ 已接线 |
| 侧栏面板 | `RosterSidebar.Build` | 凸面像素边框 | `UI/sidebar` | 320×1080 | 是 | ✅ 已接线 |
| 侧栏拉手 | `RosterSidebar.BuildTab` | 凸面像素边框 | `UI/sidebar_tab` | 24×120 | 是 | ✅ 已接线 |
| 角色卡底 | `DrumCharacterCard.BuildVisual` | 凸面像素边框 | `UI/card_bg` | 280×150 | 是 | ✅ 已接线 |
| 细节面板底 | `DrumDetailPanel.Build` | 凸面像素边框 | `UI/card_bg`（复用） | 760×480 | 是 | ✅ 已接线 |
| 主按钮 | `DrumTrackBoard` / `DrumCharacterCard` / `GameBootstrap` / `DrumDetailPanel` | 凸面像素边框 | `UI/btn_play` | 240×56 | 是 | ✅ 已接线 |
| 确定按钮 | `GameBootstrap.BuildBottomBar` | 凸面像素边框 | `UI/btn_confirm` | 120×44 | 是 | ✅ 已接线 |
| 中性按钮 | 取消 / 退出 / 停止 / 关闭 / 洗点 / 返回 | 凸面像素边框 | `UI/btn_cancel` | 120×44 | 是 | ✅ 已接线 |
| 回起点按钮 | `DrumTrackBoard.AddResetButton` | 凸面像素边框 | `UI/btn_reset` | 140×44 | 是 | ✅ 已接线 |
| 已放置鼓格 | — | 就是六边形波形本体，**不加底板** | `UI/drum_cell` | — | — | ⛔ 不做 |
| 角色头像 | 卡片内（未接入） | 无 | `Characters/Portraits/<拼音>` | 128×128 | 否 | 🕓 预留 |
| 播放/试听图标 | 按钮内（未接入） | 无图标，用像素字 | `UI/icon_play` | 40×40 | 否 | 🕓 预留 |
| 回到起点箭头 | 预留 | 无 | `UI/icon_back` | 64×64 | 否 | 🕓 预留 |

## 二、程序化绘制（无需美术）

- 节拍竖线 / 小节线 / 轨道中心横线（`DrumTrackBoard.DrawTrack`）
- 底板木纹（`DrumTrackBoard.DrawPlanks`：横向板缝 + 竖向接缝）
- 所有**六边形波形**与波形预览（`HexWaveformRenderer`，含拖拽落点虚影）
- 凸面 / 凹槽像素边框（`UIStyle.FrameRaised` / `FrameWell`，运行时生成、点采样）
- 所有文字（`PixelText`，位图像素字，图集见 `Assets/Resources/PixelFont/`）

## 三、状态说明

- **✅ 已接线**：代码已调用 `UIResource.ApplyFrameOrColor(img, "路径", 占位色)`，PNG 放对路径即生效，**不用改代码**。
- **⛔ 不做**：评估后确定不需要该素材。
- **🕓 预留**：该元素还没在 UI 里建出来，等模块落地后再接线。

> 换肤只需把 PNG 丢进对应目录。缺图时会自动回退到程序化像素边框，不会开天窗。
