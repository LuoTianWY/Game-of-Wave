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
                new Vector2(0.5f, 1f);

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

            // 暂时放开「先和三位 NPC 聊天」的门槛：按钮随时可点，直接进音轨板。
            // 任务清单保留为提示；要恢复门槛，把这里改回
            // `_enterBoardButton.interactable = AreRequiredNpcsTalked();`
            _enterBoardButton.interactable = true;
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

            Debug.Log("[目标] 打开角色面板：" + (profile != null ? profile.Name : "(null)"));

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
            Debug.Log("[目标] 已和「" + profile.Name + "」聊天，当前已完成：" +
                      string.Join("、", _talkedNpcs));

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
            BuildRehearsalGoalPanel();   // 目标横幅要压在标题条上方，所以放在 Playback 之后建

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
            // 顶部左侧：本轮排练目标（收成一条窄横幅，放在标题条下方，避免压住音轨标尺）
            var panel = UIFactory.CreatePanel(
                "RehearsalGoalPanel",
                _boardRoot.transform,
                new Color(0.12f, 0.17f, 0.16f, 0.92f)
            );

            var rt = panel.rectTransform;

            rt.anchorMin = rt.anchorMax = rt.pivot =
                new Vector2(0f, 1f);

            rt.sizeDelta = new Vector2(700f, 70f);

            rt.anchoredPosition =
                new Vector2(0f, -84f);     // 横向居中，高度仍和「回起点」同一行


            // 标题（沿用系统字：这几个字的像素字形不全，用系统字不会缺笔画）
            var title = UIFactory.CreateText(
                "GoalTitle", panel.transform, "本轮排练目标", 20, UIStyle.Accent,
                TextAnchor.MiddleLeft);
            title.raycastTarget = false;
            var titleRt = (RectTransform)title.transform;
            titleRt.anchorMin = titleRt.anchorMax = titleRt.pivot = new Vector2(0f, 1f);
            titleRt.sizeDelta = new Vector2(150f, 56f);
            titleRt.anchoredPosition = new Vector2(14f, -8f);


            // 目标内容
            var goals = UIFactory.CreateText(
                "GoalContent", panel.transform,
                "开场：需要稳定\n发展：增加层次\n整体：避免过度拥挤", 16,
                UIStyle.Text, TextAnchor.UpperLeft);
            goals.raycastTarget = false;
            var goalsRt = (RectTransform)goals.transform;
            goalsRt.anchorMin = goalsRt.anchorMax = goalsRt.pivot = new Vector2(0f, 1f);
            goalsRt.sizeDelta = new Vector2(510f, 62f);
            goalsRt.anchoredPosition = new Vector2(175f, -4f);
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
            hRt.pivot = new Vector2(0f, 1f);              // 同样靠左，别压住角色栏标题
            // 宽度要扣掉右侧角色栏那 320px，否则顶栏会盖住侧栏的「角色」标题
            hRt.sizeDelta = new Vector2(-RosterSidebar.PanelWidth, HeaderHeight);
            hRt.anchoredPosition = Vector2.zero;

            // 顶栏提示：状态文字挪到下方调音台的屏幕里了，这里只留操作说明
            var hint = UIFactory.PlacePixelText("Hints", _boardRoot.transform,
                "拖到鼓轨上 · 左键移动 · 右键收回", 1.5f,
                UIStyle.Text, PixelText.Align.Left,
                new Vector2(0f, 1f), new Vector2(20f, -14f));
            hint.raycastTarget = false;

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
        private const float ConsoleHeight = 280f;  // 底部调音台机器高度

        /// <summary>
        /// 底部「排练室调音台」：一台像素风的调试机器。
        /// 机器上半部分是状态屏幕，下半部面板上散布着形状各异的按钮
        /// （方键确定/取消、圆形主键演出！、圆形红键停止、右侧窄条退出），
        /// 再点缀旋钮、LED 灯和螺丝，像个真的工作室设备。
        /// </summary>
        private void BuildBottomBar()
        {
            const float PlatePad = 14f;
            const float ClusterX = 90f;    // 比贴左再多留一点呼吸位

            // 机器底板：优先贴控制台背景图（Assets/Resources/UI/mixer_console.png），
            // 没有图时退回程序化木框。图贴上去后，面板上的按钮/屏幕/旋钮仍由程序绘制，
            // 所以即梦出的图只需要负责「机器外观看上去很酷」，不用画按钮位置。
            var plate = UIFactory.CreatePanel("MixerConsole", _boardRoot.transform, UIStyle.Stage);
            plate.raycastTarget = false;
            var consoleArt = UIResource.Load("UI/mixer_console");
            if (consoleArt != null)
            {
                plate.sprite = consoleArt;
                plate.type = Image.Type.Simple;
                plate.preserveAspect = false;
                plate.color = Color.white;
            }
            else
            {
                UIResource.ApplyFrameOrColor(plate, "UI/header", UIStyle.Stage);
            }
            var prt = (RectTransform)plate.transform;
            prt.anchorMin = new Vector2(0f, 0f);
            prt.anchorMax = new Vector2(1f, 0f);
            prt.pivot = new Vector2(0f, 0f);              // 靠左贴齐，右缘正好停在角色栏左边
            prt.sizeDelta = new Vector2(-RosterSidebar.PanelWidth, ConsoleHeight);
            prt.anchoredPosition = Vector2.zero;

            // 内嵌的暗色操作面板（有背景图时做成半透明，让图透出来）
            var face = UIFactory.CreatePanel("Faceplate", plate.transform,
                consoleArt != null
                    ? new Color(0f, 0f, 0f, 0.18f)
                    : UIStyle.Cell);
            face.raycastTarget = false;
            if (consoleArt == null)
                UIResource.ApplyWellOrColor(face, "UI/cell_slot", UIStyle.Cell);
            var frt = (RectTransform)face.transform;
            // 横向必须用「拉伸锚点」：anchorMin.x=0 / anchorMax.x=1，
            // 这样 sizeDelta.x = -28 才是「左右各缩 14」，面板宽度 = 控制台宽 - 28。
            // 之前这里用的是点锚点（0.5,0），sizeDelta 被当成实际宽度，算出来是 -28，
            // 整块面板退化成一条线，子元素全部被推到屏幕中间偏右。
            frt.anchorMin = new Vector2(0f, 0f);
            frt.anchorMax = new Vector2(1f, 0f);
            frt.pivot = new Vector2(0.5f, 0f);
            frt.sizeDelta = new Vector2(-PlatePad * 2f, ConsoleHeight - PlatePad * 2f);
            frt.anchoredPosition = new Vector2(0f, PlatePad);

            // ---------- 状态屏幕（机器上方） ----------
            var screen = UIFactory.CreatePanel("Monitor", face.transform,
                new Color(0.08f, 0.11f, 0.09f, 1f));
            screen.raycastTarget = false;
            UIStyle.ApplyWell(screen, new Color(0.08f, 0.11f, 0.09f, 1f));
            var srt = (RectTransform)screen.transform;
            srt.anchorMin = srt.anchorMax = srt.pivot = new Vector2(0f, 1f);
            srt.sizeDelta = new Vector2(520f, 84f);
            srt.anchoredPosition = new Vector2(ClusterX, -18f);

            var screenTag = UIFactory.PlacePixelText("ScreenTag", screen.transform, "STATUS", 1.2f,
                UIStyle.TextDim, PixelText.Align.Left,
                new Vector2(0f, 1f), new Vector2(12f, -8f));
            screenTag.raycastTarget = false;

            _statusText = UIFactory.PlacePixelText("ScreenText", screen.transform, "准备演出", 2f,
                UIStyle.Ok, PixelText.Align.Left,
                new Vector2(0f, 0.5f), new Vector2(12f, 0f));
            _statusText.raycastTarget = false;

            // 屏幕细节：三条扫描线 + 右上角录制指示灯
            for (int i = 0; i < 3; i++)
            {
                var line = UIFactory.CreatePanel("Scan_" + i, screen.transform,
                    new Color(1f, 1f, 1f, 0.04f));
                line.raycastTarget = false;
                UIFactory.Place((RectTransform)line.transform, new Vector2(0f, 1f),
                    new Vector2(500f, 1f), new Vector2(10f, -20f - i * 22f));
            }

            var rec = UIFactory.CreatePanel("Rec", screen.transform, UIStyle.Danger);
            rec.raycastTarget = false;
            rec.sprite = MakeCircleSprite(10, UIStyle.Danger, Color.clear);
            rec.color = Color.white;
            UIFactory.Place((RectTransform)rec.transform, new Vector2(1f, 1f),
                new Vector2(10f, 10f), new Vector2(-16f, -12f));

            // 屏幕四角的固定螺丝
            foreach (var off in new[]
            {
                new Vector2(-248f, 34f), new Vector2(248f, 34f),
                new Vector2(-248f, -34f), new Vector2(248f, -34f)
            })
            {
                var scr = UIFactory.CreatePanel("ScreenScrew", screen.transform,
                    new Color(0f, 0f, 0f, 0.5f));
                scr.raycastTarget = false;
                UIFactory.Place((RectTransform)scr.transform, new Vector2(0.5f, 0.5f),
                    new Vector2(6f, 6f), off);
            }

            // ---------- 面板上的按钮 ----------
            var confirm = CreateSquareKey("Confirm", face.transform, "确定", UIStyle.Ok, OnConfirm);
            UIFactory.Place((RectTransform)confirm.transform, new Vector2(0f, 0f),
                new Vector2(58f, 58f), new Vector2(ClusterX, 26f));

            var cancel = CreateSquareKey("Cancel", face.transform, "取消", UIStyle.Grey, OnCancel);
            UIFactory.Place((RectTransform)cancel.transform, new Vector2(0f, 0f),
                new Vector2(58f, 58f), new Vector2(ClusterX + 70f, 26f));

            var play = CreateRoundKey("Play", face.transform, 96, UIStyle.Accent, "演出！", 1.6f, UIStyle.Ink, OnPlay);
            UIFactory.Place((RectTransform)play.transform, new Vector2(0f, 0f),
                new Vector2(96f, 96f), new Vector2(ClusterX + 180f, 24f));

            var stop = CreateRoundKey("Stop", face.transform, 62, UIStyle.Danger, "停止", 1.7f, UIStyle.Text, OnStop);
            UIFactory.Place((RectTransform)stop.transform, new Vector2(0f, 0f),
                new Vector2(62f, 62f), new Vector2(ClusterX + 310f, 28f));

            var exit = CreateSquareKey("Exit", face.transform, "退出", UIStyle.Grey, OnExitBoard);
            UIFactory.Place((RectTransform)exit.transform, new Vector2(0f, 0f),
                new Vector2(52f, 92f), new Vector2(ClusterX + 1040f, 22f));

            // ---------- 屏幕旁：VU 电平表 + BPM 小窗 ----------
            Color[] vu = { UIStyle.Ok, UIStyle.Ok, UIStyle.Ok, UIStyle.Ok,
                           UIStyle.Accent, UIStyle.Accent, UIStyle.Accent,
                           UIStyle.Danger, UIStyle.Danger, UIStyle.Danger };

            var vuBack = UIFactory.CreatePanel("VuBack", face.transform,
                new Color(0f, 0f, 0f, 0.35f));
            vuBack.raycastTarget = false;
            UIFactory.Place((RectTransform)vuBack.transform, new Vector2(0f, 1f),
                new Vector2(250f, 34f), new Vector2(ClusterX + 592f, -24f));

            for (int i = 0; i < vu.Length; i++)
            {
                var seg = UIFactory.CreatePanel("Vu_" + i, face.transform, vu[i]);
                seg.raycastTarget = false;
                UIFactory.Place((RectTransform)seg.transform, new Vector2(0f, 1f),
                    new Vector2(18f, 22f), new Vector2(ClusterX + 600f + i * 24f, -30f));
            }

            var bpm = UIFactory.CreatePanel("BpmWindow", face.transform,
                new Color(0.08f, 0.11f, 0.09f, 1f));
            bpm.raycastTarget = false;
            UIStyle.ApplyWell(bpm, new Color(0.08f, 0.11f, 0.09f, 1f));
            UIFactory.Place((RectTransform)bpm.transform, new Vector2(0f, 1f),
                new Vector2(242f, 40f), new Vector2(ClusterX + 600f, -64f));
            var bpmText = UIFactory.PlacePixelText("BpmText", bpm.transform, "BPM 120 · 4/4", 1.6f,
                UIStyle.Accent, PixelText.Align.Left,
                new Vector2(0f, 0.5f), new Vector2(12f, 0f));
            bpmText.raycastTarget = false;

            // 屏幕右下的迷你波形条
            int[] wave = { 10, 18, 26, 14, 30, 20, 12, 24, 16, 22 };
            for (int i = 0; i < wave.Length; i++)
            {
                var bar = UIFactory.CreatePanel("MiniWave_" + i, screen.transform,
                    i % 2 == 0 ? UIStyle.Accent2 : UIStyle.Ok);
                bar.raycastTarget = false;
                UIFactory.Place((RectTransform)bar.transform, new Vector2(0f, 0f),
                    new Vector2(8f, wave[i]), new Vector2(352f + i * 15f, 10f));
            }

            // VU 两侧的 L / R 通道标
            var lText = UIFactory.PlacePixelText("VuL", face.transform, "L", 1.2f,
                UIStyle.TextDim, PixelText.Align.Left,
                new Vector2(0f, 1f), new Vector2(ClusterX + 600f, -8f));
            lText.raycastTarget = false;
            var rText = UIFactory.PlacePixelText("VuR", face.transform, "R", 1.2f,
                UIStyle.TextDim, PixelText.Align.Left,
                new Vector2(0f, 1f), new Vector2(ClusterX + 832f, -8f));
            rText.raycastTarget = false;

            // ---------- 装饰：两排旋钮 / 竖推子 / 插孔 ----------
            for (int i = 0; i < 6; i++)
            {
                int row = i / 3;
                int col = i % 3;
                float kx = ClusterX + 580f + col * 54f;
                float ky = row == 0 ? 104f : 32f;

                var basePlate = UIFactory.CreatePanel("KnobBase_" + i, face.transform,
                    new Color(0f, 0f, 0f, 0.30f));
                basePlate.raycastTarget = false;
                basePlate.sprite = MakeCircleSprite(40, new Color(0f, 0f, 0f, 0.30f), Color.clear);
                basePlate.color = Color.white;
                UIFactory.Place((RectTransform)basePlate.transform, new Vector2(0f, 0f),
                    new Vector2(40f, 40f), new Vector2(kx - 5f, ky - 5f));

                var knob = UIFactory.CreatePanel("Knob_" + i, face.transform, UIStyle.Grey);
                knob.raycastTarget = false;
                knob.sprite = MakeCircleSprite(30, UIStyle.Grey, UIStyle.EdgeDeep);
                UIFactory.Place((RectTransform)knob.transform, new Vector2(0f, 0f),
                    new Vector2(30f, 30f), new Vector2(kx, ky));

                var dot = UIFactory.CreatePanel("KnobDot_" + i, face.transform, UIStyle.Text);
                dot.raycastTarget = false;
                UIFactory.Place((RectTransform)dot.transform, new Vector2(0f, 0f),
                    new Vector2(4f, 10f), new Vector2(kx, ky + 10f));

            }

            // 竖向音量推子
            var groove = UIFactory.CreatePanel("FaderGroove", face.transform,
                new Color(0f, 0f, 0f, 0.40f));
            groove.raycastTarget = false;
            UIFactory.Place((RectTransform)groove.transform, new Vector2(0f, 0f),
                new Vector2(10f, 120f), new Vector2(ClusterX + 800f, 22f));
            var faderFill = UIFactory.CreatePanel("FaderFill", face.transform, UIStyle.Accent2);
            faderFill.raycastTarget = false;
            UIFactory.Place((RectTransform)faderFill.transform, new Vector2(0f, 0f),
                new Vector2(10f, 76f), new Vector2(ClusterX + 800f, 22f));
            var faderCap = UIFactory.CreatePanel("FaderCap", face.transform, UIStyle.Text);
            faderCap.raycastTarget = false;
            UIFactory.Place((RectTransform)faderCap.transform, new Vector2(0f, 0f),
                new Vector2(32f, 16f), new Vector2(ClusterX + 800f, 92f));

            // 推子左侧的刻度线
            for (int i = 0; i < 5; i++)
            {
                var tick = UIFactory.CreatePanel("FaderTick_" + i, face.transform,
                    new Color(1f, 1f, 1f, 0.22f));
                tick.raycastTarget = false;
                UIFactory.Place((RectTransform)tick.transform, new Vector2(0f, 0f),
                    new Vector2(8f, 2f), new Vector2(ClusterX + 782f, 30f + i * 24f));
            }

            // 三个插孔（圆形的输入口）
            for (int i = 0; i < 3; i++)
            {
                var jack = UIFactory.CreatePanel("Jack_" + i, face.transform, UIStyle.Grey);
                jack.raycastTarget = false;
                jack.sprite = MakeCircleSprite(18, UIStyle.EdgeDeep, UIStyle.Grey);
                UIFactory.Place((RectTransform)jack.transform, new Vector2(0f, 0f),
                    new Vector2(18f, 18f), new Vector2(ClusterX + 880f + i * 30f, 20f));

                // 从插孔接出来的短线缆
                Color cable = i == 0 ? UIStyle.Ok : (i == 1 ? UIStyle.Accent : UIStyle.Danger);
                var wire = UIFactory.CreatePanel("Wire_" + i, face.transform, cable);
                wire.raycastTarget = false;
                UIFactory.Place((RectTransform)wire.transform, new Vector2(0f, 0f),
                    new Vector2(26f, 3f), new Vector2(ClusterX + 896f + i * 30f, 12f));

                var wireDown = UIFactory.CreatePanel("WireDrop_" + i, face.transform, cable);
                wireDown.raycastTarget = false;
                UIFactory.Place((RectTransform)wireDown.transform, new Vector2(0f, 0f),
                    new Vector2(3f, 10f), new Vector2(ClusterX + 919f + i * 30f, 12f));
            }

            // 两个拨杆开关 + 电源指示灯
            for (int i = 0; i < 2; i++)
            {
                var swBase = UIFactory.CreatePanel("SwitchBase_" + i, face.transform, UIStyle.Grey);
                swBase.raycastTarget = false;
                UIFactory.Place((RectTransform)swBase.transform, new Vector2(0f, 0f),
                    new Vector2(34f, 14f), new Vector2(ClusterX + 980f, 56f + i * 30f));

                var lever = UIFactory.CreatePanel("SwitchLever_" + i, face.transform, UIStyle.Text);
                lever.raycastTarget = false;
                UIFactory.Place((RectTransform)lever.transform, new Vector2(0f, 0f),
                    new Vector2(8f, 18f), new Vector2(ClusterX + 986f, 60f + i * 30f));
            }

            var power = UIFactory.CreatePanel("PowerLed", face.transform, UIStyle.Ok);
            power.raycastTarget = false;
            power.sprite = MakeCircleSprite(12, UIStyle.Ok, UIStyle.EdgeDeep);
            power.color = Color.white;
            UIFactory.Place((RectTransform)power.transform, new Vector2(0f, 0f),
                new Vector2(12f, 12f), new Vector2(ClusterX + 980f, 116f));
            var powerText = UIFactory.PlacePixelText("PowerText", face.transform, "PWR", 1.2f,
                UIStyle.TextDim, PixelText.Align.Left,
                new Vector2(0f, 0f), new Vector2(ClusterX + 998f, 116f));
            powerText.raycastTarget = false;

            // 旋钮上方的 LOW/MID/HIGH 标牌 + 推子的 MASTER 标牌
            string[] knobLabels = { "LOW", "MID", "HIGH" };
            for (int i = 0; i < knobLabels.Length; i++)
            {
                var kl = UIFactory.PlacePixelText("KnobLabel_" + i, face.transform,
                    knobLabels[i], 1.1f, UIStyle.TextDim, PixelText.Align.Center,
                    new Vector2(0f, 0f), new Vector2(ClusterX + 595f + i * 54f, 6f));
                kl.raycastTarget = false;
            }

            var master = UIFactory.PlacePixelText("MasterLabel", face.transform, "MASTER", 1.1f,
                UIStyle.TextDim, PixelText.Align.Center,
                new Vector2(0f, 0f), new Vector2(ClusterX + 800f, 124f));
            master.raycastTarget = false;

            // 右侧空区的喇叭网孔
            for (int row = 0; row < 6; row++)
            {
                for (int col = 0; col < 7; col++)
                {
                    var hole = UIFactory.CreatePanel("Grill_" + row + "_" + col, face.transform,
                        new Color(0f, 0f, 0f, 0.38f));
                    hole.raycastTarget = false;
                    UIFactory.Place((RectTransform)hole.transform, new Vector2(1f, 0f),
                        new Vector2(8f, 8f),
                        new Vector2(-250f + col * 18f, 34f + row * 18f));
                }
            }

            // 喇叭网下方的散热槽
            for (int i = 0; i < 3; i++)
            {
                var vent = UIFactory.CreatePanel("Vent_" + i, face.transform,
                    new Color(0f, 0f, 0f, 0.35f));
                vent.raycastTarget = false;
                UIFactory.Place((RectTransform)vent.transform, new Vector2(1f, 0f),
                    new Vector2(120f, 2f), new Vector2(-240f, 14f + i * 6f));
            }

            // LED 灯条（右上角）
            Color[] leds = { UIStyle.Ok, UIStyle.Accent, UIStyle.Danger, UIStyle.Accent2 };
            for (int i = 0; i < leds.Length; i++)
            {
                var socket = UIFactory.CreatePanel("LedSocket_" + i, face.transform,
                    new Color(0f, 0f, 0f, 0.45f));
                socket.raycastTarget = false;
                UIFactory.Place((RectTransform)socket.transform, new Vector2(0f, 1f),
                    new Vector2(18f, 18f), new Vector2(ClusterX - 2f + i * 26f, -108f));

                var led = UIFactory.CreatePanel("Led_" + i, face.transform, leds[i]);
                led.raycastTarget = false;
                UIFactory.Place((RectTransform)led.transform, new Vector2(0f, 1f),
                    new Vector2(14f, 14f), new Vector2(ClusterX + i * 26f, -110f));
            }

            // 四角螺丝
            foreach (var corner in new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) })
            {
                var screw = UIFactory.CreatePanel("Screw", plate.transform, UIStyle.EdgeDeep);
                screw.raycastTarget = false;
                UIFactory.Place((RectTransform)screw.transform, corner,
                    new Vector2(10f, 10f),
                    new Vector2(corner.x > 0.5f ? -13f : 9f, corner.y > 0.5f ? -13f : 9f));

                // 螺丝上一道横槽
                var slot = UIFactory.CreatePanel("ScrewSlot", screw.transform,
                    new Color(0f, 0f, 0f, 0.45f));
                slot.raycastTarget = false;
                UIFactory.Place((RectTransform)slot.transform, new Vector2(0.5f, 0.5f),
                    new Vector2(6f, 2f), Vector2.zero);
            }

            // 机器左上角的小铭牌
            var badge = UIFactory.CreatePanel("Badge", plate.transform,
                new Color(0.08f, 0.11f, 0.10f, 0.95f));
            badge.raycastTarget = false;
            UIFactory.Place((RectTransform)badge.transform, new Vector2(0f, 1f),
                new Vector2(150f, 24f), new Vector2(20f, -10f));
            var badgeText = UIFactory.PlacePixelText("BadgeText", badge.transform, "MIXER 01", 1.3f,
                UIStyle.Accent, PixelText.Align.Center,
                new Vector2(0.5f, 0.5f), Vector2.zero);
            badgeText.raycastTarget = false;

            // 面板上缘的一道反光，强调机器是有厚度的
            var topGlow = UIFactory.CreatePanel("TopGlow", plate.transform,
                new Color(1f, 1f, 1f, 0.10f));
            topGlow.raycastTarget = false;
            var tgr = (RectTransform)topGlow.transform;
            tgr.anchorMin = new Vector2(0f, 1f);
            tgr.anchorMax = new Vector2(1f, 1f);
            tgr.pivot = new Vector2(0.5f, 1f);
            tgr.sizeDelta = new Vector2(-24f, 2f);
            tgr.anchoredPosition = new Vector2(0f, -8f);
        }

        /// <summary>面板上的方形按键。</summary>
        private static Button CreateSquareKey(string name, Transform parent, string label,
                                              Color face, System.Action onClick)
        {
            var btn = UIFactory.CreatePixelButton(name, parent, label, 1.6f, face, UIStyle.Ink, onClick);
            UIResource.ApplyFrameOrColor(btn.image, "UI/btn_confirm", face);
            // 上缘高光 + 下缘阴影：让每个键像一块有厚度的亚克力键帽
            AddInnerEdge(btn.transform, "TopHi", true, 2f, new Color(1f, 1f, 1f, 0.20f));
            AddInnerEdge(btn.transform, "BottomShade", false, 3f, new Color(0f, 0f, 0f, 0.28f));
            return btn;
        }

        /// <summary>在按钮内部贴一条贴边的细线（高光或阴影）。</summary>
        private static void AddInnerEdge(Transform parent, string name, bool top,
                                         float thickness, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, top ? 1f : 0f);
            rt.anchorMax = new Vector2(1f, top ? 1f : 0f);
            rt.pivot = new Vector2(0.5f, top ? 1f : 0f);
            rt.sizeDelta = new Vector2(-10f, thickness);
            rt.anchoredPosition = new Vector2(0f, top ? -3f : 3f);
        }

        /// <summary>面板上的圆形按键（形状各异的「机器按钮」）。</summary>
        private static Button CreateRoundKey(string name, Transform parent, int diameter, Color face,
                                             string label, float labelScale, Color labelColor,
                                             System.Action onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = MakeCircleSprite(diameter, face, UIStyle.EdgeDeep);
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(new UnityEngine.Events.UnityAction(onClick));
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(diameter, diameter);

            var lab = UIFactory.PlacePixelText("Label", go.transform, label, labelScale,
                labelColor, PixelText.Align.Center,
                new Vector2(0.5f, 0.5f), Vector2.zero);
            lab.raycastTarget = false;

            // 圆键的细节：内圈高光、下缘暗边、左上角一点反光
            var rim = new GameObject("Rim", typeof(RectTransform), typeof(Image));
            rim.transform.SetParent(go.transform, false);
            var rimImg = rim.GetComponent<Image>();
            rimImg.sprite = MakeRingSprite(diameter - 14, new Color(1f, 1f, 1f, 0.22f), 2f);
            rimImg.color = Color.white;
            rimImg.raycastTarget = false;
            UIFactory.Place((RectTransform)rim.transform, new Vector2(0.5f, 0.5f),
                new Vector2(diameter - 14, diameter - 14), new Vector2(0f, 2f));

            var shade = new GameObject("Shade", typeof(RectTransform), typeof(Image));
            shade.transform.SetParent(go.transform, false);
            var shadeImg = shade.GetComponent<Image>();
            shadeImg.sprite = MakeRingSprite(diameter, new Color(0f, 0f, 0f, 0.32f), 3f);
            shadeImg.color = Color.white;
            shadeImg.raycastTarget = false;
            UIFactory.Place((RectTransform)shade.transform, new Vector2(0.5f, 0.5f),
                new Vector2(diameter, diameter), new Vector2(0f, -1f));

            var spec = new GameObject("Spec", typeof(RectTransform), typeof(Image));
            spec.transform.SetParent(go.transform, false);
            var specImg = spec.GetComponent<Image>();
            specImg.sprite = MakeCircleSprite(14, new Color(1f, 1f, 1f, 0.30f), Color.clear);
            specImg.color = Color.white;
            specImg.raycastTarget = false;
            UIFactory.Place((RectTransform)spec.transform, new Vector2(0.5f, 0.5f),
                new Vector2(14f, 14f), new Vector2(-diameter * 0.16f, diameter * 0.18f));
            return btn;
        }

        /// <summary>画一个空心圆环（做按钮的高光/暗边）。</summary>
        private static Sprite MakeRingSprite(int diameter, Color band, float thickness)
        {
            var tex = new Texture2D(diameter, diameter, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            float r = diameter * 0.5f;
            var px = new Color[diameter * diameter];
            for (int y = 0; y < diameter; y++)
            {
                for (int x = 0; x < diameter; x++)
                {
                    float dx = x + 0.5f - r;
                    float dy = y + 0.5f - r;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    px[y * diameter + x] = (d <= r && d >= r - thickness) ? band : Color.clear;
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, diameter, diameter),
                                 new Vector2(0.5f, 0.5f), diameter);
        }

        /// <summary>程序化画一个实心圆（带一圈深色描边），做圆形按钮和旋钮。</summary>
        private static Sprite MakeCircleSprite(int diameter, Color color, Color ring)
        {
            var tex = new Texture2D(diameter, diameter, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            float r = diameter * 0.5f;
            var px = new Color[diameter * diameter];
            for (int y = 0; y < diameter; y++)
            {
                for (int x = 0; x < diameter; x++)
                {
                    float dx = x + 0.5f - r;
                    float dy = y + 0.5f - r;
                    float d2 = dx * dx + dy * dy;
                    if (d2 <= r * r) px[y * diameter + x] = color;
                    else if (d2 <= (r + 1.05f) * (r + 1.05f)) px[y * diameter + x] = ring;
                    else px[y * diameter + x] = Color.clear;
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, diameter, diameter),
                                 new Vector2(0.5f, 0.5f), diameter);
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
