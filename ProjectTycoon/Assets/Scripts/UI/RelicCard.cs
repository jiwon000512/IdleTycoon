using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZooTycoon.UI
{
    // 설계 31 · 32: 유물 카드 하나(좌판 후보 · 유물 화면의 칸 · 격자 · 정보 줄이 같은 모양, 없는 글은 비워 둔다)
    [Serializable]
    public sealed class RelicCard
    {
        public sealed class Data
        {
            public string IconPath;
            public string Name;
            public string Effect;
            public int Stars;
            public bool Owned = true;
            public bool Selected;
            public string Badge;
        }

        // 못 모은 유물 · 빈 별 · 카드 뒷면은 외곽선 색 그림자
        public static readonly Color k_Shadow = new Color32(0x34, 0x20, 0x20, 90);

        public RectTransform Root;
        public Button Button;
        public Image Frame;
        public Image Icon;
        public Image[] Stars;
        public TextMeshProUGUI Name;
        public TextMeshProUGUI Effect;
        public GameObject Badge;
        public TextMeshProUGUI BadgeText;

        // data가 null이면 빈 칸(아이콘 · 별 · 글을 숨김)
        public void Set(Data data, Sprite chip, Sprite chipSelected)
        {
            bool has = data != null;
            Icon.enabled = has;
            Icon.sprite = has ? Resources.Load<Sprite>(data.IconPath) : null;
            Icon.color = has && data.Owned ? Color.white : k_Shadow;

            if (Frame != null)
            {
                Frame.sprite = has && data.Selected ? chipSelected : chip;
            }

            for (int i = 0; i < Stars.Length; i++)
            {
                Stars[i].enabled = has;
                Stars[i].color = has && i < data.Stars ? Color.white : k_Shadow;
            }

            if (Name != null)
            {
                Name.text = has ? data.Name : string.Empty;
            }

            if (Effect != null)
            {
                Effect.text = has ? data.Effect : string.Empty;
            }

            if (Badge != null)
            {
                Badge.SetActive(has && !string.IsNullOrEmpty(data.Badge));
                BadgeText.text = has ? data.Badge : string.Empty;
            }
        }

        // 뒷면: 지금 아이콘을 그림자로, 이름 자리에 「?」, 별 · 효과 · 이름표는 숨긴다(아트방 카드 뒷면 그림이 오기 전 임시)
        public void SetBack(Sprite chip)
        {
            Icon.enabled = true;
            Icon.color = k_Shadow;
            Frame.sprite = chip;
            Name.text = "?";
            Effect.text = string.Empty;
            Badge.SetActive(false);

            foreach (Image star in Stars)
            {
                star.enabled = false;
            }
        }

        public void SetAlpha(float alpha)
        {
            foreach (Graphic graphic in Root.GetComponentsInChildren<Graphic>())
            {
                Color color = graphic.color;
                color.a = graphic == Icon || Array.IndexOf(Stars, graphic) >= 0 ? Mathf.Min(color.a, alpha) : alpha;
                graphic.color = color;
            }
        }
    }
}
