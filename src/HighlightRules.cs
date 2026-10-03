using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;

namespace SerialScope
{
    // A user rule: lines containing a word (or matching a regular expression) get a colour
    internal sealed class HighlightRule
    {
        public string Pattern = "";
        public bool IsRegex;
        public bool MatchCase;
        public int Color;          // index into HighlightRule.Colors
        public bool Enabled = true;

        private Regex compiled;
        private string compiledFor;

        public static readonly string[] ColorNames = { "Red", "Orange", "Yellow", "Green", "Teal", "Blue", "Purple", "Pink", "Grey" };

        private static readonly Color[] DarkColors =
        {
            System.Drawing.Color.FromArgb(248, 113, 113), System.Drawing.Color.FromArgb(251, 146, 60), System.Drawing.Color.FromArgb(250, 204, 21),
            System.Drawing.Color.FromArgb(74, 222, 128), System.Drawing.Color.FromArgb(45, 212, 191), System.Drawing.Color.FromArgb(96, 165, 250),
            System.Drawing.Color.FromArgb(192, 132, 252), System.Drawing.Color.FromArgb(244, 114, 182), System.Drawing.Color.FromArgb(161, 161, 170)
        };

        private static readonly Color[] LightColors =
        {
            System.Drawing.Color.FromArgb(220, 38, 38), System.Drawing.Color.FromArgb(234, 88, 12), System.Drawing.Color.FromArgb(161, 98, 7),
            System.Drawing.Color.FromArgb(22, 163, 74), System.Drawing.Color.FromArgb(13, 148, 136), System.Drawing.Color.FromArgb(37, 99, 235),
            System.Drawing.Color.FromArgb(147, 51, 234), System.Drawing.Color.FromArgb(219, 39, 119), System.Drawing.Color.FromArgb(113, 113, 122)
        };

        public static Color ColorFor(int index, bool dark)
        {
            Color[] palette = dark ? DarkColors : LightColors;
            return palette[Math.Max(0, Math.Min(palette.Length - 1, index))];
        }

        // True if the pattern is usable (a regex that doesn't compile is reported here)
        public bool IsValid(out string error)
        {
            error = null;
            if (Pattern.Length == 0) { error = "Enter a word or pattern."; return false; }
            if (!IsRegex) return true;
            try { new Regex(Pattern); return true; }
            catch (ArgumentException ex) { error = ex.Message; return false; }
        }

        public bool Matches(string line)
        {
            if (!Enabled || Pattern.Length == 0) return false;
            if (!IsRegex)
                return line.IndexOf(Pattern, MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase) >= 0;

            string key = Pattern + (MatchCase ? "|c" : "|i");
            if (compiledFor != key)
            {
                try
                {
                    compiled = new Regex(Pattern, (MatchCase ? RegexOptions.None : RegexOptions.IgnoreCase) | RegexOptions.CultureInvariant,
                        TimeSpan.FromMilliseconds(50));
                }
                catch (ArgumentException) { compiled = null; }
                compiledFor = key;
            }
            if (compiled == null) return false;
            try { return compiled.IsMatch(line); }
            catch (RegexMatchTimeoutException) { return false; }
        }

        public HighlightRule Clone()
        {
            return new HighlightRule { Pattern = Pattern, IsRegex = IsRegex, MatchCase = MatchCase, Color = Color, Enabled = Enabled };
        }

        // ------------------------------------------------------------------ saving

        private const char RuleSeparator = '\u001E';
        private const char FieldSeparator = '\u001F';

        public static string Serialize(List<HighlightRule> rules)
        {
            var sb = new StringBuilder();
            foreach (HighlightRule r in rules)
            {
                if (sb.Length > 0) sb.Append(RuleSeparator);
                sb.Append(r.Enabled ? '1' : '0').Append(FieldSeparator)
                  .Append(r.IsRegex ? '1' : '0').Append(FieldSeparator)
                  .Append(r.MatchCase ? '1' : '0').Append(FieldSeparator)
                  .Append(r.Color).Append(FieldSeparator)
                  .Append(r.Pattern.Replace(RuleSeparator, ' ').Replace(FieldSeparator, ' '));
            }
            return sb.ToString();
        }

        public static List<HighlightRule> Parse(string text)
        {
            var rules = new List<HighlightRule>();
            if (string.IsNullOrEmpty(text)) return rules;
            foreach (string item in text.Split(RuleSeparator))
            {
                string[] f = item.Split(FieldSeparator);
                if (f.Length < 5 || f[4].Length == 0) continue;
                int color;
                int.TryParse(f[3], out color);
                rules.Add(new HighlightRule
                {
                    Enabled = f[0] == "1",
                    IsRegex = f[1] == "1",
                    MatchCase = f[2] == "1",
                    Color = Math.Max(0, Math.Min(ColorNames.Length - 1, color)),
                    Pattern = f[4]
                });
            }
            return rules;
        }
    }
}
