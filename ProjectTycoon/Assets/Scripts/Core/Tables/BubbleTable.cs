using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 22 · 데이터-테이블-규칙 8.17: 이모지 말풍선(BubbleTable.json 행). 손님·점원·웜뱃 머리 위 한 자리에 하나. 그림은 bubble_sheet의 칸 번호
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class BubbleTable : Table<string>
    {
        public const string k_Wait = "wait";
        public const string k_Note = "note";
        public const string k_Question = "question";
        public const string k_Alert = "alert";
        public const string k_Heart = "heart";
        public const string k_Angry = "angry";
        public const string k_Chat = "chat";

        public int Frame { get; set; }
        public int Frames { get; set; }
        public double FrameSeconds { get; set; }
        // 보이는 시간(초). 0 = 상태형(다음 말풍선·지우기까지)
        public double Seconds { get; set; }
    }
}
