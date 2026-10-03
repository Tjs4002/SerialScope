using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace SerialScope
{
    // Collects plotted readings while recording and writes them to a CSV file
    internal sealed class Recorder
    {
        public const int MaxRows = 1000000;

        private sealed class Row
        {
            public DateTime Time;
            public double[] Values;
        }

        private readonly List<string> columns = new List<string>();
        private readonly List<Row> rows = new List<Row>();
        private DateTime started;

        public bool IsRecording { get; private set; }

        public int RowCount
        {
            get { return rows.Count; }
        }

        public bool IsFull
        {
            get { return rows.Count >= MaxRows; }
        }

        public TimeSpan Elapsed
        {
            get { return IsRecording ? DateTime.Now - started : TimeSpan.Zero; }
        }

        public void Start()
        {
            columns.Clear();
            rows.Clear();
            started = DateTime.Now;
            IsRecording = true;
        }

        public void Stop()
        {
            IsRecording = false;
        }

        public void Discard()
        {
            IsRecording = false;
            columns.Clear();
            rows.Clear();
        }

        public void Add(List<string> names, List<double> values)
        {
            if (!IsRecording || IsFull) return;
            var row = new Row { Time = DateTime.Now, Values = new double[columns.Count + names.Count] };
            for (int i = 0; i < row.Values.Length; i++) row.Values[i] = double.NaN;

            for (int i = 0; i < names.Count; i++)
            {
                int col = columns.IndexOf(names[i]);
                if (col < 0)
                {
                    col = columns.Count;
                    columns.Add(names[i]);
                }
                row.Values[col] = values[i];
            }
            rows.Add(row);
        }

        // time, elapsed seconds, then one column per series; empty cells where a series had no value
        public void Save(string path)
        {
            var inv = CultureInfo.InvariantCulture;
            using (var writer = new StreamWriter(path, false, new UTF8Encoding(false)))
            {
                var header = new StringBuilder("time,elapsed_s");
                foreach (string c in columns) header.Append(',').Append(Escape(c));
                writer.WriteLine(header);

                var line = new StringBuilder();
                foreach (Row r in rows)
                {
                    line.Length = 0;
                    line.Append(r.Time.ToString("yyyy-MM-dd HH:mm:ss.fff", inv));
                    line.Append(',').Append((r.Time - started).TotalSeconds.ToString("0.000", inv));
                    for (int i = 0; i < columns.Count; i++)
                    {
                        line.Append(',');
                        if (i < r.Values.Length && !double.IsNaN(r.Values[i]))
                            line.Append(r.Values[i].ToString("R", inv));
                    }
                    writer.WriteLine(line);
                }
            }
        }

        private static string Escape(string field)
        {
            if (field.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return field;
            return "\"" + field.Replace("\"", "\"\"") + "\"";
        }
    }
}
