using System.Collections.Generic;
using GameKit.Events;
using GameKit.Tables;

namespace ZooTycoon.Core
{
    public enum StatueGrade
    {
        Common,
        Rare,
        Legend,
    }

    // 설계 29: 석상 능력 한 줄. 빈 줄은 Ability가 null
    public sealed class StatueLine
    {
        public StatueTable Ability { get; internal set; }
        public StatueGrade Grade { get; internal set; }
        public bool Locked { get; internal set; }
        public bool IsEmpty => Ability == null;
        public double Value => IsEmpty ? 0d : Ability.Values[(int)Grade];
    }

    // 설계 29: 웜뱃 석상의 능력 세 줄(광장 공용, 플레이어 상태라 ZooState가 든다). 반짝돌로 잠그지 않은 줄을 다시 굴린다.
    // 굴리기 값 = rollCost + 잠근 줄마다 lockCost, 잠금은 maxLocks줄까지(빈 줄은 못 잠근다). 능력은 곧바로 가게 전부에 걸린다(Boost, 같은 능력은 더한다)
    public sealed class Statue
    {
        public const int k_Lines = 3;

        private readonly EventBus m_bus;
        private readonly PlazaConfigTable m_config;
        private readonly IReadOnlyList<StatueTable> m_abilities;
        private readonly StatueLine[] m_lines = new StatueLine[k_Lines];

        public IReadOnlyList<StatueLine> Lines => m_lines;
        // 바치는 재료(반짝돌)
        public string Item => m_config.StatueItem;
        public int RollCost => m_config.StatueRollCost + LockedCount * m_config.StatueLockCost;

        public int LockedCount
        {
            get
            {
                int count = 0;

                foreach (StatueLine line in m_lines)
                {
                    count += line.Locked ? 1 : 0;
                }

                return count;
            }
        }

        public Statue(TableSet tables, EventBus bus)
        {
            m_bus = bus;
            m_config = tables.Get<PlazaConfigTable>(PlazaConfigTable.k_Main);
            m_abilities = tables.GetAll<StatueTable>();

            for (int i = 0; i < k_Lines; i++)
            {
                m_lines[i] = new StatueLine();
            }
        }

        public bool CanLock(int line)
        {
            return !m_lines[line].IsEmpty && !m_lines[line].Locked && LockedCount < m_config.StatueMaxLocks;
        }

        // 잠금 켜고 끄기는 무료. 못 잠그는 줄이면 아무 일 없음
        public void SetLocked(int line, bool locked)
        {
            if (m_lines[line].Locked == locked || (locked && !CanLock(line)))
            {
                return;
            }

            m_lines[line].Locked = locked;
            m_bus.Publish(new Events.StatueChanged(this, false));
        }

        // 반짝돌을 치르고 잠그지 않은 줄마다 능력(비중) · 등급(gradeWeights)을 새로 뽑는다. 모자라면 아무 일 없이 false
        public bool TryRoll(ZooState wallet, IRandom random)
        {
            if (!wallet.TrySpendItem(Item, RollCost))
            {
                return false;
            }

            foreach (StatueLine line in m_lines)
            {
                if (line.Locked)
                {
                    continue;
                }

                line.Ability = m_abilities[Pick(random, m_abilities.Count, i => m_abilities[i].Weight)];
                line.Grade = (StatueGrade)Pick(random, m_config.StatueGradeWeights.Count, i => m_config.StatueGradeWeights[i]);
            }

            m_bus.Publish(new Events.StatueChanged(this, true));
            return true;
        }

        // 그 능력이 걸린 줄 값의 합(없으면 0)
        public double Boost(string abilityId)
        {
            double sum = 0d;

            foreach (StatueLine line in m_lines)
            {
                if (!line.IsEmpty && line.Ability.Id == abilityId)
                {
                    sum += line.Value;
                }
            }

            return sum;
        }

        private static int Pick(IRandom random, int count, System.Func<int, double> weight)
        {
            double total = 0d;

            for (int i = 0; i < count; i++)
            {
                total += weight(i);
            }

            double roll = random.NextDouble() * total;

            for (int i = 0; i < count; i++)
            {
                roll -= weight(i);

                if (roll < 0d)
                {
                    return i;
                }
            }

            return count - 1;
        }
    }
}
