using System;
using UnityEngine;
using ZooTycoon.Core;
using Random = UnityEngine.Random;

namespace ZooTycoon.World
{
    // 설계 06 P3: 입장(WalkState) → 구경(WaitState, 코인) → 퇴장(WalkState) → 파괴. 동물이 없는 존이면 그냥 지나간다
    public sealed class Visitor : Unit
    {
        [SerializeField] private CoinPopup m_coinPrefab;
        [Tooltip("길 바깥에서 나타나고 사라지는 거리(유닛)")]
        [SerializeField] private float m_entryMargin = 1.5f;
        [Tooltip("구경 지점이 길 양 끝에서 안쪽으로 들어오는 거리(유닛)")]
        [SerializeField] private float m_viewInset = 1f;

        private VisitorRecord m_record;
        private Vector2 m_zoneCenter;
        private Vector2 m_exit;
        private WalkState m_enter;
        private WaitState m_view;
        private WalkState m_leave;

        public event Action<Visitor> Exited;

        // 규칙 예외: 연출 난수는 UnityEngine.Random을 쓴다(프로그래밍-규약 5장)
        public void Initialize(VisitorRecord record, ZoneView zone, FrameCache frames)
        {
            m_record = record;
            m_zoneCenter = zone.Center;
            Setup(record, frames);

            bool views = zone.AnimalCount > 0;
            Rect path = zone.PathBounds;
            bool leftToRight = Random.value < 0.5f;
            float z = Random.Range(path.yMin, path.yMax);
            float startX = leftToRight ? path.xMin - m_entryMargin : path.xMax + m_entryMargin;
            float exitX = leftToRight ? path.xMax + m_entryMargin : path.xMin - m_entryMargin;
            Vector2 viewPoint = new Vector2(Random.Range(path.xMin + m_viewInset, path.xMax - m_viewInset), z);

            SetLogical(new Vector2(startX, z));
            m_exit = new Vector2(exitX, z);
            m_enter = new WalkState(this, views ? EnterView : (Action)Exit);
            m_view = new WaitState(this, EnterLeave);
            m_leave = new WalkState(this, Exit);

            m_enter.SetTarget(views ? viewPoint : m_exit);
            ChangeState(m_enter);
        }

        // 설계 06 P8: 존을 보고 서서 머리 위에 코인
        private void EnterView()
        {
            m_view.SetSeconds(Random.Range((float)m_record.ViewSecondsMin, (float)m_record.ViewSecondsMax));
            ChangeState(m_view);
            Face(m_zoneCenter);

            Vector3 head = transform.position + Vector3.up * Height;
            Instantiate(m_coinPrefab, head, Quaternion.identity, transform.parent);
        }

        private void EnterLeave()
        {
            m_leave.SetTarget(m_exit);
            ChangeState(m_leave);
        }

        private void Exit()
        {
            Exited?.Invoke(this);
            Destroy(gameObject);
        }
    }
}
