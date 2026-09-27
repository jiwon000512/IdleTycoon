using TMPro;
using UnityEngine;
using ZooTycoon.Core;

namespace ZooTycoon.World
{
    // 설계 22: 머리 위 말풍선 그리기(손님·점원·웜뱃 공용). 이모지는 Core BubbleState(BubbleTable 칸 번호·프레임·경과)를 읽고, 글자는 9-slice 상자를 글 폭에 맞춘다
    public static class Bubbles
    {
        // 글자 상자 높이(꼬리 빼고, 유닛)와 글자 폭 최소. 상자는 꼬리(bubble_tail 6칸 - 겹침 1칸)만큼 머리 위로 뜬다
        public const float SayLift = 0.125f;
        private const float k_SayHeight = 0.4f;
        private const float k_SayMinWidth = 0.8f;

        public static void Show(SpriteRenderer renderer, Sprite[] frames, BubbleState state, bool visible)
        {
            BubbleTable row = state.Row;

            if (row == null || !visible)
            {
                renderer.enabled = false;
                return;
            }

            int frame = row.Frame + (row.Frames > 1 ? (int)(state.Elapsed / row.FrameSeconds) % row.Frames : 0);
            renderer.sprite = frames[Mathf.Clamp(frame, 0, frames.Length - 1)];
            renderer.enabled = true;
        }

        // 글자 말풍선: 몸통·꼬리·글자는 늘 함께 켜고 끈다(꼬리는 몸통의 자식 그림이라 몸통을 꺼도 따로 남는다)
        public static void ShowSay(SpriteRenderer box, SpriteRenderer tail, TextMeshPro text, string say, float padding)
        {
            text.text = say;
            text.enabled = true;
            text.ForceMeshUpdate();
            box.size = new Vector2(Mathf.Max(k_SayMinWidth, text.preferredWidth + padding), k_SayHeight);
            box.enabled = true;
            tail.enabled = true;
        }

        public static void HideSay(SpriteRenderer box, SpriteRenderer tail, TextMeshPro text)
        {
            box.enabled = false;
            tail.enabled = false;
            text.enabled = false;
        }
    }
}
