using System;
using System.Text;

namespace BuildAndBounce.Net
{
    /// <summary>
    /// Generates short, readable alphanumeric course share codes.
    /// Excludes easily confused characters (0/O, 1/I/L) for clean cross-platform sharing.
    /// </summary>
    public static class ShareCodeGenerator
    {
        private const string Alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";
        private static readonly Random Rnd = new Random();

        public static string Generate(int length = 6)
        {
            var sb = new StringBuilder(length);
            for (int i = 0; i < length; i++)
            {
                sb.Append(Alphabet[Rnd.Next(Alphabet.Length)]);
            }
            return sb.ToString();
        }

        public static string FormatCode(string rawCode)
        {
            if (string.IsNullOrEmpty(rawCode)) return "";
            string clean = rawCode.Trim().ToUpperInvariant();
            if (clean.Length == 6 && !clean.Contains("-"))
            {
                return $"{clean.Substring(0, 3)}-{clean.Substring(3, 3)}";
            }
            return clean;
        }

        public static string NormalizeCode(string formattedCode)
        {
            if (string.IsNullOrEmpty(formattedCode)) return "";
            return formattedCode.Replace("-", "").Trim().ToUpperInvariant();
        }
    }
}
