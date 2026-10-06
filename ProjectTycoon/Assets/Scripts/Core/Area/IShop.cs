namespace ZooTycoon.Core
{
    // 설계 47: 광장 손님이 들어가는 가게(빵집 · 횟집). 광장은 연 가게를 번갈아 손님을 보낸다(가게마다 손님 흐름이 따로)
    public interface IShop
    {
        string Id { get; }
        bool IsOpen { get; }
        bool CanAdmit { get; }
        // 광장 문에서 톡 들어온 손님이 구멍에서 나온다(자리 확인은 CanAdmit으로 부르는 쪽이)
        void Admit(VisitorTable look);
    }
}
