using System.Collections;
using UnityEngine;
using ZooTycoon.Core;

namespace ZooTycoon.World
{
    // 설계 09: 상호작용 대상이 된 사물이 0.15초 살짝 튄다(스케일 1.05 → 1)
    public static class Fx
    {
        // 든 빵은 손에서 위로 쌓인다(2026-09-23). 앞모습은 가운데, 옆모습은 보는 쪽으로 reach만큼. 발끝 기준 로컬 위치
        public static Vector3 HandOffset(Facing facing, float height, float reach)
        {
            float x = facing == Facing.Left ? -reach : facing == Facing.Right ? reach : 0f;
            return new Vector3(x, height, 0f);
        }

        // 뒷모습이면 몸 뒤(몸에 가려짐), 아니면 몸 앞
        public static int CarryOrder(Facing facing, int front, int back)
        {
            return facing == Facing.Up ? back : front;
        }

        private const float k_BounceSeconds = 0.15f;
        private const float k_BounceScale = 1.05f;

        // 튀는 사이 대상이 없어질 수 있다(설계 24: 치운 똥)
        public static IEnumerator Bounce(Transform target)
        {
            Vector3 original = Vector3.one;

            for (float t = 0f; t < k_BounceSeconds && target != null; t += Time.deltaTime)
            {
                float k = Mathf.Sin(t / k_BounceSeconds * Mathf.PI);
                target.localScale = original * (1f + (k_BounceScale - 1f) * k);
                yield return null;
            }

            if (target != null)
            {
                target.localScale = original;
            }
        }

        // 월드 한 칸(2px ÷ PPU 80)
        public const float k_Cell = 0.025f;

        // 한 변 1유닛 흰 네모(색·크기는 쓰는 쪽이). 부르는 쪽이 들고 있는다
        public static Sprite NewSquare()
        {
            return Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
        }

        // 네모 알갱이 count개가 at에서 좌우(spread)·위(lift)로 튀어 gravity로 떨어지며 cells칸 → 1칸으로 작아지다 사라진다. 칸 격자에 맞춰 움직인다
        // ponytail: 알갱이마다 GameObject를 만든다. 동시에 수백 개가 되면 PoolManager로
        public static IEnumerator Burst(Transform parent, Sprite square, Vector3 at, int count, float spread, float lift, float gravity, float seconds, int cells, Color color, int order)
        {
            Transform holder = new GameObject("Burst").transform;
            holder.SetParent(parent, false);
            Transform[] bits = new Transform[count];
            Vector2[] velocity = new Vector2[count];

            for (int i = 0; i < count; i++)
            {
                SpriteRenderer bit = new GameObject("Bit").AddComponent<SpriteRenderer>();
                bit.transform.SetParent(holder, false);
                bit.sprite = square;
                bit.color = color;
                bit.sortingOrder = order;
                bits[i] = bit.transform;
                velocity[i] = new Vector2(Random.Range(-spread, spread), Random.Range(0.5f, 1f) * lift);
            }

            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float size = Mathf.Ceil(cells * (1f - t / seconds)) * k_Cell;

                for (int i = 0; i < count; i++)
                {
                    Vector3 p = at + (Vector3)(velocity[i] * t) + Vector3.down * (0.5f * gravity * t * t);
                    bits[i].position = new Vector3(Mathf.Round(p.x / k_Cell) * k_Cell, Mathf.Round(p.y / k_Cell) * k_Cell, p.z);
                    bits[i].localScale = new Vector3(size, size, 1f);
                }

                yield return null;
            }

            Object.Destroy(holder.gameObject);
        }
    }
}
