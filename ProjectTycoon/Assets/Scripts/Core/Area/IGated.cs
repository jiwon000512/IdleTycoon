namespace ZooTycoon.Core
{
    // 설계 47 · 설계 58: 광장 문이 닫힌 채 시작해 문 앞 시트(open_shop)에서 값을 내고 여는 곳(횟집 문 · 낚시터 젖은 벽). 열릴 때까지 문 통로는 막힌다(PlazaArea.AddGate)
    public interface IGated
    {
        string Id { get; }
        bool GateOpen { get; }
        double GateCost { get; }
        // 빵집 별이 이만큼 있어야 연다(0이면 조건 없음) · 아직 모자란가
        int GateStar { get; }
        bool GateLocked { get; }
        bool TryOpenGate(Worker worker);
    }
}
