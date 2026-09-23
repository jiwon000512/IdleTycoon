using System.Collections.Generic;
using UnityEngine;
using ZooTycoon.Core;

namespace ZooTycoon.World
{
    // 설계 10: 가게 사건 → 효과음(sounds.json). 결제 짤랑 · 오븐이 다 구움 띵 · 화난 퇴장 흥.
    // 소리마다 AudioSource 하나(피치를 따로 올리려고). WorldManager가 가게 개체에 붙인다
    public sealed class ShopSound : MonoBehaviour
    {
        private sealed class Channel
        {
            public SoundRecord Record;
            public AudioSource Source;
            public float Last = float.NegativeInfinity;
            public int Combo;
        }

        private readonly Dictionary<string, Channel> m_channels = new Dictionary<string, Channel>();
        private readonly List<bool> m_ovenReady = new List<bool>();
        private ShopSim m_shop;

        public void Initialize(ShopSim shop, GameTables tables)
        {
            m_shop = shop;

            foreach (SoundRecord sound in tables.Sounds)
            {
                AudioSource source = gameObject.AddComponent<AudioSource>();
                source.clip = Resources.Load<AudioClip>(sound.Clip);
                source.playOnAwake = false;
                source.volume = (float)sound.Volume;
                m_channels[sound.Id] = new Channel { Record = sound, Source = source };
            }

            m_shop.CustomerPaid += Shop_CustomerPaid;
            m_shop.CustomerGaveUp += Shop_CustomerGaveUp;
            m_shop.OvenChanged += Shop_OvenChanged;
        }

        private void OnDestroy()
        {
            if (m_shop != null)
            {
                m_shop.CustomerPaid -= Shop_CustomerPaid;
                m_shop.CustomerGaveUp -= Shop_CustomerGaveUp;
                m_shop.OvenChanged -= Shop_OvenChanged;
            }
        }

        private void Play(string id)
        {
            Channel channel = m_channels[id];
            SoundRecord record = channel.Record;
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

        private void Shop_CustomerPaid(Customer customer, double coins)
        {
            Play(SoundRecord.k_Pay);
        }

        private void Shop_CustomerGaveUp(Customer customer)
        {
            Play(SoundRecord.k_GiveUp);
        }

        // 다 구운 빵이 없다가 생긴 순간만
        private void Shop_OvenChanged(int index)
        {
            while (m_ovenReady.Count <= index)
            {
                m_ovenReady.Add(false);
            }

            bool ready = m_shop.Ovens[index].Ready > 0;

            if (ready && !m_ovenReady[index])
            {
                Play(SoundRecord.k_OvenDone);
            }

            m_ovenReady[index] = ready;
        }
    }
}
