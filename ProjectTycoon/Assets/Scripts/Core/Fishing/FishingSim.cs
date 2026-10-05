using System;
using System.Collections.Generic;
using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 44: 물길 위 물고기와 대의 감기(디펜스 한 판). 낚시터가 매 프레임 돌리고, 오프라인은 같은 판으로 물때 하나를 따로 돌려 잰다(웜뱃 없이).
    // 대는 사거리 안에서 가장 멀리 내려간 물고기를 감는다. 월척은 처음 감는 대가 붙잡아 멈추고(그 대는 다른 것을 못 감는다), 털썩해야 낚인다.
    // 미끼는 사거리 안 물고기를 느리게, 쿵은 둘레 물고기를 잠깐 멈추게, 소용돌이 통발은 사거리 안 맨 앞 물고기를 사거리만큼 되돌린다
    internal sealed class FishingSim
    {
        private readonly FishingArea m_area;
        private readonly bool m_live;
        private readonly List<Fish> m_fish = new List<Fish>();
        private readonly Dictionary<StakeInteractable, Fish> m_hooks = new Dictionary<StakeInteractable, Fish>();
        private readonly Dictionary<StakeInteractable, double> m_whirls = new Dictionary<StakeInteractable, double>();
        // 말뚝마다 이번 틱에 감은 물고기(화면의 낚싯줄)
        private readonly Dictionary<StakeInteractable, Fish> m_targets = new Dictionary<StakeInteractable, Fish>();

        public IReadOnlyList<Fish> Fish => m_fish;
        // 낚였다(물고기 · 감은 말뚝, 털썩이면 그 말뚝) · 놓쳤다
        public event Action<Fish, StakeInteractable> Caught;
        public event Action<Fish> Escaped;
        public event Action<StakeInteractable> Hooked;

        // live: 웜뱃이 앉은 말뚝을 센다(오프라인 판정은 웜뱃 없이)
        public FishingSim(FishingArea area, bool live)
        {
            m_area = area;
            m_live = live;
        }

        public Fish HookOf(StakeInteractable stake)
        {
            return m_hooks.TryGetValue(stake, out Fish fish) ? fish : null;
        }

        // 그 말뚝 대가 지금 감는 물고기(붙잡은 월척 포함, 없으면 null)
        public Fish TargetOf(StakeInteractable stake)
        {
            return HookOf(stake) ?? (m_targets.TryGetValue(stake, out Fish fish) && m_fish.Contains(fish) ? fish : null);
        }

        public void Spawn(Fish fish)
        {
            fish.S = 0f;
            m_fish.Add(fish);
        }

        public void Tick(double dt)
        {
            MoveFish(dt);

            foreach (StakeInteractable stake in m_area.Stakes)
            {
                if (stake.Rod != null && !m_hooks.ContainsKey(stake))
                {
                    TickRod(stake, dt);
                }
            }
        }

        // 쿵: center 둘레 물고기가 seconds 동안 멈춘다
        public void Stun(Vector2 center, float radius, double seconds)
        {
            foreach (Fish fish in m_fish)
            {
                if (Vector2.Distance(m_area.Layout.PointAt(fish.S), center) <= radius)
                {
                    fish.StunLeft = Math.Max(fish.StunLeft, seconds);
                }
            }
        }

        // 붙잡은 월척을 낚는다(웜뱃 털썩 · 점원)
        public void Land(StakeInteractable stake)
        {
            if (m_hooks.TryGetValue(stake, out Fish fish))
            {
                m_hooks.Remove(stake);
                Remove(fish);
                Caught?.Invoke(fish, stake);
            }
        }

        // 대가 빠지거나 바뀌면 붙잡던 월척을 놓아 다시 헤엄친다
        public void Unhook(StakeInteractable stake)
        {
            if (m_hooks.TryGetValue(stake, out Fish fish))
            {
                m_hooks.Remove(stake);
                fish.HookedBy = null;
                fish.HookedFor = 0d;
            }
        }

        private void MoveFish(double dt)
        {
            for (int i = 0; i < m_fish.Count; i++)
            {
                Fish fish = m_fish[i];

                if (fish.HookedBy != null)
                {
                    fish.HookedFor += dt;
                    continue;
                }

                if (fish.StunLeft > 0d)
                {
                    fish.StunLeft -= dt;
                    continue;
                }

                fish.S += (float)(fish.Kind.Speed * SlowAt(fish) * dt);

                if (fish.S >= m_area.Layout.Length)
                {
                    m_fish.RemoveAt(i);
                    i--;
                    Escaped?.Invoke(fish);
                }
            }
        }

        // 사거리 안 미끼 중 가장 느리게 하는 값(없으면 1)
        private double SlowAt(Fish fish)
        {
            double slow = 1d;
            Vector2 p = m_area.Layout.PointAt(fish.S);

            foreach (StakeInteractable stake in m_area.Stakes)
            {
                if (stake.Rod != null && stake.Rod.Slow < slow && Vector2.Distance(p, stake.Position) <= m_area.RangeOf(stake))
                {
                    slow = stake.Rod.Slow;
                }
            }

            return slow;
        }

        private void TickRod(StakeInteractable stake, double dt)
        {
            float range = m_area.RangeOf(stake);
            Fish target = Frontmost(stake, range);

            if (stake.Rod.Special == RodTable.k_Whirlpool && target != null)
            {
                double left = (m_whirls.TryGetValue(stake, out double w) ? w : stake.Rod.Effect) - dt;

                if (left <= 0d && !target.Boss)
                {
                    target.S = Math.Max(0f, target.S - range);
                    left = stake.Rod.Effect;
                }

                m_whirls[stake] = left;
            }

            if (target == null)
            {
                m_targets.Remove(stake);
                return;
            }

            m_targets[stake] = target;

            if (target.Trophy)
            {
                target.HookedBy = stake;
                m_hooks[stake] = target;
                Hooked?.Invoke(stake);
                return;
            }

            target.Reeled += m_area.ReelOf(stake, target, m_live) * dt;

            if (target.Reeled >= target.Weight)
            {
                Remove(target);
                Caught?.Invoke(target, stake);
            }
        }

        // 사거리 안에서 가장 멀리 내려간(구멍에 가까운) 물고기. 붙잡힌 월척은 뺀다
        private Fish Frontmost(StakeInteractable stake, float range)
        {
            Fish best = null;

            foreach (Fish fish in m_fish)
            {
                if (fish.HookedBy == null && (best == null || fish.S > best.S) && Vector2.Distance(m_area.Layout.PointAt(fish.S), stake.Position) <= range)
                {
                    best = fish;
                }
            }

            return best;
        }

        private void Remove(Fish fish)
        {
            m_fish.Remove(fish);
        }
    }

    // 오프라인 판정처럼 같은 값이 나와야 하는 곳의 난수(씨앗 고정)
    internal sealed class SeededRandom : IRandom
    {
        private readonly Random m_random;

        public SeededRandom(int seed)
        {
            m_random = new Random(seed);
        }

        public double NextDouble()
        {
            return m_random.NextDouble();
        }
    }
}
