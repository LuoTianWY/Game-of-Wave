using UnityEngine;
using UnityEngine.UI;
using WaveTeam.Core;
using WaveTeam.Room;

namespace WaveTeam.UI
{
    /// <summary>第一关的预计目标和波形详情。全部目标状态来自正式评分器。</summary>
    public sealed class RehearsalAnalysisPanel : MonoBehaviour
    {
        public const float PanelHeight = 138f;
        public const float ReservedHeight = PanelHeight + 20f;

        private DrumTrackBoard _board;
        private readonly Text[] _states = new Text[3];
        private readonly Text[] _advice = new Text[3];
        private Text _inspection;

        public static void Create(Transform parent, DrumTrackBoard board, float consoleHeight)
        {
            var panel = UIFactory.CreatePanel("RehearsalAnalysis", parent, UIStyle.BgDeep);
            panel.raycastTarget = false;
            UIStyle.ApplyWell(panel, UIStyle.BgDeep);
            var rt = panel.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = new Vector2(1f, 0f);
            rt.offsetMin = new Vector2(12f, consoleHeight + 10f);
            rt.offsetMax = new Vector2(-RosterSidebar.PanelWidth - 12f, consoleHeight + 10f + PanelHeight);
            var view = panel.gameObject.AddComponent<RehearsalAnalysisPanel>();
            view._board = board;
            view.Build();
        }

        private void Build()
        {
            var targets = UIFactory.CreateRect("Targets", transform);
            targets.anchorMin = Vector2.zero;
            targets.anchorMax = new Vector2(0.69f, 1f);
            targets.offsetMin = new Vector2(12f, 8f);
            targets.offsetMax = new Vector2(-12f, -32f);
            var heading = AddText(transform, "Heading",
                "实时目标 · 预计状态按实际音乐判定，不直接相加角色属性", 16, UIStyle.TextDim);
            UIFactory.Place(heading.rectTransform, new Vector2(0f, 1f),
                new Vector2(1060f, 22f), new Vector2(12f, -5f));

            string[] titles = { "开场稳定 · Beat 0–4", "发展层次 · Beat 4–8", "整体密度 · Beat 0–16" };
            for (int i = 0; i < titles.Length; i++)
            {
                var column = UIFactory.CreateRect("Target_" + i, targets);
                column.anchorMin = new Vector2(i / 3f, 0f);
                column.anchorMax = new Vector2((i + 1) / 3f, 1f);
                column.offsetMin = Vector2.zero;
                column.offsetMax = new Vector2(-12f, 0f);
                AddRow(column, "Title", titles[i], 18, UIStyle.Text, 0f, 22f);
                _states[i] = AddRow(column, "State", "", 17, UIStyle.TextDim, 24f, 22f);
                _advice[i] = AddRow(column, "Advice", "", 16, UIStyle.TextDim, 48f, 44f);
            }

            var details = UIFactory.CreateRect("WaveformInfo", transform);
            details.anchorMin = new Vector2(0.70f, 0f);
            details.anchorMax = Vector2.one;
            details.offsetMin = new Vector2(6f, 8f);
            details.offsetMax = new Vector2(-12f, -5f);
            AddRow(details, "Title", "波形详情 · 单击固定，双击试听", 16, UIStyle.TextDim, 0f, 22f);
            _inspection = AddRow(details, "Details", "", 17, UIStyle.Text, 29f, 94f);

            _board.OnArrangementChanged += Refresh;
            _board.OnInspectionChanged += ShowInspection;
            Refresh();
            ShowInspection(null, false);
        }

        private void Refresh()
        {
            var result = RehearsalEvaluator.Evaluate(_board);
            bool waiting = _board.Placed.Count == 0;
            SetTarget(0, result.OpeningStableGood, result.OpeningAdvice, waiting);
            SetTarget(1, result.DevelopmentLayeredGood, result.DevelopmentAdvice, waiting);
            SetTarget(2, result.DensityGood, result.DensityAdvice, waiting);
        }

        private void SetTarget(int index, bool achieved, string advice, bool waiting)
        {
            _states[index].text = waiting ? "等待编排" : achieved ? "预计达成" : "尚未达成";
            _states[index].color = achieved ? UIStyle.Ok : UIStyle.Accent;
            _advice[index].text = advice;
        }

        private void ShowInspection(PlacedDrum drum, bool selected)
        {
            if (drum == null)
            {
                _inspection.text = "悬停轨道上的波形查看信息。\n单击后，信息会保留在这里。\n属性反映角色特质；目标取决于编排。";
                return;
            }
            var stats = RehearsalTraits.GetStats(drum.Character.Noun);
            _inspection.text = drum.Character.Name + " · " + drum.Character.Noun + (selected ? "  已选中" : "  悬停")
                + "\n稳定 " + stats.Stability + " / 层次 " + stats.Layering + " / 密度 " + stats.Density
                + "\nLayer " + (drum.LaneIndex + 1) + " · 开始 " + drum.StartBeat.ToString("0.##")
                + " · 结束 " + (drum.StartBeat + drum.DurationBeats).ToString("0.##");
        }

        private static Text AddText(Transform parent, string name, string content, int size, Color color)
        {
            var text = UIFactory.CreateText(name, parent, content, size, color, TextAnchor.UpperLeft);
            text.raycastTarget = false;
            return text;
        }

        private static Text AddRow(Transform parent, string name, string content, int size, Color color, float top, float height)
        {
            var text = AddText(parent, name, content, size, color);
            var rt = text.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, height);
            rt.anchoredPosition = new Vector2(0f, -top);
            return text;
        }

        private void OnDestroy()
        {
            if (_board == null) return;
            _board.OnArrangementChanged -= Refresh;
            _board.OnInspectionChanged -= ShowInspection;
        }
    }
}
