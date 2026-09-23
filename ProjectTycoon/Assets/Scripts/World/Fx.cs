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

        public static IEnumerator Bounce(Transform target)
        {
            Vector3 original = Vector3.one;

            for (float t = 0f; t < k_BounceSeconds; t += Time.deltaTime)
            {
                float k = Mathf.Sin(t / k_BounceSeconds * Mathf.PI);
                target.localScale = original * (1f + (k_BounceScale - 1f) * k);
                yield return null;
            }

            target.localScale = original;
        }
    }
}
