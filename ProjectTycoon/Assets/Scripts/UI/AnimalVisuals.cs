using UnityEngine;
using ZooTycoon.Core;

namespace ZooTycoon.UI
{
    // 데이터-테이블-규칙 5장: 스프라이트는 animals.sprite 경로, 등급 색은 grades.colorHex
    public static class AnimalVisuals
    {
        public static Sprite SpriteOf(AnimalRecord animal)
        {
            return Resources.Load<Sprite>(animal.Sprite);
        }

        public static Color GradeColorOf(GameTables tables, AnimalRecord animal)
        {
            ColorUtility.TryParseHtmlString(tables.GetGrade(animal.Grade).ColorHex, out Color color);
            return color;
        }
    }
}
