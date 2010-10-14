using System;
using System.Reflection;
using LitJson;
using System.Collections.Generic;
using System.Linq;
using Kayak.Core;

namespace Kayak.Framework
{
    public static partial class Extensions
    {
        public static void Concat<K, V>(this IDictionary<K, V> target, params IDictionary<K, V>[] srcs)
        {
            foreach (var dict in srcs.Where(s => s != null))
                foreach (var pair in dict)
                    target[pair.Key] = dict[pair.Key];
        }
    }
}
