using System.Collections;
using UnityEngine;

namespace ZooTycoon.World
{
    // 설계 09: 상호작용 대상이 된 사물이 0.15초 살짝 튄다(스케일 1.05 → 1)
    public static class Fx
    {
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
