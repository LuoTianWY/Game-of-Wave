using System;
using System.Collections;
using System.Globalization;
using System.IO;
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

[InitializeOnLoad]
public static class MelodySmoke
{
    private static MelodyLab lab;
    private static DrumTrackBoard board;
    private static PerformancePlayer player;
    private static int stage;
    private static double deadline;
    private static double resumeAt;
    private static int voices;
    private static int clipBaseline;
    private static int playerBaseline;
    private static string expected;
    private static bool Restart => Environment.GetCommandLineArgs().Contains("-chapterRestart");
    private static bool Graphics => !Environment.GetCommandLineArgs().Contains("-nographics");
    private static string Root => Directory.GetParent(Application.dataPath).FullName;
    private static string SavePath => Path.Combine(Application.persistentDataPath, MelodyLab.ArrangementFile);

    static MelodySmoke()
    {
        if (!SessionState.GetBool("WaveTeam.MelodySmoke", false)) return;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode) EditorApplication.update += Tick;
        };
    }

    public static void Run()
    {
        var args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "-chapterTestProfile");
        if (index < 0) throw new Exception("Isolated test profile required.");
        PlayerSettings.companyName = "WaveTeam-MelodyValidation-" + args[index + 1];
        PlayerSettings.productName = "Isolated";
        SessionState.SetBool("WaveTeam.MelodySmoke", true);
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(EditorBuildSettings.scenes[0].path);
        EditorApplication.delayCall += EditorApplication.EnterPlaymode;
    }

    private static T F<T>(object obj, string name) => (T)obj.GetType()
        .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(obj);
    private static void C(object obj, string name, params object[] args) => obj.GetType()
        .GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(obj, args);
    private static void Need(bool valid, string message)
    {
        if (!valid) throw new Exception("Melody stage " + stage + ": " + message);
    }
    private static void Next(int value, double delay = 0)
    {
        stage = value;
        resumeAt = EditorApplication.timeSinceStartup + delay;
        deadline = EditorApplication.timeSinceStartup + 30;
    }
    private static object[] Hits() => F<ICollection>(player, "_hits").Cast<object>().ToArray();
    private static T Hit<T>(object hit, string field) => (T)hit.GetType().GetField(field).GetValue(hit);
    private static int SynthClips() => Resources.FindObjectsOfTypeAll<AudioClip>().Count(clip => clip.name.StartsWith("synth."));
    private static void Bind()
    {
        lab = UnityEngine.Object.FindObjectOfType<MelodyLab>();
        board = F<DrumTrackBoard>(lab, "_board");
        player = F<PerformancePlayer>(lab, "_player");
        Canvas.ForceUpdateCanvases();
    }
    private static Vector2 Point(float beat, int lane)
    {
        var timeline = F<RectTransform>(board, "_timeline");
        return RectTransformUtility.WorldToScreenPoint(null, timeline.TransformPoint(new Vector3(
            F<BeatSpace>(board, "_space").ToX(beat), DrumTrackBoard.TrackRowY(lane), 0)));
    }
    private static string N(float value) => value.ToString("R", CultureInfo.InvariantCulture);
    private static string Canonical(BoardSaveData data) => string.Join("|", data.drums
        .OrderBy(clip => clip.LaneIndex).ThenBy(clip => clip.startBeat).Select(clip =>
            clip.LaneIndex + "/" + N(clip.startBeat) + "/" + N(clip.durationBeats) + "/" + clip.characterName + "/" +
            clip.waveKind + "/" + clip.instrumentId + "/" + clip.sampleName + "/" +
            string.Join(",", (clip.melodyNotes ?? Array.Empty<MelodyNote>()).Select(note =>
                N(note.StartBeat) + ":" + N(note.DurationBeats) + ":" + note.MidiNote + ":" + N(note.Velocity))) + "/" +
            string.Join(",", (clip.rhythmPoints ?? Array.Empty<SavedRhythmPoint>()).Select(point => N(point.beat)))));

    private static void ValidateTone(AudioClip clip, int midi, float beats)
    {
        Need(clip != null && Math.Abs(clip.samples / (double)clip.frequency - beats * 0.5) < 0.0001,
            "exact note duration");
        var pcm = new float[clip.samples];
        Need(clip.GetData(pcm, 0), "read actual synthesized PCM");
        Need(pcm.All(sample => !float.IsNaN(sample) && Math.Abs(sample) <= 1f) &&
            pcm.Any(sample => Math.Abs(sample) > 0.01), "audible bounded PCM");
        Need(Math.Abs(pcm[0]) < 0.00001 && Math.Abs(pcm[pcm.Length - 1]) < 0.00001, "finite attack and release");
        int start = clip.frequency / 20;
        int end = Math.Min(clip.samples - 1, (int)(clip.frequency * 0.45));
        int crossings = 0;
        for (int i = start + 1; i < end; i++)
            if (pcm[i - 1] <= 0 && pcm[i] > 0) crossings++;
        double measured = crossings * clip.frequency / (double)(end - start);
        double expectedFrequency = 440 * Math.Pow(2, (midi - 69) / 12.0);
        Need(Math.Abs(measured - expectedFrequency) < 6, "PCM fundamental MIDI " + midi + " measured " + measured);
    }

    private static void Tick()
    {
        try
        {
            if (deadline == 0) deadline = EditorApplication.timeSinceStartup + 30;
            Need(EditorApplication.timeSinceStartup < deadline, "timeout");
            if (EditorApplication.timeSinceStartup < resumeAt) return;
            var boot = UnityEngine.Object.FindObjectOfType<GameBootstrap>();
            if (boot == null || F<GameObject>(boot, "_roomRoot") == null) return;
            if (stage == 0)
            {
                Need(Application.persistentDataPath.Contains("WaveTeam-MelodyValidation-"), "isolated saves");
                clipBaseline = SynthClips();
                playerBaseline = UnityEngine.Object.FindObjectsOfType<PerformancePlayer>().Length;
                MelodyLab.Open();
                Bind();
                Need(board != null && !F<NpcInteractor>(boot, "_npcInteractor").enabled, "isolated test workspace");
                if (Restart)
                {
                    C(lab, "Load");
                    Need(Canonical(board.ToSaveData()) == File.ReadAllText(Path.Combine(Root, "expected-melody.txt")),
                        "same melody after restarting editor");
                    C(lab, "PreviewBoard");
                    Need(Hits().Count(hit => Hit<WaveKind>(hit, "Kind") != WaveKind.Rhythm) == 16,
                        "restored pitched audio events");
                    Need(!RehearsalProgress.IsFirstComplete && !RehearsalProgress.Level2Unlocked, "no chapter progress pollution");
                    Debug.Log("MELODY RESTART PASS");
                    EditorApplication.Exit(0);
                    return;
                }

                var waves = F<System.Collections.Generic.List<DrumCharacter>>(lab, "_waves");
                int[][] expectedNotes = {
                    new[] {60,64,67,64}, new[] {64,69,60,67}, new[] {67,64,62,60}, new[] {36,43,45,43}
                };
                for (int i = 0; i < expectedNotes.Length; i++)
                {
                    var card = lab.GetComponentsInChildren<DrumCharacterCard>().Single(item => item.Character == waves[i]);
                    Need(card.transform.Find("WaveType").GetComponent<Text>().text.Contains(waves[i].Kind.ToString()) &&
                        card.transform.Find("NoteNames").GetComponent<Text>().text.Contains(MelodyNote.Name(expectedNotes[i][0])),
                        "card wave type and actual note labels");
                    card.transform.Find("Play").GetComponent<Button>().onClick.Invoke();
                    var hits = Hits();
                    Need(hits.Length == 4 && hits.Select(hit => Hit<int>(hit, "MidiNote")).SequenceEqual(expectedNotes[i]),
                        "actual note sequence " + waves[i].Name);
                    Need(hits.Select(hit => Hit<float>(hit, "Beat")).SequenceEqual(new[] {0f,1f,2f,3f}), "note times");
                    foreach (var hit in hits)
                        ValidateTone(Hit<AudioClip>(hit, "Clip"), Hit<int>(hit, "MidiNote"), Hit<float>(hit, "DurationBeats"));
                    Debug.Log("MELODY PCM PASS " + waves[i].Name + " MIDI " + string.Join(",", expectedNotes[i]));
                }

                var lengths = new DrumCharacter("Duration test", WaveKind.Melody, waves[0].InstrumentId, new[] {
                    new MelodyNote(0, 0.5f, 72, 0.3f), new MelodyNote(1, 2, 60, 0.9f)
                }, 4);
                C(lab, "PreviewWave", lengths);
                Need(Hit<AudioClip>(Hits()[0], "Clip").samples / (double)Hit<AudioClip>(Hits()[0], "Clip").frequency == 0.25,
                    "half-beat note");
                Need(Hit<AudioClip>(Hits()[1], "Clip").samples / (double)Hit<AudioClip>(Hits()[1], "Clip").frequency == 1,
                    "two-beat note");
                Need(player.GetComponents<AudioSource>().Any(source => source.clip == Hit<AudioClip>(Hits()[0], "Clip") &&
                    Math.Abs(source.volume - 0.3f) < 0.001 && source.pitch == 1), "velocity controls amplitude");

                C(lab, "PreviewWave", waves[0]);
                var cached = Hit<AudioClip>(Hits()[0], "Clip");
                C(lab, "PreviewWave", waves[0]);
                Need(Hit<AudioClip>(Hits()[0], "Clip") == cached, "same-note clip cache reuse");
                Next(1, 1.0); return;
            }
            if (stage == 1)
            {
                int scheduled = F<int>(player, "_nextHit");
                // At one second the lookahead may already include the third beat.
                Need(player.IsPlaying && scheduled >= 2 && scheduled <= 3,
                    "sequential note scheduling: " + scheduled + " queued events");
                Next(2, 1.4); return;
            }
            if (stage == 2)
            {
                Need(!player.IsPlaying && player.GetComponents<AudioSource>().All(source => !source.isPlaying),
                    "finite notes finish without sustain");
                C(lab, "FillDemo");
                Next(21, 0.1); return;
            }
            if (stage == 21)
            {
                Need(board.Placed.Count == 3 && board.Placed.Select(clip => clip.Character.Kind).Distinct().Count() == 3,
                    "mixed three-track arrangement");
                var melody = board.Placed.First(clip => clip.Character.Kind == WaveKind.Melody);
                var bass = board.Placed.First(clip => clip.Character.Kind == WaveKind.Bass);
                Need(melody.MelodyContour != null && bass.MelodyContour != null &&
                    !board.CanPlace(bass.Character, 0, 0) && board.CanPlace(bass.Character, 4, 0), "renderer and overlap rules");
                Need(board.TryMove(melody, 4, 2) && board.TryMove(melody, 0, 0), "pitched cross-track move");
                Canvas.ForceUpdateCanvases();
                var pointer = new PointerEventData(EventSystem.current) {
                    button = PointerEventData.InputButton.Left, position = Point(0.5f, 0)
                };
                var raycasts = new System.Collections.Generic.List<RaycastResult>();
                EventSystem.current.RaycastAll(pointer, raycasts);
                Need(raycasts.Count > 0 && ExecuteEvents.GetEventHandler<IBeginDragHandler>(raycasts[0].gameObject) ==
                    melody.Rect.gameObject, "contour receives drag events through parent: " +
                    string.Join(",", raycasts.Select(hit => hit.gameObject.name)) +
                    "; depth=" + melody.MelodyContour.depth + "; cull=" + melody.MelodyContour.canvasRenderer.cull +
                    "; target=" + melody.MelodyContour.raycastTarget + "; contains=" +
                    RectTransformUtility.RectangleContainsScreenPoint(melody.MelodyContour.rectTransform, pointer.position));
                var handle = melody.Rect.GetComponent<PlacedDrumHandle>();
                handle.OnBeginDrag(pointer);
                pointer.position = Point(4.01f, 2);
                handle.OnDrag(pointer);
                Need(melody.LaneIndex == 0 && F<MelodyContourRenderer>(board, "_ghostContour").isActiveAndEnabled,
                    "pitched drag ghost preserves data until drop");
                handle.OnEndDrag(pointer);
                Need(melody.StartBeat == 4 && melody.LaneIndex == 2, "pitched pointer drop");
                pointer.position = Point(0.01f, 0);
                handle.OnBeginDrag(pointer); handle.OnEndDrag(pointer);
                Need(melody.StartBeat == 0 && melody.LaneIndex == 0, "pitched pointer return");
                pointer.position = Point(2, 0); pointer.scrollDelta = Vector2.up;
                board.OnScroll(pointer);
                Canvas.ForceUpdateCanvases();
                var beatSpace = F<BeatSpace>(board, "_space");
                Need(Math.Abs(melody.Rect.anchoredPosition.x - beatSpace.ToX(0)) < 0.01 &&
                    Math.Abs(melody.MelodyContour.rectTransform.rect.width - beatSpace.SpanToWidth(4)) < 0.01,
                    "pitched contour follows shared zoom and time grid");
                C(board, "ResetView");
                C(lab, "PreviewBoard");
                var voices = F<ICollection>(player, "_voices").Cast<object>()
                    .Where(voice => (double)voice.GetType().GetField("BusyUntil").GetValue(voice) > AudioSettings.dspTime).ToArray();
                Need(voices.Length == 3, "simultaneous drum, bass and melody voices");
                double startDsp = F<double>(player, "_startDsp");
                foreach (var voice in voices)
                {
                    var source = (AudioSource)voice.GetType().GetField("Source").GetValue(voice);
                    double busy = (double)voice.GetType().GetField("BusyUntil").GetValue(voice);
                    Need(Math.Abs(busy - source.clip.samples / (double)source.clip.frequency - startDsp) < 0.00001,
                        "same scheduled DSP start");
                }
                Next(3, 0.85); return;
            }
            if (stage == 3)
            {
                var space = F<BeatSpace>(board, "_space");
                var cursor = F<RectTransform>(board, "_playhead");
                float beat = (float)((AudioSettings.dspTime - F<double>(player, "_startDsp")) * 2);
                Need(cursor.gameObject.activeSelf && Math.Abs(cursor.anchoredPosition.x - space.ToX(beat)) < 8, "DSP playhead");
                voices = player.GetComponents<AudioSource>().Length;
                player.Stop();
                C(lab, "PreviewBoard");
                Next(4, 0.85); return;
            }
            if (stage == 4)
            {
                Need(player.GetComponents<AudioSource>().Length == voices, "voice pool stable on replay");
                player.Stop();
                Next(5, 1.3); return;
            }
            if (stage == 5)
            {
                Need(player.GetComponents<AudioSource>().All(source => !source.isPlaying) && !player.IsPlaying,
                    "stop cancels queued notes");
                var waves = F<System.Collections.Generic.List<DrumCharacter>>(lab, "_waves");
                board.Place(waves[1], 4, 0);
                board.Place(waves[2], 8, 0);
                C(lab, "Save");
                expected = Canonical(board.ToSaveData());
                File.WriteAllText(Path.Combine(Root, "expected-melody.txt"), expected);
                string json = File.ReadAllText(SavePath);
                Need(json.Contains("MidiNote") && json.Contains("Velocity") && json.Contains("instrumentId"), "pitched save fields");
                board.Clear();
                C(lab, "Load");
                Need(Canonical(board.ToSaveData()) == expected, "save/load exact notes and wave types");
                var malformed = JsonUtility.FromJson<BoardSaveData>(json);
                malformed.drums.First(clip => clip.waveKind != WaveKind.Rhythm).instrumentId = "missing";
                File.WriteAllText(SavePath, JsonUtility.ToJson(malformed));
                C(lab, "Load");
                Need(Canonical(board.ToSaveData()) == expected, "failed load retains arrangement");
                File.WriteAllText(SavePath, json);

                var legacy = JsonUtility.FromJson<BoardSaveData>(
                    "{\"trackLengthBeats\":16,\"drums\":[{\"characterName\":\"Legacy Kick\",\"sampleName\":\"kick\",\"waveformType\":\"Kick\",\"category\":0,\"durationBeats\":4,\"startBeat\":0,\"LaneIndex\":0,\"rhythmPoints\":[{\"beat\":0,\"accent\":true},{\"beat\":2}]}]}");
                board.Clear();
                var restore = typeof(RehearsalProgress).GetMethod("RestoreEdits", BindingFlags.Static | BindingFlags.NonPublic);
                Need((bool)restore.Invoke(null, new object[] { board, legacy, DrumLibrary.Load() }), "old drum JSON loads");
                Need(board.Placed.Single().Character.Kind == WaveKind.Rhythm &&
                    board.Placed.Single().Character.PlaybackPoints.Select(point => point.Beat).SequenceEqual(new[] {0f,2f}),
                    "legacy drum events preserved");
                C(lab, "Load");
                C(lab, "PreviewBoard");
                Need(Hits().Count(hit => Hit<WaveKind>(hit, "Kind") != WaveKind.Rhythm) == 16, "restored melody scheduler");
                lab.Close();
                Next(6, 0.5); return;
            }
            if (stage == 6)
            {
                Need(SynthClips() == clipBaseline &&
                    UnityEngine.Object.FindObjectsOfType<PerformancePlayer>().Length == playerBaseline, "close releases voices and PCM clips");
                Need(F<NpcInteractor>(boot, "_npcInteractor").enabled && GameObject.Find("Player").GetComponent<PlayerMovement>().enabled,
                    "room controls restored");
                MelodyLab.Open(); Bind(); C(lab, "Load");
                Need(Canonical(board.ToSaveData()) == expected, "reenter preserves saved notes");
                C(lab, "PreviewBoard");
                if (!Graphics) { Finish(); return; }
                SetSize(1920, 1080);
                Next(7, 1.5); return;
            }
            if (stage == 7)
            {
                Need(Screen.width == 1920 && Screen.height == 1080, "1080p size");
                Canvas.ForceUpdateCanvases();
                ScreenCapture.CaptureScreenshot(Path.Combine(Root, "melody-1920x1080.png"));
                Next(8, 1); return;
            }
            if (stage == 8)
            {
                Need(File.Exists(Path.Combine(Root, "melody-1920x1080.png")), "1080p capture");
                SetSize(1920, 1200); Next(9, 1.5); return;
            }
            if (stage == 9)
            {
                Need(Screen.width == 1920 && Screen.height == 1200, "1200p size");
                Canvas.ForceUpdateCanvases();
                ScreenCapture.CaptureScreenshot(Path.Combine(Root, "melody-1920x1200.png"));
                Next(10, 1); return;
            }
            Need(File.Exists(Path.Combine(Root, "melody-1920x1200.png")), "1200p capture");
            Finish();
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    private static void Finish()
    {
        var root = F<GameObject>(lab, "_root");
        var help = root.transform.Find("Help").GetComponent<RectTransform>();
        var reset = board.transform.Find("ResetView").GetComponent<RectTransform>();
        var helpCorners = new Vector3[4]; var resetCorners = new Vector3[4];
        help.GetWorldCorners(helpCorners); reset.GetWorldCorners(resetCorners);
        Need(helpCorners[0].y > resetCorners[1].y, "header instructions clear reset button");
        Need(!RehearsalProgress.IsFirstComplete && !RehearsalProgress.Level2Unlocked, "chapter progress untouched");
        Debug.Log("MELODY PASS: PCM frequencies, finite duration, velocity, shared DSP, polyphony, lifecycle, legacy and new saves");
        EditorApplication.Exit(0);
    }

    private static void SetSize(int width, int height)
    {
        var assembly = typeof(Editor).Assembly;
        var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
        var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
        var sizes = singleton.GetProperty("instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
        var group = sizesType.GetMethod("GetGroup").Invoke(sizes,
            new[] { Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeGroupType"), "Standalone") });
        var size = Activator.CreateInstance(assembly.GetType("UnityEditor.GameViewSize"), new[] {
            Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeType"), "FixedResolution"),
            (object)width, height, "Melody validation"
        });
        group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
        int index = (int)group.GetType().GetMethod("GetBuiltinCount").Invoke(group, null)
            + (int)group.GetType().GetMethod("GetCustomCount").Invoke(group, null) - 1;
        var viewType = assembly.GetType("UnityEditor.GameView");
        var view = EditorWindow.GetWindow(viewType);
        viewType.GetProperty("selectedSizeIndex", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).SetValue(view, index);
        view.Repaint();
    }
}
