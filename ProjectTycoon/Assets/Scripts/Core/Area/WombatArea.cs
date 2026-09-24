using System;
using System.Collections.Generic;
using System.Numerics;
using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 13: 웜뱃이 걷는 곳 하나(빵집·광장 공통). 곳이 조립한 사물 중 range 안 가장 가까운 것을 대상으로 고르고,
    // range 안 사물의 auto 행동은 할 수 있으면 바로 하고, 버튼으로 대상의 manual 행동을 한다(설계 09 v0.4 11장).
    // 굴은 모른다: 걷는 땅(WombatNav)과 들어오는 곳(Entrance)만 곳이 알려 준다
    public abstract class WombatArea
    {
        private readonly List<Interactable> m_inRange = new List<Interactable>();
        private Interactable m_target;
        private Interactable m_lastTarget;
        private ActionTable m_lastAction;

        public Wombat Wombat { get; }
        public bool WombatPresent { get; private set; }
        public Vector2 WombatPosition => Wombat.Mover.Position;
        public Facing WombatFacing => Wombat.Mover.Facing;
        public bool WombatMoving => WombatPresent && Wombat.Moving;
        // range 안 사물 중 가장 가까운 것. 없으면 null
        public Interactable Target => m_target;

        // 대상의 actions를 순서대로 보고 지금 할 수 있는 첫 manual 행동(버튼). 없으면 null
        public ActionTable TargetAction
        {
            get
            {
                if (m_target == null)
                {
                    return null;
                }

                foreach (string id in m_target.Table.Actions)
                {
                    ActionTable action = Tables.Get<ActionTable>(id);

                    if (!action.IsAuto && m_target.CanDo(id, Wombat.Hands))
                    {
                        return action;
                    }
                }

                return null;
            }
        }

        // 대상이 바뀌었거나 대상의 버튼 행동이 바뀌었다(다 구웠다·빵을 들었다 등)
        public event Action TargetChanged;

        protected TableSet Tables { get; }
        // 곳이 조립한 사물. 순서 = 거리가 같을 때와 auto 행동의 우선순위
        protected List<Interactable> Things { get; } = new List<Interactable>();
        protected abstract BurrowNav WombatNav { get; }
        protected abstract Vector2 Entrance { get; }

        protected WombatArea(TableSet tables, Wombat wombat)
        {
            Tables = tables;
            Wombat = wombat;
        }

        // 매 프레임. 순서: 웜뱃(걷기 → 대상·auto 행동) → 사물 → 곳 고유
        public void Tick(double dt)
        {
            TickWombat(dt);

            foreach (Interactable thing in Things)
            {
                thing.Tick(dt);
            }

            TickArea(dt);
        }

        // 버튼: 대상의 manual 행동을 한다. 시트를 여는 행동(open·dig)은 화면 몫이라 false
        public bool TryInteract()
        {
            ActionTable action = TargetAction;

            if (action == null || action.Id == ActionTable.k_Open || action.Id == ActionTable.k_Dig)
            {
                return false;
            }

            m_target.Do(action.Id, Wombat.Hands);
            return true;
        }

        // 설계 11: 다른 곳에서 들어온다. 입구 아래 바닥에 선다
        public void Enter()
        {
            EnterAt(Entrance);
        }

        // 다른 곳으로 나갔다: 걷기·자동 행동이 멈추고 대상이 없다
        public void Leave()
        {
            WombatPresent = false;
            Wombat.Moving = false;
            RefreshTarget();
        }

        protected void EnterAt(Vector2 position)
        {
            Wombat.Mover.Place(position);
            Wombat.Mover.Facing = Facing.Down;
            WombatPresent = true;
            RefreshTarget();
        }

        protected virtual void TickArea(double dt)
        {
        }

        // 설계 09 3장: 조이스틱 방향으로 걷고, 막히면 벽을 따라 미끄러진다. 걸은 뒤 대상을 다시 고른다
        private void TickWombat(double dt)
        {
            if (!WombatPresent)
            {
                return;
            }

            Wombat.Moving = WombatWalker.Step(Wombat.Mover, Wombat.Input, Wombat.Speed, dt, WombatNav);
            RefreshTarget();
        }

        private void RefreshTarget()
        {
            m_target = WombatPresent ? GatherInRange() : null;
            ActionTable action = TargetAction;

            if (m_target != m_lastTarget || action != m_lastAction)
            {
                m_lastTarget = m_target;
                m_lastAction = action;
                OnTargetChanged();
            }
        }

        // range 안 사물을 모으고, 그 사물들의 auto 행동을 할 수 있으면 하고, 가장 가까운 것을 돌려준다
        private Interactable GatherInRange()
        {
            Vector2 p = Wombat.Mover.Position;
            float best = float.MaxValue;
            Interactable found = null;
            m_inRange.Clear();

            foreach (Interactable thing in Things)
            {
                float distance = thing.DistanceTo(p);

                if (distance > (float)thing.Table.Range)
                {
                    continue;
                }

                m_inRange.Add(thing);

                if (distance < best)
                {
                    best = distance;
                    found = thing;
                }
            }

            foreach (Interactable thing in m_inRange)
            {
                foreach (string id in thing.Table.Actions)
                {
                    if (Tables.Get<ActionTable>(id).IsAuto && thing.CanDo(id, Wombat.Hands))
                    {
                        thing.Do(id, Wombat.Hands);
                    }
                }
            }

            return found;
        }

        private void OnTargetChanged()
        {
            TargetChanged?.Invoke();
        }
    }
}
