using System.Collections.Generic;
using UnityEngine;

namespace ZooTycoon.UI
{
    // 설계 55: 오른쪽 메뉴 줄. 단추는 각 화면에 그대로 두고, 단추마다 자리 칸(부모)을 씌워 켜진 것만 위에서부터 차례로 쌓는다(꺼진 단추는 자리를 남기지 않음).
    // 칸을 줄여 k_Size로 보인다(지금은 132px 그림을 줄여 씀 — 104px 그림이 오면 배수 1). 톡 튀기(UiFx.Pulse)는 단추 자신의 크기라 칸과 겹치지 않는다
    public sealed class MenuRail : MonoBehaviour
    {
        private const float k_Size = 104f;
        private const float k_Gap = 20f;
        private const float k_Margin = 16f;

        private readonly List<RectTransform> m_items = new List<RectTransform>();
        private readonly List<RectTransform> m_slots = new List<RectTransform>();

        // 위에서부터 부른 차례대로 쌓는다
        public void Add(RectTransform item)
        {
            RectTransform slot = new GameObject(item.name + "Slot", typeof(RectTransform)).GetComponent<RectTransform>();
            slot.SetParent(item.parent, false);
            slot.SetSiblingIndex(item.GetSiblingIndex());
            slot.anchorMin = slot.anchorMax = slot.pivot = Vector2.one;
            slot.sizeDelta = item.rect.size;
            item.SetParent(slot, false);
            item.anchorMin = item.anchorMax = item.pivot = new Vector2(0.5f, 0.5f);
            item.anchoredPosition = Vector2.zero;
            m_items.Add(item);
            m_slots.Add(slot);
        }

        private void LateUpdate()
        {
            float y = -k_Margin;

            for (int i = 0; i < m_items.Count; i++)
            {
                RectTransform item = m_items[i];

                if (!item.gameObject.activeSelf)
                {
                    continue;
                }

                float scale = k_Size / item.rect.width;
                RectTransform slot = m_slots[i];
                slot.localScale = new Vector3(scale, scale, 1f);
                slot.anchoredPosition = new Vector2(-k_Margin, y);
                y -= item.rect.height * scale + k_Gap;
            }
        }
    }
}
