using UnityEngine;
using UnityEngine.UI;
using WaveTeam.Core;

namespace WaveTeam.UI
{
    /// <summary>
    /// 角色信息面板：左边放大版人物（同样是方块占位，比例和场景里那个一致），
    /// 右边依次是 性格 / 等级 / 目前波形 / 正在进行的任务，底部一排功能按钮（占位）。
    ///
    /// 关闭方式给了三个：右下角「关闭」按钮、Esc、点面板外的暗底。
    /// 面板是自建自销的 —— Open 时整棵建出来，Close 时 Destroy 掉自己并回调上层，
    /// 不像音轨板那样常驻，省得留一堆隐藏节点。
    ///
    /// 文字全部用 PixelText（不用 UIFactory 那套内置 Arial），这样面板和角色头顶的
    /// 名牌是同一种字形。Canvas 的参考分辨率是 1920×1080，所以这里 1 个单位 = 1 参考像素，
    /// pixelScale=2 就是「一个字体像素占 2 参考像素」，一个汉字 32×34，正文大小刚好。
    /// </summary>
    public sealed class CharacterPanel : MonoBehaviour
    {
        private const float PanelW = 1140f;
        private const float PanelH = 660f;

        private const float TitleScale = 4f;
        private const float TextScale = 2f;

        // 右栏内部坐标（相对右栏左上角）。标签最长的「正在进行的任务」是 7 个字，
        // 在 TextScale=2 下一个汉字 32 单位 = 224，所以值必须从 250 之后起，否则两栏会叠。
        // 值的可用宽度 = 660 − 250 = 410，416 就超了，故每行最多 12 个字（12×32 = 384）。
        private const float LabelX = 0f;
        private const float ValueX = 250f;
        private const int ValueWrapChars = 12;

        private System.Action _onClose;
        private bool _closing;

        public NpcProfile Profile { get; private set; }

        /// <summary>建出面板并显示。parent 一般是 GameBootstrap 的那张 Canvas。</summary>
        public static CharacterPanel Open(Transform parent, NpcProfile profile, System.Action onClose)
        {
            var go = new GameObject("CharacterPanel", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            UIFactory.Stretch((RectTransform)go.transform);

            var panel = go.AddComponent<CharacterPanel>();
            panel._onClose = onClose;
            panel.Profile = profile;
            panel.Build(profile);
            return panel;
        }

        public void Close()
        {
            if (_closing) return;      // 点暗底和按 Esc 可能同帧都触发，只放行一次
            _closing = true;
            var cb = _onClose;
            _onClose = null;
            Destroy(gameObject);
            if (cb != null) cb();
        }

        private void Build(NpcProfile p)
        {
            // 整屏暗底：压住排练室，同时吃掉点击（面板外的点击直接关掉）
            var backdrop = GetComponent<Image>();
            backdrop.color = new Color(0f, 0f, 0f, 0.62f);
            backdrop.raycastTarget = true;
            var closeBtn = backdrop.gameObject.AddComponent<Button>();
            closeBtn.transition = Selectable.Transition.None;
            closeBtn.onClick.AddListener(Close);

            var box = UIFactory.CreatePanel("Box", transform, new Color(0.09f, 0.11f, 0.18f, 0.99f));
            box.raycastTarget = true;      // 挡住暗底，点面板内部不会误关
            UIFactory.Place((RectTransform)box.transform, new Vector2(0.5f, 0.5f),
                            new Vector2(PanelW, PanelH), Vector2.zero);

            BuildTitle(box.transform, p);
            BuildPortrait(box.transform, p);
            BuildStats(box.transform, p);
            BuildActions(box.transform);
        }

        private void BuildTitle(Transform box, NpcProfile p)
        {
            var title = PixelText.Create("Title", box, p.Name, TitleScale, Color.white);
            UIFactory.Place((RectTransform)title.transform, new Vector2(0f, 1f),
                            title.PreferredSize, new Vector2(44f, -34f));

            var line = UIFactory.CreatePanel("Divider", box, UIStyle.Line);
            line.raycastTarget = false;
            UIFactory.Place((RectTransform)line.transform, new Vector2(0.5f, 1f),
                            new Vector2(PanelW - 88f, 2f), new Vector2(0f, -110f));
        }

        /// <summary>
        /// 放大版人物：和 NpcActor 用同一套「身 + 头」比例（头宽 / 身宽 = 0.44/0.62 = 0.71），
        /// 换到 UI 上就是两个 Image，免得面板里的人和一场景里的人长得不一样。
        /// </summary>
        private void BuildPortrait(Transform box, NpcProfile p)
        {
            var frame = UIFactory.CreatePanel("Portrait", box, new Color(0.13f, 0.17f, 0.27f, 1f));
            frame.raycastTarget = false;
            var frameRt = (RectTransform)frame.transform;
            UIFactory.Place(frameRt, new Vector2(0f, 1f), new Vector2(340f, 400f), new Vector2(40f, -140f));

            var c = TraitColor.Of(p.Trait);
            AddBlock(frameRt, "Body", new Vector2(120f, 210f), new Vector2(0f, 40f), c);
            AddBlock(frameRt, "Head", new Vector2(86f, 86f), new Vector2(0f, 250f), Lighten(c, 0.25f));

            // 素材注释：这几个方块以后会被真正的人物立绘替换掉
            var hint = PixelText.Create("Hint", frameRt, "放大人物素材（占位）", 1.4f,
                                        new Color(0.55f, 0.60f, 0.72f), PixelText.Align.Center);
            UIFactory.Place((RectTransform)hint.transform, new Vector2(0.5f, 0f),
                            hint.PreferredSize, new Vector2(0f, 10f));
        }

        private static void AddBlock(Transform parent, string name, Vector2 size, Vector2 pos, Color color)
        {
            var img = UIFactory.CreatePanel(name, parent, color);
            img.raycastTarget = false;
            UIFactory.Place((RectTransform)img.transform, new Vector2(0.5f, 0f), size, pos);
        }

        private void BuildStats(Transform box, NpcProfile p)
        {
            var col = UIFactory.CreateRect("Stats", box);
            UIFactory.Place(col, new Vector2(0f, 1f), new Vector2(660f, 400f), new Vector2(430f, -140f));

            float y = 0f;
            AddRow(col, ref y, "性格", p.TraitText, TraitColor.Of(p.Trait));
            AddRow(col, ref y, "等级", "Lv." + p.Level, Color.white);
            AddRow(col, ref y, "目前波形", p.WaveformText, Color.white);
            AddRow(col, ref y, "正在进行的任务", Wrap(p.Task, ValueWrapChars),
                   new Color(0.80f, 0.85f, 0.95f));
        }

        private static void AddRow(RectTransform col, ref float y, string label, string value, Color valueColor)
        {
            var lab = PixelText.Create("L_" + label, col, label, TextScale,
                                       new Color(0.52f, 0.58f, 0.72f));
            UIFactory.Place((RectTransform)lab.transform, new Vector2(0f, 1f),
                            lab.PreferredSize, new Vector2(LabelX, y));

            var val = PixelText.Create("V_" + label, col, value, TextScale, valueColor);
            UIFactory.Place((RectTransform)val.transform, new Vector2(0f, 1f),
                            val.PreferredSize, new Vector2(ValueX, y));

            // 行高跟着值走：任务那行可能被折成两行，不能被下一行压住
            y -= Mathf.Max(56f, val.PreferredSize.y + 26f);
        }

        private void BuildActions(Transform box)
        {
            float x = 44f;
            foreach (var label in NpcRoster.ActionPlaceholders)
            {
                string act = label;      // 显式拷一份给闭包，避免捕获到循环变量
                MakeButton(box, "Act_" + label, label, new Vector2(160f, 56f),
                           new Vector2(0f, 0f), new Vector2(x, 40f),
                           new Color(0.22f, 0.27f, 0.40f),
                           () => OnPlaceholderAction(act));
                x += 180f;
            }

            MakeButton(box, "Close", "关闭", new Vector2(170f, 56f),
                       new Vector2(1f, 0f), new Vector2(-44f, 40f),
                       new Color(0.72f, 0.32f, 0.32f), Close);

            var hint = PixelText.Create("EscHint", box, "Esc 关闭", 1.4f,
                                        new Color(0.50f, 0.55f, 0.68f));
            UIFactory.Place((RectTransform)hint.transform, new Vector2(1f, 0f),
                            hint.PreferredSize, new Vector2(-224f, 56f));
        }

        /// <summary>
        /// 自己搭而不是用 UIFactory.CreateButton：那个按钮的标签是内置 Arial 的 Text，
        /// 和面板其他地方的点阵字不是一套。这里按钮壳子照抄，标签换成 PixelText。
        /// </summary>
        private static Button MakeButton(Transform parent, string name, string label, Vector2 size,
                                         Vector2 anchor, Vector2 pos, Color color,
                                         System.Action onClick = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = true;

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            var text = PixelText.Create("Label", go.transform, label, TextScale, Color.white,
                                        PixelText.Align.Center);
            var rt = (RectTransform)text.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = text.PreferredSize;

            UIFactory.Place((RectTransform)go.transform, anchor, size, pos);
            return btn;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) Close();
        }

        /// <summary>
        /// 功能按钮还没设计，先用日志明确回应一下 —— 点了完全没动静
        /// 会被当成「按钮坏了」，有行日志至少说明点击是通的。
        /// </summary>
        private void OnPlaceholderAction(string label)
        {
            Debug.Log("[角色面板]「" + label + "」是占位按钮，功能未实现。作用对象：" +
                      (Profile != null ? Profile.Name : "?"));
        }

        /// <summary>按字数硬折行。中文字形等宽，数个数就够了，不必量像素。</summary>
        private static string Wrap(string s, int perLine)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= perLine) return s;
            var sb = new System.Text.StringBuilder(s.Length + 4);
            for (int i = 0; i < s.Length; i++)
            {
                if (i > 0 && i % perLine == 0) sb.Append('\n');
                sb.Append(s[i]);
            }
            return sb.ToString();
        }

        private static Color Lighten(Color c, float amount)
        {
            return new Color(Mathf.Lerp(c.r, 1f, amount),
                             Mathf.Lerp(c.g, 1f, amount),
                             Mathf.Lerp(c.b, 1f, amount), c.a);
        }
    }
}
