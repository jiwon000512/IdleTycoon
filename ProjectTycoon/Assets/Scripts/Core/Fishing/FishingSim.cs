using System;
using System.Collections.Generic;
using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 44: 물길 위 물고기와 대의 감기(디펜스 한 판). 낚시터가 매 프레임 돌리고, 오프라인은 같은 판으로 물때 하나를 따로 돌려 잰다(웜뱃 없이).
    // 대는 사거리 안에서 가장 멀리 내려간 물고기를 감는다. 월척은 처음 감는 대가 붙잡아 멈추고(그 대는 다른 것을 못 감는다), 털썩 · 점원이 건져야 낚인다.
    // 미끼는 사거리 안 물고기를 느리게, 쿵은 둘레 물고기를 잠깐 멈추게 한다. 설계 46 미끼 노점 소용돌이: 간격마다 물길 맨 앞 물고기를 whirlDistance만큼 되돌린다.
    // 설계 49 종류 특징(RodTable): 여러 마리 함께 감기(Targets) · 월척을 붙잡지 않고 감기(BigGame) · 대물 둔화(BossSlow) · 주기 멈춤(StunEvery). 대물은 붙잡히지 않고 소용돌이에도 안 밀린다
    internal sealed class FishingSim
    {
        private readonly FishingArea m_area;
        private readonly List<Fish> m_fish = new List<Fish>();
        private readonly Dictionary<StakeInteractable, Fish> m_hooks = new Dictionary<StakeInteractable, Fish>();
        // 그물: 말뚝마다 다음 멈춤까지 남은 초
        private readonly Dictionary<StakeInteractable, double> m_stunLeft = new Dictionary<StakeInteractable, double>();
        // 이번에 감을 물고기(말뚝 하나를 도는 동안만 쓴다)
        private readonly List<Fish> m_front = new List<Fish>();
        private double m_whirlLeft;
        // 말뚝마다 이번 틱에 감은 물고기(화면의 낚싯줄, 맨 앞부터. 여러 마리 감는 대는 줄도 여럿)
        private readonly Dictionary<StakeInteractable, List<Fish>> m_reeling = new Dictionary<StakeInteractable, List<Fish>>();
        private static readonly Fish[] s_none = new Fish[0];

        public IReadOnlyList<Fish> Fish => m_fish;
        // 낚였다(물고기 · 감은 말뚝, 털썩이면 그 말뚝) · 놓쳤다
        public event Action<Fish, StakeInteractable> Caught;
        public event Action<Fish> Escaped;
        public event Action<StakeInteractable> Hooked;
        // 그물이 사거리 안 물고기를 멈췄다(말뚝)
        public event Action<StakeInteractable> Stunned;
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
            if (m_hooks.TryGetValue(stake, out Fish hooked))
            {
                return hooked;
            }

            // 다른 대가 먼저 낚아 간 물고기는 건너뛴다
            IReadOnlyList<Fish> reeling = TargetsOf(stake);

            for (int i = 0; i < reeling.Count; i++)
            {
                if (m_fish.Contains(reeling[i]))
                {
                    return reeling[i];
                }
            }

            return null;
        }

        // 그 말뚝 대가 지금 감는 물고기 전부(붙잡은 월척이면 그것 하나). 낚여 사라진 물고기가 한 틱 남아 있을 수 있다
        public IReadOnlyList<Fish> TargetsOf(StakeInteractable stake)
        {
            return m_reeling.TryGetValue(stake, out List<Fish> reeling) ? reeling : (IReadOnlyList<Fish>)s_none;
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
                if (stake.Rod == null)
                {
                    continue;
                }

                TickStun(stake, dt);

                if (!m_hooks.ContainsKey(stake))
                {
                    TickRod(stake, dt);
                }
            }
        }

        // 말뚝의 대가 바뀌었다(사기 · 옮기기 · 합치기 · 불러오기): 그 말뚝의 그물 주기를 처음부터 다시 센다
        public void Forget(StakeInteractable stake)
        {
            m_stunLeft.Remove(stake);
            m_reeling.Remove(stake);
        }

        // 그물: StunEvery초마다 사거리 안 헤엄치는 물고기를 Stun초 멈춘다(닿은 물고기가 없으면 닿을 때까지 기다린다). 꽂은 뒤 첫 멈춤도 한 주기를 기다린다
        private void TickStun(StakeInteractable stake, double dt)
        {
            RodTable rod = stake.Rod;

            if (rod.StunEvery <= 0d)
            {
                return;
            }

            double left = m_stunLeft.TryGetValue(stake, out double waited) ? waited : rod.StunEvery;
            left = Math.Max(0d, left - dt);

            if (left <= 0d)
            {
                float range = m_area.RangeOf(stake);

                foreach (Fish fish in m_fish)
                {
                    if (fish.HookedBy == null && m_area.InReach(stake, m_area.Layout.PointAt(fish.S), range))
                    {
                        fish.StunLeft = Math.Max(fish.StunLeft, rod.Stun);
                        left = rod.StunEvery;
                    }
                }

                if (left > 0d)
                {
                    Stunned?.Invoke(stake);
                }
            }

            m_stunLeft[stake] = left;
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

            // 대물은 밀지 않는다(느린 대물이 되돌려지기만 하면 물때가 끝나지 않는다)
            foreach (Fish fish in m_fish)
            {
                if (fish.HookedBy == null && !fish.Boss && (front == null || fish.S > front.S))
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

                fish.S += (float)(fish.Speed * SlowAt(fish) * dt);

                if (fish.S >= m_area.Layout.Length)
                {
                    m_fish.RemoveAt(i);
                    i--;
                    Escaped?.Invoke(fish);
                }
            }
        }

        // 사거리 안 대 중 가장 느리게 하는 값(없으면 1). 대물에게는 대물 둔화(닻대)도 본다
        private double SlowAt(Fish fish)
        {
            double slow = 1d;
            Vector2 p = m_area.Layout.PointAt(fish.S);

            foreach (StakeInteractable stake in m_area.Stakes)
            {
                if (stake.Rod == null)
                {
                    continue;
                }

                double rodSlow = fish.Boss ? Math.Min(stake.Rod.Slow, stake.Rod.BossSlow) : stake.Rod.Slow;

                if (rodSlow < slow && m_area.InReach(stake, p, m_area.RangeOf(stake)))
                {
                    slow = rodSlow;
                }
            }

            return slow;
        }

        // 사거리 안 맨 앞부터 Targets마리를 감는다. 월척은 붙잡아 멈추고 그 대는 더 못 감는다(큰 놈 잡이 BigGame은 붙잡지 않고 그대로 감는다)
        private void TickRod(StakeInteractable stake, double dt)
        {
            RodTable rod = stake.Rod;
            Front(stake, m_area.RangeOf(stake), rod.Targets);

            if (!m_reeling.TryGetValue(stake, out List<Fish> reeling))
            {
                reeling = new List<Fish>();
                m_reeling[stake] = reeling;
            }

            reeling.Clear();
            reeling.AddRange(m_front);

            foreach (Fish target in m_front)
            {
                if (target.Trophy && rod.BigGame <= 0d)
                {
                    reeling.Clear();
                    reeling.Add(target);
                    target.HookedBy = stake;
                    m_hooks[stake] = target;
                    Hooked?.Invoke(stake);
                    return;
                }

                target.Reeled += m_area.ReelOf(stake, target) * dt;

                if (target.Reeled >= target.Weight)
                {
                    reeling.Remove(target);
                    Remove(target);
                    Caught?.Invoke(target, stake);
                }
            }
        }

        // m_front에 사거리(말뚝 위쪽 반원) 안 물고기를 멀리 내려간(막다른 끝에 가까운) 순으로 count마리까지 담는다. 붙잡힌 월척은 뺀다
        private void Front(StakeInteractable stake, float range, int count)
        {
            m_front.Clear();

            foreach (Fish fish in m_fish)
            {
                if (fish.HookedBy == null && m_area.InReach(stake, m_area.Layout.PointAt(fish.S), range))
                {
                    m_front.Add(fish);
                }
            }

            m_front.Sort((a, b) => b.S.CompareTo(a.S));

            if (m_front.Count > count)
            {
                m_front.RemoveRange(count, m_front.Count - count);
            }
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
