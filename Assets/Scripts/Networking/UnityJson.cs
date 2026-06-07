using System;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Theexonet.Networking
{
    internal static class UnityJson
    {
        private static readonly Regex NullTokenRegex = new(@":\s*null(?=\s*[,}])", RegexOptions.Compiled);

        public static T FromJson<T>(string json) where T : class
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                return JsonUtility.FromJson<T>(json);
            }
            catch (ArgumentException)
            {
                // JsonUtility cannot read explicit JSON nulls for string fields.
                return JsonUtility.FromJson<T>(NullTokenRegex.Replace(json, ": \"\""));
            }
        }
    }
}
