using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WaveTeam.Audio;

namespace WaveTeam.UI
{
    /// <summary>
    /// 鼓组原型入口：搭建画布 + 鼓轨板 + 右侧角色栏，加载鼓样本、随机分队并接线拖放。
    /// 按下 Play 即自动运行。节奏基准 120 BPM（见 BgmPlayer）。
    /// （「演出！」播放按钮在 C12 接入，届时再挂到这里。）
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        private DrumTrackBoard _board;
        private RosterSidebar _sidebar;
        private Canvas _canvas;
        private PerformancePlayer _perf;
        private BgmPlayer _bgm;
        private Text _statusText;

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
            _canvas = BuildCanvas();
            EnsureEventSystem();

            _board = DrumTrackBoard.Create(_canvas.transform);
            _sidebar = RosterSidebar.Create(_canvas.transform);

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

                // 接线：卡片拖到板上落位；松手落在侧栏内则回原位、不放置
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
            _statusText = UIFactory.CreateText("Status", _canvas.transform,
                "把右侧角色拖到鼓轨上（同轨不重叠）· 左键拖动移动 · 右键收回", 26,
                new Color(0.8f, 0.8f, 0.85f, 1f), TextAnchor.MiddleCenter);
            var stRt = (RectTransform)_statusText.transform;
            stRt.anchorMin = stRt.anchorMax = stRt.pivot = new Vector2(0.5f, 1f);
            stRt.sizeDelta = new Vector2(1400, 40);
            stRt.anchoredPosition = new Vector2(0, -20);
            _statusText.raycastTarget = false;

            _bgm = gameObject.AddComponent<BgmPlayer>();

            var perfGo = new GameObject("PerformancePlayer");
            perfGo.transform.SetParent(transform, false);
            _perf = perfGo.AddComponent<PerformancePlayer>();
            _perf.Setup(_board, _bgm);
            _perf.OnFinished = msg => _statusText.text = msg;

            var btn = UIFactory.CreateButton("Play", _canvas.transform, "演出！", OnPlay);
            var rt = (RectTransform)btn.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(240, 64);
            rt.anchoredPosition = new Vector2(0, 20);

            var confirm = UIFactory.CreateButton("Confirm", _canvas.transform, "确定", OnConfirm);
            UIResource.TryApply(confirm.image, "UI/btn_confirm");
            var crt = (RectTransform)confirm.transform;
            crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0f, 0f);
            crt.sizeDelta = new Vector2(120, 44);
            crt.anchoredPosition = new Vector2(16, 16);

            var cancel = UIFactory.CreateButton("Cancel", _canvas.transform, "取消", OnCancel);
            UIResource.TryApply(cancel.image, "UI/btn_cancel");
            var xrt = (RectTransform)cancel.transform;
            xrt.anchorMin = xrt.anchorMax = xrt.pivot = new Vector2(0f, 0f);
            xrt.sizeDelta = new Vector2(120, 44);
            xrt.anchoredPosition = new Vector2(150, 16);
        }

        private void OnPlay()
        {
            if (_perf.IsPlaying) return;
            _statusText.text = "演出中……";
            _perf.Play();
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
