using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WaveTeam.Audio;
using WaveTeam.Core;

namespace WaveTeam.UI
{
    /// <summary>入口：搭建 UI、管理编排与播放流程。按下 Play 即自动运行。</summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        private TaskDef _task;
        private Progression _progression = new Progression();
        private Sequencer _sequencer;
        private ChiptuneSynth _synth;
        private TrackPanel _selected;
        private readonly List<TrackPanel> _panels = new List<TrackPanel>();
        private readonly List<Character> _roster = new List<Character>();

        private RectTransform _rosterContainer;
        private Text _scoreText;
        private Text _resourceText;
        private Text _statusText;
        private Image _flash;
        private bool _unlocked;

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
            _task = SampleData.MorningDelivery();
            _roster.AddRange(SampleData.Roster);

            BuildAudio();
            BuildUi();
            RefreshRoster();
            RefreshResources();
            RunSelfCheck();
        }

        private void BuildAudio()
        {
            _synth = gameObject.AddComponent<ChiptuneSynth>();
            _sequencer = gameObject.AddComponent<Sequencer>();
            _sequencer.Synth = _synth;
            _sequencer.Bpm = 120f;
            _sequencer.OnBeat += HandleBeat;
            _sequencer.OnFinished += HandleFinished;
        }

        private void BuildUi()
        {
            EnsureEventSystem();

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960, 540);
            var root = canvasGo.transform;

            // 全屏闪屏（结果特效），不拦截点击
            _flash = UIFactory.CreatePanel("Flash", root, new Color(0f, 0f, 0f, 0f));
            UIFactory.Stretch((RectTransform)_flash.transform);
            _flash.raycastTarget = false;

            var title = UIFactory.CreateText("Title", root, "波形小队 · 核心原型", 24, Color.white);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(960, 34), new Vector2(0, -16));

            var rosterLabel = UIFactory.CreateText("RosterLabel", root, "角色库（点击角色加入当前选中轨）", 13, new Color(0.7f, 0.7f, 0.7f));
            UIFactory.Place(rosterLabel.rectTransform, new Vector2(0f, 1f), new Vector2(500, 18), new Vector2(10, -50));

            _rosterContainer = UIFactory.CreateRect("Roster", root);
            UIFactory.Place(_rosterContainer, new Vector2(0f, 1f), new Vector2(960, 44), new Vector2(10, -70));

            var unlockBtn = UIFactory.CreateButton("Unlock", root, "解锁神秘嘉宾(50)", TryUnlock);
            UIFactory.Place((RectTransform)unlockBtn.transform, new Vector2(0f, 1f), new Vector2(150, 32), new Vector2(10, -120));

            var tracksLabel = UIFactory.CreateText("TracksLabel", root, "声部（点击轨道选中，撤销移除末尾）", 13, new Color(0.7f, 0.7f, 0.7f));
            UIFactory.Place(tracksLabel.rectTransform, new Vector2(0f, 1f), new Vector2(500, 18), new Vector2(10, -158));

            for (int i = 0; i < _task.Tracks.Count; i++)
            {
                var panel = new TrackPanel(_task.Tracks[i], root, OnSelectTrack);
                UIFactory.Place(panel.Rect, new Vector2(0f, 1f), new Vector2(940, 84), new Vector2(10, -182 - i * 90));
                _panels.Add(panel);
            }

            _scoreText = UIFactory.CreateText("Score", root, "得分: -", 16, Color.white);
            UIFactory.Place(_scoreText.rectTransform, new Vector2(0f, 0f), new Vector2(400, 26), new Vector2(10, 30));
            _resourceText = UIFactory.CreateText("Resource", root, "资源: 0", 16, Color.white);
            UIFactory.Place(_resourceText.rectTransform, new Vector2(0f, 0f), new Vector2(160, 26), new Vector2(420, 30));

            var playBtn = UIFactory.CreateButton("Play", root, "▶ 排练演出", Play);
            UIFactory.Place((RectTransform)playBtn.transform, new Vector2(1f, 0f), new Vector2(170, 48), new Vector2(-10, 30));

            _statusText = UIFactory.CreateText("Status", root, "请把角色编排进各轨", 14, new Color(1f, 0.9f, 0.4f));
            UIFactory.Place(_statusText.rectTransform, new Vector2(0.5f, 0f), new Vector2(600, 22), new Vector2(0, 10));
        }

        private void RefreshRoster()
        {
            foreach (Transform child in _rosterContainer) Destroy(child.gameObject);
            for (int i = 0; i < _roster.Count; i++)
            {
                var c = _roster[i];
                var btn = UIFactory.CreateButton("Char_" + c.Name, _rosterContainer, RosterLabel(c), () => OnPickCharacter(c));
                btn.image.color = TraitColor.Of(c.Tiles[0].Trait);
                UIFactory.Place((RectTransform)btn.transform, new Vector2(0f, 1f), new Vector2(110, 42), new Vector2(i * 116, 0));
            }
        }

        private static string RosterLabel(Character c)
        {
            var sb = new StringBuilder();
            sb.Append(c.Name).Append(" ");
            foreach (var t in c.Tiles) sb.Append(t.Pitch);
            return sb.ToString();
        }

        private void OnSelectTrack(TrackPanel panel)
        {
            _selected = panel;
            foreach (var p in _panels) p.SetSelected(p == panel);
        }

        private void OnPickCharacter(Character c)
        {
            if (_selected == null) { SetStatus("请先点击一条轨道选中它"); return; }
            _selected.AddCharacter(c);
            SetStatus("已把 " + c.Name + " 加入「" + _selected.Track.RoleName + "」");
        }

        private void TryUnlock()
        {
            if (_unlocked) { SetStatus("已经解锁过了"); return; }
            if (_progression.TrySpend(SampleData.UnlockCost))
            {
                _unlocked = true;
                _roster.Add(SampleData.Mysterious);
                RefreshRoster();
                RefreshResources();
                SetStatus("解锁了「神秘嘉宾」！");
            }
            else
            {
                SetStatus("资源不足（需要 " + SampleData.UnlockCost + "）");
            }
        }

        private void Play()
        {
            if (_sequencer.IsPlaying) return;
            bool any = false;
            foreach (var t in _task.Tracks) if (t.Chain.Count > 0) any = true;
            if (!any) { SetStatus("先往轨道里放几个角色吧"); return; }

            _scoreText.text = "得分: 演出中…";
            _sequencer.Play(_task);
        }

        private void HandleBeat(int beat)
        {
            SetStatus("演出中…… 第 " + (beat + 1) + " 拍");
        }

        private void HandleFinished()
        {
            int totalOk = 0, totalSteps = 0;
            var sb = new StringBuilder();
            foreach (var track in _task.Tracks)
            {
                var played = ChainTimeline.Flatten(track.Chain);
                var result = AccuracyScorer.Score(track.TargetScore, played);
                sb.Append(track.RoleName).Append(' ').Append(Mathf.RoundToInt(result.Accuracy * 100f)).Append("%  ");
                totalOk += result.Perfect + result.Tolerated;
                totalSteps += result.TotalSteps;
            }
            float overall = totalSteps == 0 ? 0f : (float)totalOk / totalSteps;
            int gained = _progression.Settle(overall);

            _scoreText.text = "得分: " + Mathf.RoundToInt(overall * 100f) + "%  [" + sb.ToString().Trim() + "]";
            RefreshResources();

            // 分级特效：按准确度给不同颜色的闪屏与文案
            if (overall >= 0.95f) { SetStatus("完美合拍！本次获得资源 +" + gained); StartCoroutine(Flash(new Color(0.2f, 1f, 0.4f, 0.35f))); }
            else if (overall >= 0.7f) { SetStatus("大体合拍，有个别走音。获得资源 +" + gained); StartCoroutine(Flash(new Color(1f, 0.9f, 0.3f, 0.3f))); }
            else { SetStatus("各弹各的…… 获得资源 +" + gained); StartCoroutine(Flash(new Color(1f, 0.4f, 0.4f, 0.3f))); }
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

        private void RefreshResources()
        {
            _resourceText.text = "资源: " + _progression.Resources;
        }

        private void SetStatus(string msg)
        {
            _statusText.text = msg;
        }

        private void RunSelfCheck()
        {
            var melody = _task.Tracks[0];
            var perfectChain = new List<Character> { SampleData.Aning, SampleData.Xiaochuan, SampleData.Laozhou, SampleData.Shitou };
            var perfect = AccuracyScorer.Score(melody.TargetScore, ChainTimeline.Flatten(perfectChain));
            Debug.Log("[波形小队] 自检 · 完美解准确度 = " + (perfect.Accuracy * 100f) + "% (应 100%)");

            var badChain = new List<Character> { SampleData.Shitou, SampleData.Shitou };
            var bad = AccuracyScorer.Score(melody.TargetScore, ChainTimeline.Flatten(badChain));
            Debug.Log("[波形小队] 自检 · 乱拼准确度 = " + (bad.Accuracy * 100f) + "% (应远低于 100%)");
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }
    }
}
