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
    }
}
