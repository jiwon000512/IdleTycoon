using System;
using System.Collections.Generic;
using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 44: 물길 위 물고기와 대의 감기(디펜스 한 판). 낚시터가 매 프레임 돌리고, 오프라인은 같은 판으로 물때 하나를 따로 돌려 잰다(웜뱃 없이).
    // 대는 사거리 안에서 가장 멀리 내려간 물고기를 감는다. 월척은 처음 감는 대가 붙잡아 멈추고(그 대는 다른 것을 못 감는다), 털썩 · 점원이 건져야 낚인다.
    // 미끼는 사거리 안 물고기를 느리게, 쿵은 둘레 물고기를 잠깐 멈추게 한다. 설계 46 미끼 노점 소용돌이: 간격마다 물길 맨 앞 물고기를 whirlDistance만큼 되돌린다
    internal sealed class FishingSim
    {
        private readonly FishingArea m_area;
        private readonly List<Fish> m_fish = new List<Fish>();
        private readonly Dictionary<StakeInteractable, Fish> m_hooks = new Dictionary<StakeInteractable, Fish>();
        private double m_whirlLeft;
        // 말뚝마다 이번 틱에 감은 물고기(화면의 낚싯줄)
        private readonly Dictionary<StakeInteractable, Fish> m_targets = new Dictionary<StakeInteractable, Fish>();

        public IReadOnlyList<Fish> Fish => m_fish;
        // 낚였다(물고기 · 감은 말뚝, 털썩이면 그 말뚝) · 놓쳤다
        public event Action<Fish, StakeInteractable> Caught;
        public event Action<Fish> Escaped;
        public event Action<StakeInteractable> Hooked;
        // 소용돌이가 되돌렸다(물고기 · 되돌리기 전 자리)
        public event Action<Fish, float> Whirled;

        public FishingSim(FishingArea area)
        {
            m_area = area;
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
            TickWhirl(dt);

            foreach (StakeInteractable stake in m_area.Stakes)
            {
                if (stake.Rod != null && !m_hooks.ContainsKey(stake))
                {
                    TickRod(stake, dt);
                }
            }
        }

        // 소용돌이: 간격마다 물길 맨 앞(막다른 끝에 가장 가까운) 헤엄치는 물고기를 되돌린다
        private void TickWhirl(double dt)
        {
            double interval = m_area.WhirlInterval;

            if (interval <= 0d)
            {
                return;
            }

            m_whirlLeft = Math.Min(m_whirlLeft, interval) - dt;

            if (m_whirlLeft > 0d)
            {
                return;
            }

            m_whirlLeft = interval;
            Fish front = null;

            foreach (Fish fish in m_fish)
            {
                if (fish.HookedBy == null && (front == null || fish.S > front.S))
                {
                    front = fish;
                }
            }

            if (front != null)
            {
                float from = front.S;
                front.S = Math.Max(0f, front.S - (float)m_area.Config.WhirlDistance);
                Whirled?.Invoke(front, from);
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
                if (stake.Rod != null && stake.Rod.Slow < slow && m_area.InReach(stake, p, m_area.RangeOf(stake)))
                {
                    slow = stake.Rod.Slow;
                }
            }

            return slow;
        }

        private void TickRod(StakeInteractable stake, double dt)
        {
            Fish target = Frontmost(stake, m_area.RangeOf(stake));

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

            target.Reeled += m_area.ReelOf(stake, target) * dt;

            if (target.Reeled >= target.Weight)
            {
                Remove(target);
                Caught?.Invoke(target, stake);
            }
        }

        // 사거리(말뚝 위쪽 반원) 안에서 가장 멀리 내려간(막다른 끝에 가까운) 물고기. 붙잡힌 월척은 뺀다
        private Fish Frontmost(StakeInteractable stake, float range)
        {
            Fish best = null;

            foreach (Fish fish in m_fish)
            {
                if (fish.HookedBy == null && (best == null || fish.S > best.S) && m_area.InReach(stake, m_area.Layout.PointAt(fish.S), range))
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
