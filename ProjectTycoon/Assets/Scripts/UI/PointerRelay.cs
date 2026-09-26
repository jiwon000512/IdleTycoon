using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ZooTycoon.UI
{
    // 설계 18: 편집 모드에서 화면 전체(굴 위) 터치를 받아 넘기는 투명 판. 사물 집기·카메라 팬은 Presenter가 판단한다
    public sealed class PointerRelay : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public event Action<PointerEventData> PointerDown;
        public event Action<PointerEventData> Dragged;
        public event Action<PointerEventData> PointerUp;

        public void OnPointerDown(PointerEventData eventData)
        {
            PointerDown?.Invoke(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            Dragged?.Invoke(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            PointerUp?.Invoke(eventData);
        }
    }
}
