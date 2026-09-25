using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WaveTeam.Audio;

namespace WaveTeam.UI
{
    /// <summary>
    /// 鼓组原型入口：启动默认停在「排练室」场景几何，屏幕只叠一个「进入音轨板」按钮；
    /// 点按钮搭出鼓轨板 + 右侧角色栏 + 演出/确定/取消/退出，退出后回到排练室。
    /// 按下 Play 即自动运行。节奏基准 120 BPM（见 BgmPlayer）。
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        private DrumTrackBoard _board;
        private RosterSidebar _sidebar;
        private Canvas _canvas;
        private PerformancePlayer _perf;
        private BgmPlayer _bgm;
        private Text _statusText;

        private GameObject _roomRoot;   // 排练室入口 UI（「进入音轨板」按钮）
        private GameObject _boardRoot;  // 音轨板 UI 根（进入后所有元素，退出即销毁）

        private const string RoomScene = "排练室";
        private static bool _started;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (_started) return;
            _started = true;
            var go = new GameObject("GameBootstrap");
            DontDestroyOnLoad(go);
            go.AddComponent<GameBootstrap>();
        }

        private void Start()
        {
            // 启动场景不是排练室（空场景 / SampleScene 等）→ 切到排练室后再初始化；
            // GameBootstrap 已 DontDestroyOnLoad 跨场景存活，切完由 sceneLoaded 回调继续。
            if (SceneManager.GetActiveScene().name != RoomScene)
            {
                SceneManager.sceneLoaded += OnSceneLoaded;
                SceneManager.LoadScene(RoomScene);
                return;
            }
            Initialize();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != RoomScene) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Initialize();
        }

        private void Initialize()
        {
            _canvas = BuildCanvas();
            EnsureEventSystem();
            ShowRoomEntry();
        }

        // ---------- 排练室（入口） ----------

        private void ShowRoomEntry()
        {
            if (_roomRoot != null) { _roomRoot.SetActive(true); return; }

            _roomRoot = new GameObject("RoomEntry", typeof(RectTransform));
            _roomRoot.transform.SetParent(_canvas.transform, false);
            UIFactory.Stretch((RectTransform)_roomRoot.transform);

            var title = UIFactory.CreateText("Title", _roomRoot.transform, "排练室", 48,
                UIStyle.Text, TextAnchor.MiddleCenter);
            UIStyle.OutlineText(title);
            var tRt = (RectTransform)title.transform;
            tRt.anchorMin = tRt.anchorMax = tRt.pivot = new Vector2(0.5f, 1f);
            tRt.sizeDelta = new Vector2(600, 64);
            tRt.anchoredPosition = new Vector2(0, -40);
            title.raycastTarget = false;

            var btn = UIFactory.CreateButton("EnterBoard", _roomRoot.transform, "进入音轨板", OnEnterBoard);
            UIStyle.ApplyRound(btn.image, UIStyle.Accent);
            UIStyle.OutlineText(btn.GetComponentInChildren<Text>());
            var bRt = (RectTransform)btn.transform;
            bRt.anchorMin = bRt.anchorMax = bRt.pivot = new Vector2(0.5f, 0.5f);
            bRt.sizeDelta = new Vector2(320, 80);
            bRt.anchoredPosition = Vector2.zero;
        }

        private void OnEnterBoard()
        {
            if (_roomRoot != null) _roomRoot.SetActive(false);
            BuildBoard();
        }

        private void OnExitBoard()
        {
            // 退出前中止 BGM 与演出；播放器组件复用不销毁（避免累积 AudioSource）
            if (_perf != null) _perf.Stop();
            if (_bgm != null) _bgm.Stop();
            if (_boardRoot != null) { Destroy(_boardRoot); _boardRoot = null; }
            _board = null;
            _sidebar = null;
            _statusText = null;
            ShowRoomEntry();
        }

        // ---------- 音轨板 ----------

        private void BuildBoard()
        {
            if (_boardRoot != null) return;

            _boardRoot = new GameObject("TrackBoard", typeof(RectTransform));
            _boardRoot.transform.SetParent(_canvas.transform, false);
            UIFactory.Stretch((RectTransform)_boardRoot.transform);

            // 舞台兜底背景：只在音轨板内显示，盖住排练室
            var bg = UIFactory.CreatePanel("Background", _boardRoot.transform, UIStyle.BgDeep);
            bg.raycastTarget = false;
            UIFactory.Stretch((RectTransform)bg.transform);

            _board = DrumTrackBoard.Create(_boardRoot.transform);
            _sidebar = RosterSidebar.Create(_boardRoot.transform);

            var samples = DrumLibrary.Load();
            if (samples.Count == 0)
            {
                Debug.LogWarning("[波形小队] 未在 Resources/Audio/drums 下找到可识别的鼓音色，请先导入 ogg（见波形映射表.md）");
            }
            else
            {
                var characters = Roster.Build(Roster.DefaultNames, samples);
                _sidebar.Populate(characters);
                Debug.Log("[波形小队] 已加载鼓音色 " + samples.Count + " 个，随机分队 " + characters.Count + " 名角色");

                foreach (var card in _sidebar.Cards)
                    card.OnDragEnd = (c, pos) => TryDrop(c, pos);
            }

            BuildPlayback();

            if (samples.Count == 0)
                _statusText.text = "未找到鼓音色：请把 ogg 放进 Assets/Resources/Audio/drums（见波形映射表.md）";
        }

        private bool TryDrop(DrumCharacterCard card, Vector2 screenPos)
        {
            if (RectTransformUtility.RectangleContainsScreenPoint(_sidebar.Rect, screenPos, null))
                return false;
            return _board.TryPlaceFromCard(card, screenPos);
        }

        private static Canvas BuildCanvas()
        {
            var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0f;
            return canvas;
        }

        private void BuildPlayback()
        {
            _statusText = UIFactory.CreateText("Status", _boardRoot.transform,
                "把右侧角色拖到鼓轨上（同轨不重叠）· 左键拖动移动 · 右键收回", 26,
                UIStyle.Text, TextAnchor.MiddleCenter);
            UIStyle.OutlineText(_statusText);
            var stRt = (RectTransform)_statusText.transform;
            stRt.anchorMin = stRt.anchorMax = stRt.pivot = new Vector2(0.5f, 1f);
            stRt.sizeDelta = new Vector2(1400, 40);
            stRt.anchoredPosition = new Vector2(0, -20);
            _statusText.raycastTarget = false;

            // BGM / 演出播放器跨「进入-退出」复用，避免重复 AddComponent 累积 AudioSource
            if (_bgm == null) _bgm = gameObject.AddComponent<BgmPlayer>();
            if (_perf == null)
            {
                var perfGo = new GameObject("PerformancePlayer");
                perfGo.transform.SetParent(transform, false);
                _perf = perfGo.AddComponent<PerformancePlayer>();
            }
            _perf.Setup(_board, _bgm);
            _perf.OnFinished = msg => _statusText.text = msg;

            var btn = UIFactory.CreateButton("Play", _boardRoot.transform, "演出！", OnPlay);
            UIStyle.ApplyRound(btn.image, UIStyle.Accent);
            UIStyle.OutlineText(btn.GetComponentInChildren<Text>());
            var rt = (RectTransform)btn.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(240, 64);
            rt.anchoredPosition = new Vector2(0, 20);

            var stop = UIFactory.CreateButton("Stop", _boardRoot.transform, "停止", OnStop);
            UIStyle.ApplyRound(stop.image, new Color(0.72f, 0.32f, 0.32f, 1f));
            UIStyle.OutlineText(stop.GetComponentInChildren<Text>());
            var srt = (RectTransform)stop.transform;
            srt.anchorMin = srt.anchorMax = srt.pivot = new Vector2(0.5f, 0f);
            srt.sizeDelta = new Vector2(120, 64);
            srt.anchoredPosition = new Vector2(200, 20);

            var confirm = UIFactory.CreateButton("Confirm", _boardRoot.transform, "确定", OnConfirm);
            UIStyle.ApplyRound(confirm.image, UIStyle.Ok);
            UIStyle.OutlineText(confirm.GetComponentInChildren<Text>());
            var crt = (RectTransform)confirm.transform;
            crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0f, 0f);
            crt.sizeDelta = new Vector2(120, 44);
            crt.anchoredPosition = new Vector2(16, 16);

            var cancel = UIFactory.CreateButton("Cancel", _boardRoot.transform, "取消", OnCancel);
            UIStyle.ApplyRound(cancel.image, new Color(0.30f, 0.33f, 0.40f, 1f));
            UIStyle.OutlineText(cancel.GetComponentInChildren<Text>());
            var xrt = (RectTransform)cancel.transform;
            xrt.anchorMin = xrt.anchorMax = xrt.pivot = new Vector2(0f, 0f);
            xrt.sizeDelta = new Vector2(120, 44);
            xrt.anchoredPosition = new Vector2(150, 16);

            var exit = UIFactory.CreateButton("Exit", _boardRoot.transform, "退出", OnExitBoard);
            UIStyle.ApplyRound(exit.image, new Color(0.30f, 0.33f, 0.40f, 1f));
            UIStyle.OutlineText(exit.GetComponentInChildren<Text>());
            var ert = (RectTransform)exit.transform;
            ert.anchorMin = ert.anchorMax = ert.pivot = new Vector2(1f, 0f);
            ert.sizeDelta = new Vector2(120, 44);
            ert.anchoredPosition = new Vector2(-16, 16);
        }

        private void OnPlay()
        {
            if (_perf.IsPlaying) return;
            _statusText.text = "演出中……";
            _perf.Play();
        }

        private void OnStop()
        {
            if (_perf != null) _perf.Stop();
            if (_bgm != null) _bgm.Stop();
            _statusText.text = "已停止";
        }

        private void OnConfirm()
        {
            var data = _board.ToSaveData();
            string json = JsonUtility.ToJson(data, true);
            string path = Path.Combine(Application.persistentDataPath, "drum_arrangement.json");
            File.WriteAllText(path, json);
            Debug.Log("[波形小队] 已确定，编排已保存：\n" + json);
            _statusText.text = "已确定 · 已保存 " + data.drums.Count + " 个鼓 → " + path;
        }

        private void OnCancel()
        {
            _board.Clear();
            _statusText.text = "已取消（鼓轨已清空）";
        }

        private void Update()
        {
            if (_board == null) return; // 排练室视图下无键盘操作

            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            if (ctrl && Input.GetKeyDown(KeyCode.S))
            {
                OnConfirm();
            }
            else if (ctrl && Input.GetKeyDown(KeyCode.Z))
            {
                _board.Undo();
            }
            else if (!ctrl && Input.GetKeyDown(KeyCode.Escape))
            {
                OnCancel();
            }
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }
    }
}
