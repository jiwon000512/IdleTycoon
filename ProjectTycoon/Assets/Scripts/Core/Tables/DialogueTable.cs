using System.Collections.Generic;
using GameKit.Tables;

namespace ZooTycoon.Core
{
    public enum DialogueSpeaker
    {
        Clerk,
        Wombat,
        Visitor,
    }

    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class DialogueLineData
    {
        public DialogueSpeaker Speaker { get; set; }
        // StringTable id 목록. 하나를 균등 난수로
        public List<string> Texts { get; set; }
        // 같이 띄울 이모지(BubbleTable id). 없으면 null
        public string Bubble { get; set; }
    }

    // 설계 22 · 데이터-테이블-규칙 8.18: 대화(DialogueTable.json 행). 줄을 lineSeconds 간격으로 말하는 이의 머리 위에 차례로
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class DialogueTable : Table<string>
    {
        public const string k_ClerkWake = "clerk_wake";

        public double LineSeconds { get; set; }
        public List<DialogueLineData> Lines { get; set; }
    }
}
