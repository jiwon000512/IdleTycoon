using System.Collections.Generic;
using System.Globalization;
using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 화면 문구(StringTable) 조회
    public static class Texts
    {
        public static string Text(this TableSet tables, string id)
        {
            return tables.Get<StringTable>(id).Ko;
        }

        // {0} 서식. 숫자 서식은 호출부가 만든다
        public static string Format(this TableSet tables, string id, params object[] args)
        {
            return string.Format(CultureInfo.InvariantCulture, tables.Text(id), args);
        }

        // 화면에 나올 수 있는 글자: 출력 가능한 ASCII(숫자·K/M/B 표기·기호) + 말줄임표 + 문구·빵·업그레이드 이름. 폰트 아틀라스를 이 글자로 굽는다(FontBaker)
        public static string Characters(TableSet tables)
        {
            SortedSet<char> chars = new SortedSet<char>();

            for (char c = ' '; c <= '~'; c++)
            {
                chars.Add(c);
            }

            // TMP가 넘치는 글을 줄일 때 쓰는 말줄임표(없으면 기본 폰트에서 찾는다)
            chars.Add('…');

            foreach (StringTable row in tables.GetAll<StringTable>())
            {
                Add(chars, row.Ko);
            }

            foreach (BreadTable row in tables.GetAll<BreadTable>())
            {
                Add(chars, row.Name);
            }

            foreach (InteractableTable row in tables.GetAll<InteractableTable>())
            {
                if (row.Upgrade != null)
                {
                    Add(chars, row.Upgrade.Name);
                    Add(chars, row.Upgrade.EffectFormat);
                }
            }

            return string.Concat(chars);
        }

        private static void Add(SortedSet<char> chars, string text)
        {
            foreach (char c in text)
            {
                if (!char.IsControl(c))
                {
                    chars.Add(c);
                }
            }
        }
    }
}
