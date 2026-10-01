using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using GameKit.Events;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.World
{
    // 설계 24 → 설계 37: 곳 하나의 똥 그림 조각(빵집 · 광장 · 농장이 하나씩 든다, 굴 파기의 DigView처럼). 코루틴은 곳 화면(host) 밑에서 돈다.
    // 떨어지면 엉덩이 높이에서 톡 떨어져 흙먼지와 함께 한 번 튀고, 치우면 납작해지며 사라지고 흰 반짝(거름 국자 코인은 그 자리 위에).
    // 거름을 얻으면(PoopsCleaned) 웜뱃 머리 위에 거름 「+N」 팝업(획득 팝업 쌓기 규칙)과 거름빛 · 금빛 알갱이
    public sealed class PoopViews : IDisposable
    {
        private const float k_DropHeight = 0.3f;
        private const float k_DropSeconds = 0.12f;
        private const float k_CleanSeconds = 0.15f;
        private const int k_Bits = 6;
        private const int k_BitOrder = 1000;
        private const float k_PopupHeight = 1.4f;
        private const int k_GainBits = 8;
        private const string k_CoinKey = "coin_popup";
        private const string k_GainKey = "harvest_popup";
        private static readonly Color k_Dirt = new Color(0.45f, 0.3f, 0.18f);
        private static readonly Color k_Sparkle = new Color32(255, 247, 222, 255);
        private static readonly Color k_Manure = new Color32(122, 78, 42, 255);
        private static readonly Color k_Gold = new Color32(242, 193, 78, 255);

        private readonly MonoBehaviour m_host;
        private readonly WombatArea m_area;
        private readonly SpriteAnimator m_prefab;
        private readonly Sprite[] m_frames;
        private readonly float m_frameRate;
        private readonly CoinPopup m_popupPrefab;
        private readonly TableSet m_tables;
        private readonly FrameCache m_icons;
        private readonly Func<System.Numerics.Vector2, Vector3> m_toWorld;
        private readonly Dictionary<PoopInteractable, SpriteAnimator> m_poops = new Dictionary<PoopInteractable, SpriteAnimator>();
        private readonly IDisposable[] m_subscriptions;
        private Sprite m_white;

        public PoopViews(MonoBehaviour host, WombatArea area, EventBus bus, SpriteAnimator prefab, Sprite[] frames, float frameRate,
            CoinPopup popupPrefab, TableSet tables, FrameCache icons, Func<System.Numerics.Vector2, Vector3> toWorld)
        {
            m_host = host;
            m_area = area;
            m_prefab = prefab;
            m_frames = frames;
            m_frameRate = frameRate;
            m_popupPrefab = popupPrefab;
            m_tables = tables;
            m_icons = icons;
            m_toWorld = toWorld;
            m_subscriptions = new[]
            {
                bus.Subscribe<Events.PoopDropped>(Bus_PoopDropped),
                bus.Subscribe<Events.PoopCleaned>(Bus_PoopCleaned),
                bus.Subscribe<Events.PoopsCleaned>(Bus_PoopsCleaned),
            };
        }

        public void Dispose()
        {
            foreach (IDisposable subscription in m_subscriptions)
            {
                subscription.Dispose();
            }
        }

        // 대상이 된 똥이 한 번 튄다
        public void Bounce(PoopInteractable poop)
        {
            if (m_poops.TryGetValue(poop, out SpriteAnimator view))
            {
                m_host.StartCoroutine(Fx.Bounce(view.transform));
            }
        }

        private Sprite White => m_white != null ? m_white : m_white = Fx.NewSquare();

        private void Bus_PoopDropped(Events.PoopDropped e)
        {
            if (e.Poop.Area != m_area)
            {
                return;
            }

            SpriteAnimator poop = UnityEngine.Object.Instantiate(m_prefab, m_host.transform);
            poop.Play(m_frames, m_frameRate);
            m_poops[e.Poop] = poop;
            m_host.StartCoroutine(Drop(poop.transform, m_toWorld(e.Poop.Position)));
        }

        // 치웠다(사물 밑에 깔렸어도): 납작해지며 사라지고 흰 반짝
        private void Bus_PoopCleaned(Events.PoopCleaned e)
        {
            if (!m_poops.TryGetValue(e.Poop, out SpriteAnimator poop))
            {
                return;
            }

            m_poops.Remove(e.Poop);
            m_host.StartCoroutine(Fx.Burst(m_host.transform, White, poop.transform.position + Vector3.up * 0.15f, k_Bits, 0.8f, 1.5f, 6f, 0.35f, 2, k_Sparkle, k_BitOrder));
            m_host.StartCoroutine(Clean(poop.transform));

            if (e.Coins > 0d)
            {
                CoinPopup popup = UnityEngine.Object.Instantiate(m_popupPrefab, poop.transform.position + Vector3.up * 0.4f, Quaternion.identity, m_host.transform);
                popup.Show(m_tables.Format(k_CoinKey, e.Coins.ToString("0", CultureInfo.InvariantCulture)));
            }
        }

        // 설계 37: 거름 「+N」은 거두기처럼 웜뱃 머리 위(그림이 아니라 sim 자리), 알갱이는 그 자리에서 거름빛 · 금빛으로
        private void Bus_PoopsCleaned(Events.PoopsCleaned e)
        {
            if (e.Area != m_area)
            {
                return;
            }

            Vector3 head = m_toWorld(m_area.Wombat.Mover.Position) + Vector3.up * k_PopupHeight;
            CoinPopup popup = UnityEngine.Object.Instantiate(m_popupPrefab, head, Quaternion.identity, m_host.transform);
            popup.Show(m_tables.Format(k_GainKey, e.Count), m_icons.Get(m_tables.Get<ItemTable>(e.Item).Icon)[0]);
            m_host.StartCoroutine(Fx.Burst(m_host.transform, White, head, k_GainBits, 0.9f, 2f, 8f, 0.45f, 2, k_Manure, k_BitOrder));
            m_host.StartCoroutine(Fx.Burst(m_host.transform, White, head, k_GainBits, 0.9f, 2f, 8f, 0.45f, 2, k_Gold, k_BitOrder));
        }

        private IEnumerator Drop(Transform poop, Vector3 at)
        {
            for (float t = 0f; t < k_DropSeconds; t += Time.deltaTime)
            {
                if (poop == null)
                {
                    yield break;
                }

                float k = t / k_DropSeconds;
                poop.position = at + Vector3.up * (k_DropHeight * (1f - k * k));
                yield return null;
            }

            if (poop == null)
            {
                yield break;
            }

            poop.position = at;
            m_host.StartCoroutine(Fx.Burst(m_host.transform, White, at, k_Bits, 0.6f, 1.2f, 8f, 0.3f, 2, k_Dirt, k_BitOrder));
            yield return Fx.Bounce(poop);
        }

        private static IEnumerator Clean(Transform poop)
        {
            for (float t = 0f; t < k_CleanSeconds; t += Time.deltaTime)
            {
                float k = t / k_CleanSeconds;
                poop.localScale = new Vector3(1f + 0.3f * k, 1f - k, 1f);
                yield return null;
            }

            UnityEngine.Object.Destroy(poop.gameObject);
        }
    }
}
