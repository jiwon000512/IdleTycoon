using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 40: 가게 입구 평가판(빵집은 입구 칠판 장식 앞 바닥, StarConfigTable boardX · boardY). 길을 막지 않고 편집 모드에서 집지 않는다.
    // 버튼(evaluate)이 평가 팝업을 연다(EvaluationBoardOpened). 부르기는 팝업에서
    public sealed class BoardInteractable : Interactable
    {
        public const string k_Id = "board";

        public Evaluation Evaluation { get; }
        public Vector2 Position { get; }

        public BoardInteractable(InteractableTable table, WombatArea area, Evaluation evaluation) : base(table, area)
        {
            Evaluation = evaluation;
            Position = new Vector2((float)evaluation.Config.BoardX, (float)evaluation.Config.BoardY);
        }

        public override float DistanceTo(Vector2 p)
        {
            return Vector2.Distance(p, Position);
        }

        public void Open()
        {
            Area.Bus.Publish(new Events.EvaluationBoardOpened(Evaluation));
        }
    }
}
