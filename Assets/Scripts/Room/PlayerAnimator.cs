using UnityEngine;

namespace WaveTeam.Room
{
    /// <summary>
    /// 主角的像素小人 + 行走动画。
    ///
    /// 素材：Assets/Resources/Characters/Player/player_&lt;down|up|side&gt;_&lt;0..3&gt;.png，
    /// 每帧 32×48、PPU 32（= 1.0×1.5 世界单位）、轴心在脚底。
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

        /// <summary>每秒帧数。8 帧/秒是像素游戏走路的常见节奏，再快会像在抽筋。</summary>
        public const float Fps = 8f;

        /// <summary>脚底相对 Player 轴心的偏移：胶囊体 0.5×1 居中，所以底边在 −0.5。</summary>
        public const float FeetOffset = -0.5f;

        private enum Facing { Down, Up, Side }

        private SpriteRenderer _sr;
        private Sprite[] _down = new Sprite[4];
        private Sprite[] _up = new Sprite[4];
        private Sprite[] _side = new Sprite[4];
        private Facing _facing = Facing.Down;
        private bool _flip;
        private int _frame;
        private float _timer;

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
                float step = 1f / Fps;
                while (_timer >= step)
                {
                    _timer -= step;
                    _frame = (_frame + 1) % 4;
                }
            }
            else
            {
                // 站住时回到第 0 帧（双脚着地的那一帧）
                _frame = 0;
                _timer = 0f;
            }

            Apply();
            UpdateSorting();
        }

        private void Apply()
        {
            if (_sr == null) return;
            var set = _facing == Facing.Up ? _up : (_facing == Facing.Side ? _side : _down);
            if (set != null && set[_frame] != null) _sr.sprite = set[_frame];
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
                string n = s.name;                                  // player_down_0
                int cut = n.LastIndexOf('_');
                if (cut <= 0) continue;
                int idx;
                if (!int.TryParse(n.Substring(cut + 1), out idx) || idx < 0 || idx > 3) continue;

                string dir = n.Substring(0, cut);
                if (dir.EndsWith("down")) _down[idx] = s;
                else if (dir.EndsWith("up")) _up[idx] = s;
                else if (dir.EndsWith("side")) _side[idx] = s;
            }
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

            // 贴图 PPU=32、轴心在脚底 → 直接把子物体放到胶囊底边，不用缩放
            child.localPosition = new Vector3(0f, FeetOffset, 0f);
            child.localScale = Vector3.one;
            child.localRotation = Quaternion.identity;
            _sr.color = Color.white;
            _sr.flipX = false;
        }
    }
}
