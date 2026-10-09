using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 31 · 34: 광장 떠돌이 행상(너구리)에게 말 걸기. 행상이 좌판 자리에 서 있는 동안만 웜뱃이 닿고(거리 = 행상까지),
    // 행동 relic이면 행상이 웜뱃을 보고 인사 한 줄(DialogueTable merchant_hello)을 말한 뒤 뽑기 팝업이 뜬다(MerchantTalked).
    // 뽑기는 광장 난수로 지갑의 유물(Relics)에서 후보를 뽑는다. 수레는 없다(2026-10-01 사용자)
    public sealed class MerchantInteractable : Interactable
    {
        public const string k_Id = "merchant";

        private readonly IRandom m_random;
        private readonly DialogueTable m_hello;
        private readonly Vector2 m_stall;

        public Relics Relics => Area.Wombat.Worker.Wallet.Relics;
        public RelicMerchant Merchant { get; }
        public bool IsOpen => Merchant.IsOpen;

        public MerchantInteractable(InteractableTable table, PlazaArea plaza, IRandom random, RelicMerchant merchant) : base(table, plaza)
        {
            m_random = random;
            Merchant = merchant;
            m_hello = plaza.Tables.Get<DialogueTable>(DialogueTable.k_MerchantHello);
            PlazaConfigTable config = plaza.Tables.Get<PlazaConfigTable>(PlazaConfigTable.k_Main);
            m_stall = new Vector2((float)config.MerchantX, (float)config.MerchantY);
        }

        public override float DistanceTo(Vector2 p)
        {
            return IsOpen ? Vector2.Distance(p, Merchant.Figure.Position) : float.MaxValue;
        }

        // 행상이 없으면 좌판 자리(올 곳)로 데려간다(2026-10-09 리뷰: 화살표가 웜뱃 머리 위에 떴다)
        public override Vector2? GuidePoint(Vector2 from)
        {
            return IsOpen ? Merchant.Figure.Position : m_stall;
        }

        // 행상이 웜뱃 쪽을 보고 인사 한 줄을 고른다
        public void Talk()
        {
            PlazaVisitor figure = Merchant.Figure;
            figure.Face(Area.Wombat.Mover.Position);
            DialogueLineData line = m_hello.Lines[0];
            string text = line.Texts[(int)(m_random.NextDouble() * line.Texts.Count) % line.Texts.Count];
            Area.Bus.Publish(new Events.MerchantTalked(this, text, m_hello.LineSeconds));
        }

        public bool TryDraw()
        {
            return Relics.TryDraw(m_random);
        }
    }
}
