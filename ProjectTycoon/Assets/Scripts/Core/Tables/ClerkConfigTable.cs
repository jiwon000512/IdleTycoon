using System.Collections.Generic;
using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 협상 타이밍 바의 색. 표시가 멈춘 색이 유지·인하·인상 확률을 정한다
    public enum ZoneColor
    {
        Green,
        White,
        Red,
    }

    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class NegotiationZone
    {
        public ZoneColor Color { get; set; }
        // 바 가운데(0.5)에서 이 거리 안이면 이 색. 첫 맞는 것
        public double HalfWidth { get; set; }
        // 유지·인하·인상 확률 가중치
        public int Keep { get; set; }
        public int Down { get; set; }
        public int Up { get; set; }
    }

    // 설계 21 · 데이터-테이블-규칙 8.16: 점원 월급·후보·일머리·협상 숫자(ClerkConfigTable.json 한 행, Id main)
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class ClerkConfigTable : Table<string>
    {
        public const string k_Main = "main";

        public double WagePeriodSeconds { get; set; }
        public int CandidateCount { get; set; }
        public double RefreshCost { get; set; }
        // 일머리 = 1 + ⌊99 × r^SkillSkew⌋. 클수록 높은 일머리가 드물다
        public double SkillSkew { get; set; }
        public double WagePerSkill { get; set; }
        // 한 바퀴 끝날 때마다 확률 (100 − 일머리)/100로 딴짓(멍 때리기), 이 사이 균등 초
        public double IdleSecondsMin { get; set; }
        public double IdleSecondsMax { get; set; }
        // 표시 왕복 속도(바 폭/초), 일머리 1 → 100에 따라
        public double MarkerSpeedMin { get; set; }
        public double MarkerSpeedMax { get; set; }
        // 이 안에 탭하지 않으면 빨강
        public double NegotiateSeconds { get; set; }
        // 변동 금액 = 기본 월급 × 이 사이 균등 난수, 정수(최소 1)
        public double ChangeMin { get; set; }
        public double ChangeMax { get; set; }
        public List<NegotiationZone> Zones { get; set; }
        public List<string> Names { get; set; }
    }
}
