#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WaveTeam.Audio;
using WaveTeam.Core;
using WaveTeam.Room;

namespace WaveTeam.UI
{
    /// <summary>Editor/development-only mixed-wave workspace, isolated from chapter progress and scoring.</summary>
    public sealed class MelodyLab : MonoBehaviour
    {
        public const string ArrangementFile = "melody_lab_arrangement.json";
        public const string ArrangementId = "MelodyLab";
        private static MelodyLab _instance;
        private GameObject _root;
        private DrumTrackBoard _board;
        private PerformancePlayer _player;
        private BgmPlayer _bgm;
        private Text _status;
        private List<DrumCharacter> _waves;
        private NpcInteractor _interactor;
        private PlayerMovement _movement;
        private bool _interactorEnabled;
        private bool _movementEnabled;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (_instance != null) return;
            var go = new GameObject("MelodyLab");
            _instance = go.AddComponent<MelodyLab>();
            DontDestroyOnLoad(go);
        }

        public static void Open()
        {
            Initialize();
            if (_instance._root != null) return;
            var boot = FindObjectOfType<GameBootstrap>();
            if (boot == null || !boot.CanOpenMelodyLab)
            {
                Debug.Log("[Melody Lab] 请先关闭对话或音轨板，返回排练室再打开测试台。");
                return;
            }
            _instance.Build();
        }

        private void Build()
        {
            _interactor = FindObjectOfType<NpcInteractor>();
            _movement = FindObjectOfType<PlayerMovement>();
            _interactorEnabled = _interactor != null && _interactor.enabled;
            _movementEnabled = _movement != null && _movement.enabled;
            if (_interactor != null) _interactor.enabled = false;
            if (_movement != null) _movement.enabled = false;

            _root = new GameObject("MelodyLabCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _root.transform.SetParent(transform, false);
            var canvas = _root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            var scaler = _root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            if (EventSystem.current == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            _board = DrumTrackBoard.Create(_root.transform, 128f, 134f);
            var resetView = (RectTransform)_board.transform.Find("ResetView");
            resetView.anchoredPosition = new Vector2(24f, -136f);
            var sidebar = RosterSidebar.Create(_root.transform);
            _waves = MelodyPhrases.CreateTestWaves();
            foreach (var sample in DrumLibrary.Load())
            {
                if (sample.Definition.Type != WaveformType.Kick && sample.Definition.Type != WaveformType.Snare) continue;
                _waves.Add(new DrumCharacter(sample.Definition.Name, sample, RhythmPattern.FourOnFloor(),
                    HexCategory.Percussion, fixedRhythm: true));
            }

            var playerGo = new GameObject("MelodyLabPlayer");
            playerGo.transform.SetParent(_root.transform, false);
            _bgm = playerGo.AddComponent<BgmPlayer>();
            _player = playerGo.AddComponent<PerformancePlayer>();
            _player.Setup(_board, _bgm, useBackgroundMusic: false, playbackBpm: 120f);
            sidebar.Populate(_waves, PreviewWave, showTraits: false);
            _board.IsDropBlocked = pos => RectTransformUtility.RectangleContainsScreenPoint(sidebar.Rect, pos, null);
            foreach (var card in sidebar.Cards) _board.WireCard(card);
            _board.OnPreviewCharacter = PreviewWave;
            _board.OnArrangementChanged = () =>
            {
                _player.Stop();
                SetStatus("编排已更新。点击试听三轨，或双击片段单独试听。");
            };

            var title = UIFactory.CreateText("Title", _root.transform, "MELODY LAB · 开发测试台", 28, UIStyle.Accent);
            UIFactory.Place(title.rectTransform, new Vector2(0f, 1f),
                new Vector2(1500f, 42f), new Vector2(24f, -18f));
            title.raycastTarget = false;
            var help = UIFactory.CreateText("Help", _root.transform,
                "鼓、低音与旋律可放入任意 Layer。拖放、跨轨移动、右键收回、滚轮缩放沿用音轨板规则。\n键音和低音为实时生成的合成音色；120 BPM。Ctrl+Shift+M 打开，Esc 返回排练室。", 19, UIStyle.Text);
            UIFactory.Place(help.rectTransform, new Vector2(0f, 1f),
                new Vector2(1500f, 58f), new Vector2(24f, -63f));
            help.raycastTarget = false;

            var footer = UIFactory.CreatePanel("Controls", _root.transform, UIStyle.BgDeep);
            footer.rectTransform.anchorMin = Vector2.zero;
            footer.rectTransform.anchorMax = new Vector2(1f, 0f);
            footer.rectTransform.offsetMin = Vector2.zero;
            footer.rectTransform.offsetMax = new Vector2(-RosterSidebar.PanelWidth, 134f);
            AddButton(footer.transform, "Demo", "填入三轨示例", 0, FillDemo);
            AddButton(footer.transform, "Preview", "试听三轨", 1, PreviewBoard);
            AddButton(footer.transform, "Stop", "停止", 2, () => { _player.Stop(); SetStatus("已停止"); });
            AddButton(footer.transform, "Save", "保存测试编排", 3, Save);
            AddButton(footer.transform, "Load", "读取测试编排", 4, Load);
            AddButton(footer.transform, "Clear", "清空", 5, () => _board.Clear());
            AddButton(footer.transform, "Close", "返回排练室", 6, Close);
            _status = UIFactory.CreateText("Status", footer.transform, "", 20, UIStyle.Ok);
            UIFactory.Place(_status.rectTransform, new Vector2(0f, 0f),
                new Vector2(1510f, 38f), new Vector2(24f, 14f));
            _status.raycastTarget = false;
            SetStatus("先试听 Melody A / B / C 和 Bass，再把它们放到三轨中组合。");
        }

        private void AddButton(Transform parent, string name, string label, int index, Action action)
        {
            var button = UIFactory.CreateButton(name, parent, label, action);
            UIStyle.ApplyFrame(button.image, UIStyle.Grey);
            var text = button.GetComponentInChildren<Text>();
            text.fontSize = 19;
            text.raycastTarget = false;
            UIFactory.Place((RectTransform)button.transform, new Vector2(0f, 0f),
                new Vector2(190f, 44f), new Vector2(24f + index * 218f, 71f));
        }

        private void PreviewWave(DrumCharacter wave)
        {
            _player.PreviewCharacter(wave, _ => SetStatus("片段试听结束"));
            SetStatus("试听 · " + wave.Name + " · " + wave.Kind);
        }

        private void PreviewBoard()
        {
            _player.Stop();
            if (_board.Placed.Count == 0) { SetStatus("先拖入片段，再试听三轨编排。"); return; }
            _player.PreviewArrangement(_ => SetStatus("三轨试听结束"));
            SetStatus("试听当前三轨编排");
        }

        private void FillDemo()
        {
            _board.Clear();
            _board.Place(_waves[0], 0f, 0);
            _board.Place(_waves[3], 0f, 1);
            foreach (var wave in _waves)
                if (wave.Kind == WaveKind.Rhythm) { _board.Place(wave, 0f, 2); break; }
            SetStatus("示例 · Melody A + Bass + 鼓点。可以自行替换和移动。");
        }

        private void Save()
        {
            var data = _board.ToSaveData();
            data.challengeId = ArrangementId;
            data.bpm = _player.CurrentBpm;
            data.withoutBgm = true;
            SetStatus(RehearsalProgress.TryWrite(ArrangementFile, data, out _)
                ? "已保存到独立的 melody_lab_arrangement.json" : "测试编排保存失败");
        }

        private void Load()
        {
            string path = Path.Combine(Application.persistentDataPath, ArrangementFile);
            if (!File.Exists(path)) { SetStatus("还没有保存测试编排"); return; }
            try
            {
                var data = JsonUtility.FromJson<BoardSaveData>(File.ReadAllText(path));
                if (!Validate(data)) { SetStatus("测试编排无效，当前编排保留。"); return; }
                _board.Clear();
                RehearsalProgress.RestoreEdits(_board, data, DrumLibrary.Load());
                _player.Setup(_board, _bgm, useBackgroundMusic: false, playbackBpm: data.bpm);
                SetStatus("已恢复测试编排，包括音符、音色、轨道与时值。");
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
            {
                SetStatus("读取失败：" + e.Message);
            }
        }

        private static bool Validate(BoardSaveData data)
        {
            if (data == null || data.challengeId != ArrangementId || data.trackLengthBeats != 16 ||
                !(data.bpm >= 20f && data.bpm < 400f) || data.drums == null || data.drums.Count > 128) return false;
            var samples = DrumLibrary.Load();
            foreach (var clip in data.drums)
            {
                if (clip == null || clip.locked || clip.LaneIndex < 0 || clip.LaneIndex >= DrumTrackBoard.TrackCount ||
                    !(clip.startBeat >= 0f && clip.startBeat + clip.durationBeats <= 16f) ||
                    !RehearsalProgress.TryRestoreCharacter(clip, samples, out _)) return false;
                foreach (var other in data.drums)
                {
                    if (ReferenceEquals(clip, other) || other == null || clip.LaneIndex != other.LaneIndex) continue;
                    if (clip.startBeat < other.startBeat + other.durationBeats &&
                        clip.startBeat + clip.durationBeats > other.startBeat) return false;
                }
            }
            return true;
        }

        private void SetStatus(string text) { if (_status != null) _status.text = text; }

        public void Close()
        {
            if (_root == null) return;
            _player.Stop();
            _root.SetActive(false);
            Destroy(_root);
            _root = null;
            _board = null;
            _player = null;
            _status = null;
            _waves = null;
            if (_interactor != null) _interactor.enabled = _interactorEnabled;
            if (_movement != null) _movement.enabled = _movementEnabled;
        }

        private void Update()
        {
            if (_root != null && Input.GetKeyDown(KeyCode.Escape)) Close();
            if (_root == null && Input.GetKey(KeyCode.LeftControl) &&
                Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.M)) Open();
        }

        private void OnDestroy()
        {
            Close();
            if (_instance == this) _instance = null;
        }
    }
}
#endif
