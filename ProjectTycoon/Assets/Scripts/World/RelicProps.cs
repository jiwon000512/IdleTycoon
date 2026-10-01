using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using ZooTycoon.Core;

namespace ZooTycoon.World
{
    // 설계 31 E: 칸에 끼운 유물을 가게에 건다(곳의 View가 하나씩 든다). 걸 자리(RelicTable.anchor)의 기준점은 View가 Core 좌표로 주고,
    // 유물 그림은 기준점 + (dx, dy)에 선다. 깊이는 기준점(사물 밑변)으로 정렬해 사물 바로 앞에 그려진다. 효과가 날 때 View가 Pop(효과)로 톡 튀게 한다
    public sealed class RelicProps
    {
        private const float k_SortNudge = 0.01f;

        private readonly MonoBehaviour m_host;
        private readonly Relics m_relics;
        private readonly Func<string, IEnumerable<System.Numerics.Vector2>> m_anchors;
        private readonly Func<System.Numerics.Vector2, Vector3> m_toWorld;
        private readonly List<KeyValuePair<string, Transform>> m_props = new List<KeyValuePair<string, Transform>>();

        public RelicProps(MonoBehaviour host, Relics relics, Func<string, IEnumerable<System.Numerics.Vector2>> anchors, Func<System.Numerics.Vector2, Vector3> toWorld)
        {
            m_host = host;
            m_relics = relics;
            m_anchors = anchors;
            m_toWorld = toWorld;
        }

        // 칸 · 사물 자리가 바뀌면 다시 건다
        public void Rebuild()
        {
            foreach (KeyValuePair<string, Transform> prop in m_props)
            {
                UnityEngine.Object.Destroy(prop.Value.gameObject);
            }

            m_props.Clear();

            foreach (RelicTable relic in m_relics.Slots)
            {
                if (relic == null)
                {
                    continue;
                }

                foreach (System.Numerics.Vector2 at in m_anchors(relic.Anchor))
                {
                    m_props.Add(new KeyValuePair<string, Transform>(relic.Effect, Create(relic, at)));
                }
            }
        }

        // 그 효과의 유물이 걸려 있으면 톡 튄다
        public void Pop(string effect)
        {
            foreach (KeyValuePair<string, Transform> prop in m_props)
            {
                if (prop.Key == effect && prop.Value != null)
                {
                    m_host.StartCoroutine(Fx.Bounce(prop.Value.GetChild(0)));
                }
            }
        }

        private Transform Create(RelicTable relic, System.Numerics.Vector2 at)
        {
            GameObject root = new GameObject("Relic_" + relic.Id);
            root.transform.SetParent(m_host.transform, false);
            root.transform.position = m_toWorld(at) + Vector3.down * k_SortNudge;
            root.AddComponent<SortingGroup>();

            GameObject body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3((float)relic.Dx, (float)relic.Dy + k_SortNudge, 0f);
            SpriteRenderer sprite = body.AddComponent<SpriteRenderer>();
            sprite.sprite = Resources.Load<Sprite>(relic.Icon);
            return root.transform;
        }
    }
}
