using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using WaveTeam.Audio;
using WaveTeam.Core;
using WaveTeam.Room;
using WaveTeam.UI;

// Copied to Assets/Editor only inside the validation project's temporary copy.
[InitializeOnLoad]
public static class ChapterSmoke
{
    private static GameBootstrap boot;
    private static DrumTrackBoard board;
    private static PerformancePlayer perf;
    private static int stage;
    private static double deadline;
    private static double resumeAt;
    private static int voices;
    private static string expected;
    private static Vector3 roomPosition;
    private static bool Restart => Environment.GetCommandLineArgs().Contains("-chapterRestart");
    private static bool Graphics => !Environment.GetCommandLineArgs().Contains("-nographics");
    private static string Root => Directory.GetParent(Application.dataPath).FullName;
    private static GameObject Modal => Field<GameObject>(boot, "_roomModal");
    private static GameObject Result => Field<GameObject>(boot, "_resultPanel");

    static ChapterSmoke()
    {
        if (!SessionState.GetBool("WaveTeam.ChapterSmoke", false)) return;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode) EditorApplication.update += Tick;
        };
    }

    public static void Run()
    {
        var args = Environment.GetCommandLineArgs();
        int profile = Array.IndexOf(args, "-chapterTestProfile");
        if (profile < 0 || profile + 1 >= args.Length) throw new Exception("An isolated test profile is required.");
        PlayerSettings.companyName = "WaveTeam-ChapterValidation-" + args[profile + 1];
        PlayerSettings.productName = "Isolated";
        SessionState.SetBool("WaveTeam.ChapterSmoke", true);
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(EditorBuildSettings.scenes[0].path);
        EditorApplication.delayCall += EditorApplication.EnterPlaymode;
    }

    private static T Field<T>(object obj, string name) => (T)obj.GetType()
        .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(obj);
    private static void Call(object obj, string name, params object[] args) => obj.GetType()
        .GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(obj, args);
    private static void Need(bool valid, string message)
    {
        if (!valid) throw new Exception("Stage " + stage + ": " + message);
    }
    private static void Click(GameObject obj, string name) =>
        obj.GetComponentsInChildren<Button>(true).First(button => button.name == name).onClick.Invoke();
    private static void Next(int next)
    {
        stage = next;
        deadline = EditorApplication.timeSinceStartup + 35;
    }
    private static void Bind()
    {
        board = Field<DrumTrackBoard>(boot, "_board");
        perf = Field<PerformancePlayer>(boot, "_perf");
        Canvas.ForceUpdateCanvases();
    }
    private static DrumCharacter[] Band() =>
        Field<RosterSidebar>(boot, "_sidebar").Cards.Select(card => card.Character).ToArray();
    private static void Talk(string name)
    {
        Call(boot, "OpenCharacterPanel", NpcRoster.All.First(profile => profile.Name == name));
        var panel = Field<CharacterPanel>(boot, "_characterPanel");
        Call(panel, "ShowDialogue");
        panel.Close();
    }
    private static void Story(string name, int pages)
    {
        Call(boot, "OpenCharacterPanel", NpcRoster.All.First(profile => profile.Name == name));
        Need(Modal != null, "NPC story opens");
        for (int i = 0; i < pages; i++) Click(Modal, "Continue");
        Click(Modal, "Continue");
    }
    private static void Enter(int option)
    {
        Call(boot, "OnEquipmentInteract");
        Click(Modal, "Option_" + option);
        if (Modal != null && Modal.name == "MissingBeatBriefing") Click(Modal, "StartRepair");
        Bind();
    }
    private static string Number(float value) => value.ToString("R", CultureInfo.InvariantCulture);
    private static string Canonical(BoardSaveData data) => string.Join("|", data.drums.Select(clip =>
        clip.characterName + "/" + clip.sampleName + "/" + clip.LaneIndex + "/" + Number(clip.startBeat) + "/" +
        Number(clip.durationBeats) + "/" + clip.connectedToNext + "/" +
        string.Join(",", clip.rhythmPoints.Select(point => Number(point.beat)))));
    private static string Events(BoardSaveData data) => string.Join("|", data.drums
        .SelectMany(clip => clip.rhythmPoints.Select(point => (Beat: clip.startBeat + point.beat, Sound: clip.sampleName)))
        .OrderBy(hit => hit.Beat).ThenBy(hit => hit.Sound).Select(hit => Number(hit.Beat) + "/" + hit.Sound));
    private static string ScheduledEvents() => string.Join("|", Field<ICollection>(perf, "_hits").Cast<object>()
        .Select(hit => (Beat: (float)hit.GetType().GetField("Beat").GetValue(hit),
                       Sound: ((AudioClip)hit.GetType().GetField("Clip").GetValue(hit)).name))
        .OrderBy(hit => hit.Beat).ThenBy(hit => hit.Sound).Select(hit => Number(hit.Beat) + "/" + hit.Sound));

    private static void FillFull(bool alternate)
    {
        board.Clear();
        var band = Band();
        for (int beat = 0; beat < 16; beat += 4) board.Place(band[alternate ? 2 : 0], beat, 0);
        for (int beat = 4; beat < 16; beat += 4) board.Place(band[alternate ? 0 : 2], beat, 1);
        for (int beat = 8; beat < 16; beat += 4) board.Place(band[1], beat, 2);
    }

    private static void Tick()
    {
        try
        {
            if (deadline == 0) deadline = EditorApplication.timeSinceStartup + 35;
            Need(EditorApplication.timeSinceStartup < deadline, "timeout");
            if (EditorApplication.timeSinceStartup < resumeAt) return;
            boot = UnityEngine.Object.FindObjectOfType<GameBootstrap>();
            if (boot == null || Field<GameObject>(boot, "_roomRoot") == null) return;
            if (stage == 0)
            {
                Need(Application.persistentDataPath.Contains("WaveTeam-ChapterValidation-"),
                    "isolated data at " + Application.persistentDataPath);
                if (Restart)
                {
                    Need(RehearsalProgress.ReadyForFirst && RehearsalProgress.IsFirstComplete &&
                        RehearsalProgress.Level2Unlocked && RehearsalProgress.Level3Unlocked &&
                        RehearsalProgress.Level3Completed && RehearsalProgress.EndingSeen, "restart progress");
                    Need(Modal == null && RehearsalProgress.LoadSongs().Count == 2, "no repeated story, library persists");
                    Enter(3);
                    Click(Modal, "Option_0");
                    Bind();
                    Need(Canonical(board.ToSaveData()) == File.ReadAllText(Path.Combine(Root, "expected-song.txt")),
                        "restart restores exact clips and connections");
                    Need(ScheduledEvents() == Events(RehearsalProgress.LoadSongs()[0]), "restart audio events");
                    Debug.Log("CHAPTER RESTART PASS");
                    EditorApplication.Exit(0);
                    return;
                }
                Call(boot, "EnterBoard", RehearsalChallenge.First);
                Call(boot, "EnterBoard", RehearsalChallenge.MissingBeat);
                Call(boot, "EnterBoard", RehearsalChallenge.OurOwnSound);
                Need(Field<DrumTrackBoard>(boot, "_board") == null, "fresh entry gates");
                Need(GameObject.Find("EnterBoard") == null && GameObject.Find("EnterMissingBeat") == null,
                    "no permanent level buttons");
                var interactor = Field<NpcInteractor>(boot, "_npcInteractor");
                var equipment = Field<Transform>(interactor, "_equipment");
                Need(equipment != null, "existing device registered");
                var player = GameObject.Find("Player");
                player.transform.position = equipment.position + Field<Vector3>(interactor, "_equipmentOffset");
                Call(interactor, "Update");
                Need(interactor.EquipmentNearby, "equipment proximity");
                roomPosition = player.transform.position;
                Talk("阿宁"); Talk("小川"); Talk("石头");
                Need(RehearsalProgress.ReadyForFirst, "all three actual dialogue callbacks");
                Enter(0);
                var band = Band();
                Need(band[0].Sample.FileName == "kick" && band[1].Sample.FileName == "hi_hat" &&
                    band[2].Sample.FileName == "snare", "NPC and audio identity");
                board.Place(band[0], 0, 0); board.Place(band[1], 4, 1);
                Need(RehearsalEvaluator.Evaluate(board).Score < 100, "two clips cannot ace first");
                Call(boot, "OnPlay");
                Next(1); return;
            }
            if (stage == 1)
            {
                if (Result == null) return;
                Need(!RehearsalProgress.IsFirstComplete && !RehearsalProgress.Level2Unlocked, "non-perfect progression");
                Click(Result, "RetryButton");
                FillFull(false);
                Need(RehearsalEvaluator.Evaluate(board).Score == 100, "first full musical structure");
                Call(boot, "OnPlay");
                Next(2); return;
            }
            if (stage == 2)
            {
                if (Result == null) return;
                Need(RehearsalProgress.IsFirstComplete, "first perfect recorded");
                Click(Result, "ExitResultButton");
                Need(Modal == null && Field<Text>(boot, "_objectiveText").text.Contains("与小川交谈"), "active story quest");
                Need(GameObject.Find("Player").transform.position == roomPosition, "player location retained");
                Call(boot, "OpenCharacterPanel", NpcRoster.All.First(profile => profile.Name == "小川"));
                Click(Modal, "Close");
                Need(!RehearsalProgress.Level2Unlocked, "closed story not complete");
                Story("小川", 4);
                Need(RehearsalProgress.Level2Unlocked, "story2 gate");
                Enter(1);
                var cards = Band();
                var locked = board.Placed[0];
                board.Remove(locked);
                Need(!board.TryMove(locked, 4, 1) && board.Placed.Count == 9, "locked safety");
                var combos = new[] { (0,3,100), (0,4,45), (1,3,45), (1,4,10), (2,5,10), (0,-1,40), (-1,3,40), (-1,-1,0) };
                foreach (var (snare, hat, score) in combos)
                {
                    board.Clear();
                    if (snare >= 0) board.Place(cards[snare], 4, 1);
                    if (hat >= 0) board.Place(cards[hat], 4, 2);
                    Need(RehearsalEvaluator.Evaluate(board, RehearsalChallenge.MissingBeat).Score == score, "dual gap scores");
                }
                board.Place(cards[0], 4, 1); board.Place(cards[3], 4, 2);
                Call(boot, "OnPlay");
                Next(3); return;
            }
            if (stage == 3)
            {
                if (Result == null) return;
                Need(RehearsalProgress.Level2Completed && !RehearsalProgress.Level3Unlocked, "third story required");
                Click(Result, "ExitResultButton");
                Story("阿宁", 4);
                Need(RehearsalProgress.Level3Unlocked, "third unlocked by NPC");
                Enter(2);
                FillFull(false);
                Need(RehearsalEvaluator.Evaluate(board, RehearsalChallenge.OurOwnSound).Score == 100, "composition one");
                FillFull(true);
                Need(RehearsalEvaluator.Evaluate(board, RehearsalChallenge.OurOwnSound).Score == 100, "different valid composition");
                FillFull(false);
                var clip = board.Placed.First(item => item.StartBeat == 4 && item.LaneIndex == 0);
                var character = clip.Character;
                board.Remove(clip);
                var variant = new DrumCharacter("阿宁 · 合奏", character.Sample, character.Pattern, character.Category, fixedRhythm: true);
                board.Place(variant, 4, 0);
                var first = board.Placed.First(item => item.StartBeat == 0 && item.LaneIndex == 0);
                Click(first.Rect.gameObject, "Seam");
                Need(first.ConnectedToNext, "music connection to save");
                expected = Canonical(board.ToSaveData());
                File.WriteAllText(Path.Combine(Root, "expected-song.txt"), expected);
                Call(boot, "OnPlay");
                Next(4); return;
            }
            if (stage == 4)
            {
                if (Result == null) return;
                Click(Result, "SaveSongButton");
                var input = Modal.GetComponentInChildren<InputField>();
                input.text = " ";
                Click(Modal, "Save");
                Need(Modal != null && RehearsalProgress.LoadSongs().Count == 0, "empty name cannot save");
                input.text = "Our First Test Song";
                Click(Modal, "Save");
                var song = RehearsalProgress.LoadSongs().Single();
                Need(song.score == 100 && song.songName == "Our First Test Song" && song.bpm == 120 &&
                    song.withoutBgm && Canonical(song) == expected, "full named work saved");
                Click(Result, "ExitResultButton");
                Call(boot, "OpenCharacterPanel", NpcRoster.All.First(profile => profile.Name == "阿宁"));
                Click(Modal, "Close");
                Need(!RehearsalProgress.EndingSeen, "unfinished ending");
                Call(boot, "OpenCharacterPanel", NpcRoster.All.First(profile => profile.Name == "阿宁"));
                for (int i = 0; i < 3; i++) Click(Modal, "Continue");
                Need(Modal.transform.Find("StoryCard/Heading").GetComponent<Text>().text == "OUR FIRST SONG", "final title");
                Click(Modal, "Continue");
                Bind();
                Need(RehearsalProgress.EndingSeen && perf.IsPlaying && Canonical(board.ToSaveData()) == expected,
                    "ending plays actual saved work");
                Need(ScheduledEvents() == Events(song), "all saved audio events restored");
                resumeAt = EditorApplication.timeSinceStartup + 1.2;
                Next(5); return;
            }
            if (stage == 5)
            {
                var cursor = Field<RectTransform>(board, "_playhead");
                var space = Field<BeatSpace>(board, "_space");
                float beat = (float)((AudioSettings.dspTime - Field<double>(perf, "_startDsp")) * 2);
                Need(cursor.gameObject.activeSelf && Mathf.Abs(cursor.anchoredPosition.x - space.ToX(beat)) < 8,
                    "DSP cursor sync");
                Call(boot, "OnStop");
                Need(!cursor.gameObject.activeSelf, "stop resets cursor");
                Call(boot, "PreviewArrangement");
                resumeAt = EditorApplication.timeSinceStartup + 1.3;
                Next(6); return;
            }
            if (stage == 6)
            {
                voices = perf.GetComponents<AudioSource>().Length;
                Call(boot, "OnStop"); Call(boot, "PreviewArrangement");
                resumeAt = EditorApplication.timeSinceStartup + 1.3;
                Next(7); return;
            }
            if (stage == 7)
            {
                Need(perf.GetComponents<AudioSource>().Length == voices, "voice reuse");
                Call(boot, "OnStop");
                Need(perf.GetComponents<AudioSource>().All(source => !source.isPlaying), "all audio stops");
                board.Clear();
                var band = Band();
                board.Place(band[0], 0, 0);
                board.Place(band[0], 4, 0); board.Place(band[3], 4, 1);
                board.Place(band[0], 8, 0); board.Place(band[2], 8, 1); board.Place(band[1], 8, 2);
                var score = RehearsalEvaluator.Evaluate(board, RehearsalChallenge.OurOwnSound);
                Need(score.ChallengePassed && score.Score == 90, "qualified imperfect work");
                Call(boot, "OnPlay");
                Next(8); return;
            }
            if (stage == 8)
            {
                if (Result == null) return;
                Click(Result, "SaveSongButton");
                Modal.GetComponentInChildren<InputField>().text = "A Different Qualified Song";
                Click(Modal, "Save");
                Need(RehearsalProgress.LoadSongs().Count == 2, "multiple works retained");
                Click(Result, "ExitResultButton");
                Enter(3);
                Click(Modal, "Option_0");
                Bind();
                Need(Canonical(board.ToSaveData()) == expected, "library restores first work");
                Need(ScheduledEvents() == Events(RehearsalProgress.LoadSongs()[0]), "library playback exact");
                if (!Graphics) { Finish(); return; }
                SetGameViewSize(1920, 1080);
                resumeAt = EditorApplication.timeSinceStartup + 1.5;
                Next(9); return;
            }
            if (stage == 9)
            {
                Need(Screen.width == 1920 && Screen.height == 1080, "1080p actual render size");
                Canvas.ForceUpdateCanvases();
                CheckLayout();
                ScreenCapture.CaptureScreenshot(Path.Combine(Root, "chapter-1920x1080.png"));
                resumeAt = EditorApplication.timeSinceStartup + 1;
                Next(10); return;
            }
            if (stage == 10)
            {
                Need(File.Exists(Path.Combine(Root, "chapter-1920x1080.png")), "1080p capture");
                SetGameViewSize(1920, 1200);
                resumeAt = EditorApplication.timeSinceStartup + 1.5;
                Next(11); return;
            }
            if (stage == 11)
            {
                Need(Screen.width == 1920 && Screen.height == 1200, "1200p actual render size");
                Canvas.ForceUpdateCanvases();
                CheckLayout();
                ScreenCapture.CaptureScreenshot(Path.Combine(Root, "chapter-1920x1200.png"));
                resumeAt = EditorApplication.timeSinceStartup + 1;
                Next(12); return;
            }
            Need(File.Exists(Path.Combine(Root, "chapter-1920x1200.png")), "1200p capture");
            Finish();
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    private static void CheckLayout()
    {
        var timeline = Field<RectTransform>(board, "_timeline");
        Need(DrumTrackBoard.TrackRowY(0) + DrumTrackBoard.TrackHeight / 2 < timeline.rect.yMax &&
             DrumTrackBoard.TrackRowY(2) - DrumTrackBoard.TrackHeight / 2 > timeline.rect.yMin, "all rows within viewport");
        var space = Field<BeatSpace>(board, "_space");
        Need(space.ToX(0) >= 0 && space.ToX(16) <= timeline.rect.width, "sixteen beats initially fit");
    }
    private static void SetGameViewSize(int width, int height)
    {
        // The Editor's Game View controls Screen size; Screen.SetResolution targets players.
        var assembly = typeof(Editor).Assembly;
        var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
        var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
        var sizes = singleton.GetProperty("instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
        var groupType = assembly.GetType("UnityEditor.GameViewSizeGroupType");
        var group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { Enum.Parse(groupType, "Standalone") });
        var sizeType = assembly.GetType("UnityEditor.GameViewSize");
        var modeType = assembly.GetType("UnityEditor.GameViewSizeType");
        var size = Activator.CreateInstance(sizeType, new[] {
            Enum.Parse(modeType, "FixedResolution"), (object)width, height, "Chapter validation"
        });
        group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
        int builtins = (int)group.GetType().GetMethod("GetBuiltinCount").Invoke(group, null);
        int custom = (int)group.GetType().GetMethod("GetCustomCount").Invoke(group, null);
        var viewType = assembly.GetType("UnityEditor.GameView");
        var view = EditorWindow.GetWindow(viewType);
        viewType.GetProperty("selectedSizeIndex", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(view, builtins + custom - 1);
        view.Repaint();
    }
    private static void Finish()
    {
        Debug.Log("CHAPTER FULL PASS: NPC, equipment, 3 levels, 90/100 compositions, song library, ending, exact audio restore");
        EditorApplication.Exit(0);
    }
}
