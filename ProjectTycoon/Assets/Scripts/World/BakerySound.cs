using System.Collections.Generic;
using UnityEngine;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.World
{
    // 설계 10: 가게 사건 → 효과음(SoundTable). 결제 짤랑 · 오븐이 다 구움 띵 · 화난 퇴장 뿌우~.
    // 소리마다 AudioSource 하나(피치를 따로 올리려고). WorldManager가 빵집 개체에 붙인다
    public sealed class BakerySound : MonoBehaviour
    {
        private sealed class Channel
        {
            public SoundTable Sound;
            public AudioSource Source;
            public float Last = float.NegativeInfinity;
            public int Combo;
        }

        private readonly Dictionary<string, Channel> m_channels = new Dictionary<string, Channel>();
        private readonly Dictionary<OvenInteractable, bool> m_ovenReady = new Dictionary<OvenInteractable, bool>();
        private BakeryArea m_shop;

        public void Initialize(BakeryArea shop, TableSet tables)
        {
            m_shop = shop;

            foreach (SoundTable sound in tables.GetAll<SoundTable>())
            {
                AudioSource source = gameObject.AddComponent<AudioSource>();
                source.clip = Resources.Load<AudioClip>(sound.Clip);
                source.playOnAwake = false;
                source.volume = (float)sound.Volume;
                m_channels[sound.Id] = new Channel { Sound = sound, Source = source };
            }

            m_shop.VisitorPaid += Shop_VisitorPaid;
            m_shop.VisitorGaveUp += Shop_VisitorGaveUp;
            m_shop.ThingChanged += Shop_ThingChanged;
        }

        private void OnDestroy()
        {
            if (m_shop != null)
            {
                m_shop.VisitorPaid -= Shop_VisitorPaid;
                m_shop.VisitorGaveUp -= Shop_VisitorGaveUp;
                m_shop.ThingChanged -= Shop_ThingChanged;
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
            channel.Source.pitch = Mathf.Min((float)record.PitchMax, 1f + (float)record.PitchStep * channel.Combo);
            channel.Source.PlayOneShot(channel.Source.clip);
        }

        private void Shop_VisitorPaid(BakeryVisitor visitor, double coins)
        {
            Play(SoundTable.k_Pay);
        }

        private void Shop_VisitorGaveUp(BakeryVisitor visitor)
        {
            Play(SoundTable.k_GiveUp);
        }

        // 다 구운 빵이 없다가 생긴 순간만
        private void Shop_ThingChanged(Interactable thing)
        {
            if (!(thing is OvenInteractable oven))
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
