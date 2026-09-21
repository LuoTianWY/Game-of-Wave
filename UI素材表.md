# UI 素材表（导入对照）

> **用途**：把「需要美术素材的 UI 元素」映射到具体文件目录，方便后续导入真实 UI。
> **导入规则**：把 PNG 放到 `Assets/Resources/<路径>`，导入设置选 **Sprite (2D and UI)**、透明底、格式 PNG；带边框/圆角的面板用 **9-slice**（`Image.Type.Sliced`）避免拉伸变形。
> **占位说明**：所有行当前都用程序化色块 / 内置圆角占位，缺素材也能跑通原型。加载接口统一走 `UIResource.Load("UI/xxx")` / `UIResource.ApplySpriteOrColor`。
> **关系**：本表与《游玩界面需求表》二节 A1–A14 一一对应（括号内为编号），另补充鼓组原型特有的格槽/已放鼓等条目。

## 一、素材对照表

| UI 元素 | 代码位置 | 当前占位 | Resources 路径 | 建议尺寸(px) | 9-slice | 状态 |
|---|---|---|---|---|---|---|
| 画布底色 | `GameBootstrap.BuildCanvas` | 纯色 `UIStyle.BgDeep` | `UI/bg_canvas` | 1920×1080 | 否 | 待接线 |
| 鼓轨底板 | `DrumTrackBoard.Build` | 纯色 `UIStyle.Stage`（已试载 baseplate） | `UI/baseplate`（A2） | 512×512 无缝平铺 | 否 | ✅ 已接线 |
| 格槽（16 格） | `DrumTrackBoard.DrawTrack` | 圆角纯色交替 | `UI/cell_slot` | 240×152 | 是 | 待接线 |
| 已放置鼓格 | `DrumTrackBoard.CreatePlaced` | 圆角青灰 | `UI/drum_cell` | 240×160 | 是 | 待接线 |
| 侧栏面板 | `RosterSidebar.Build` | 圆角 `UIStyle.Panel` | `UI/sidebar`（A4） | 320×1080 | 是 | 待接线 |
| 侧栏拉手 | `RosterSidebar.BuildTab` | 圆角 `UIStyle.Accent2` | `UI/sidebar_tab`（A5） | 24×120 | 是 | 待接线 |
| 角色卡底 | `DrumCharacterCard.BuildVisual` | 圆角 `UIStyle.Card` | `UI/card_bg`（A6） | 280×150 | 是 | 待接线 |
| 角色头像 | 卡片内（未接入） | 无 | `Characters/<名字>`（A7） | 128×128（@2x 256） | 否 | 预留 |
| 试听按钮 | `DrumCharacterCard.AddPlayButton` | 圆角青 + 文字 | `UI/btn_play` | 76×34 | 是 | 待接线 |
| 演出！按钮 | `GameBootstrap.BuildPlayback` | 圆角青 | `UI/btn_play`（大） | 240×64 | 是 | 待接线 |
| 确定按钮 | `GameBootstrap.BuildPlayback` | 圆角绿 | `UI/btn_confirm`（A11） | 120×44 | 是 | 待接线 |
| 取消按钮 | `GameBootstrap.BuildPlayback` | 圆角灰 | `UI/btn_cancel`（A12） | 120×44 | 是 | 待接线 |
| 回起点按钮 | `DrumTrackBoard.AddResetButton` | 圆角灰 | `UI/btn_reset` | 140×44 | 是 | 待接线 |
| 顶部状态栏 | `GameBootstrap.BuildPlayback` | 纯文字 | `UI/header`（A3） | 1920×72 | 是 | 待接线 |
| 播放/试听图标 | 按钮内（未接入） | 无图标 | `UI/icon_play`（A8） | 40×40 | 否 | 预留 |
| 回到起点箭头 | 预留（未来左缘吸附） | 无 | `UI/icon_back`（A10） | 64×64 | 否 | 预留 |

## 二、程序化绘制（无需美术）

- 节拍竖线 / 小节线（`DrumTrackBoard.DrawTrack`）
- 声部轨横线 / 分隔线（`DrumTrackBoard.DrawTrack`）
- 所有**波形曲线**与波形预览（`WaveformRenderer`）
- 拖拽落点高亮（`UI/highlight_ok` / `UI/highlight_bad`，A13/A14 也可程序化替代）

## 三、状态说明

- **✅ 已接线**：代码已调用 `UIResource.ApplySpriteOrColor(bg, "UI/baseplate", …)`，PNG 放对路径即生效。
- **待接线**：目前用 `UIStyle.ApplyRound`（圆角纯色）占位，需要我把对应元素改成 `UIResource.ApplySpriteOrColor(img, "路径", 占位色)` 后才随贴图生效。
- **预留**：该元素还没在 UI 里建出来（如头像、图标、左缘箭头），等模块落地后再接线。

> 后续把「待接线」全部接到 `UIResource` 后，你只需把 PNG 丢进对应目录，无需改代码即可换肤。
