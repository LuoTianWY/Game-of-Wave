using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WaveTeam.Audio;
using WaveTeam.Core;
using WaveTeam.Room;
using WaveTeam.UI;

// Runs only after being copied into the validator's temporary Unity project.
[InitializeOnLoad]
public static class AnalysisSmoke
{
    private static GameBootstrap boot;
    private static DrumTrackBoard board;
    private static GameObject analysis;
    private static int stage;
    private static double timeout;
    private static double resumeAt;
    private static bool Graphics => !Environment.GetCommandLineArgs().Contains("-nographics");
    private static string Root => System.IO.Directory.GetParent(Application.dataPath).FullName;

    static AnalysisSmoke()
    {
        if (!SessionState.GetBool("WaveTeam.AnalysisSmoke", false)) return;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode) EditorApplication.update += Tick;
        };
    }

    public static void Run()
    {
        var args = Environment.GetCommandLineArgs();
        int profile = Array.IndexOf(args, "-chapterTestProfile");
        if (profile < 0) throw new Exception("Isolated test profile required.");
        PlayerSettings.companyName = "WaveTeam-AnalysisValidation-" + args[profile + 1];
        PlayerSettings.productName = "Isolated";
        SessionState.SetBool("WaveTeam.AnalysisSmoke", true);
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(EditorBuildSettings.scenes[0].path);
        EditorApplication.delayCall += EditorApplication.EnterPlaymode;
    }

    private static T Field<T>(object obj, string name) => (T)obj.GetType()
        .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(obj);
    private static void Call(object obj, string name, params object[] args) => obj.GetType()
        .GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(obj, args);
    private static void Need(bool valid, string message)
    {
        if (!valid) throw new Exception("Analysis stage " + stage + ": " + message);
    }
    private static Text Details => analysis.transform.Find("WaveformInfo/Details").GetComponent<Text>();
    private static Text State(int index) => analysis.transform.Find("Targets/Target_" + index + "/State").GetComponent<Text>();
    private static void Click(GameObject root, string name) =>
        root.GetComponentsInChildren<Button>(true).First(button => button.name == name).onClick.Invoke();
    private static void Next(int next)
    {
        stage = next;
        timeout = EditorApplication.timeSinceStartup + 30;
    }
    private static DrumCharacter[] Band() =>
        Field<RosterSidebar>(boot, "_sidebar").Cards.Select(card => card.Character).ToArray();
    private static void Parity()
    {
        var result = RehearsalEvaluator.Evaluate(board);
        bool[] achieved = { result.OpeningStableGood, result.DevelopmentLayeredGood, result.DensityGood };
        for (int i = 0; i < achieved.Length; i++)
            Need((State(i).text == "预计达成") == achieved[i], "UI and formal condition " + i);
    }
    private static void Fill()
    {
        board.Clear();
        var band = Band();
        for (int beat = 0; beat < 16; beat += 4) board.Place(band[0], beat, 0);
        for (int beat = 4; beat < 16; beat += 4) board.Place(band[2], beat, 1);
        for (int beat = 8; beat < 16; beat += 4) board.Place(band[1], beat, 2);
    }
    private static Vector2 Point(float beat, int lane)
    {
        var timeline = Field<RectTransform>(board, "_timeline");
        var space = Field<BeatSpace>(board, "_space");
        return RectTransformUtility.WorldToScreenPoint(null,
            timeline.TransformPoint(new Vector3(space.ToX(beat), DrumTrackBoard.TrackRowY(lane), 0f)));
    }

    private static void Tick()
    {
        try
        {
            if (timeout == 0) timeout = EditorApplication.timeSinceStartup + 30;
            Need(EditorApplication.timeSinceStartup < timeout, "timeout");
            if (EditorApplication.timeSinceStartup < resumeAt) return;
            boot = UnityEngine.Object.FindObjectOfType<GameBootstrap>();
            if (boot == null || Field<GameObject>(boot, "_roomRoot") == null) return;
            if (stage == 0)
            {
                Need(Application.persistentDataPath.Contains("WaveTeam-AnalysisValidation-"), "isolated test saves");
                foreach (string name in new[] { "阿宁", "小川", "石头" })
                    Call(boot, "OnNpcTalked", NpcRoster.All.First(profile => profile.Name == name));
                Call(boot, "EnterBoard", RehearsalChallenge.First);
                board = Field<DrumTrackBoard>(boot, "_board");
                analysis = Field<GameObject>(boot, "_boardRoot").transform.Find("RehearsalAnalysis").gameObject;
                Canvas.ForceUpdateCanvases();
                Need(analysis != null && analysis.GetComponentsInChildren<Graphic>().All(graphic => !graphic.raycastTarget),
                    "analysis never captures pointer events");
                var cards = Field<RosterSidebar>(boot, "_sidebar").Cards;
                Need(cards.Count >= 4, "four character cards");
                foreach (var card in cards)
                {
                    var stats = RehearsalTraits.GetStats(card.Character.Noun);
                    string expected = "稳定 " + stats.Stability + " / 层次 " + stats.Layering + " / 密度 " + stats.Density;
                    Need(card.transform.Find("GameplayHint").GetComponent<Text>().text == expected, "real trait values");
                    Need(card.transform.Find("Trait").GetComponent<Text>().text.Contains(card.Character.Noun), "trait name");
                    Need(card.GetComponentsInChildren<Button>().Any(button => button.name == "Play"), "audition still present");
                }
                Need(Enumerable.Range(0, 3).All(index => State(index).text == "等待编排"), "empty state");
                Parity();
                var band = Band();
                var first = board.Place(band[0], 0f, 0);
                Need(State(0).text == "预计达成", "opening updates immediately");
                board.Place(band[0], 4f, 0); board.Place(band[2], 4f, 1);
                Need(State(1).text == "预计达成", "development updates immediately");
                Fill();
                Need(RehearsalEvaluator.Evaluate(board).Score == 100, "complete structure");
                var original = board.Placed.First(clip => clip.LaneIndex == 0 && clip.StartBeat == 8);
                board.Remove(original);
                var densePattern = new RhythmPattern(Enumerable.Range(0, 32)
                    .Select(index => new RhythmPoint(index / 8f, 0, true)).ToArray(), 4f);
                var dense = new DrumCharacter("密集测试", band[0].Sample, densePattern, band[0].Category, fixedRhythm: true);
                Need(board.Place(dense, 8, 0) != null, "dense test passage");
                Need(State(2).text == "尚未达成" &&
                    analysis.transform.Find("Targets/Target_2/Advice").GetComponent<Text>().text.Contains("过密"),
                    "density explains actual excess events");
                Parity();
                board.Undo(); board.Undo();
                Need(State(2).text == "预计达成", "undo restores condition");
                first = board.Placed.First(clip => clip.StartBeat == 0 && clip.LaneIndex == 0);
                var handle = first.Rect.GetComponent<PlacedDrumHandle>();
                var pointer = new PointerEventData(EventSystem.current) {
                    button = PointerEventData.InputButton.Left, clickCount = 1, position = Point(0, 0)
                };
                handle.OnPointerEnter(pointer);
                Need(Details.text.Contains(first.Character.Name) && Details.text.Contains("结束 4") &&
                    first.Rect.GetComponent<Outline>().enabled, "hover details and highlight");
                handle.OnPointerClick(pointer); handle.OnPointerExit(pointer);
                Need(Details.text.Contains("已选中"), "selection persists after hover exit");
                handle.OnBeginDrag(pointer);
                pointer.position = Point(0, 2);
                var timeline = Field<RectTransform>(board, "_timeline");
                RectTransformUtility.ScreenPointToLocalPointInRectangle(timeline, pointer.position, null, out var local);
                Debug.Log("First-beat roundtrip value: " + Field<BeatSpace>(board, "_space").ToBeat(local.x).ToString("R"));
                handle.OnDrag(pointer);
                Need(first.LaneIndex == 0, "preview does not mutate clip");
                handle.OnEndDrag(pointer);
                Need(first.LaneIndex == 2 && Details.text.Contains("Layer 3"),
                    "cross-lane drag and details update; lane=" + first.LaneIndex + "; " + Details.text);
                var occupied = board.Placed.First(clip => clip.StartBeat == 12 && clip.LaneIndex == 1);
                board.Remove(occupied);
                Need(board.TryMove(first, 12, 1) && Details.text.Contains("开始 12") &&
                    Details.text.Contains("结束 16"), "horizontal move refreshes range");
                Parity();
                pointer.button = PointerEventData.InputButton.Right;
                handle.OnPointerClick(pointer);
                Need(!Details.text.Contains("已选中"), "removing selection clears details");
                Fill();
                Parity();
                Call(boot, "OnPlay");
                Next(1); return;
            }
            if (stage == 1)
            {
                var resultPanel = Field<GameObject>(boot, "_resultPanel");
                if (resultPanel == null) return;
                Need(resultPanel.transform.Find("ResultCard/Score").GetComponent<Text>().text.Contains("100 / 100"),
                    "actual performance result agrees with realtime goals");
                Click(resultPanel, "RetryButton");
                var handle = board.Placed[0].Rect.GetComponent<PlacedDrumHandle>();
                var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, clickCount = 1 };
                handle.OnPointerEnter(pointer); handle.OnPointerClick(pointer); handle.OnPointerExit(pointer);
                if (!Graphics) { PuzzleIsolation(); return; }
                SetSize(1920, 1080);
                resumeAt = EditorApplication.timeSinceStartup + 1.5;
                Next(2); return;
            }
            if (stage == 2)
            {
                Need(Screen.width == 1920 && Screen.height == 1080, "1080p render size");
                Layout();
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(Root, "analysis-1920x1080.png"));
                resumeAt = EditorApplication.timeSinceStartup + 1;
                Next(3); return;
            }
            if (stage == 3)
            {
                Need(System.IO.File.Exists(System.IO.Path.Combine(Root, "analysis-1920x1080.png")), "1080p capture");
                SetSize(1920, 1200);
                resumeAt = EditorApplication.timeSinceStartup + 1.5;
                Next(4); return;
            }
            if (stage == 4)
            {
                Need(Screen.width == 1920 && Screen.height == 1200, "1200p render size");
                Layout();
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(Root, "analysis-1920x1200.png"));
                resumeAt = EditorApplication.timeSinceStartup + 1;
                Next(5); return;
            }
            Need(System.IO.File.Exists(System.IO.Path.Combine(Root, "analysis-1920x1200.png")), "1200p capture");
            PuzzleIsolation();
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    private static void PuzzleIsolation()
    {
        Call(boot, "OnExitBoard");
        Call(boot, "OpenCharacterPanel", NpcRoster.All.First(profile => profile.Name == "小川"));
        var modal = Field<GameObject>(boot, "_roomModal");
        for (int i = 0; i < 5; i++) Click(modal, "Continue");
        Call(boot, "OnMissingBeatEntry");
        Click(Field<GameObject>(boot, "_roomModal"), "StartRepair");
        board = Field<DrumTrackBoard>(boot, "_board");
        Need(Field<GameObject>(boot, "_boardRoot").transform.Find("RehearsalAnalysis") == null, "no puzzle answer panel");
        var cards = Field<RosterSidebar>(boot, "_sidebar").Cards;
        Need(cards.All(card => card.transform.Find("Trait") == null), "no puzzle trait grading cues");
        board.Place(cards[0].Character, 4, 1); board.Place(cards[3].Character, 4, 2);
        Need(RehearsalEvaluator.Evaluate(board, RehearsalChallenge.MissingBeat).Score == 100, "puzzle scoring regression");
        Debug.Log("ANALYSIS PASS: real attributes, hover/selection, drag, live/formal parity, density, undo, rendered layouts, puzzle isolation");
        EditorApplication.Exit(0);
    }

    private static void Layout()
    {
        Canvas.ForceUpdateCanvases();
        var bounds = new Vector3[4];
        analysis.GetComponent<RectTransform>().GetWorldCorners(bounds);
        float bottom = bounds[0].y, top = bounds[1].y;
        var rows = Field<RectTransform>(board, "_rows");
        float trackBottom = rows.TransformPoint(new Vector3(0, DrumTrackBoard.TrackRowY(2) - DrumTrackBoard.TrackHeight / 2, 0)).y;
        Need(top < trackBottom, "analysis below every waveform and drag lane");
        var console = Field<GameObject>(boot, "_boardRoot").transform.Find("MixerConsole").GetComponent<RectTransform>();
        console.GetWorldCorners(bounds);
        Need(bottom > bounds[1].y, "analysis above controls");
    }

    private static void SetSize(int width, int height)
    {
        var assembly = typeof(Editor).Assembly;
        var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
        var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
        var sizes = singleton.GetProperty("instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
        var groupType = assembly.GetType("UnityEditor.GameViewSizeGroupType");
        var group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { Enum.Parse(groupType, "Standalone") });
        var size = Activator.CreateInstance(assembly.GetType("UnityEditor.GameViewSize"), new[] {
            Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeType"), "FixedResolution"),
            (object)width, height, "Analysis validation"
        });
        group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
        int index = (int)group.GetType().GetMethod("GetBuiltinCount").Invoke(group, null)
            + (int)group.GetType().GetMethod("GetCustomCount").Invoke(group, null) - 1;
        var viewType = assembly.GetType("UnityEditor.GameView");
        var view = EditorWindow.GetWindow(viewType);
        viewType.GetProperty("selectedSizeIndex", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(view, index);
        view.Repaint();
    }
}
