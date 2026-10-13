using UnityEngine;
using UnityEngine.UI;
using WaveTeam.Audio;
using WaveTeam.Core;

namespace WaveTeam.UI
{
    /// <summary>
    /// 细节预览弹窗：点击板上角色的六边形波形后打开，展示完整节奏骨架
    /// （已解锁节奏点亮色发光、未解锁暗灰），并提供「加点」「洗点」「关闭」。
    /// </summary>
    public sealed class DrumDetailPanel : MonoBehaviour
    {
        private DrumCharacter _character;
        private HexWaveformRenderer _waveform;
        private PixelText _pointsText;

        public static DrumDetailPanel Show(DrumCharacter character, Transform canvasRoot)
        {
            // 先关掉旧的，避免叠多个
            var old = canvasRoot.Find("DrumDetailPanel");
            if (old != null) Destroy(old.gameObject);

            var go = new GameObject("DrumDetailPanel", typeof(RectTransform), typeof(Image), typeof(DrumDetailPanel));
            go.transform.SetParent(canvasRoot, false);
            go.transform.SetAsLastSibling();
            var panel = go.GetComponent<DrumDetailPanel>();
            panel._character = character;
            panel.Build();
            return panel;
        }

        private void Build()
        {
            // 全屏遮罩（挡住背后交互，仅面板内可点）
            var mask = GetComponent<Image>();
            mask.color = new Color(0f, 0f, 0f, 0.55f);
            UIFactory.Stretch((RectTransform)transform);

            // 中央面板
            var panel = UIFactory.CreatePanel("Panel", transform, UIStyle.Card);
            UIResource.ApplyFrameOrColor(panel, "UI/card_bg", UIStyle.Card);
            var prt = (RectTransform)panel.transform;
            prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(760f, 480f);
            prt.anchoredPosition = Vector2.zero;

            // 标题：名字 · 性格名词
            var title = UIFactory.PlacePixelText("Title", panel.transform,
                _character.Name + " · " + _character.Noun, 2.4f,
                UIStyle.Text, PixelText.Align.Center,
                new Vector2(0.5f, 1f), new Vector2(0f, -18f));
            title.raycastTarget = false;

            // 波形预览：完整骨架，未解锁部分画成暗灰；可左右/上下拖动查看（范围 = 波形最远格）
            var scrollGo = new GameObject("WaveScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollGo.transform.SetParent(panel.transform, false);
            var scrollRt = (RectTransform)scrollGo.transform;
            scrollRt.anchorMin = new Vector2(0f, 0.22f);
            scrollRt.anchorMax = new Vector2(1f, 1f);
            scrollRt.offsetMin = new Vector2(24f, 70f);
            scrollRt.offsetMax = new Vector2(-24f, -70f);
            var scrollBg = scrollGo.GetComponent<Image>();
            scrollBg.color = new Color(0f, 0f, 0f, 0f); // 透明命中区，接收空白处拖拽
            scrollBg.raycastTarget = true;

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(scrollGo.transform, false);
            var vrt = (RectTransform)viewport.transform;
            vrt.anchorMin = Vector2.zero;
            vrt.anchorMax = Vector2.one;
            vrt.offsetMin = Vector2.zero;
            vrt.offsetMax = Vector2.zero;

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            var contentRt = (RectTransform)content.transform;
            contentRt.anchorMin = contentRt.anchorMax = contentRt.pivot = new Vector2(0f, 1f); // 左上角锚定
            contentRt.anchoredPosition = Vector2.zero;

            var waveGo = new GameObject("Waveform", typeof(RectTransform), typeof(CanvasRenderer), typeof(HexWaveformRenderer));
            waveGo.transform.SetParent(content.transform, false);
            _waveform = waveGo.GetComponent<HexWaveformRenderer>();
            _waveform.SetPattern(_character.Pattern, RhythmPattern.StableHash(_character.Name));
            _waveform.SetUnlocked(_character.UnlockedCells);
            _waveform.SetPitch(40f); // 固定格距：格子够大便于点击，超出可视区可拖动查看
            _waveform.color = Color.white;                      // 颜色由音色大类贴图提供
            _waveform.SetCategory(_character.Category);          // 六边形外观 = 音色大类贴图
            _waveform.raycastTarget = true; // 可直接点击波形上的格子加点（点到哪加到哪）
            _waveform.CellClicked += OnCellClicked;
            var wrt = (RectTransform)waveGo.transform;
            wrt.anchorMin = Vector2.zero;
            wrt.anchorMax = Vector2.one;
            wrt.offsetMin = Vector2.zero;
            wrt.offsetMax = Vector2.zero;
            wrt.pivot = new Vector2(0f, 0.5f); // 左缘锚定：q=0 贴内容左边，整段波形（含右端点）都落在内容内

            // 内容尺寸 = 波形理想尺寸（不足可视区则撑满；右侧多留 160px，拖到底能看清右端点）
            var contentSize = _waveform.GetContentSize();
            contentRt.sizeDelta = new Vector2(Mathf.Max(contentSize.x + 160f, vrt.rect.width), Mathf.Max(contentSize.y, vrt.rect.height));

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.horizontal = true;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = true;
            scroll.scrollSensitivity = 24f;
            scroll.viewport = vrt;
            scroll.content = contentRt;

            // 解锁进度文字
            _pointsText = UIFactory.PlacePixelText("Points", panel.transform, "", 1.8f,
                UIStyle.Text, PixelText.Align.Center,
                new Vector2(0.5f, 0f), new Vector2(0f, 56f));
            _pointsText.raycastTarget = false;
            RefreshText();

            // 加点 / 洗点 / 关闭
            var add = UIFactory.CreatePixelButton("Add", panel.transform, "加点", 1.8f,
                UIStyle.Accent, UIStyle.Ink, OnAdd);
            UIResource.ApplyFrameOrColor(add.image, "UI/btn_play", UIStyle.Accent);
            var art = (RectTransform)add.transform;
            art.anchorMin = art.anchorMax = art.pivot = new Vector2(0f, 0f);
            art.sizeDelta = new Vector2(180f, 52f);
            art.anchoredPosition = new Vector2(24f, 16f);

            var reset = UIFactory.CreatePixelButton("Reset", panel.transform, "洗点", 1.8f,
                new Color(0.55f, 0.36f, 0.20f, 1f), UIStyle.Text, OnReset);
            UIResource.ApplyFrameOrColor(reset.image, "UI/btn_cancel", new Color(0.55f, 0.36f, 0.20f, 1f));
            var rrt = (RectTransform)reset.transform;
            rrt.anchorMin = rrt.anchorMax = rrt.pivot = new Vector2(0f, 0f);
            rrt.sizeDelta = new Vector2(180f, 52f);
            rrt.anchoredPosition = new Vector2(224f, 16f);

            var close = UIFactory.CreatePixelButton("Close", panel.transform, "关闭", 1.8f,
                UIStyle.Grey, UIStyle.Text, Close);
            UIResource.ApplyFrameOrColor(close.image, "UI/btn_cancel", UIStyle.Grey);
            var crt = (RectTransform)close.transform;
            crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(1f, 0f);
            crt.sizeDelta = new Vector2(140f, 52f);
            crt.anchoredPosition = new Vector2(-24f, 16f);
        }

        private void OnAdd()
        {
            _character.AddPoint();
            _waveform.SetUnlocked(_character.UnlockedCells);
            RefreshText();
        }

        /// <summary>左键点击波形格子：点到哪加到哪，前缀亮到该格（不直通下一节点）；右键：反向收缩到该格之前。</summary>
        private void OnCellClicked(int cellIndex, bool right)
        {
            if (right) _character.LockToCell(cellIndex);
            else _character.UnlockToCell(cellIndex);
            _waveform.SetUnlocked(_character.UnlockedCells);
            RefreshText();
        }

        private void OnReset()
        {
            _character.ResetPoints();
            _waveform.SetUnlocked(_character.UnlockedCells);
            RefreshText();
        }

        private void RefreshText()
        {
            _pointsText.Text = "已解锁 " + _character.UnlockedPoints + " / " + _character.Pattern.Points.Length + " 个节奏点";
        }

        private void Close()
        {
            Destroy(gameObject);
        }
    }
}
