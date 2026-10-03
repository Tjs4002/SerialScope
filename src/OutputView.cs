using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace SerialScope
{
    internal enum LineKind { Normal, Error, Warning, Debug }

    // Recognises error / warning / debug lines from ESP-IDF, Arduino-ESP32 and general log output
    internal static class LogHighlighter
    {
        private static readonly Regex ErrorPattern = new Regex(
            @"^\s*E \(\d+\)|^\s*(\[\s*\d+\])?\[E\]|\b(error|exception|panic|fatal|assert(ion)? failed|guru meditation|abort\(\)|backtrace)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex WarningPattern = new Regex(
            @"^\s*W \(\d+\)|^\s*(\[\s*\d+\])?\[W\]|\b(warn(ing)?|fail(ed|ure)?|timed? ?out)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex DebugPattern = new Regex(
            @"^\s*[DV] \(\d+\)|^\s*(\[\s*\d+\])?\[[DV]\]",
            RegexOptions.Compiled);

        public static LineKind Classify(string line)
        {
            if (line.Length == 0) return LineKind.Normal;
            if (ErrorPattern.IsMatch(line)) return LineKind.Error;
            if (WarningPattern.IsMatch(line)) return LineKind.Warning;
            if (DebugPattern.IsMatch(line)) return LineKind.Debug;
            return LineKind.Normal;
        }
    }

    // Read-only rich text output with coloured segments, search highlighting and scroll-position-preserving appends
    internal sealed class OutputView : RichTextBox, IThemed
    {
        private const int WM_SETREDRAW = 0x000B;
        private const int EM_GETSCROLLPOS = 0x04DD;
        private const int EM_SETSCROLLPOS = 0x04DE;

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref Point lParam);

        public Color TextColor { get; private set; }
        public Color MutedColor { get; private set; }
        public Color ErrorColor { get; private set; }
        public Color WarningColor { get; private set; }
        private Color matchBack;
        private Theme theme;

        private int batchDepth;
        private bool followEnd;
        private Point savedScroll;
        private int savedSelStart, savedSelLength;
        private bool hasHighlights;

        public OutputView()
        {
            ReadOnly = true;
            BorderStyle = BorderStyle.None;
            WordWrap = false;
            ScrollBars = RichTextBoxScrollBars.Both;
            DetectUrls = false;
            HideSelection = false;
            MaxLength = int.MaxValue;
            ApplyTheme(Theme.Dark);
        }

        // Sets the colours used for new text. Existing text is recoloured by the owner redrawing it.
        public void ApplyTheme(Theme t)
        {
            theme = t;
            TextColor = t.OutputText;
            MutedColor = t.Muted;
            ErrorColor = t.IsDark ? Color.FromArgb(248, 113, 113) : Color.FromArgb(220, 38, 38);
            WarningColor = t.IsDark ? Color.FromArgb(251, 191, 36) : Color.FromArgb(180, 83, 9);
            matchBack = t.IsDark ? Color.FromArgb(113, 63, 18) : Color.FromArgb(254, 240, 138);
            BackColor = t.OutputBack;
            ForeColor = t.OutputText;
        }

        public bool IsDark
        {
            get { return theme == null || theme.IsDark; }
        }

        public Color ColorFor(LineKind kind)
        {
            switch (kind)
            {
                case LineKind.Error: return ErrorColor;
                case LineKind.Warning: return WarningColor;
                case LineKind.Debug: return MutedColor;
                default: return TextColor;
            }
        }

        // ------------------------------------------------------------------ appending

        // Wrap a group of appends: redraw is suspended, and the view either follows the end
        // or keeps the user's scroll position and selection exactly where they were
        public void BeginAppend(bool follow)
        {
            if (batchDepth++ > 0) return;
            followEnd = follow;
            SendMessage(Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
            savedSelStart = SelectionStart;
            savedSelLength = SelectionLength;
            SendMessage(Handle, EM_GETSCROLLPOS, IntPtr.Zero, ref savedScroll);
        }

        public void EndAppend()
        {
            if (--batchDepth > 0) return;
            if (followEnd)
            {
                Select(TextLength, 0);
            }
            else
            {
                Select(Math.Min(savedSelStart, TextLength), savedSelLength);
                SendMessage(Handle, EM_SETSCROLLPOS, IntPtr.Zero, ref savedScroll);
            }
            SendMessage(Handle, WM_SETREDRAW, (IntPtr)1, IntPtr.Zero);
            Invalidate();
            if (followEnd) ScrollToEnd();
        }

        public void ScrollToEnd()
        {
            Select(TextLength, 0);
            ScrollToCaret();
        }

        public void Append(string text, Color color)
        {
            if (text.Length == 0) return;
            Select(TextLength, 0);
            SelectionColor = color;
            if (hasHighlights) SelectionBackColor = BackColor;
            SelectedText = text;
        }

        public void ColorRange(int start, int length, Color color)
        {
            if (length <= 0) return;
            Select(start, length);
            SelectionColor = color;
        }

        // Removes the oldest whole lines once the text grows past maxChars; returns how many characters were removed
        public int Trim(int maxChars, int keepChars)
        {
            if (TextLength <= maxChars) return 0;
            int line = GetLineFromCharIndex(TextLength - keepChars);
            int cut = GetFirstCharIndexFromLine(line + 1);
            if (cut <= 0) cut = TextLength - keepChars;
            Select(0, cut);
            SelectedText = "";
            savedSelStart = Math.Max(0, savedSelStart - cut);
            return cut;
        }

        // ------------------------------------------------------------------ search

        // Highlights every match and returns their positions (at most maxMatches)
        public List<int> HighlightAll(string term, bool matchCase, int maxMatches)
        {
            ClearHighlights();
            var found = new List<int>();
            if (string.IsNullOrEmpty(term) || TextLength == 0) return found;

            var options = matchCase ? RichTextBoxFinds.MatchCase : RichTextBoxFinds.None;
            int start = 0;
            SendMessage(Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
            Point scroll = Point.Empty;
            SendMessage(Handle, EM_GETSCROLLPOS, IntPtr.Zero, ref scroll);
            while (start < TextLength && found.Count < maxMatches)
            {
                int at = Find(term, start, TextLength, options);
                if (at < 0) break;
                found.Add(at);
                SelectionBackColor = matchBack;
                start = at + term.Length;
            }
            hasHighlights = found.Count > 0;
            SendMessage(Handle, EM_SETSCROLLPOS, IntPtr.Zero, ref scroll);
            SendMessage(Handle, WM_SETREDRAW, (IntPtr)1, IntPtr.Zero);
            Invalidate();
            return found;
        }

        public void ClearHighlights()
        {
            if (!hasHighlights) return;
            int selStart = SelectionStart, selLength = SelectionLength;
            Point scroll = Point.Empty;
            SendMessage(Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
            SendMessage(Handle, EM_GETSCROLLPOS, IntPtr.Zero, ref scroll);
            SelectAll();
            SelectionBackColor = BackColor;
            Select(selStart, selLength);
            SendMessage(Handle, EM_SETSCROLLPOS, IntPtr.Zero, ref scroll);
            SendMessage(Handle, WM_SETREDRAW, (IntPtr)1, IntPtr.Zero);
            Invalidate();
            hasHighlights = false;
        }

        // Selects a match and scrolls it into view
        public void ShowMatch(int index, int length)
        {
            Select(index, length);
            ScrollToCaret();
        }

        public new void Clear()
        {
            base.Clear();
            hasHighlights = false;
        }
    }
}
