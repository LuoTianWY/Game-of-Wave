using UnityEngine;
using WaveTeam.Core;

namespace WaveTeam.Room
{
    /// <summary>
    /// 把名册里的角色摆进排练室，并给 Player 挂上互动检测。
    ///
    /// 位置是手调的，规则：
    ///   · 全部落在活动区 x −8~7 / y −5~−1 内，保证玩家走得到；
    ///   · 这组点位当初是为了避开旧背景图上画进去的音箱柜和麦架而定的；
    ///     现在换成 room_backdrop_anime_v1（地板上没有画任何家具），点位仍然全部落在活动区内，
    ///     所以保持不变。以后要挪位置，只要保证在活动区里就行。
    ///   · 前后错开 y，不要排成一条直线；越靠下的排序越靠前，由 NpcActor 自动算。
    ///
    /// 与道具在画面上会有轻微遮挡，但遮挡方向是对的（站得靠下的挡在靠上的前面），
    /// 看起来就是「站在沙发/凳子前面」，不是穿模。
    /// </summary>
    public static class RehearsalRoomSpawner
    {
        /// <summary>与 NpcRoster.All 一一对应的落脚点（角色脚底的世界坐标）。</summary>
        private static readonly Vector2[] Spots =
        {
            new Vector2(-7.40f, -3.50f),   // 阿宁
            new Vector2(-4.70f, -3.00f),   // 小川
            new Vector2(-1.50f, -3.70f),   // 老周
            new Vector2( 0.85f, -2.70f),   // 石头
            new Vector2( 3.00f, -3.60f),   // 小雨
            new Vector2( 5.60f, -2.90f),   // 神秘嘉宾
        };

        private const float InteractRadius = 1.7f;

        /// <summary>
        /// 生成角色并接好互动。重复调用会先清掉上一批（换场景后旧对象已经没了，
        /// 但同一场景里重复调用也不会叠人）。
        /// </summary>
        public static NpcInteractor Spawn()
        {
            var old = GameObject.Find("NPCs");
            if (old != null) Object.Destroy(old);

            var root = new GameObject("NPCs");
            var profiles = NpcRoster.All;
            int n = Mathf.Min(profiles.Count, Spots.Length);
            for (int i = 0; i < n; i++)
            {
                var p = profiles[i];
                NpcActor.Create(p, new Vector3(Spots[i].x, Spots[i].y, 0f), root.transform);
            }
            if (profiles.Count > Spots.Length)
            {
                Debug.LogWarning("[排练室] 名册里有 " + profiles.Count + " 名角色，但只配了 " +
                                 Spots.Length + " 个落脚点，多出来的没生成。");
                n = Spots.Length;
            }

            return AttachToPlayer(root.transform);
        }

        /// <summary>给 Player 挂上 NpcInteractor 并登记全部角色；已经挂过就复用。</summary>
        private static NpcInteractor AttachToPlayer(Transform npcRoot)
        {
            var player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogWarning("[排练室] 场景里找不到 Player，角色互动未启用。");
                return null;
            }

            var interactor = player.GetComponent<NpcInteractor>();
            if (interactor == null) interactor = player.AddComponent<NpcInteractor>();
            interactor.Radius = InteractRadius;

            var actors = npcRoot.GetComponentsInChildren<NpcActor>();
            foreach (var a in actors) interactor.Add(a);
            Debug.Log("[排练室] 已放入 " + actors.Length + " 名角色，互动半径 " + InteractRadius + "。");
            return interactor;
        }
    }
}
