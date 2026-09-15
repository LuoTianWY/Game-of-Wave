using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WaveTeam.Audio;
using WaveTeam.Core;

namespace WaveTeam.UI
{
    /// <summary>
    /// 入口：搭建游玩窗口、管理拖拽编排与「检查」播放流程。按下 Play 即自动运行。
    /// 节奏基准：120 BPM、4/4 拍（见 BgmPlayer）。
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        // —— 视觉常量 ——
        private const float PixelsPerBeat = 144f;  // 每拍横向像素
        private const float TileMaxHeight = 180f;  // 最高 tile 的高度
        private const float MaxPitch = 7f;         // 当前角色里的最高音高
        private const float LeadInBeats = 1f;      // 播放头在最左波形前方的提前量（拍）

        // 游玩方框尺寸
        private const float PlayAreaWidth = 1880f; // 方框宽
        private const float PlayAreaHeight = 500f; // 方框高

        // 游玩方框内的布局（本地坐标，左上为原点）
        private const float LaneLeft = 40f;        // 波形链左起点
        private const float LaneCenterY = -300f;   // 波形卡垂直中心

        // 底部频谱条
        private const int SpectrumBarCount = 12;   // 竖条根数
        private const float SpectrumBaseH = 10f;   // 竖条基准高度
        private const float SpectrumMaxH = 110f;   // 竖条跳动峰值高度

        private RectTransform _canvasRoot;
        private BgmPlayer _bgm;

        private RectTransform _rosterRoot;
        private RectTransform _playArea;
        private RectTransform _playCardsRoot;
        private RectTransform _dragRoot;
        private Image _snapLine;

        private Text _fitText;
        private Text _statusText;
        private Image _flash;

        private readonly List<RectTransform> _spectrumBars = new List<RectTransform>();

        // 状态
        private readonly List<Character> _roster = new List<Character>();
        private readonly List<Character> _chain = new List<Character>();

        // 拖拽
        private DraggableCard _dragCard;
        private Vector3 _dragOffset;

        // 波形链起点 X（按当前链宽动态居中，替代固定的 LaneLeft）
        private float _laneOriginX = LaneLeft;

        // 播放
        private bool _playing;
        private List<PlayedNote> _notes;
        private float _playStartTime;
        private int _activeBlockIndex = -1;
        private readonly List<CardBlock> _blocks = new List<CardBlock>();

        private static bool _started;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (_started) return;
            _started = true;
            var go = new GameObject("GameBootstrap");
            go.AddComponent<GameBootstrap>();
        }

        private void Start()
        {
            _roster.AddRange(SampleData.All);
            BuildAudio();
            BuildUi();
            RebuildRoster();
            RebuildPlayArea();
            RunSelfCheck();
        }

        private void Update()
        {
            if (!_playing) return;

            float beat = CurrentBeat();
            UpdateRhythmFeedback(beat);

            float total = ChainTimeline.TotalBeats(_chain);
            if (beat >= LeadInBeats + total + 0.6f)
                FinishPlayback();
        }

        // ---------- 音频 ----------

        private void BuildAudio()
        {
            _bgm = gameObject.AddComponent<BgmPlayer>();
        }

        private float CurrentBeat()
        {
            if (_bgm.HasClip && _bgm.IsPlaying)
                return _bgm.Time / _bgm.BeatSeconds;                  // 采样级：跟随真实音频
            return (Time.time - _playStartTime) / _bgm.BeatSeconds;  // 回退：无 BGM 或已播完
        }

        // ---------- UI 搭建 ----------

        private void BuildUi()
        {
            EnsureEventSystem();

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);  // 标准电脑屏幕，文字按原生分辨率渲染更清晰
            scaler.matchWidthOrHeight = 0f;                        // 以宽度为准缩放，横向布局在任何屏幕比例都不错位
            var root = canvasGo.transform;
            _canvasRoot = (RectTransform)canvasGo.transform;

            var title = UIFactory.CreateText("Title", root, "波形小队 · 拼波形（120 BPM · 4/4）", 44, Color.white, TextAnchor.MiddleCenter);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(1400, 60), new Vector2(0, -28));

            var rosterLabel = UIFactory.CreateText("RosterLabel", root, "待选区（拖角色到下方方框，前后位置会自动吸附）", 28, new Color(0.7f, 0.7f, 0.7f));
            UIFactory.Place(rosterLabel.rectTransform, new Vector2(0f, 1f), new Vector2(1200, 36), new Vector2(40, -92));

            _rosterRoot = UIFactory.CreateRect("Roster", root);
            UIFactory.Place(_rosterRoot, new Vector2(0f, 1f), new Vector2(1880, 260), new Vector2(40, -132));

            // 游玩方框
            _playArea = UIFactory.CreateRect("PlayArea", root);
            UIFactory.Place(_playArea, new Vector2(0f, 1f), new Vector2(PlayAreaWidth, PlayAreaHeight), new Vector2(40, -420));
            var playBg = _playArea.gameObject.AddComponent<Image>();
            playBg.color = new Color(0.08f, 0.09f, 0.12f, 0.96f);
            playBg.raycastTarget = false;

            // 波形卡放置层（链条）
            _playCardsRoot = UIFactory.CreateRect("PlayCards", _playArea);
            FillLocal(_playCardsRoot, PlayAreaWidth, PlayAreaHeight);

            // 吸附线（拖拽落点预览，放在卡片层之上）
            _snapLine = UIFactory.CreatePanel("SnapLine", _playArea, new Color(1f, 0.9f, 0.3f, 0.9f));
            _snapLine.raycastTarget = false;
            UIFactory.Place((RectTransform)_snapLine.transform, new Vector2(0f, 0.5f), new Vector2(6, 400), new Vector2(0, 0));
            _snapLine.gameObject.SetActive(false);

            // 底部：契合% + 检查按钮 + 频谱条 + 状态
            _fitText = UIFactory.CreateText("Fit", root, "契合: -", 44, Color.white);
            UIFactory.Place(_fitText.rectTransform, new Vector2(0f, 0f), new Vector2(640, 68), new Vector2(40, 40));

            var checkBtn = UIFactory.CreateButton("Check", root, "▶ 检查", Check);
            UIFactory.Place((RectTransform)checkBtn.transform, new Vector2(1f, 0f), new Vector2(300, 96), new Vector2(-40, 40));

            BuildSpectrum(root);

            _statusText = UIFactory.CreateText("Status", root, "把角色拖进方框，按「检查」听契合度", 28, new Color(1f, 0.9f, 0.4f));
            UIFactory.Place(_statusText.rectTransform, new Vector2(0.5f, 0f), new Vector2(1400, 30), new Vector2(0, 8));

            // 最上层拖拽层（无 Graphic，不拦截射线）
            _dragRoot = UIFactory.CreateRect("DragRoot", root);
            UIFactory.Stretch(_dragRoot);

            // 全屏闪光层（放在最顶层，闪屏时覆盖所有 UI）
            _flash = UIFactory.CreatePanel("Flash", root, new Color(0f, 0f, 0f, 0f));
            UIFactory.Stretch((RectTransform)_flash.transform);
            _flash.raycastTarget = false;
        }

        private static void FillLocal(RectTransform rt, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = Vector2.zero;
        }

        /// <summary>底部一排竖长条（类音频频谱），跟随节奏「跳一下」。</summary>
        private void BuildSpectrum(Transform parent)
        {
            const float stripW = 700f, stripH = 120f, gap = 6f;
            float barW = (stripW - gap * (SpectrumBarCount - 1)) / SpectrumBarCount;

            var strip = UIFactory.CreateRect("Spectrum", parent);
            UIFactory.Place(strip, new Vector2(0.5f, 0f), new Vector2(stripW, stripH), new Vector2(0, 40));

            float startX = -stripW * 0.5f + barW * 0.5f;
            for (int i = 0; i < SpectrumBarCount; i++)
            {
                var bar = UIFactory.CreatePanel("Bar_" + i, strip, new Color(0.35f, 0.85f, 0.95f, 1f));
                bar.raycastTarget = false;
                var rt = (RectTransform)bar.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.sizeDelta = new Vector2(barW, SpectrumBaseH);
                rt.anchoredPosition = new Vector2(startX + i * (barW + gap), 0f);
                _spectrumBars.Add(rt);
            }
        }

        // ---------- 编排重建 ----------

        private void RebuildRoster()
        {
            foreach (Transform child in _rosterRoot) Destroy(child.gameObject);
            float x = 16f;
            foreach (var c in _roster)
            {
                var card = DraggableCard.Create(c, true, PixelsPerBeat, TileMaxHeight, MaxPitch);
                card.transform.SetParent(_rosterRoot, false);
                card.Rect.anchoredPosition = new Vector2(x, -_rosterRoot.rect.height * 0.5f);
                WireCard(card);
                x += card.Rect.sizeDelta.x + 16f;
            }
        }

        private void RebuildPlayArea()
        {
            foreach (Transform child in _playCardsRoot) Destroy(child.gameObject);
            _blocks.Clear();

            // 按当前链宽把波形居中：前后各留一拍留白，整体放进方框正中间
            float chainBeats = ChainTimeline.TotalBeats(_chain);
            float displayBeats = LeadInBeats + chainBeats + LeadInBeats;
            _laneOriginX = Mathf.Max(LaneLeft, (_playArea.rect.width - displayBeats * PixelsPerBeat) * 0.5f);

            float beats = 0f;
            foreach (var c in _chain)
            {
                var card = DraggableCard.Create(c, false, PixelsPerBeat, TileMaxHeight, MaxPitch);
                card.transform.SetParent(_playCardsRoot, false);
                card.Rect.anchoredPosition = new Vector2(_laneOriginX + (LeadInBeats + beats) * PixelsPerBeat, LaneCenterY);
                WireCard(card);

                foreach (var b in card.Blocks)
                    _blocks.Add(new CardBlock { Image = b.Image, Base = b.Base, Onset = beats + b.Onset, Duration = b.Duration, Pitch = b.Pitch });

                beats += ChainTimeline.TotalBeats(c);
            }
        }

        private void WireCard(DraggableCard card)
        {
            card.OnDragBegin = HandleBegin;
            card.OnDragging = HandleDrag;
            card.OnDragEnd = HandleEnd;
        }

        // ---------- 拖拽 ----------

        private void HandleBegin(DraggableCard card, Vector2 screenPos)
        {
            if (_playing) return;

            _dragCard = card;
            if (card.InRoster) _roster.Remove(card.Data);
            else _chain.Remove(card.Data);

            card.transform.SetParent(_dragRoot, true); // 提到最上层，保持屏幕位置

            Vector3 world;
            RectTransformUtility.ScreenPointToWorldPointInRectangle(_canvasRoot, screenPos, null, out world);
            _dragOffset = card.Rect.position - world;

            RebuildRoster();
            RebuildPlayArea();
        }

        private void HandleDrag(DraggableCard card, Vector2 screenPos)
        {
            if (_dragCard != card) return;

            Vector3 world;
            RectTransformUtility.ScreenPointToWorldPointInRectangle(_canvasRoot, screenPos, null, out world);
            card.Rect.position = world + _dragOffset;

            bool inPlay = RectTransformUtility.RectangleContainsScreenPoint(_playArea, screenPos, null);
            if (inPlay)
            {
                int index = Mathf.Clamp(ComputeInsertIndex(screenPos), 0, _chain.Count);
                _snapLine.gameObject.SetActive(true);
                _snapLine.rectTransform.anchoredPosition = new Vector2(InsertX(index), 0f);
            }
            else
            {
                _snapLine.gameObject.SetActive(false);
            }
        }

        private void HandleEnd(DraggableCard card, Vector2 screenPos)
        {
            if (_dragCard != card) { _dragCard = null; return; }

            bool inPlay = RectTransformUtility.RectangleContainsScreenPoint(_playArea, screenPos, null);
            if (inPlay)
            {
                int index = Mathf.Clamp(ComputeInsertIndex(screenPos), 0, _chain.Count);
                _chain.Insert(index, card.Data);
            }
            else
            {
                _roster.Add(card.Data);
            }

            _dragCard = null;
            _snapLine.gameObject.SetActive(false);
            Destroy(card.gameObject);

            RebuildRoster();
            RebuildPlayArea();
        }

        private int ComputeInsertIndex(Vector2 screenPos)
        {
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_playCardsRoot, screenPos, null, out local))
                return _chain.Count;

            float beats = 0f;
            int index = 0;
            foreach (var c in _chain)
            {
                float total = ChainTimeline.TotalBeats(c);
                float centerX = _laneOriginX + (LeadInBeats + beats) * PixelsPerBeat + total * PixelsPerBeat * 0.5f;
                if (local.x > centerX) index++;
                beats += total;
            }
            return index;
        }

        private float InsertX(int index)
        {
            float beats = 0f;
            for (int i = 0; i < index && i < _chain.Count; i++)
                beats += ChainTimeline.TotalBeats(_chain[i]);
            return _laneOriginX + (LeadInBeats + beats) * PixelsPerBeat;
        }

        // ---------- 检查 / 播放 ----------

        private void Check()
        {
            if (_playing) return;
            if (_chain.Count == 0) { SetStatus("先拖几个角色进方框吧"); return; }

            _notes = ChainTimeline.Flatten(_chain);
            _playing = true;
            _playStartTime = Time.time;
            _activeBlockIndex = -1;

            _bgm.Play();
            _fitText.text = "契合: 演出中…";
            SetStatus("演出中…… 屏幕闪烁就是你的节奏点");
        }

        /// <summary>内部播放头扫过每个节奏块起点时：闪屏 + 底部频谱条跳一下（播放头本身不可见）。</summary>
        private void UpdateRhythmFeedback(float beat)
        {
            int idx = FindActiveBlock(beat - LeadInBeats);
            if (idx == _activeBlockIndex) return;

            if (_activeBlockIndex >= 0 && _activeBlockIndex < _blocks.Count)
                _blocks[_activeBlockIndex].Image.color = _blocks[_activeBlockIndex].Base;
            _activeBlockIndex = idx;

            if (idx >= 0 && idx < _blocks.Count)
            {
                _blocks[idx].Image.color = Color.white; // 高亮当前 tile
                StartCoroutine(FlashBeat());
                StartCoroutine(BounceBars());
            }
        }

        private int FindActiveBlock(float localBeat)
        {
            for (int i = 0; i < _blocks.Count; i++)
            {
                var b = _blocks[i];
                if (localBeat >= b.Onset - 0.0001f && localBeat < b.Onset + b.Duration)
                    return i;
            }
            return -1;
        }

        private void FinishPlayback()
        {
            _playing = false;

            float score = _notes.Count == 0 ? 0f : RhythmScorer.Score(_notes);
            int pct = Mathf.RoundToInt(score * 100f);
            _fitText.text = "契合: " + pct + "%";

            if (score >= 0.95f) { SetStatus("完美合拍！全队锁拍"); StartCoroutine(Flash(new Color(0.2f, 1f, 0.4f, 0.35f))); }
            else if (score >= 0.7f) { SetStatus("大体合拍，个别走音"); StartCoroutine(Flash(new Color(1f, 0.9f, 0.3f, 0.3f))); }
            else { SetStatus("各弹各的…… 试试调整前后顺序"); StartCoroutine(Flash(new Color(1f, 0.4f, 0.4f, 0.3f))); }

            ResetBlocks();
        }

        private void ResetBlocks()
        {
            for (int i = 0; i < _blocks.Count; i++)
                _blocks[i].Image.color = _blocks[i].Base;
            _activeBlockIndex = -1;
            for (int i = 0; i < _spectrumBars.Count; i++)
                _spectrumBars[i].sizeDelta = new Vector2(_spectrumBars[i].sizeDelta.x, SpectrumBaseH);
        }

        private IEnumerator Flash(Color color)
        {
            _flash.color = color;
            float t = 0f;
            while (t < 0.8f)
            {
                t += Time.deltaTime;
                var c = _flash.color;
                c.a = Mathf.Lerp(color.a, 0f, t / 0.8f);
                _flash.color = c;
                yield return null;
            }
            _flash.color = new Color(0f, 0f, 0f, 0f);
        }

        /// <summary>节奏块起点的一下白色闪屏。</summary>
        private IEnumerator FlashBeat()
        {
            float t = 0f, dur = 0.18f;
            while (t < dur)
            {
                t += Time.deltaTime;
                _flash.color = new Color(1f, 1f, 1f, 0.16f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / dur)));
                yield return null;
            }
            _flash.color = new Color(0f, 0f, 0f, 0f);
        }

        /// <summary>底部频谱条整体「跳一下」：随机峰值顶起再回落。</summary>
        private IEnumerator BounceBars()
        {
            float[] peaks = new float[_spectrumBars.Count];
            for (int i = 0; i < peaks.Length; i++)
                peaks[i] = Random.Range(0.25f, 1f);

            float t = 0f, dur = 0.28f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / dur)); // 0 → 1 → 0
                for (int i = 0; i < _spectrumBars.Count; i++)
                {
                    float h = Mathf.Lerp(SpectrumBaseH, SpectrumMaxH, peaks[i] * k);
                    _spectrumBars[i].sizeDelta = new Vector2(_spectrumBars[i].sizeDelta.x, h);
                }
                yield return null;
            }

            for (int i = 0; i < _spectrumBars.Count; i++)
                _spectrumBars[i].sizeDelta = new Vector2(_spectrumBars[i].sizeDelta.x, SpectrumBaseH);
        }

        private void SetStatus(string msg)
        {
            _statusText.text = msg;
        }

        private void RunSelfCheck()
        {
            var good = new List<Character> { SampleData.Aning, SampleData.Shitou };
            float goodScore = RhythmScorer.Score(ChainTimeline.Flatten(good));
            Debug.Log("[波形小队] 自检 · [阿宁,石头] 契合 = " + (goodScore * 100f) + "% (应 100%)");

            var bad = new List<Character> { SampleData.Xiaoyu, SampleData.Mysterious };
            float badScore = RhythmScorer.Score(ChainTimeline.Flatten(bad));
            Debug.Log("[波形小队] 自检 · [小雨,神秘嘉宾] 契合 = " + (badScore * 100f) + "% (应低于 100%)");
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }
    }
}
