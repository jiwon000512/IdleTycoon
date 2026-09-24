using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using GameKit.UI;

namespace ZooTycoon.UI
{
    // 설계 08 → 설계 09: 가게 HUD = 조이스틱 + 상호작용 버튼(글자 없이 행동 아이콘. v0.4: 아이콘 경로는 actions.json). 가게 이름은 상단 HUD 시안 C에서 뺐다.
    // 설계 11: 곳을 옮기면 화면 전체 검은 이미지가 사라지며 새 곳이 드러난다
    public sealed class ShopHudView : UIView
    {
        private static readonly Color k_DisabledIcon = new Color(1f, 1f, 1f, 0.4f);

        [SerializeField] private Joystick m_joystick;
        [SerializeField] private Button m_interactButton;
        [SerializeField] private Image m_interactIcon;
        [Tooltip("화면 전환 페이드(검은 전체 화면, 평소 알파 0)")]
        [SerializeField] private Image m_fade;
        [SerializeField] private float m_fadeSeconds = 0.3f;

        private readonly Dictionary<string, Sprite> m_icons = new Dictionary<string, Sprite>();

        public event Action<System.Numerics.Vector2> JoystickMoved;
        public event Action InteractClicked;

        private void Awake()
        {
            m_interactButton.onClick.AddListener(InteractButton_Clicked);
            m_joystick.Moved += Joystick_Moved;
        }

        // iconPath: Resources/ 기준. null이면 아이콘은 그대로 두고 흐리게만
        public void SetInteract(string iconPath, bool enabled)
        {
            if (iconPath != null)
            {
                if (!m_icons.TryGetValue(iconPath, out Sprite sprite))
                {
                    sprite = Resources.Load<Sprite>(iconPath);
                    m_icons[iconPath] = sprite;
                }

                m_interactIcon.sprite = sprite;
            }

            m_interactIcon.color = enabled ? Color.white : k_DisabledIcon;
            m_interactButton.interactable = enabled;
        }

        public void FadeIn()
        {
            StopAllCoroutines();
            StartCoroutine(FadeRoutine());
        }

        private IEnumerator FadeRoutine()
        {
            for (float t = 0f; t < m_fadeSeconds; t += Time.unscaledDeltaTime)
            {
                m_fade.color = new Color(0f, 0f, 0f, 1f - t / m_fadeSeconds);
                yield return null;
            }

            m_fade.color = Color.clear;
        }

        private void InteractButton_Clicked()
        {
            InteractClicked?.Invoke();
        }

        private void Joystick_Moved(Vector2 value)
        {
            JoystickMoved?.Invoke(new System.Numerics.Vector2(value.x, value.y));
        }
    }
}
