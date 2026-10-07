using System.Text;

namespace SpeciesNetPostprocessor.Core.Taxonomy;

/// <summary>
/// A minimal CSV reader that mimics the default behavior of pandas.read_csv() followed by
/// fillna(''): the first row is the header, blank lines are skipped, fields may be quoted,
/// and empty fields (or pandas' default NA strings) become empty strings.
/// </summary>
public sealed class CsvTable
{
    // pandas' default NA values
    private static readonly HashSet<string> NaValues = new(StringComparer.Ordinal)
    {
        "", "#N/A", "#N/A N/A", "#NA", "-1.#IND", "-1.#QNAN", "-NaN", "-nan", "1.#IND", "1.#QNAN",
        "<NA>", "N/A", "NA", "NULL", "NaN", "None", "n/a", "nan", "null"
    };

    public List<string> Columns { get; } = new();
    public List<Dictionary<string, string>> Rows { get; } = new();

    public static CsvTable Parse(string text, string sourceName)
    {
        // Remove a byte order mark if present
        if (text.Length > 0 && text[0] == '﻿')
        {
            text = text.Substring(1);
        }

        var records = ParseRecords(text);
        var table = new CsvTable();
        if (records.Count == 0)
        {
            throw new PostprocessingException($"Taxonomy list {sourceName} is empty");
        }

        table.Columns.AddRange(records[0]);

        for (int iRecord = 1; iRecord < records.Count; iRecord++)
        {
            List<string> record = records[iRecord];
            if (record.Count > table.Columns.Count)
            {
                throw new PostprocessingException(
                    $"Error reading {sourceName}: line {iRecord + 1} has {record.Count} fields, " +
                    $"but the header has {table.Columns.Count}");
            }
            var row = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int iColumn = 0; iColumn < table.Columns.Count; iColumn++)
            {
                string value = iColumn < record.Count ? record[iColumn] : string.Empty;
                if (NaValues.Contains(value))
                {
                    value = string.Empty;
                }
                row[table.Columns[iColumn]] = value.Trim();
            }
            table.Rows.Add(row);
        }

        return table;
    }

    /// <summary>
    /// Splits [text] into records, handling quoted fields (which may contain commas, newlines,
    /// and doubled quotes).  Blank lines are skipped.
    /// </summary>
    private static List<List<string>> ParseRecords(string text)
    {
        var records = new List<List<string>>();
        var record = new List<string>();
        var field = new StringBuilder();
        bool inQuotes = false;
        bool recordHasContent = false;

        void EndField()
        {
            record.Add(field.ToString());
            field.Clear();
        }

        void EndRecord()
        {
            EndField();
            if (recordHasContent)
            {
                records.Add(record);
            }
            record = new List<string>();
            recordHasContent = false;
        }

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(c);
                }
                continue;
            }

            switch (c)
            {
                case '"':
                    inQuotes = true;
                    recordHasContent = true;
                    break;
                case ',':
                    EndField();
                    recordHasContent = true;
                    break;
                case '\r':
                    if (i + 1 < text.Length && text[i + 1] == '\n')
                    {
                        i++;
                    }
                    EndRecord();
                    break;
                case '\n':
                    EndRecord();
                    break;
                default:
                    field.Append(c);
                    if (!char.IsWhiteSpace(c))
                    {
                        recordHasContent = true;
                    }
                    break;
            }
        }

        if (field.Length > 0 || record.Count > 0 || recordHasContent)
        {
            EndRecord();
        }

        return records;
    }
}
