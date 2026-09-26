using System;
using System.Collections.Generic;
using System.Numerics;
using GameKit.Events;
using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 13: 웜뱃이 걷는 곳 하나(빵집·광장 공통). 곳이 조립한 사물 중 range 안 가장 가까운 것을 대상으로 고르고,
    // range 안 사물의 auto 행동은 할 수 있으면 바로 하고, 버튼으로 대상의 manual 행동을, 시트 줄로 sheet 행동을 한다(설계 09 v0.4 11장).
    // v0.5: 행동은 ActionTable 행마다 행동 객체(ActionFactory). 굴은 모른다: 걷는 땅(WombatNav)과 들어오는 곳(Entrance)만 곳이 알려 준다
    public abstract partial class WombatArea
    {
        private readonly Dictionary<string, InteractAction> m_actions = new Dictionary<string, InteractAction>();
        private readonly Dictionary<string, int> m_upgradeLevels = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly List<Interactable> m_inRange = new List<Interactable>();
        private Interactable m_target;
        private Interactable m_lastTarget;
        private ActionTable m_lastAction;

        public TableSet Tables { get; }
        // 설계 16: 곳의 사건 버스. 사물·손님이 이걸로 사건을 낸다
        public EventBus Bus { get; }
        public Wombat Wombat { get; }
        public bool WombatPresent { get; private set; }
        // range 안 사물 중 가장 가까운 것. 없으면 null
        public Interactable Target => m_target;
        // 곳이 조립한 사물. 순서 = 거리가 같을 때와 auto 행동의 우선순위
        public IReadOnlyList<Interactable> Things => Placed;

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
                    InteractAction action = m_actions[id];

                    if (action.Table.Mode == ActionMode.Manual && action.CanDo(Wombat.Worker, m_target))
                    {
                        return action.Table;
                    }
                }

                return null;
            }
        }

        protected List<Interactable> Placed { get; } = new List<Interactable>();
        protected abstract BurrowNav WombatNav { get; }
        protected abstract Vector2 Entrance { get; }

        protected WombatArea(TableSet tables, Wombat wombat, EventBus bus)
        {
            Tables = tables;
            Wombat = wombat;
            Bus = bus;
            PlaceCell = (float)tables.Get<ConfigTable>(ConfigTable.k_PlaceCell).Value;

            foreach (ActionTable action in tables.GetAll<ActionTable>())
            {
                m_actions[action.Id] = ActionFactory.Create(action);
            }
        }

        // 매 프레임. 순서: 웜뱃(걷기 → 대상·auto 행동) → 곳 고유(손님) → 사물(계산대 타이머·오븐)
        public void Tick(double dt)
        {
            TickWombat(dt);
            TickArea(dt);

            foreach (Interactable thing in Placed)
            {
                thing.Tick(dt);
            }
        }

        // 웜뱃이 이 사물의 range 안에 있나(이번 프레임 기준)
        public bool IsInRange(Interactable thing)
        {
            return m_inRange.Contains(thing);
        }

        // 버튼: 대상의 manual 행동을 한다. 시트 열기는 화면 몫이라 false
        public bool TryInteract()
        {
            ActionTable table = TargetAction;

            if (table == null || m_actions[table.Id].OpensSheet)
            {
                return false;
            }

            m_actions[table.Id].Do(Wombat.Worker, m_target);
            return true;
        }

        // 사물의 시트 행동들(actions 순서 = 시트 줄 순서)
        public IEnumerable<SheetAction> SheetActions(Interactable target)
        {
            foreach (string id in target.Table.Actions)
            {
                if (m_actions[id] is SheetAction action)
                {
                    yield return action;
                }
            }
        }

        // 시트 줄 누르기
        public bool TryChoose(string actionId, Interactable target, string option)
        {
            return ((SheetAction)m_actions[actionId]).TryChoose(Wombat.Worker, target, option);
        }

        // v0.6: 업그레이드 단계는 사물 종류 공통이라 곳이 센다
        public int UpgradeLevel(string interactableId)
        {
            m_upgradeLevels.TryGetValue(interactableId, out int level);
            return level;
        }

        internal void LevelUp(string interactableId)
        {
            m_upgradeLevels[interactableId] = UpgradeLevel(interactableId) + 1;
            Bus.Publish(new Events.Upgraded(this, interactableId));
        }

        // 설계 11: 다른 곳에서 들어온다. 입구 아래 바닥(통로 띠 바로 밑)에 서고, 누르고 있던 조이스틱을 놓을 때까지 걷지 않는다(굴을 등진 채. 놓기 전에 띠로 되돌아가지 않게)
        public void Enter()
        {
            EnterAt(Entrance);
            Wombat.WaitRelease();
        }

        // 다른 곳으로 나갔다: 걷기·자동 행동이 멈추고 대상이 없다
        public void Leave()
        {
            WombatPresent = false;
            Wombat.Stop();
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

        // 조이스틱으로 걸은 뒤 대상을 다시 고른다
        private void TickWombat(double dt)
        {
            if (!WombatPresent)
            {
                return;
            }

            Wombat.Walk(WombatNav, dt);
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
                Bus.Publish(new Events.TargetChanged(this));
            }
        }

        // range 안 사물을 모으고, 그 사물들의 auto 행동을 할 수 있으면 하고, 가장 가까운 것을 돌려준다
        private Interactable GatherInRange()
        {
            Vector2 p = Wombat.Mover.Position;
            float best = float.MaxValue;
            Interactable found = null;
            m_inRange.Clear();

            foreach (Interactable thing in Placed)
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
                    InteractAction action = m_actions[id];

                    if (action.Table.IsAuto && action.CanDo(Wombat.Worker, thing))
                    {
                        action.Do(Wombat.Worker, thing);
                    }
                }
            }

            return found;
        }
    }
}
