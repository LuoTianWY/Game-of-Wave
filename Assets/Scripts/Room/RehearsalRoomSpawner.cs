using System.Collections.Generic;
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
        /// 出生点微调（世界单位）。场景文件不进仓库（组内引擎版本不一致），
        /// 所以需要让所有人都看到的位置微调写在代码里；没列出的角色按场景里的出生点站。
        /// </summary>
        private static readonly Dictionary<string, Vector3> SpawnOffsets =
            new Dictionary<string, Vector3>
            {
                { "小川", new Vector3(0f, 0.90f, 0f) },   // 往上挪接近一格
            };

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
                    spawnPoint.position + OffsetFor(profile.Name),
                    root.transform);
            }

            return AttachToPlayer(root.transform);
        }

        private static Vector3 OffsetFor(string profileName)
        {
            Vector3 offset;
            if (profileName != null && SpawnOffsets.TryGetValue(profileName, out offset))
                return offset;
            return Vector3.zero;
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
