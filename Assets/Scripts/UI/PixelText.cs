using UnityEngine;
using UnityEngine.UI;

namespace WaveTeam.UI
{
    /// <summary>
    /// 像素文字。继承 MaskableGraphic，在 OnPopulateMesh 里给每个字吐一个四边形，
    /// 从 PixelFont 的图集里取 UV —— 每个字都是硬边方块，缩放多少倍都不糊。
    ///
    /// 为什么不用 Text + 自制 Font 资源：Unity 的动态字体没法从 1-bit 位图生成
    /// （Font 资源要么走系统字体，要么走手工排的 bitmap 字表 YAML，都很别扭）。
    /// 直接建网格最直接，而且同一个组件既能用在屏幕空间 UI（信息面板），
    /// 也能用在世界空间（NPC 头顶的名牌），不必写两套。
    ///
    /// 图集是白色 RGB + 墨迹 Alpha，所以颜色直接由 Graphic.color 顶点色染出来，
    /// 一个字一个颜色也没问题（见 OnPopulateMesh 里的顶点着色）。
    /// </summary>
    [AddComponentMenu("波形小队/像素文字")]
    public sealed class PixelText : MaskableGraphic
    {
        public enum Align { Left, Center, Right }

        [SerializeField, TextArea(1, 6)] private string _text = "像素文字";
        [SerializeField] private Align _align = Align.Left;

        /// <summary>一个字体像素占几个 UI 单位。整数倍最清晰（1/2/3…），非整数会出现大小不一的方块。</summary>
        [SerializeField] private float _pixelScale = 2f;

        /// <summary>行距（字体像素），在本行高度之外额外加的空隙。</summary>
        [SerializeField] private int _lineGap = 3;

        public string Text
        {
            get { return _text; }
            set
            {
                if (_text == value) return;
                _text = value;
                SetVerticesDirty();
            }
        }

        public Align Alignment
        {
            get { return _align; }
            set { if (_align != value) { _align = value; SetVerticesDirty(); } }
        }

        public float PixelScale
        {
            get { return _pixelScale; }
            set { if (!Mathf.Approximately(_pixelScale, value)) { _pixelScale = value; SetVerticesDirty(); } }
        }

        public int LineGap
        {
            get { return _lineGap; }
            set { if (_lineGap != value) { _lineGap = value; SetVerticesDirty(); } }
        }

        /// <summary>图集交给 Graphic 当 _MainTex 用（UI/Default 材质的 _MainTex 是 PerRendererData）。</summary>
        public override Texture mainTexture
        {
            get
            {
                var font = PixelFont.Instance;
                return font != null && font.Atlas != null ? font.Atlas : s_WhiteTexture;
            }
        }

        /// <summary>这套字的整体尺寸（UI 单位），用来给父物体定 RectTransform 大小。</summary>
        public Vector2 PreferredSize
        {
            get
            {
                var font = PixelFont.Instance;
                if (font == null || string.IsNullOrEmpty(_text)) return Vector2.zero;
                // 用吸附后的缩放：版面尺寸要和实际画出来的一致，否则面板按 PreferredSize
                // 排出来的行距会对不上字，字一变大就叠在一起。
                float scale = EffectiveScale();
                int w = 0;
                foreach (var line in _text.Split('\n'))
                    w = Mathf.Max(w, font.MeasureWidth(line));
                int lines = font.LineCount(_text);
                float h = lines * font.CellH + (lines - 1) * _lineGap;
                return new Vector2(w * scale, h * scale);
            }
        }

        /// <summary>
        /// 实际使用的缩放 —— 把 _pixelScale 吸附到「屏幕像素的整数倍」。
        ///
        /// 点阵字必须整倍缩放。一个 16 像素宽的字形如果落在 21.3 个屏幕像素上，
        /// 点采样会让某些列占 2 像素、某些占 1 像素，笔画粗细不匀 —— 看上去就是糊、
        /// 就是「字体没显示对」。吸附之后每个字形像素恒定占 N 个屏幕像素，才是像素风。
        ///
        /// 注意吸附是**按屏幕像素**做的，不是按 UI 单位：参考分辨率 1920 宽的画布
        /// 在 1280 宽的窗口下 scaleFactor 是 0.667，此时 pixelScale=2 其实只占 1.33 个
        /// 屏幕像素，同样不整。所以要乘上 scaleFactor 再取整。
        /// </summary>
        private float EffectiveScale()
        {
            if (_pixelScale <= 0f) return 0.0001f;

            float pxPerUnit = ScreenPixelsPerUnit();
            if (pxPerUnit <= 0f) return _pixelScale;

            float onScreen = _pixelScale * pxPerUnit;
            // 不足 1 个屏幕像素就没法整倍了（再吸就成 0），保持原样
            if (onScreen < 1f) return _pixelScale;

            return Mathf.RoundToInt(onScreen) / pxPerUnit;
        }

        /// <summary>当前节点上「1 个 UI 单位 = 多少屏幕像素」。取不到相机时返回 0。</summary>
        private float ScreenPixelsPerUnit()
        {
            var c = canvas;
            if (c == null) return 0f;

            // 屏幕空间：CanvasScaler 已经把「窗口 / 参考分辨率」算进 scaleFactor 了
            if (c.renderMode != RenderMode.WorldSpace) return c.scaleFactor;

            // 世界空间：正交相机下 1 个世界单位占 Screen.height / (2 * 半高) 个屏幕像素；
            // 再乘自身 lossyScale 才是「1 个 UI 单位」占多少（名牌那层 canvas.localScale = 1）。
            var cam = c.worldCamera != null ? c.worldCamera : Camera.main;
            if (cam == null || !cam.orthographic) return 0f;

            float pxPerWorld = Screen.height / (2f * cam.orthographicSize);
            return pxPerWorld * transform.lossyScale.y;
        }

        // 已经吐出过网格没有。见 Update() 里的兜底说明。
        private bool _hasMesh;

        // 兜底自绘用的家什：绕开 uGUI 直接给 CanvasRenderer 塞网格。
        private Mesh _fallbackMesh;
        private VertexHelper _fallbackVh;
        private int _staleFrames;

        /// <summary>
        /// 兜底重建。
        ///
        /// 起因是排查过的一个真问题：Prefab/运行时新建的 UI 节点上，Graphic 的
        /// canvasRenderer 可能一直是 null —— Graphic 只声明了 [RequireComponent(RectTransform)]，
        /// 没有 CanvasRenderer，运行时 AddComponent 不会顺手补，只能指望 canvasRenderer 的
        /// getter 懒加载。那个兜底一旦失效，Graphic.Rebuild 第一行的
        ///     if (canvasRenderer == null || canvasRenderer.cull) return;
        /// 就静默早退，整条几何重建链路被跳过：组件活着、Rebuild 确实被调用、
        /// rect 也是正的，可 OnPopulateMesh 一次都不进，屏幕上什么都没有，日志里一个错都没有。
        ///
        /// 根治办法是在 Create 里就把 CanvasRenderer 建好（见那里）。这里留的是第二道网：
        /// 先按常规请求一次重建，连着几帧都要不来网格，就自己建网格直接交给
        /// CanvasRenderer（见 BuildMeshDirectly）—— 那条路不经过脏标记、重建队列和
        /// PerformUpdate，基类怎么想都拦不住。
        ///
        /// 必须放在 Update 而不是 Rebuild 里：PerformUpdate 期间禁止再往重建队列里加东西，
        /// 在 Rebuild 里调 SetVerticesDirty() 会被判为非法并报错。
        /// </summary>
        private void Update()
        {
            if (_hasMesh) return;
            if (string.IsNullOrEmpty(_text)) return;
            if (!isActiveAndEnabled) return;

            SetVerticesDirty();

            // 注意这里**不**在 SetVerticesDirty 里清零 _staleFrames：那句每帧都会执行，
            // 清完再自增永远到不了 3，兜底就成了死代码。计数只由发布 _hasMesh 来重置。
            if (++_staleFrames < 3) return;

            BuildMeshDirectly();
            // 不论 OnPopulateMesh 有没有早退（字体没载入时会），都就此收手，
            // 免得每帧重来。之后再有真正的重画请求会把 _hasMesh 拨回 false。
            _hasMesh = true;
        }

        /// <summary>任何「需要重画」的请求都把兜底开关拨回去，交给 Update 重新盯。</summary>
        public override void SetVerticesDirty()
        {
            base.SetVerticesDirty();
            _hasMesh = false;
        }

        /// <summary>
        /// 绕开 uGUI 重建链路，直接把网格塞给 CanvasRenderer。
        ///
        /// 排版代码不复制一份，而是复用 OnPopulateMesh —— 自己喂一个 VertexHelper 进去、
        /// 把结果 FillMesh 出来即可，两边永远一致。
        /// </summary>
        private void BuildMeshDirectly()
        {
            var cr = canvasRenderer;
            if (cr == null) return;

            if (_fallbackMesh == null)
            {
                _fallbackMesh = new Mesh { name = "PixelTextMesh", hideFlags = HideFlags.DontSave };
                _fallbackVh = new VertexHelper();
            }

            _fallbackMesh.Clear();
            _fallbackVh.Clear();
            OnPopulateMesh(_fallbackVh);
            _fallbackVh.FillMesh(_fallbackMesh);

            // 材质走 materialForRendering，带上 Mask 之类的 IMaterialModifier，
            // 和被裁切时 uGUI 自己设的是同一份。
            cr.materialCount = 1;
            cr.SetMaterial(materialForRendering, 0);
            cr.SetTexture(mainTexture);
            cr.SetMesh(_fallbackMesh);
        }

        // 分辨率变了，吸附出来的整倍数跟着变，得重新吐一遍网格。
        // 每帧只比一个 int，比在 EffectiveScale 里缓存依赖便宜。
        private int _builtScreenHeight;

        private void LateUpdate()
        {
            if (Screen.height == _builtScreenHeight) return;
            _builtScreenHeight = Screen.height;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var font = PixelFont.Instance;
            if (font == null || string.IsNullOrEmpty(_text)) return;

            float scale = EffectiveScale();
            Rect rect = GetPixelAdjustedRect();
            // AddVert 收的是 Color32，这里显式转一次：字色是逐顶点写的，
            // 所以同一段文字里不同字可以不同色（面板上「性格」那行就是用 TraitColor 染的）。
            Color32 baseColor = color;

            float lineStep = (font.CellH + _lineGap) * scale;
            float y = rect.yMax;

            foreach (var line in _text.Split('\n'))
            {
                float lineW = font.MeasureWidth(line) * scale;
                float x;
                switch (_align)
                {
                    case Align.Center: x = rect.center.x - lineW * 0.5f; break;
                    case Align.Right: x = rect.xMax - lineW; break;
                    default: x = rect.xMin; break;
                }

                for (int i = 0; i < line.Length; i++)
                {
                    char c = line[i];
                    Vector2 uvMin, uvMax;
                    if (font.GetUv(c, out uvMin, out uvMax))
                    {
                        float w = font.CellW * scale;
                        float h = font.CellH * scale;
                        // 每格顶边对齐行顶：所有字共用同一个绘制原点（字体的 ascender 左上），
                        // 所以汉字和西文的基线天然对齐，不必逐字调位置。
                        AddQuad(vh, x, y, w, h, uvMin, uvMax, baseColor);
                    }
                    x += font.AdvanceOf(c) * scale;
                }
                y -= lineStep;
            }
            _hasMesh = true;   // 兜底开关：这一帧确实吐出网格了，Update 不用再补脏标记
        }

        private static void AddQuad(VertexHelper vh, float x, float y, float w, float h,
                                    Vector2 uvMin, Vector2 uvMax, Color32 c)
        {
            int i = vh.currentVertCount;
            vh.AddVert(new Vector3(x, y - h, 0f), c, new Vector2(uvMin.x, uvMin.y));
            vh.AddVert(new Vector3(x, y, 0f), c, new Vector2(uvMin.x, uvMax.y));
            vh.AddVert(new Vector3(x + w, y, 0f), c, new Vector2(uvMax.x, uvMax.y));
            vh.AddVert(new Vector3(x + w, y - h, 0f), c, new Vector2(uvMax.x, uvMin.y));
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i, i + 2, i + 3);
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            SetVerticesDirty();
        }
#endif

        /// <summary>建一个像素文字节点，并按内容把 RectTransform 调到刚好包住。</summary>
        public static PixelText Create(string name, Transform parent, string text,
                                       float pixelScale, Color color, Align align = Align.Left)
        {
            // 先把空物体挂到父节点下，最后才加 PixelText。
            //
            // 反过来写（new GameObject(name, typeof(RectTransform), typeof(PixelText)) 再 SetParent）
            // 会让组件在**游离状态**下 OnEnable：那一刻它头上还没有 Canvas，Graphic.OnEnable 里的
            // CacheCanvas() 拿到 null 并据此注册，之后再挂父节点不一定能把它排回重建队列 ——
            // 表现就是组件活着、参数全对，但 OnPopulateMesh 一次都不被调用，屏幕上什么都没有。
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            // 必须显式补 CanvasRenderer。
            //
            // Graphic 上只有 [RequireComponent(typeof(RectTransform))]，**没有** CanvasRenderer
            // （见 com.unity.ugui 的 Graphic.cs 类声明）。编辑器里从菜单建 UI 时是编辑器顺手加的，
            // 运行时 new GameObject + AddComponent 则不会 —— 只剩 Graphic.canvasRenderer 的
            // getter 兜底懒加。而那个兜底一旦失效，Graphic.Rebuild 第一行的
            //     if (canvasRenderer == null || canvasRenderer.cull) return;
            // 就直接早退：几何重建整条链路被跳过，OnPopulateMesh 一次都不进，
            // 屏幕上没有一个字、也不报任何错。这正是之前查到的东西。
            if (go.GetComponent<CanvasRenderer>() == null &&
                go.AddComponent<CanvasRenderer>() == null)
                Debug.LogError("[像素字体] 无法为 '" + name + "' 添加 CanvasRenderer，该文字不会显示。");

            var t = go.AddComponent<PixelText>();
            t._text = text;
            t._pixelScale = pixelScale;
            t._align = align;
            t.color = color;
            t.raycastTarget = false;

            var rt = (RectTransform)go.transform;
            var size = t.PreferredSize;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            return t;
        }
    }
}
