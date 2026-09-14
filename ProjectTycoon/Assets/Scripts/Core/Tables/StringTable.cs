using System;
using System.Collections.Generic;
using System.Globalization;

namespace ZooTycoon.Core
{
    public sealed class StringTable
    {
        private readonly Dictionary<string, string> m_textsById =
            new Dictionary<string, string>(StringComparer.Ordinal);

        public StringTable(IReadOnlyList<StringRecord> rows)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                m_textsById[rows[i].Id] = rows[i].Ko;
            }
        }

        public string Get(string id)
        {
            if (!m_textsById.TryGetValue(id, out string text))
            {
                throw new KeyNotFoundException($"문구 키 '{id}'가 strings.json에 없다.");
            }

            return text;
        }

        public string Format(string id, params object[] args)
        {
            return string.Format(CultureInfo.InvariantCulture, Get(id), args);
        }
    }
}
