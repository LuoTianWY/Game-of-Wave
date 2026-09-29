using UnityEngine;
using UnityEngine.UI;
using WaveTeam.Core;
using WaveTeam.UI;

namespace WaveTeam.Room
{
    /// <summary>
    /// 排练室里的一个角色。美术资源到位之前，用「身 + 头」两个方块当占位素材，
    /// 颜色取自性格（和音轨板里 TraitColor 是同一套，玩家能凭颜色认出性格）。
    ///
    /// 头顶挂一块**世界空间 Canvas**，里面两个像素文字：名字常显，互动提示默认隐藏。
    /// 用世界空间 Canvas 而不是 TextMesh，是为了和 GameBootstrap 那套 uGUI 共用同一个
    /// PixelText 组件 —— 屏幕上和头顶的字是同一份实现，看着才一致。
    ///
    /// 角色是**运行时生成**的（由 RehearsalRoomSpawner 创建），不进场景文件。
    /// 这样坐标改了不用重存场景，也不必给编辑器写序列化逻辑。
    /// </summary>
    public sealed class NpcActor : MonoBehaviour
    {
        // 占位块尺寸（世界单位）；脚底在 transform.position
        private const float BodyW = 0.62f;
        private const float BodyH = 1.10f;
        private const float HeadSize = 0.44f;
        private const float TotalHeight = BodyH + HeadSize;   // 1.54

        private const float NamePixelScale = 0.020f;          // 一个字体像素 = 0.02 世界单位
        private const float PromptPixelScale = 0.016f;

        private static Sprite _blockSprite;

        private SpriteRenderer _body;
        private SpriteRenderer _head;
        private PixelText _nameText;
        private PixelText _promptText;
        private Color _bodyColor;

        public NpcProfile Profile { get; private set; }

        /// <summary>互动判定用的锚点：角色脚底（也就是 transform.position）。</summary>
        public Vector3 Anchor { get { return transform.position; } }

        public static NpcActor Create(NpcProfile profile, Vector3 feetPosition, Transform parent)
        {
            var go = new GameObject("NPC_" + profile.Name);
            go.transform.SetParent(parent, false);
            go.transform.position = feetPosition;
            var npc = go.AddComponent<NpcActor>();
            npc.Build(profile);
            return npc;
        }

        private void Build(NpcProfile profile)
        {
            Profile = profile;
            _bodyColor = TraitColor.Of(profile.Trait);

            int order = SortingOrderFor(transform.position.y);

            _body = MakeBlock("Body", new Vector2(BodyW, BodyH),
                              new Vector3(0f, BodyH * 0.5f, 0f), _bodyColor, order);
            _head = MakeBlock("Head", new Vector2(HeadSize, HeadSize),
                              new Vector3(0f, BodyH + HeadSize * 0.5f, 0f),
                              Lighten(_bodyColor, 0.25f), order);

            BuildNameplate(order);
        }

        private SpriteRenderer MakeBlock(string name, Vector2 size, Vector3 localPos,
                                         Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            // 占位块没有碰撞体，直接用 localScale 拉成想要的大小（共用一张 1×1 白图）
            go.transform.localScale = new Vector3(size.x, size.y, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = BlockSprite();
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        /// <summary>
        /// 名牌：世界空间 Canvas，1 个 UI 单位 = 1 世界单位（不设 CanvasScaler）。
        /// Canvas 的 sortingOrder 比角色本身高一截，保证名字压在同排道具上面。
        ///
        /// 轴点务必显式设成 (0.5, 0)：新建的 RectTransform 默认是中心轴点，
        /// 而锚点 (0.5,0) / (0.5,1) 指的是**矩形底边/顶边**。轴点不设的话，
        /// 「底边」会落在 canvas 中心，名牌整体下沉半个高度，正好糊在角色脸上。
        /// 这里把轴点放在底边，于是 localPosition.y 就是名牌区域的下沿，好算。
        /// </summary>
        private void BuildNameplate(int bodyOrder)
        {
            const float CanvasH = 0.80f;
            var canvasGo = new GameObject("Nameplate", typeof(RectTransform), typeof(Canvas));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = bodyOrder + 60;

            var rt = (RectTransform)canvasGo.transform;
            rt.pivot = new Vector2(0.5f, 0f);
            rt.localScale = Vector3.one;
            rt.sizeDelta = new Vector2(3f, CanvasH);
            rt.localPosition = new Vector3(0f, TotalHeight + 0.30f, 0f);   // 下沿 = 1.84

            // 名字贴顶边 → 实际占 2.30 ~ 2.64，比头顶(1.54)高出 0.76
            var name = PixelText.Create("Name", rt, Profile.Name, NamePixelScale,
                                        Color.white, PixelText.Align.Center);
            var nrt = (RectTransform)name.transform;
            nrt.anchorMin = nrt.anchorMax = nrt.pivot = new Vector2(0.5f, 1f);
            nrt.anchoredPosition = Vector2.zero;
            _nameText = name;

            // 提示挂在名字正下方 → 实际占 1.93 ~ 2.20，离头顶(1.54)还有 0.39
            var prompt = PixelText.Create("Prompt", rt, "按 F 互动", PromptPixelScale,
                                          new Color(1f, 0.88f, 0.42f), PixelText.Align.Center);
            var prt = (RectTransform)prompt.transform;
            prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 1f);
            prt.anchoredPosition = new Vector2(0f, -0.44f);
            _promptText = prompt;
            prompt.gameObject.SetActive(false);
        }

        /// <summary>玩家进入/离开互动范围时调用。离开就立刻收起提示。</summary>
        public void SetPromptVisible(bool visible)
        {
            if (_promptText != null && _promptText.gameObject.activeSelf != visible)
                _promptText.gameObject.SetActive(visible);
        }

        /// <summary>被选为互动目标时整体提亮一点，让玩家知道 F 会作用在谁身上。</summary>
        public void SetHighlight(bool on)
        {
            if (_body != null) _body.color = on ? Lighten(_bodyColor, 0.30f) : _bodyColor;
            if (_nameText != null) _nameText.color = on ? new Color(1f, 0.94f, 0.72f) : Color.white;
        }

        // 和道具同一套排序规则：越靠下（越靠近镜头）越靠前
        private static int SortingOrderFor(float worldY)
        {
            return Mathf.RoundToInt(-worldY * 100f);
        }

        private static Color Lighten(Color c, float amount)
        {
            return new Color(Mathf.Lerp(c.r, 1f, amount),
                             Mathf.Lerp(c.g, 1f, amount),
                             Mathf.Lerp(c.b, 1f, amount), c.a);
        }

        /// <summary>
        /// 运行时用的纯白 1×1 精灵。不能像 Player 占位块那样去取编辑器内置资源
        /// （AssetDatabase 是编辑器专属），所以直接在内存里造一张 —— 角色是运行时
        /// 生成的、不进场景，不存在「存不下来」的问题。
        /// </summary>
        private static Sprite BlockSprite()
        {
            if (_blockSprite == null)
            {
                // whiteTexture 是 4×4，PPU 取 4 正好得到 1×1 世界单位
                _blockSprite = Sprite.Create(Texture2D.whiteTexture,
                                             new Rect(0f, 0f, 4f, 4f),
                                             new Vector2(0.5f, 0.5f), 4f);
            }
            return _blockSprite;
        }
    }
}
