using System;
using UnityEngine;
using GameKit.Audio;
using GameKit.Events;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.World
{
    // 설계 23: 웜뱃이 있는 곳의 배경음악(BgmTable). 표에 그 곳 행이 없으면 지금 곡을 이어서 튼다. WorldManager가 붙인다
    public sealed class AreaBgm : MonoBehaviour
    {
        private Mall m_mall;
        private TableSet m_tables;
        private IDisposable m_subscription;

        public void Initialize(Mall mall, EventBus bus, TableSet tables)
        {
            m_mall = mall;
            m_tables = tables;
            m_subscription = bus.Subscribe<Events.AreaChanged>(Bus_AreaChanged);
            Play(mall.Active);
        }

        private void OnDestroy()
        {
            m_subscription?.Dispose();
        }

        private void Bus_AreaChanged(Events.AreaChanged e)
        {
            Play(e.Active);
        }

        private void Play(WombatArea area)
        {
            string id = area == m_mall.Bakery ? BgmTable.k_Bakery : BgmTable.k_Plaza;

            foreach (BgmTable row in m_tables.GetAll<BgmTable>())
            {
                if (row.Id == id)
                {
                    SoundManager.Instance.PlayBgm(Resources.Load<AudioClip>(row.Clip), (float)row.Volume);
                    return;
                }
            }
        }
    }
}
