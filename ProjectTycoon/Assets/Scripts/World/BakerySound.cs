using System;
using System.Collections.Generic;
using UnityEngine;
using GameKit.Audio;
using GameKit.Events;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.World
{
    // 설계 10: 가게 사건 → 효과음(SoundTable). 결제 짤랑 · 오븐이 다 구움 띵 · 화난 퇴장 뿌우~.
    // 재생은 GameKit SoundManager 효과음 풀(설계 23). 최소 간격·연속 피치는 여기서 정한다. WorldManager가 빵집 개체에 붙인다
    public sealed class BakerySound : MonoBehaviour
    {
        private sealed class Channel
        {
            public SoundTable Sound;
            public AudioClip Clip;
            public float Last = float.NegativeInfinity;
            public int Combo;
        }

        private readonly Dictionary<string, Channel> m_channels = new Dictionary<string, Channel>();
        private readonly Dictionary<OvenInteractable, bool> m_ovenReady = new Dictionary<OvenInteractable, bool>();
        private BakeryArea m_shop;
        private IDisposable[] m_subscriptions;

        public void Initialize(BakeryArea shop, EventBus bus, TableSet tables)
        {
            m_shop = shop;

            foreach (SoundTable sound in tables.GetAll<SoundTable>())
            {
                m_channels[sound.Id] = new Channel { Sound = sound, Clip = Resources.Load<AudioClip>(sound.Clip) };
            }

            m_subscriptions = new[]
            {
                bus.Subscribe<Events.BakeryVisitorPaid>(Bus_VisitorPaid),
                bus.Subscribe<Events.ThingChanged>(Bus_ThingChanged),
            };
        }

        private void OnDestroy()
        {
            if (m_subscriptions != null)
            {
                foreach (IDisposable subscription in m_subscriptions)
                {
                    subscription.Dispose();
                }
            }
        }

        private void Play(string id)
        {
            Channel channel = m_channels[id];
            SoundTable record = channel.Sound;
            float since = Time.time - channel.Last;

            if (since < record.MinGap)
            {
                return;
            }

            channel.Combo = since < record.ComboSeconds ? channel.Combo + 1 : 0;
            channel.Last = Time.time;
            float pitch = Mathf.Min((float)record.PitchMax, 1f + (float)record.PitchStep * channel.Combo);
            SoundManager.Instance.PlaySfx(channel.Clip, (float)record.Volume, pitch);
        }

        private void Bus_VisitorPaid(Events.BakeryVisitorPaid e)
        {
            if (e.Visitor.Bakery == m_shop)
            {
                Play(SoundTable.k_Pay);
            }
        }

        // 다 구운 빵이 없다가 생긴 순간만
        private void Bus_ThingChanged(Events.ThingChanged e)
        {
            if (!(e.Thing is OvenInteractable oven) || oven.Bakery != m_shop)
            {
                return;
            }

            bool ready = oven.Ready > 0;
            m_ovenReady.TryGetValue(oven, out bool wasReady);

            if (ready && !wasReady)
            {
                Play(SoundTable.k_OvenDone);
            }

            m_ovenReady[oven] = ready;
        }
    }
}
