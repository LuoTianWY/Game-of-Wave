using UnityEngine;
using WaveTeam.Core;

namespace WaveTeam.Room
{
    /// <summary>
    /// 把名册里的角色摆进排练室，并给 Player 挂上互动检测。
    ///
    /// NPC 的出生位置由场景中的 NPCSpawnPoints 控制。
    /// 每个角色寻找一个名为 "Spawn_角色名" 的子物体。
    ///
    /// 例如：
    /// NPCSpawnPoints
    /// ├── Spawn_阿宁
    /// ├── Spawn_小川
    /// └── Spawn_石头
    ///
    /// Level Designer 只需要移动这些 Spawn Point，
    /// 不需要再修改代码里的坐标。
    /// </summary>
    public static class RehearsalRoomSpawner
    {
        private const float InteractRadius = 1.7f;

        /// <summary>
        /// 根据场景里的 Spawn Point 生成 NPC。
        /// </summary>
        public static NpcInteractor Spawn()
        {
            // 如果之前已经生成过 NPC，就先清掉
            var old = GameObject.Find("NPCs");
            if (old != null)
                Object.Destroy(old);

            var root = new GameObject("NPCs");

            // 找场景中的出生点父物体
            var spawnRoot = GameObject.Find("NPCSpawnPoints");

            if (spawnRoot == null)
            {
                Debug.LogWarning(
                    "[排练室] 找不到 NPCSpawnPoints，无法生成 NPC。");

                return AttachToPlayer(root.transform);
            }

            var profiles = NpcRoster.All;

            foreach (var profile in profiles)
            {
                string spawnName = "Spawn_" + profile.Name;

                Transform spawnPoint =
                    spawnRoot.transform.Find(spawnName);

                if (spawnPoint == null)
                {
                    Debug.LogWarning(
                        "[排练室] 找不到出生点：" + spawnName +
                        "，角色 " + profile.Name + " 未生成。");

                    continue;
                }

                NpcActor.Create(
                    profile,
                    spawnPoint.position,
                    root.transform);
            }

            return AttachToPlayer(root.transform);
        }

        /// <summary>
        /// 给 Player 挂上 NpcInteractor，并登记全部生成出来的角色。
        /// </summary>
        private static NpcInteractor AttachToPlayer(Transform npcRoot)
        {
            var player = GameObject.Find("Player");

            if (player == null)
            {
                Debug.LogWarning(
                    "[排练室] 场景里找不到 Player，角色互动未启用。");

                return null;
            }

            var interactor =
                player.GetComponent<NpcInteractor>();

            if (interactor == null)
                interactor = player.AddComponent<NpcInteractor>();

            interactor.Radius = InteractRadius;

            var actors =
                npcRoot.GetComponentsInChildren<NpcActor>();

            foreach (var actor in actors)
                interactor.Add(actor);

            Debug.Log(
                "[排练室] 已放入 " +
                actors.Length +
                " 名角色，互动半径 " +
                InteractRadius +
                "。");

            return interactor;
        }
    }
}
