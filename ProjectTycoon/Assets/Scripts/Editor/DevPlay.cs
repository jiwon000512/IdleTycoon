using System.Linq;
using GameKit.UI;
using UnityEngine;
using ZooTycoon.Core;
using ZooTycoon.Game;
using ZooTycoon.UI;
using ZooTycoon.World;
using Vec2 = System.Numerics.Vector2;

namespace ZooTycoon.Editor
{
    // 검증 플레이 도구: 셸 unity-editor/scripts/dev.sh가 eval로 부른다(플레이 중에만). 게임 규칙은 건드리지 않고 치트 창 · Core API만 쓴다
    public static class DevPlay
    {
        // 캡처 크기(capture.sh와 같다)
        private const float k_ShotWidth = 1080f;
        private const float k_ShotHeight = 1920f;

        public static GameManager G => GameManager.Instance;
        public static Mall M => G.Mall;

        // play.sh가 부른다: 이번 플레이는 저장하지 않고, 돌아왔을 때 · 소식지 팝업을 닫는다
        public static string Begin()
        {
            G.PauseSave();
            UIManager.Instance.GetView<OfflineView>().Close();
            UIManager.Instance.GetView<EvaluationView>().Close();
            return "save off, popups closed";
        }

        // 치트 창 줄 이름 · 버튼 글로(예: Cheat("곳 이동", "횟집"))
        public static string Cheat(string name, string label)
        {
            return Console().Run(name, label);
        }

        public static string Cheats()
        {
            return Console().List();
        }

        // 지금 곳에 놓인 사물 id의 n번째 곁(일하는 자리, 없으면 손님 자리)에 웜뱃을 세운다
        public static string Near(string id, int n = 0)
        {
            IPlaced thing = Thing(id, n);

            if (thing == null)
            {
                return Missing(id, n);
            }

            bool worker = thing.Kind.Spots.Any(s => s.Role == SpotRole.Worker);
            M.Wombat.Mover.Place(Placement.SpotOf(thing, worker ? SpotRole.Worker : SpotRole.Customer));
            return Status();
        }

        // 사물 n번째 둘레를 캡처 그림 픽셀로: "x y w h"(왼쪽 위 기준). 카메라 세로 크기 기준으로 계산해 게임 뷰 비율과 무관하다
        public static string Rect(string id, int n = 0, float pad = 1.5f)
        {
            IPlaced thing = Thing(id, n);

            if (thing == null)
            {
                return Missing(id, n);
            }

            Camera camera = Camera.main;
            float perUnit = k_ShotHeight / (camera.orthographicSize * 2f);
            Vector3 eye = camera.transform.position;
            Vec2 at = WorldManager.Instance.OriginOf(M.Active) + thing.Position;
            float half = ((float)thing.Kind.HalfWidth + pad) * perUnit;
            float cx = k_ShotWidth / 2f + (at.X - eye.x) * perUnit;
            float cy = k_ShotHeight / 2f - (at.Y - eye.y) * perUnit;
            return Mathf.RoundToInt(cx - half) + " " + Mathf.RoundToInt(cy - half * 1.5f) + " " + Mathf.RoundToInt(half * 2f) + " " + Mathf.RoundToInt(half * 2f);
        }

        public static string Status()
        {
            Hands hands = M.Wombat.Worker.Hands;
            string hand = hands.Order != null ? hands.Order.Dish.Id + (hands.Order.Cut ? ":plate" : ":fish") : hands.Count > 0 ? hands.Bread.Id + "x" + hands.Count : "-";
            Vec2 at = M.Wombat.Mover.Position;
            return "t=" + Time.time.ToString("F1") + " x" + Time.timeScale + " area=" + M.Active.Id + " coins=" + G.State.Coins.ToString("F0") + " at=" + at.X.ToString("F1") + "," + at.Y.ToString("F1")
                + " hand=" + hand + " target=" + (M.Active.Target?.Table.Id ?? "-");
        }

        private static IPlaced Thing(string id, int n)
        {
            return M.Active.Things.OfType<IPlaced>().Where(t => ((Interactable)t).Table.Id == id).Skip(n).FirstOrDefault();
        }

        private static string Missing(string id, int n)
        {
            return "없는 사물: " + id + "#" + n + " (지금 곳 " + M.Active.Id + ": " + string.Join(", ", M.Active.Things.OfType<IPlaced>().Select(t => ((Interactable)t).Table.Id).Distinct()) + ")";
        }

        private static CheatConsole Console()
        {
            return Object.FindAnyObjectByType<CheatConsole>();
        }
    }
}
