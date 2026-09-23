using System;
using System.Collections.Generic;
using UnityEngine;
using ZooTycoon.Core;

namespace ZooTycoon.World
{
    // 설계 08 v0.5: 빵집 손님 개체. 규칙은 ShopSim(Core)이 갖고, 이 개체는 받은 경로를 받은 시간 안에 걷고 말풍선·코인만 띄운다
    public sealed class ShopCustomer : Unit
    {
        [SerializeField] private CoinPopup m_coinPrefab;
        [SerializeField] private SpriteRenderer m_bubble;
        [SerializeField] private SpriteRenderer m_bubbleIcon;
        [SerializeField] private Sprite m_angrySprite;

        private const float k_ShakeAmount = 0.03f;
        private const float k_ShakeSpeed = 40f;

        private readonly Queue<Vector2> m_path = new Queue<Vector2>();
        private Customer m_customer;
        private float m_warnSeconds;
        private VisitorRecord m_look;
        private WalkState m_walk;
        private WaitState m_idle;
        private Action m_arrived;

        public Vector2 Destination { get; private set; }

        public void Initialize(Customer customer, float warnSeconds, VisitorRecord look, FrameCache frames, Sprite want, Vector2 start)
        {
            m_customer = customer;
            m_warnSeconds = warnSeconds;
            m_look = look;
            Setup(look, frames);
            m_walk = new WalkState(this, Walk_Arrived);
            m_idle = new WaitState(this, () => { });
            m_idle.SetSeconds(float.MaxValue);
            m_bubbleIcon.sprite = want;
            SetLogical(start);
            Destination = start;
            ChangeState(m_idle);
            // 말풍선은 머리 위(외형마다 키가 다르다)
            m_bubble.transform.localPosition = new Vector3(0f, Height, 0f);
        }

        // seconds > 0이면 그 시간에 도착하도록 속도를 맞춘다(Core가 센 시간). 0이면 기본 걸음
        public void Walk(IReadOnlyList<Vector2> points, float seconds, Action arrived = null)
        {
            m_path.Clear();
            float length = 0f;
            Vector2 from = Logical;

            foreach (Vector2 point in points)
            {
                m_path.Enqueue(point);
                length += Vector2.Distance(from, point);
                from = point;
            }

            Destination = from;
            m_arrived = arrived;
            SetMoveSpeed(seconds > 0f && length > 0f ? length / seconds : (float)m_look.MoveSpeed);
            Walk_Arrived();
        }

        public void HideBubble()
        {
            m_bubble.gameObject.SetActive(false);
        }

        public void ShowAngry()
        {
            m_bubble.gameObject.SetActive(true);
            m_bubble.sprite = m_angrySprite;
            m_bubbleIcon.enabled = false;
        }

        // 연출 1차: 줄 옆(sideOffset)으로 비켜 띄워 뒷 손님에 묻히지 않게
        public void PopCoin(string amount, float sideOffset)
        {
            CoinPopup popup = Instantiate(m_coinPrefab, m_bubble.transform.position + Vector3.right * sideOffset, Quaternion.identity, transform.parent);
            popup.Show(amount);
        }

        // 연출 1차: 빈 진열대 앞에서 남은 인내가 warnSeconds 이하면 말풍선이 좌우로 흔들린다
        protected override void Update()
        {
            base.Update();
            bool warn = m_customer.Phase == CustomerPhase.AtShelf && m_customer.Timer <= m_warnSeconds;
            float x = warn ? Mathf.Sin(Time.time * k_ShakeSpeed) * k_ShakeAmount : 0f;
            m_bubble.transform.localPosition = new Vector3(x, Height, 0f);
        }

        protected override Vector3 ToScreen(Vector2 logical)
        {
            return new Vector3(logical.x, logical.y, 0f);
        }

        private void Walk_Arrived()
        {
            if (m_path.Count > 0)
            {
                m_walk.SetTarget(m_path.Dequeue());
                ChangeState(m_walk);
                return;
            }

            ChangeState(m_idle);
            Action arrived = m_arrived;
            m_arrived = null;
            arrived?.Invoke();
        }
    }
}
