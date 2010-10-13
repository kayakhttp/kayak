using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using System.IO;
using Kayak.Core;

namespace Kayak
{
    public static partial class Extensions
    {
        public static string GetString(this IEnumerable<ArraySegment<byte>> buffers)
        {
            return buffers.GetString(Encoding.UTF8);
        }

        public static string GetString(this IEnumerable<ArraySegment<byte>> buffers, Encoding encoding)
        {
            var sb = new StringBuilder(buffers.Sum(s => s.Count));

            foreach (var b in buffers)
                sb.Append(encoding.GetString(b.Array, b.Offset, b.Count));

            return sb.ToString();
        }

        public static string GetString(this ArraySegment<byte> b)
        {
            return b.GetString(Encoding.UTF8);
        }

        public static string GetString(this ArraySegment<byte> b, Encoding encoding)
        {
            return encoding.GetString(b.Array, b.Offset, b.Count);
        }

        static IEnumerable<byte> GetBytes(this IEnumerable<ArraySegment<byte>> buffers)
        {
            foreach (var seg in buffers)
                for (int i = seg.Offset; i < seg.Offset + seg.Count; i++)
                    yield return seg.Array[i];
        }
    }
}
