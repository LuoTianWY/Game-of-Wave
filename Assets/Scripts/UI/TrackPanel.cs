using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using WaveTeam.Core;

namespace WaveTeam.UI
{
    /// <summary>一条轨道的展示：声部角色、目标谱、玩家链条，支持选中与撤销。</summary>
    public sealed class TrackPanel
    {
        public Track Track { get; private set; }
        public RectTransform Rect { get; private set; }
        public bool Selected { get; private set; }

        private const float TileW = 20f;
        private const float TileH = 22f;

        private readonly Image _bg;
        private readonly Text _chainLabel;
        private readonly RectTransform _chainStrip;

        public TrackPanel(Track track, Transform parent, System.Action<TrackPanel> onSelect)
        {
            Track = track;
            _bg = UIFactory.CreatePanel("Track_" + track.RoleName, parent, new Color(0.16f, 0.16f, 0.22f, 1f));
            Rect = (RectTransform)_bg.transform;
            var btn = _bg.gameObject.AddComponent<Button>();
            btn.targetGraphic = _bg;
            btn.onClick.AddListener(() => onSelect(this));

            var roleText = UIFactory.CreateText("Role", _bg.transform, track.RoleName, 18, Color.white);
            UIFactory.Place(roleText.rectTransform, new Vector2(0f, 1f), new Vector2(70, 26), new Vector2(10, -6));

            var targetLabel = UIFactory.CreateText("TargetLabel", _bg.transform, "目标", 11, new Color(0.7f, 0.7f, 0.7f));
            UIFactory.Place(targetLabel.rectTransform, new Vector2(0f, 1f), new Vector2(36, 16), new Vector2(88, -10));
            UIFactory.CreateTileStrip("TargetStrip", _bg.transform, track.TargetScore, TileW, TileH, new Vector2(128, -8));

            var chainLabel = UIFactory.CreateText("ChainLabel", _bg.transform, "队伍", 11, new Color(0.7f, 0.7f, 0.7f));
            UIFactory.Place(chainLabel.rectTransform, new Vector2(0f, 1f), new Vector2(36, 16), new Vector2(88, -44));
            _chainStrip = UIFactory.CreateTileStrip("ChainStrip", _bg.transform, new List<Tile>(), TileW, TileH, new Vector2(128, -42));

            _chainLabel = UIFactory.CreateText("ChainText", _bg.transform, "", 11, Color.white);
            UIFactory.Place(_chainLabel.rectTransform, new Vector2(0f, 1f), new Vector2(620, 16), new Vector2(310, -44));

            var removeBtn = UIFactory.CreateButton("Remove", _bg.transform, "撤销", RemoveLast);
            UIFactory.Place((RectTransform)removeBtn.transform, new Vector2(1f, 1f), new Vector2(56, 28), new Vector2(-10, -6));

            SetSelected(false);
            Refresh();
        }

        public void SetSelected(bool selected)
        {
            Selected = selected;
            _bg.color = selected ? new Color(0.22f, 0.40f, 0.26f, 1f) : new Color(0.16f, 0.16f, 0.22f, 1f);
        }

        public void AddCharacter(Character c)
        {
            Track.Chain.Add(c);
            Refresh();
        }

        public void RemoveLast()
        {
            if (Track.Chain.Count > 0) Track.Chain.RemoveAt(Track.Chain.Count - 1);
            Refresh();
        }

        public void Refresh()
        {
            var flat = new List<Tile>();
            var names = new StringBuilder();
            foreach (var c in Track.Chain)
            {
                names.Append(c.Name).Append(" ");
                foreach (var t in c.Tiles) flat.Add(t);
            }
            UIFactory.PopulateStrip(_chainStrip, flat, TileW, TileH);
            _chainLabel.text = Track.Chain.Count == 0 ? "(空)" : names.ToString();
        }
    }
}
