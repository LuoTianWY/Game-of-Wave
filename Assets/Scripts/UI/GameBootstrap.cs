using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WaveTeam.Audio;
using WaveTeam.Core;
using WaveTeam.Room;

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
        private PixelText _statusText;

        private GameObject _roomRoot;   // 排练室入口 UI（「进入音轨板」按钮）
        private GameObject _boardRoot;  // 音轨板 UI 根（进入后所有元素，退出即销毁）
        private GameObject _resultPanel;

        private NpcInteractor _npcInteractor;    // 挂在 Player 上；跨场景会随之销毁，用「==null」判断重建
        private CharacterPanel _characterPanel;  // 当前打开的角色信息面板，同一时刻只允许一个

        private GameObject _objectiveRoot;
        private PixelText _objectiveText;
        private Button _enterBoardButton;

        private readonly HashSet<string> _talkedNpcs = new HashSet<string>();

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

            var title = UIFactory.PlacePixelText("Title", _roomRoot.transform, "排练室", 4f,
                UIStyle.Text, PixelText.Align.Center,
                new Vector2(0.5f, 1f), new Vector2(0, -34));
            title.raycastTarget = false;

            // 缩到右下角：房间本身才是这一屏的主体，按钮不该压在地板中间
            _enterBoardButton = UIFactory.CreatePixelButton("EnterBoard", _roomRoot.transform, "进入音轨板", 1.8f,
                UIStyle.Accent, UIStyle.Ink, OnEnterBoard);
            UIResource.ApplyFrameOrColor(_enterBoardButton.image, "UI/btn_play", UIStyle.Accent);
            var bRt = (RectTransform)_enterBoardButton.transform;
            bRt.anchorMin = bRt.anchorMax = bRt.pivot = new Vector2(1f, 0f);
            bRt.sizeDelta = new Vector2(200, 56);
            bRt.anchoredPosition = new Vector2(-24, 24);
           
            RefreshEnterBoardState();
            EnsureRoomCharacters();
            EnsurePlayerAvatar();
            BuildObjectiveUI();
        }

        /// <summary>
        /// 主角像素小人的运行时兜底：即使场景里还是旧版那个橙色方块，
        /// 进游戏也会被换成带行走动画的小人（贴图和组件都在这里补上，不用重建场景）。
        /// </summary>
        private void EnsurePlayerAvatar()
        {
            var player = GameObject.Find("Player");
            if (player == null) return;
            PlayerAnimator.EnsureOn(player);
        }



        private void BuildObjectiveUI()
        {
            if (_objectiveRoot != null) return;

            _objectiveRoot = new GameObject(
                "ObjectiveUI",
                typeof(RectTransform));

            _objectiveRoot.transform.SetParent(
                _canvas.transform,
                false);

            var rt = (RectTransform)_objectiveRoot.transform;

            rt.anchorMin = rt.anchorMax = rt.pivot =
                new Vector2(0f, 1f);

            rt.sizeDelta = new Vector2(420f, 240f);
            rt.anchoredPosition = new Vector2(30f, -30f);

            var bg = UIFactory.CreatePanel(
                "Background",
                _objectiveRoot.transform,
                new Color(UIStyle.BgDeep.r, UIStyle.BgDeep.g, UIStyle.BgDeep.b, 0.84f));

            UIFactory.Stretch((RectTransform)bg.transform);

            var title = PixelText.Create(
                "Title",
                _objectiveRoot.transform,
                "排练准备",
                2f,
                UIStyle.Accent);

            UIFactory.Place(
                (RectTransform)title.transform,
                new Vector2(0f, 1f),
                title.PreferredSize,
                new Vector2(20f, -20f));

            _objectiveText = PixelText.Create(
                "Objectives",
                _objectiveRoot.transform,
                "□ 和阿宁聊天\n□ 和小川聊天\n□ 和石头聊天\n□ 开始排练",
                1.5f,
                Color.white);

            UIFactory.Place(
                (RectTransform)_objectiveText.transform,
                new Vector2(0f, 1f),
                _objectiveText.PreferredSize,
                new Vector2(20f, -75f));
        }
        private bool AreRequiredNpcsTalked()
        {
            return _talkedNpcs.Contains("阿宁")
                && _talkedNpcs.Contains("小川")
                && _talkedNpcs.Contains("石头");
        }

        private void RefreshEnterBoardState()
        {
            if (_enterBoardButton == null) return;

            bool ready = AreRequiredNpcsTalked();

            _enterBoardButton.interactable = ready;
        }


        // ---------- 排练室里的角色 ----------

        /// <summary>
        /// 放入角色并接上互动。GameBootstrap 是跨场景存活的（DontDestroyOnLoad），
        /// 但角色和 Player 都随场景销毁，所以这里靠「已销毁的 Unity 对象 == null」
        /// 来判断要不要重建 —— 换个场景回来会自动重来一批，不会叠人。
        /// </summary>
        private void EnsureRoomCharacters()
        {
            if (_npcInteractor != null) return;

            _npcInteractor = RehearsalRoomSpawner.Spawn();
            if (_npcInteractor == null) return;
            _npcInteractor.OnInteract = OpenCharacterPanel;
        }

        /// <summary>按 F 命中角色时打开信息面板，同时把互动锁住（收提示、停走动、不再响应 F）。</summary>
        private void OpenCharacterPanel(NpcProfile profile)
        {
            if (_characterPanel != null) return;

            if (_npcInteractor != null)
                _npcInteractor.SetLocked(true);

            _characterPanel = CharacterPanel.Open(
                _canvas.transform,
                profile,
                OnCharacterPanelClosed,
                OnNpcTalked);
        }

        private void OnNpcTalked(NpcProfile profile)
        {
            if (profile == null) return;

            _talkedNpcs.Add(profile.Name);

            RefreshObjectiveUI();
            RefreshEnterBoardState();
        }

        private void RefreshObjectiveUI()
        {
            if (_objectiveText == null) return;

            bool aning = _talkedNpcs.Contains("阿宁");
            bool xiaochuan = _talkedNpcs.Contains("小川");
            bool shitou = _talkedNpcs.Contains("石头");

            bool allTalked = aning && xiaochuan && shitou;

            _objectiveText.Text =
                (aning ? "[X]" : "[ ]") + " 和阿宁聊天\n" +
                (xiaochuan ? "[X]" : "[ ]") + " 和小川聊天\n" +
                (shitou ? "[X]" : "[ ]") + " 和石头聊天\n" +
                (allTalked ? "[X]" : "[ ]") + " 开始排练";
        }
        private void OnCharacterPanelClosed()
        {
            _characterPanel = null;
            if (_npcInteractor != null) _npcInteractor.SetLocked(false);
        }

        private void OnEnterBoard()
        {
            if (_roomRoot != null) _roomRoot.SetActive(false);
            // 音轨板开着的时候不能再让 F 去开角色面板：那边整屏被音轨板盖住，
            // 玩家看不见是谁被选中，但按 F 仍然会命中最近的角色。
            if (_npcInteractor != null) _npcInteractor.SetLocked(true);
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
            if (_npcInteractor != null) _npcInteractor.SetLocked(false);
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
            UIResource.ApplySpriteOrColor(bg, "UI/bg_canvas", UIStyle.BgDeep);

            _board = DrumTrackBoard.Create(_boardRoot.transform);
            _sidebar = RosterSidebar.Create(_boardRoot.transform);

            BuildRehearsalGoalPanel();

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

                // 落点虚影要排除右侧栏：拖回侧栏时不显示虚影、也不落位
                _board.IsDropBlocked = pos =>
                    RectTransformUtility.RectangleContainsScreenPoint(_sidebar.Rect, pos, null);
                foreach (var card in _sidebar.Cards)
                    _board.WireCard(card);
            }

            BuildPlayback();

            if (samples.Count == 0)
                _statusText.Text = "未找到鼓音色：请把 ogg 放进 Assets/Resources/Audio/drums（见波形映射表.md）";
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

        private void BuildRehearsalGoalPanel()
        {
            // 左上角：本轮排练目标
            var panel = UIFactory.CreatePanel(
                "RehearsalGoalPanel",
                _boardRoot.transform,
                new Color(0.10f, 0.10f, 0.10f, 0.82f)
            );

            var rt = panel.rectTransform;

            rt.anchorMin = rt.anchorMax = rt.pivot =
                new Vector2(0f, 1f);

            rt.sizeDelta = new Vector2(380f, 165f);

            rt.anchoredPosition =
                new Vector2(20f, -150f);


            // 标题
            var title = UIFactory.CreateText(
                "GoalTitle",
                panel.transform,
                "本轮排练目标",
                24,
                UIStyle.Text,
                TextAnchor.MiddleLeft
            );

            title.raycastTarget = false;

            var titleRt = (RectTransform)title.transform;

            titleRt.anchorMin = titleRt.anchorMax = titleRt.pivot =
                new Vector2(0f, 1f);

            titleRt.sizeDelta = new Vector2(340f, 35f);

            titleRt.anchoredPosition =
                new Vector2(18f, -15f);


            // 目标内容
            var goals = UIFactory.CreateText(
                "GoalContent",
                panel.transform,
                "开场：需要稳定\n发展：增加层次\n整体：避免过度拥挤",
                20,
                UIStyle.Text,
                TextAnchor.UpperLeft
            );

            goals.raycastTarget = false;

            var goalsRt = (RectTransform)goals.transform;

            goalsRt.anchorMin = goalsRt.anchorMax = goalsRt.pivot =
                new Vector2(0f, 1f);

            goalsRt.sizeDelta = new Vector2(340f, 105f);

            goalsRt.anchoredPosition =
                new Vector2(18f, -55f);
        }

        private void BuildPlayback()
        {
            // 顶部提示条：一条木头条 + 居中像素字（原来是直接浮在背景上的系统字）
            var header = UIFactory.CreatePanel("Header", _boardRoot.transform, UIStyle.Stage);
            header.raycastTarget = false;
            UIResource.ApplyFrameOrColor(header, "UI/header", UIStyle.Stage);
            var hRt = (RectTransform)header.transform;
            hRt.anchorMin = new Vector2(0f, 1f);
            hRt.anchorMax = new Vector2(1f, 1f);
            hRt.pivot = new Vector2(0.5f, 1f);
            // 宽度要扣掉右侧角色栏那 320px，否则顶栏会盖住侧栏的「角色」标题
            hRt.sizeDelta = new Vector2(-RosterSidebar.PanelWidth, HeaderHeight);
            hRt.anchoredPosition = Vector2.zero;

            _statusText = UIFactory.PlacePixelText("Status", _boardRoot.transform,
                "把右侧角色拖到鼓轨上（同轨不重叠）· 左键拖动移动 · 右键收回", 2f,
                UIStyle.Text, PixelText.Align.Center,
                new Vector2(0.5f, 1f), new Vector2(0f, -24f));
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


            _perf.OnFinished = msg =>
            {
                if (_board == null || _board.Placed.Count == 0)
                {
                    _statusText.Text = msg;
                    return;
                }

                var result = RehearsalEvaluator.Evaluate(_board);

                _statusText.Text = "排练评分：" + result.Score;

                if (_resultPanel != null)
                    Destroy(_resultPanel);

                _resultPanel = RehearsalResultPanel.Show(
                    _boardRoot.transform,
                    result,

                    // 重新编排
                    () =>
                    {
                        _resultPanel = null;
                        _statusText.Text = "调整波形后再次演出";
                    },

                    // 返回排练室
                    () =>
                    {
                        _resultPanel = null;
                        OnExitBoard();
                    }
                );
            };


            BuildBottomBar();
        }

        private const float HeaderHeight = 74f;   // 顶部提示条高度
        private const float BottomBarHeight = 72f; // 底部工具栏高度

        /// <summary>
        /// 底部一条通栏木制工具栏，按钮全部垂直居中对齐：
        /// 左：确定 / 取消；中：演出！/ 停止；右：退出。
        /// 原来是五个按钮散在三个角落、高度还不一致，主次分不出来。
        /// </summary>
        private void BuildBottomBar()
        {
            var bar = UIFactory.CreatePanel("BottomBar", _boardRoot.transform, UIStyle.Stage);
            bar.raycastTarget = false;
            UIResource.ApplyFrameOrColor(bar, "UI/header", UIStyle.Stage);
            var brt = (RectTransform)bar.transform;
            brt.anchorMin = new Vector2(0f, 0f);
            brt.anchorMax = new Vector2(1f, 0f);
            brt.pivot = new Vector2(0.5f, 0f);
            brt.sizeDelta = new Vector2(-RosterSidebar.PanelWidth, BottomBarHeight);
            brt.anchoredPosition = Vector2.zero;

            // 次要按钮：44 高，垂直居中
            float y = (BottomBarHeight - 44f) * 0.5f;
            var confirm = UIFactory.CreatePixelButton("Confirm", _boardRoot.transform, "确定", 1.8f,
                UIStyle.Ok, UIStyle.Ink, OnConfirm);
            UIResource.ApplyFrameOrColor(confirm.image, "UI/btn_confirm", UIStyle.Ok);
            UIFactory.Place((RectTransform)confirm.transform, new Vector2(0f, 0f),
                new Vector2(120f, 44f), new Vector2(16f, y));

            var cancel = UIFactory.CreatePixelButton("Cancel", _boardRoot.transform, "取消", 1.8f,
                UIStyle.Grey, UIStyle.Text, OnCancel);
            UIResource.ApplyFrameOrColor(cancel.image, "UI/btn_cancel", UIStyle.Grey);
            UIFactory.Place((RectTransform)cancel.transform, new Vector2(0f, 0f),
                new Vector2(120f, 44f), new Vector2(150f, y));

            var exit = UIFactory.CreatePixelButton("Exit", _boardRoot.transform, "退出", 1.8f,
                UIStyle.Grey, UIStyle.Text, OnExitBoard);
            UIResource.ApplyFrameOrColor(exit.image, "UI/btn_cancel", UIStyle.Grey);
            UIFactory.Place((RectTransform)exit.transform, new Vector2(1f, 0f),
                new Vector2(120f, 44f), new Vector2(-(RosterSidebar.PanelWidth + 16f), y));

            // 主操作：56 高，垂直居中
            float y2 = (BottomBarHeight - 56f) * 0.5f;
            var play = UIFactory.CreatePixelButton("Play", _boardRoot.transform, "演出！", 2.4f,
                UIStyle.Accent, UIStyle.Ink, OnPlay);
            UIResource.ApplyFrameOrColor(play.image, "UI/btn_play", UIStyle.Accent);
            UIFactory.Place((RectTransform)play.transform, new Vector2(0.5f, 0f),
                new Vector2(240f, 56f), new Vector2(-170f, y2));

            var stop = UIFactory.CreatePixelButton("Stop", _boardRoot.transform, "停止", 2.4f,
                UIStyle.Danger, UIStyle.Text, OnStop);
            UIResource.ApplyFrameOrColor(stop.image, "UI/btn_cancel", UIStyle.Danger);
            UIFactory.Place((RectTransform)stop.transform, new Vector2(0.5f, 0f),
                new Vector2(120f, 56f), new Vector2(80f, y2));
        }

        private void OnPlay()
        {
            if (_perf.IsPlaying) return;
            _statusText.Text = "演出中……";
            _perf.Play();
        }

        private void OnStop()
        {
            if (_perf != null) _perf.Stop();
            if (_bgm != null) _bgm.Stop();
            _statusText.Text = "已停止";
        }

        private void OnConfirm()
        {
            var data = _board.ToSaveData();
            string json = JsonUtility.ToJson(data, true);
            string path = Path.Combine(Application.persistentDataPath, "drum_arrangement.json");
            File.WriteAllText(path, json);


            Debug.Log("[波形小队] 已确定，编排已保存：\n" + json);
            _statusText.Text = "已确定 · 已保存 " + data.drums.Count + " 个鼓 → " + path;
        }

        private void OnCancel()
        {
            _board.Clear();
            _statusText.Text = "已取消（鼓轨已清空）";
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
