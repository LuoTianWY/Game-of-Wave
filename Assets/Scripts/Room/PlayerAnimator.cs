using UnityEngine;

namespace WaveTeam.Room
{
    /// <summary>
    /// 主角的像素小人 + 行走动画。
    ///
    /// 素材：Assets/Resources/Characters/Player/player_&lt;down|up|side&gt;_&lt;0..3&gt;.png        行走
    ///       Assets/Resources/Characters/Player/player_&lt;down|up|side&gt;_idle_&lt;0..1&gt;.png  站立呼吸
    /// 每帧 32×52、PPU 32（= 1.0×1.625 世界单位）、轴心在脚底。
    /// 由 Tools/make_player_sprite.py 生成，改了配色或体型重跑那个脚本即可。
    ///
    /// 朝向按输入的主轴决定（和 PlayerMovement 用的是同一套 Input.GetAxisRaw）：
    /// 上下用 down / up 两组，左右共用 side 一组、靠 flipX 镜像。
    /// 不做真正的「走路位移检测」，所以即使以后换了移动实现（比如不用刚体），
    /// 只要输入轴还是这两个名字，动画就照常工作。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerAnimator : MonoBehaviour
    {
        /// <summary>Resources 下的目录名。</summary>
        public const string ResourceDir = "Characters/Player";

        /// <summary>默认走路帧率。8 帧/秒是像素游戏走路的常见节奏，再快会像在抽筋。</summary>
        public const float DefaultWalkFps = 8f;

        /// <summary>
        /// 默认呼吸节拍：2 帧/秒 × 6 拍 ≈ 3 秒一次呼吸。
        /// 只有「吸气 / 呼气」两帧贴图，靠每拍停留 3 次的节奏把两帧撑出一个缓慢的起伏，
        /// 免得 1px 的变化一秒来回好几趟、看着像在发抖。
        /// 1 像素是像素画能干净表现的最小位移，想让起伏更轻只能把节奏放慢。
        /// </summary>
        public const float DefaultIdleFps = 2f;

        /// <summary>一轮呼吸的节奏表：0 = 呼气（原样），1 = 吸气（胸腔抬起 1px）。</summary>
        private static readonly int[] BreathSteps = { 0, 0, 0, 1, 1, 1 };

        /// <summary>脚底相对 Player 轴心的偏移：胶囊体 0.5×1 居中，所以底边在 −0.5。</summary>
        public const float FeetOffset = -0.5f;

        [Header("体型")]
        [Tooltip("主角在世界里的身高（单位=1 格地板）。贴图原尺寸是 1.625，调大只往上长，脚不会陷进地面。")]
        [SerializeField, Range(0.6f, 3f)] private float height = 1.75f;

        [Header("动作频率")]
        [Tooltip("走路每秒播放几帧。8 = 原来的节奏；调到 6 会显得更沉，12 会显得更急。")]
        [SerializeField, Range(2f, 20f)] private float walkFps = DefaultWalkFps;

        [Tooltip("站立呼吸每秒几拍。一整轮是 6 拍，所以一次呼吸的秒数 = 6 ÷ 这个值。2 ≈ 3 秒一次。")]
        [SerializeField, Range(0.4f, 8f)] private float idleFps = DefaultIdleFps;

        /// <summary>当前设定的世界身高（读给别的系统用，比如让 NPC 和主角一样高）。</summary>
        public float WorldHeight { get { return height; } }

        private enum Facing { Down, Up, Side }

        private SpriteRenderer _sr;
        private Sprite[] _down = new Sprite[4];
        private Sprite[] _up = new Sprite[4];
        private Sprite[] _side = new Sprite[4];
        private Sprite[] _idleDown = new Sprite[2];
        private Sprite[] _idleUp = new Sprite[2];
        private Sprite[] _idleSide = new Sprite[2];
        private Facing _facing = Facing.Down;
        private bool _flip;
        private bool _moving;
        private int _frame;
        private float _timer;
        private int _idleStep;
        private float _idleTimer;
        private Transform _visual;
        private float _appliedHeight = float.NaN;

        /// <summary>给运行时兜底用：确保 Player 上有这个组件，并建好 Visual 子物体。</summary>
        public static PlayerAnimator EnsureOn(GameObject player)
        {
            if (player == null) return null;
            var a = player.GetComponent<PlayerAnimator>();
            if (a == null) a = player.AddComponent<PlayerAnimator>();
            return a;
        }

        private void Awake()
        {
            LoadSprites();
            EnsureRenderer();
            Apply();
        }

        private void Update()
        {
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            bool moving = Mathf.Abs(h) > 0.01f || Mathf.Abs(v) > 0.01f;
            _moving = moving;

            if (moving)
            {
                // 主轴决定朝向：横向优先，斜着走时看起来更自然
                if (Mathf.Abs(h) >= Mathf.Abs(v))
                {
                    _facing = Facing.Side;
                    _flip = h < 0f;
                }
                else
                {
                    _facing = v > 0f ? Facing.Up : Facing.Down;
                    _flip = false;
                }

                _timer += Time.deltaTime;
                float step = 1f / Mathf.Max(0.1f, walkFps);
                while (_timer >= step)
                {
                    _timer -= step;
                    _frame = (_frame + 1) % 4;
                }

                // 走动时把呼吸归零，停下后再从「呼气」慢慢起伏，不会突然弹一下
                _idleStep = 0;
                _idleTimer = 0f;
            }
            else
            {
                // 站住时回到第 0 帧（双脚着地的那一帧），并开始呼吸
                _frame = 0;
                _timer = 0f;

                _idleTimer += Time.deltaTime;
                float step = 1f / Mathf.Max(0.1f, idleFps);
                while (_idleTimer >= step)
                {
                    _idleTimer -= step;
                    _idleStep = (_idleStep + 1) % BreathSteps.Length;
                }
            }

            Apply();
            ApplyHeight();
            UpdateSorting();
        }

        private void Apply()
        {
            if (_sr == null) return;
            var set = _facing == Facing.Up ? _up : (_facing == Facing.Side ? _side : _down);
            var idle = _facing == Facing.Up ? _idleUp : (_facing == Facing.Side ? _idleSide : _idleDown);

            Sprite next = null;
            if (!_moving)
            {
                int breathe = BreathSteps[_idleStep];
                if (idle != null && breathe < idle.Length) next = idle[breathe];
            }
            if (next == null && set != null && _frame < set.Length) next = set[_frame];
            if (next != null) _sr.sprite = next;
            _sr.flipX = _flip;
        }

        /// <summary>
        /// 和道具、NPC 用同一套排序（−y×100），走到家具后面会被正确挡住。
        /// 用**脚底**的 y，而不是轴心的 y —— 和 NPC 的约定一致。
        /// </summary>
        private void UpdateSorting()
        {
            if (_sr == null) return;
            _sr.sortingOrder = Mathf.RoundToInt(-(transform.position.y + FeetOffset) * 100f);
        }

        private void LoadSprites()
        {
            var all = Resources.LoadAll<Sprite>(ResourceDir);
            if (all == null || all.Length == 0)
            {
                Debug.LogWarning("[主角] 找不到 " + ResourceDir + " 下的行走贴图，" +
                                 "主角会保持默认方块。跑 Tools/make_player_sprite.py 可以重新生成。");
                return;
            }

            foreach (var s in all)
            {
                if (s == null) continue;
                // player_down_0 / player_down_idle_0
                var parts = s.name.Split('_');
                if (parts.Length < 3 || parts[0] != "player") continue;

                int idx;
                if (!int.TryParse(parts[parts.Length - 1], out idx)) continue;

                bool idle = parts.Length >= 4 && parts[2] == "idle";
                switch (parts[1])
                {
                    case "down": Put(idle ? _idleDown : _down, idx, s); break;
                    case "up": Put(idle ? _idleUp : _up, idx, s); break;
                    case "side": Put(idle ? _idleSide : _side, idx, s); break;
                }
            }
        }

        /// <summary>越界的帧号直接忽略——多放一张图进文件夹也不会让动画报错。</summary>
        private static void Put(Sprite[] set, int idx, Sprite s)
        {
            if (set != null && idx >= 0 && idx < set.Length) set[idx] = s;
        }

        /// <summary>找到（或建出）Visual 子物体，把 SpriteRenderer 调成贴图要求的参数。</summary>
        private void EnsureRenderer()
        {
            var child = transform.Find("Visual");
            if (child == null)
            {
                var go = new GameObject("Visual");
                go.transform.SetParent(transform, false);
                child = go.transform;
            }

            _sr = child.GetComponent<SpriteRenderer>();
            if (_sr == null) _sr = child.gameObject.AddComponent<SpriteRenderer>();
            _visual = child;

            // 贴图 PPU=32、轴心在脚底 → 直接把子物体放到胶囊底边，不用缩放
            child.localPosition = new Vector3(0f, FeetOffset, 0f);
            child.localRotation = Quaternion.identity;
            _sr.color = Color.white;
            _sr.flipX = false;

            _appliedHeight = float.NaN;
            ApplyHeight();
        }

        /// <summary>
        /// 按 Inspector 里的身高缩放贴图。贴图轴心在脚底，所以放大时只往上长，脚钉在原地。
        /// 每帧检查一次数值有没有变，这样 Play 的时候拖滑块也能立刻看到效果。
        /// </summary>
        private void ApplyHeight()
        {
            if (_visual == null) return;
            if (Mathf.Approximately(height, _appliedHeight)) return;

            var probe = FirstSprite();
            float artHeight = (probe != null && probe.bounds.size.y > 0.001f)
                ? probe.bounds.size.y
                : 1.625f;                                   // 贴图还没导进来时的兜底

            float s = Mathf.Max(0.01f, height) / artHeight;
            _visual.localScale = new Vector3(s, s, 1f);
            _appliedHeight = height;
        }

        /// <summary>随便找一张已经加载的帧，用来量这套贴图原本有多高。</summary>
        private Sprite FirstSprite()
        {
            foreach (var set in new[] { _down, _up, _side, _idleDown, _idleUp, _idleSide })
            {
                if (set == null) continue;
                foreach (var s in set)
                    if (s != null) return s;
            }
            return null;
        }
    }
}
