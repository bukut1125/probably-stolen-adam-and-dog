using System;
using System.Collections.Generic;

namespace DogCustomer
{
    internal static class PortraitBinding
    {
        internal static bool TryBind<T>(IDictionary<string, T> dictionary, string key, T portrait) where T : class
        {
            if (dictionary == null || key == null || portrait == null) return false;
            if (dictionary.TryGetValue(key, out var existing)) return ReferenceEquals(existing, portrait);
            dictionary.Add(key, portrait);
            return true;
        }
    }
}
