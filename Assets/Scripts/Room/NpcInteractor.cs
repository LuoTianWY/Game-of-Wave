using System.Collections.Generic;
using UnityEngine;
using WaveTeam.Core;

namespace WaveTeam.Room
{
    /// <summary>
    /// 挂在 Player 上：每帧找出「半径内最近的那个角色」，走近就让他亮出「按 F 互动」，
    /// 按 F 时把角色资料交给上层（GameBootstrap）去开信息面板。
    ///
    /// 用回调而不是直接 new 面板：这样 Room 这层不用认识 UI 那层的类型，
    /// 面板长什么样、由谁摆，都和互动逻辑解耦。
    /// </summary>
    public sealed class NpcInteractor : MonoBehaviour
    {
        /// <summary>互动半径（世界单位）。比角色身宽（0.62）大不少，容得下走位误差。</summary>
        public float Radius = 1.7f;

        /// <summary>按 F 命中目标时回调，参数是目标角色的资料。</summary>
        public System.Action<NpcProfile> OnInteract;

        private readonly List<NpcActor> _npcs = new List<NpcActor>();
        private NpcActor _target;
        private bool _locked;
        private PlayerMovement _movement;

        /// <summary>当前被选中的目标（可能为 null）。</summary>
        public NpcActor Target { get { return _target; } }

        private void Awake()
        {
            _movement = GetComponent<PlayerMovement>();
        }

        public void Add(NpcActor npc)
        {
            if (npc != null && !_npcs.Contains(npc)) _npcs.Add(npc);
        }

        /// <summary>
        /// 面板打开时锁住：收起提示、停掉走动，并且不再响应 F。
        /// 不锁的话按 F 会一边开面板一边又把 F 传下去，而且面板开着还能走到别的角色那儿。
        /// </summary>
        public void SetLocked(bool locked)
        {
            _locked = locked;
            if (locked) SetTarget(null);
            if (_movement != null) _movement.enabled = !locked;
        }

        private void Update()
        {
            if (_locked) return;
            SetTarget(FindNearestInRange());
            if (_target != null && Input.GetKeyDown(KeyCode.F))
            {
                var profile = _target.Profile;
                if (OnInteract != null) OnInteract(profile);
            }
        }

        private NpcActor FindNearestInRange()
        {
            NpcActor best = null;
            float bestSqr = Radius * Radius;
            Vector2 self = transform.position;

            for (int i = 0; i < _npcs.Count; i++)
            {
                var npc = _npcs[i];
                if (npc == null) continue;                   // 被销毁的槽位直接跳过
                float sqr = (self - (Vector2)npc.Anchor).sqrMagnitude;
                if (sqr <= bestSqr)
                {
                    bestSqr = sqr;
                    best = npc;
                }
            }
            return best;
        }

        /// <summary>切换目标：旧目标收起提示并取消提亮，新目标反之。</summary>
        private void SetTarget(NpcActor next)
        {
            if (_target == next) return;
            if (_target != null)
            {
                _target.SetPromptVisible(false);
                _target.SetHighlight(false);
            }
            _target = next;
            if (_target != null)
            {
                _target.SetPromptVisible(true);
                _target.SetHighlight(true);
            }
        }
    }
}
