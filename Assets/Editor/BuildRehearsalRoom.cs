using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace WaveTeam.EditorTools
{
    /// <summary>
    /// 把排练室场景从「几何毛坯」重建成真正的排练室：
    /// 删掉色块占位图块 → 挂上拼好的房间背景 → 按位置摆好抠过背景的道具 → 按新布局重铺碰撞。
    ///
    /// 场景里的房间矩形是世界 x −9.5~8.5、y −6.5~5.5（18×12 单位）。当前背景
    /// room_backdrop_anime_v1.png 是 2560×1440 @ 120px/单位，挂在 (−0.5, −0.5) 严丝合缝。
    ///
    /// 这版背景图上**没有画任何落地家具**——后墙只有海报、挂着的吉他和串灯，地板是干净的。
    /// 实测墙脚线在图像 row 565，换算成世界坐标 y ≈ 0.79，和道具贴墙用的 BaseY 0.85 正好对上。
    /// 墙面上半部分（y 2.2 以上）画着两把挂琴和装饰，太高的道具会挡到，摆位时留意。
    ///
    /// 菜单：波形小队/重建排练室场景（刻意没绑快捷键，原因见 BuildMenu 上方注释）
    /// 首次放进工程时会自动跑一次，之后由上面的菜单手动触发。
    /// </summary>
    public static class BuildRehearsalRoom
    {
        private const string ScenePath = "Assets/Scenes/排练室.unity";
        private const string RoomName = "Room";                 // 生成物的总父节点，重复执行时整体重建
        private const string BackdropAsset = "Assets/Sprites/Room/room_backdrop_anime_v1.png";
        private const string CutoutDir = "Assets/Sprites/Room/CutoutV2/";
        private const string AutoRunPrefKey = "WaveTeam.RehearsalRoom.Built.v3";
        private const string VisualName = "Visual";             // Player 下的占位显示子物体

        /// <summary>毛坯占位对象：全部删掉，只留 Grid 和 Collision。</summary>
        private static readonly string[] BlockoutNames =
        {
            "Wall", "Floor", "Door",
            "Rehearsal Area", "Rest Area", "Discussion Area",
            "Props_Back", "Props_Front",
        };

        private struct Prop
        {
            public string Name;    // 对应 Assets/Sprites/Room/Cutout/<Name>.png
            public float X;
            public float BaseY;    // 道具**底边**贴地的世界 y（脚本按精灵高度反推中心点）
            public float ScaleX;
            public float ScaleY;

            public Prop(string name, float x, float baseY, float scaleX, float scaleY)
            {
                Name = name; X = x; BaseY = baseY; ScaleX = scaleX; ScaleY = scaleY;
            }

            /// <summary>等比缩放的重载，省得每行都写两遍。</summary>
            public Prop(string name, float x, float baseY, float scale)
                : this(name, x, baseY, scale, scale)
            {
            }
        }

        /// <summary>
        /// 道具摆放。沿房间宽度分三排——越靠下离镜头越近，缩放越大，
        /// 渲染顺序按 y 自动推（见 SortingOrder），不用手写。
        /// 注意 x 4.0~6.6 是背景上画好的麦架，三排都避开，否则会叠在麦架上。
        /// </summary>
        private static readonly Prop[] Props =
        {
            // 位置和缩放取自组员提交的场景（0232de2），是直接读 排练室.unity 誊出来的原值，
            // 不再另行手改。BaseY = 物体中心的 y − 精灵高度的一半，和 PlaceProps 的算法互逆，
            // 所以这一组数字重建出来的结果和场景逐像素一致。
            // 顺序按 BaseY 从大到小（后 → 前）；实际遮挡由 PlaceProps 按 y 自动算，不依赖顺序。
            new Prop("keyboard",         1.89f,  0.762f, 1.373f, 1.285f),  // 电子琴
            new Prop("cabinet",          4.46f,  0.433f, 1.014f, 1.090f),  // 木柜
            new Prop("guitar_electric", -1.72f,  0.393f, 1.164f, 1.035f),  // 电吉他
            new Prop("guitar_acoustic", -0.62f,  0.390f, 1.069f, 1.076f),  // 木吉他
            new Prop("bass",             0.55f,  0.386f, 1.024f, 1.006f),  // 贝斯
            new Prop("guitar_case",     -3.90f,  0.373f, 1.366f, 1.140f),  // 吉他琴盒
            new Prop("shelf",            6.47f,  0.136f, 1.568f, 1.591f),  // 书架
            new Prop("guitar_stand",     1.76f, -0.040f, 0.900f, 0.900f),  // 折叠琴架
            new Prop("sofa",            -7.08f, -0.676f, 2.134f, 1.964f),  // 沙发
            new Prop("instrument_case",  6.77f, -0.856f, 1.273f, 1.279f),  // 航空箱
            new Prop("amp",             -4.54f, -1.211f, 0.928f, 0.888f),  // 音箱
            new Prop("drum_kit",         1.41f, -1.590f, 1.500f, 1.598f),  // 架子鼓（排练区核心）
            new Prop("stool",           -0.97f, -1.667f, 0.950f, 0.950f),  // 圆吧凳
            new Prop("mic_stand",       -1.12f, -2.467f, 1.115f, 1.115f),  // 麦克风架
            new Prop("chair",           -2.78f, -5.009f, 1.170f, 1.181f),  // 木椅
            new Prop("chair",           -6.95f, -5.139f, 1.170f, 1.181f),  // 木椅（第二把，场景里显示为 chair (1)）
            new Prop("coffee_table",    -4.88f, -5.421f, 1.868f, 2.160f),  // 茶几
            new Prop("trash_bin",        7.87f, -5.571f, 1.169f, 1.110f),  // 垃圾桶
        };

        // ── 碰撞布局 ──
        // 地板可见范围是 y −6.5~0.96，所以 y>=0 整条都是墙+贴墙家具带，不可走。
        // 留出的活动区：x −8~7、y −5~−1。
        private const int RoomMinX = -9, RoomMaxX = 8;    // 房间外框（含）
        private const int RoomMinY = -6, RoomMaxY = 5;
        private const int WalkMinY = -5, WalkMaxY = -1;   // 活动区

        /// <summary>
        /// 前置检查：背景图与全部道具精灵是否都已导入。
        /// 自动重建是在脚本刚编译完触发的，那时新放进工程的 PNG 可能还在导入队列里，
        /// 直接开建会得到一间空房间并把它存进场景，所以这里必须先确认素材齐了。
        /// </summary>
        private static bool AssetsReady(out string missing)
        {
            missing = null;
            if (AssetDatabase.LoadAssetAtPath<Sprite>(BackdropAsset) == null)
            {
                missing = BackdropAsset;
                return false;
            }
            foreach (var p in Props)
            {
                string path = CutoutDir + p.Name + ".png";
                if (AssetDatabase.LoadAssetAtPath<Sprite>(path) == null)
                {
                    missing = path;
                    return false;
                }
            }
            return true;
        }

        // 菜单入口必须是 void：返回 bool 的方法会被 Unity 当成菜单的校验函数。
        //
        // 这里**故意不挂快捷键**。原来写的是 %#r（Ctrl+Shift+R），但 Unity 内置菜单里
        // 也有条目占着它，于是 ShortcutManager 每次都弹 ConflictResolverWindow 来仲裁，
        // 而那个窗口关闭时会走它自己的一个 bug 分支，抛
        // InvalidOperationException: No active profile —— 报错栈全在 UnityEditor.
        // ShortcutManagement 里，跟重建逻辑无关，但会吓人一跳。去掉快捷键就没有仲裁，
        // 也就不会再有这个异常；要快捷键的话可以在 Edit > Shortcuts 里自己绑一个
        // （比如 Ctrl+Shift+Alt+R，三修饰键基本不会撞）。
        [MenuItem("波形小队/重建排练室场景", false, 0)]
        private static void BuildMenu() { TryBuild(); }

        /// <summary>真正干活的方法。返回是否真的重建了（素材没齐 / 场景结构不对时返回 false）。</summary>
        public static bool TryBuild()
        {
            if (!AssetsReady(out string missing))
            {
                Debug.LogWarning("[排练室] 素材还没导入完（缺 " + missing + "），本次不重建。" +
                                 "等导入结束后用菜单「波形小队/重建排练室场景」重跑。");
                return false;
            }

            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
                scene = EditorSceneManager.OpenScene(ScenePath);
            }

            var grid = GameObject.Find("Grid");
            if (grid == null)
            {
                Debug.LogError("[排练室] 找不到 Grid 对象，场景结构已变，放弃重建。");
                return false;
            }

            var collision = grid.transform.Find("Collision");
            if (collision == null)
            {
                Debug.LogError("[排练室] 找不到 Grid/Collision，放弃重建。");
                return false;
            }
            var tilemap = collision.GetComponent<Tilemap>();

            // Collision 的图块资源直接从现有图块上取，不依赖硬编码的资源路径
            var tileAsset = FindExistingTile(tilemap);
            if (tileAsset == null)
            {
                Debug.LogError("[排练室] Collision 上一个图块都没有，取不到 Tile 资源，放弃重建。");
                return false;
            }

            BackupScene();
            RemoveBlockout(grid);
            RebuildCollision(tilemap, tileAsset);
            int strays = RemoveStrayRoots();
            var root = BuildRoomRoot();
            CreateBackdrop(root.transform);
            PlaceProps(root.transform);
            FixCamera();
            FixPlayer();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[排练室] 重建完成：背景 + " + Props.Length + " 件道具 + 碰撞已按新布局重铺" +
                      (strays > 0 ? "，并清掉了 " + strays + " 个根节点上的重名副本。" : "。"));
            return true;
        }

        /// <summary>
        /// 改动前先备份场景。这个工程没有版本控制（不是 git 仓库），脚本又会自动保存，
        /// 所以这是唯一的安全网：出问题可以把 排练室.backup.unity 改名回来。
        /// 备份只在还没有备份时写一次，不会覆盖掉最初那份毛坯。
        /// </summary>
        private static void BackupScene()
        {
            string backup = ScenePath.Substring(0, ScenePath.Length - ".unity".Length) + ".backup.unity";
            if (File.Exists(backup)) return;
            File.Copy(ScenePath, backup);
            AssetDatabase.ImportAsset(backup);
            Debug.Log("[排练室] 已备份原始场景 → " + backup);
        }

        /// <summary>返回一个已存在的非空图块，用来拿到这个场景在用的 Tile 资源。</summary>
        private static TileBase FindExistingTile(Tilemap tilemap)
        {
            foreach (var pos in tilemap.cellBounds.allPositionsWithin)
            {
                var t = tilemap.GetTile(pos);
                if (t != null) return t;
            }
            return null;
        }

        private static void RemoveBlockout(GameObject grid)
        {
            foreach (var name in BlockoutNames)
            {
                var t = grid.transform.Find(name);
                if (t != null) Object.DestroyImmediate(t.gameObject);
            }
        }

        /// <summary>
        /// 重铺碰撞：房间外框 + y>=0 的整条墙/家具带都封上，
        /// 只留下 x −8~7、y −5~−1 这块地板给玩家走。
        /// </summary>
        private static void RebuildCollision(Tilemap tilemap, TileBase tile)
        {
            // 这层图块以前是当**可见几何**用的（毛坯阶段的色块房间），它的 m_Color 是
            // rgb(171,14,14) 的深红，TilemapRenderer 又是启用的、order 0，于是整块盖在
            // 背景图（order −1000）上面——这就是「房间底下垫了张纯红色底图」的真正来源，
            // 和相机 Clear Flags 毫无关系（所以之前改清屏色完全没用）。
            // 现在房间有真正的背景图了，碰撞层只该做碰撞，渲染必须关掉。
            var renderer = tilemap.GetComponent<TilemapRenderer>();
            if (renderer != null) renderer.enabled = false;
            tilemap.color = Color.white;      // 顺带把红色染回去，万一以后又开渲染也不会是红的

            tilemap.ClearAllTiles();
            int n = 0;
            for (int y = RoomMinY; y <= RoomMaxY; y++)
            {
                for (int x = RoomMinX; x <= RoomMaxX; x++)
                {
                    bool edge = x == RoomMinX || x == RoomMaxX || y == RoomMinY || y == RoomMaxY;
                    bool blocked = edge || y > WalkMaxY || y < WalkMinY;
                    if (!blocked) continue;
                    tilemap.SetTile(new Vector3Int(x, y, 0), tile);
                    n++;
                }
            }
            tilemap.CompressBounds();
            Debug.Log("[排练室] 碰撞图块 " + n + " 块，活动区 x −8~7 / y −5~−1。");
        }

        /// <summary>
        /// 清掉手工搭场景时留在**根节点**上的重名副本。
        ///
        /// 成因：生成物统一挂在 Room 下，重建时只删 Room 这一个节点。如果之前手工往场景里
        /// 拖过同名道具（或背景图），它们挂在根节点上，不受影响，于是画面上就是两套叠在一起。
        /// 这里按名字比对，只清「和生成物重名」的根对象；手工加进来的、不在 Props 名单里的
        /// 对象（比如额外的 mic_stand）不会被碰。
        /// </summary>
        private static int RemoveStrayRoots()
        {
            string backdropName = Path.GetFileNameWithoutExtension(BackdropAsset);
            int removed = 0;
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (go.name == RoomName) continue;
                if (go.name == "Backdrop" || go.name == backdropName || IsPropName(go.name))
                {
                    Debug.Log("[排练室] 清掉根节点上的重名副本：" + go.name);
                    Object.DestroyImmediate(go);
                    removed++;
                }
            }
            return removed;
        }

        private static bool IsPropName(string name)
        {
            for (int i = 0; i < Props.Length; i++)
                if (Props[i].Name == name) return true;
            return false;
        }

        /// <summary>生成物统一挂在 Room 下，重复执行时先整体删掉旧的，避免越跑越多。</summary>
        private static GameObject BuildRoomRoot()
        {
            var old = GameObject.Find(RoomName);
            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject(RoomName);
            root.transform.position = Vector3.zero;
            return root;
        }

        private static void CreateBackdrop(Transform parent)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BackdropAsset);
            if (sprite == null)
            {
                Debug.LogError("[排练室] 加载不到背景图 " + BackdropAsset +
                               "（确认它的 Texture Type 是 Sprite (2D and UI)）。");
                return;
            }
            var go = new GameObject("Backdrop");
            go.transform.SetParent(parent, false);
            // 图是 2160×1440 @ 120px/单位 = 18×12 单位，与房间矩形等大，中心就是房间中心
            go.transform.position = new Vector3(-0.5f, -0.5f, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = -1000;      // 永远压在所有道具和玩家下面
        }

        private static void PlaceProps(Transform parent)
        {
            var holder = new GameObject("Props");
            holder.transform.SetParent(parent, false);

            foreach (var p in Props)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(CutoutDir + p.Name + ".png");
                if (sprite == null)
                {
                    Debug.LogWarning("[排练室] 跳过 " + p.Name + "：加载不到 " + CutoutDir + p.Name + ".png");
                    continue;
                }

                var go = new GameObject(p.Name);
                go.transform.SetParent(holder.transform, false);
                go.transform.localScale = new Vector3(p.ScaleX, p.ScaleY, 1f);

                // 精灵轴心在正中，所以中心 = 底边 + 高度的一半（高度已含缩放）
                float h = sprite.bounds.size.y * p.ScaleY;
                go.transform.position = new Vector3(p.X, p.BaseY + h * 0.5f, 0f);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                // 越靠下（越近）画得越靠前：同一排内也按 y 自动分先后
                sr.sortingOrder = Mathf.RoundToInt(-go.transform.position.y * 100f);
            }
        }

        /// <summary>
        /// 相机拉到能看全整间房：房间 18×12，正交半高取 6 即可（16:9 下横向可看 21.33 单位，
        /// 正好等于背景图宽度，铺满不留边）。
        ///
        /// Clear Flags 必须显式设成 Solid Color：场景里 m_SkyboxMaterial 是空的，
        /// 工程里也没有任何天空盒材质（GraphicsSettings 里连 m_DefaultSkybox 都没有），
        /// 于是 Skybox 模式下相机会直接不清颜色缓冲——画面底下就是一块内容未定义的
        /// 红色噪底。清屏色取背景图最外侧 6 列的平均色，两边能无缝接上。
        /// </summary>
        private static void FixCamera()
        {
            var cam = Camera.main;
            if (cam == null) return;
            cam.transform.position = new Vector3(-0.5f, -0.5f, -10f);
            cam.orthographic = true;
            cam.orthographicSize = 6f;

            cam.clearFlags = CameraClearFlags.SolidColor;
            // rgb(37, 32, 43)：从当前背景图 room_backdrop_anime_v1.png 最外圈 6 列实测的平均色。
            // 换背景图之后要重新量一次这个值，否则超宽屏上两边会露出不一样的颜色。
            cam.backgroundColor = new Color(37f / 255f, 32f / 255f, 43f / 255f, 1f);
        }

        /// <summary>
        /// Player 身上只有 PlayerMovement + CapsuleCollider2D + Rigidbody2D，没有任何渲染组件，
        /// 所以一直是隐形的。这里补一个占位块，尺寸对齐胶囊 (0.5×1)。
        ///
        /// 两个必须注意的点：
        /// 一是**不能**用 new Texture2D + Sprite.Create 造精灵。运行时创建的贴图不是资源，
        /// 存场景时 sprite 引用会落成 `sprite: {fileID: 0}`——重开场景占位块就消失了
        /// （第一版就是这么写的，实测重开场景后 Player 又隐身了）。
        /// 改用内置资源精灵，它是真实资源，fileID 能正常序列化进场景。
        /// 二是**不能**用 transform.localScale 缩放占位块。SpriteRenderer 和
        /// CapsuleCollider2D 在同一个物体上，缩放会连碰撞体一起压扁。
        /// 所以占位块挂在子物体 Visual 上，只在子物体上做缩放。
        /// </summary>
        private static void FixPlayer()
        {
            var player = GameObject.Find("Player");
            if (player == null) return;
            player.transform.position = new Vector3(0f, -3f, 0f);

            // 清掉第一版留在 Player 本体上的坏 SpriteRenderer（sprite 引用是空的）
            var stale = player.GetComponent<SpriteRenderer>();
            if (stale != null) Object.DestroyImmediate(stale);

            // 重复执行时先删旧的，避免越跑越多
            var oldVisual = player.transform.Find(VisualName);
            if (oldVisual != null) Object.DestroyImmediate(oldVisual.gameObject);

            var sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            if (sprite == null)
            {
                Debug.LogWarning("[排练室] 取不到内置精灵，Player 占位块跳过（不影响其他内容）。");
                return;
            }

            var visual = new GameObject(VisualName);
            visual.transform.SetParent(player.transform, false);
            visual.transform.localPosition = Vector3.zero;

            // 内置精灵的像素尺寸不固定，按它自己的 bounds 反推缩放，正好铺成 0.5×1 单位
            var size = sprite.bounds.size;
            visual.transform.localScale = new Vector3(0.5f / size.x, 1f / size.y, 1f);

            var sr = visual.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = new Color(0.95f, 0.72f, 0.35f, 0.85f);
            sr.sortingOrder = 500;        // 站在所有道具前面
        }

        private const int AutoRunMaxTries = 300;   // 每帧一次，约 5 秒，够等完素材导入

        /// <summary>
        /// 首次放进工程（脚本编译完）时自动跑一次，省得手动点菜单。
        ///
        /// 两个坑：
        /// 一是素材导入和脚本编译在同一次刷新里，delayCall 触发时道具 PNG 可能还没进
        /// AssetDatabase，直接开会建成空房间——所以等 AssetsReady 再动手。
        /// 二是标记只在**成功**时才落，失败就留着让下次编译重试，免得一次失败就永远放弃。
        /// </summary>
        [InitializeOnLoadMethod]
        private static void AutoBuildOnce()
        {
            string key = AutoRunPrefKey + "::" + Application.dataPath;
            if (EditorPrefs.GetBool(key, false)) return;
            if (!File.Exists(ScenePath))
            {
                Debug.LogWarning("[排练室] 找不到 " + ScenePath + "，跳过自动重建。");
                return;
            }

            int tries = 0;
            EditorApplication.CallbackFunction tick = null;
            tick = () =>
            {
                if (++tries > AutoRunMaxTries)
                {
                    EditorApplication.delayCall -= tick;
                    Debug.LogWarning("[排练室] 等了 " + AutoRunMaxTries + " 帧素材仍未就绪，" +
                                     "请手动执行菜单「波形小队/重建排练室场景」。");
                    return;
                }
                if (!AssetsReady(out _))          // 素材还在导入，下一帧再看
                {
                    EditorApplication.delayCall += tick;
                    return;
                }
                EditorApplication.delayCall -= tick;
                if (TryBuild()) EditorPrefs.SetBool(key, true);
            };
            EditorApplication.delayCall += tick;
        }
    }
}
