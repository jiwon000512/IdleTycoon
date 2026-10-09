using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GameKit.Events;
using GameKit.Tables;
using TMPro;
using UnityEngine;
using ZooTycoon.Core;

namespace ZooTycoon.World
{
    // 설계 54: 「가기」 길잡이 화살표와 보상 「+N」. 가리키는 것(다음 통로 · 마지막 사물)이 화면 안이면 그 위에서 톡톡, 밖이면 화면 끝에서 그쪽을 가리킨다.
    // 다른 곳으로 가는 길이면 곳 이름표(「농장」)를 붙인다. 받기 · 상자 보상은 웜뱃 머리 위 획득 팝업(겹치지 않게 쌓임)
    public sealed class GuideView : MonoBehaviour
    {
        // 가리키는 점 위 화살표 끝 높이(가장 낮게) · 몸체 그림 위끝과의 틈 · 몸체로 볼 발끝 거리 · 톡톡 폭과 빠르기(유닛)
        private const float k_Above = 1.4f;
        private const float k_TopGap = 0.25f;
        private const float k_FootSnap = 0.05f;
        private const float k_Bob = 0.1f;
        private const float k_BobSpeed = 6f;
        // 화살표 꼬리와 곳 이름표 사이 틈(유닛)
        private const float k_TagGap = 0.25f;
        private const float k_PopupHeight = 1.3f;
        private const int k_Order = 70;
        // 화살표가 머무는 화면 안쪽(화면 비율): 위는 HUD 알약 줄, 오른쪽은 메뉴 버튼 줄을 비킨다
        private static readonly Rect k_Inside = Rect.MinMaxRect(0.07f, 0.05f, 0.83f, 0.9f);

        private Mall m_mall;
        private TableSet m_tables;
        private Func<WombatArea, Vector3> m_origin;
        private CoinPopup m_popupPrefab;
        private Transform m_marker;
        private SpriteRenderer m_arrow;
        private SpriteRenderer m_tag;
        private TextMeshPro m_label;
        private IDisposable m_rewarded;
        private Interactable m_pointed;
        private readonly List<SpriteRenderer> m_bodies = new List<SpriteRenderer>();

        // arrow: 아래를 가리키는 그림(피벗 = 화살 끝)
        public void Initialize(Mall mall, EventBus bus, TableSet tables, Func<WombatArea, Vector3> origin, CoinPopup popupPrefab, Sprite arrow, Sprite tag)
        {
            m_mall = mall;
            m_tables = tables;
            m_origin = origin;
            m_popupPrefab = popupPrefab;
            m_marker = new GameObject("Guide").transform;
            m_marker.SetParent(transform, false);
            m_arrow = new GameObject("Arrow").AddComponent<SpriteRenderer>();
            m_arrow.transform.SetParent(m_marker, false);
            m_arrow.sprite = arrow;
            m_arrow.sortingOrder = k_Order;
            m_tag = new GameObject("Tag").AddComponent<SpriteRenderer>();
            m_tag.transform.SetParent(transform, false);
            m_tag.sprite = tag;
            m_tag.drawMode = SpriteDrawMode.Sliced;
            m_tag.sortingOrder = k_Order;
            m_label = new GameObject("Label").AddComponent<TextMeshPro>();
            m_label.transform.SetParent(m_tag.transform, false);
            m_label.fontSize = 3.5f;
            m_label.alignment = TextAlignmentOptions.Center;
            m_label.color = new Color32(0x2E, 0x23, 0x20, 0xFF);
            m_label.sortingOrder = k_Order + 1;
            m_label.rectTransform.sizeDelta = new Vector2(4f, 1f);
            m_rewarded = bus.Subscribe<Events.GoalRewarded>(Bus_GoalRewarded);
            Hide();
        }

        private void OnDestroy()
        {
            m_rewarded?.Dispose();
        }

        private void LateUpdate()
        {
            Guide guide = m_mall?.Guide;

            if (guide == null || !guide.Active || guide.Pointed == null)
            {
                Hide();
                return;
            }

            System.Numerics.Vector2 at = guide.Pointed is IPlaced placed ? placed.Position : guide.Pointed.GuidePoint(m_mall.Wombat.Mover.Position) ?? m_mall.Wombat.Mover.Position;
            Vector3 foot = m_origin(m_mall.Active) + new Vector3(at.X, at.Y, 0f);

            if (guide.Pointed != m_pointed)
            {
                m_pointed = guide.Pointed;
                FindBodies(foot);
            }

            Vector3 target = foot + Vector3.up * Above(foot);
            Camera camera = Camera.main;
            Vector3 view = camera.WorldToViewportPoint(target);
            bool inside = k_Inside.Contains(view);
            Vector3 edge = inside ? target : camera.ViewportToWorldPoint(new Vector3(Mathf.Clamp(view.x, k_Inside.xMin, k_Inside.xMax), Mathf.Clamp(view.y, k_Inside.yMin, k_Inside.yMax), view.z));
            edge.z = 0f;
            // 화면 안이면 아래를 가리키고, 밖이면 화면 끝에서 대상 쪽을 가리킨다. 픽셀 그림이 깨지지 않게 90도 단위로(더 먼 축 쪽, 2026-10-09 아트방)
            Vector2 away = target - edge;
            Vector2 toward = inside ? Vector2.down : Mathf.Abs(away.x) > Mathf.Abs(away.y) ? new Vector2(Mathf.Sign(away.x), 0f) : new Vector2(0f, Mathf.Sign(away.y));
            float bob = Mathf.Sin(Time.time * k_BobSpeed) * k_Bob;
            m_arrow.enabled = true;
            m_marker.position = edge - (Vector3)(toward * bob);
            m_marker.rotation = Quaternion.Euler(0f, 0f, Vector2.SignedAngle(Vector2.down, toward));

            if (guide.Toward == null)
            {
                m_tag.enabled = false;
                m_label.enabled = false;
                return;
            }

            // 곳 이름표: 화살표 꼬리 뒤(화면 안이면 위, 화면 끝이면 안쪽). 꼬리에서 틈만큼 떼고, 이름표는 그 축의 반 길이만큼 더(2026-10-09 리뷰: 옆을 가리킬 때 꼬리에 겹쳤다)
            m_label.text = AreaName(guide.Toward);
            m_label.ForceMeshUpdate();
            Vector2 size = new Vector2(m_label.textBounds.size.x + 0.5f, 0.68f);
            m_tag.size = size;
            float half = toward.x != 0f ? size.x * 0.5f : size.y * 0.5f;
            m_tag.transform.position = m_marker.position - (Vector3)(toward * (m_arrow.sprite.bounds.size.y + k_TagGap + half));
            m_tag.enabled = true;
            m_label.enabled = true;
        }

        // 가리키는 사물의 몸체: 발끝이 그 점에 선 그림들(바닥에 깐 밭처럼 정렬이 음수인 그림은 뺀다)과, 그 그림의 부모(사물) 아래에서
        // 같은 열 위에 뜨는 표시(오븐 다 구움 · 연기 — 2026-10-09 화살표가 덮었다). 부모가 곳 · 월드면 사물이 아니라 표시를 찾지 않는다. 통로 · 물가 · 벽은 없다
        private void FindBodies(Vector3 foot)
        {
            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true).Where(r => r.sortingOrder >= 0 && r != m_arrow && r != m_tag).ToArray();
            List<SpriteRenderer> standing = renderers.Where(r => ((Vector2)(r.transform.position - foot)).sqrMagnitude < k_FootSnap * k_FootSnap).ToList();
            HashSet<Transform> owners = new HashSet<Transform>(standing.Select(r => r.transform.parent).Where(owner => owner != transform && owner.parent != transform));
            m_bodies.Clear();
            m_bodies.AddRange(standing);
            m_bodies.AddRange(renderers.Where(r => owners.Contains(r.transform.parent) && Mathf.Abs(r.transform.position.x - foot.x) < k_FootSnap && r.transform.position.y > foot.y));
        }

        // 화살표 끝 높이: 키 큰 몸체(석상 · 화덕)는 그 위끝 위, 아니면 정해진 높이
        private float Above(Vector3 foot)
        {
            float above = k_Above;

            foreach (SpriteRenderer body in m_bodies)
            {
                if (body != null && body.enabled && body.gameObject.activeInHierarchy)
                {
                    above = Mathf.Max(above, body.bounds.max.y - foot.y + k_TopGap);
                }
            }

            return above;
        }

        private void Hide()
        {
            m_arrow.enabled = false;
            m_tag.enabled = false;
            m_label.enabled = false;
        }

        // 곳 이름 띠와 같은 이름(농장은 층)
        private string AreaName(string id)
        {
            WombatArea area = m_mall.Areas.FirstOrDefault(a => a.Id == id);
            return area is FarmArea farm ? m_tables.Format("loc_farm", farm.Number) : m_tables.Text("loc_" + id);
        }

        private void Bus_GoalRewarded(Events.GoalRewarded e)
        {
            System.Numerics.Vector2 p = m_mall.Wombat.Mover.Position;
            Vector3 head = m_origin(m_mall.Active) + new Vector3(p.X, p.Y + k_PopupHeight, 0f);

            if (e.Coins > 0d)
            {
                Instantiate(m_popupPrefab, head, Quaternion.identity, transform).Show(m_tables.Format("coin_popup", e.Coins.ToString("0", CultureInfo.InvariantCulture)));
            }

            foreach (KeyValuePair<string, int> item in e.Items)
            {
                Sprite icon = Resources.Load<Sprite>(m_tables.Get<ItemTable>(item.Key).Icon);
                Instantiate(m_popupPrefab, head, Quaternion.identity, transform).Show(m_tables.Format("coin_popup", item.Value), icon);
            }
        }
    }
}
