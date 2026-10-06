using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

internal static class SheetCsvTable
{
    public static List<Dictionary<string, string>> Read(string path, string key, params string[] columns)
    {
        var rows = CardSheetImporter.ReadCsv(File.ReadAllText(path));
        if (rows.Count == 0) throw new FormatException(path + ": 헤더가 없습니다.");
        var headers = rows[0].Select(s => s.Trim().TrimStart('\uFEFF')).ToArray();
        if (headers.Distinct().Count() != headers.Length || columns.Any(c => !headers.Contains(c))) throw new FormatException(path + ": 열 이름 누락 또는 중복");
        var output = new List<Dictionary<string, string>>();
        var keys = new HashSet<string>();
        foreach (var values in rows.Skip(1))
        {
            var row = headers.Select((h, i) => new { h, value = i < values.Count ? values[i].Trim() : "" }).ToDictionary(v => v.h, v => v.value);
            if (row[key].Length == 0) continue;
            if (!keys.Add(row[key])) throw new FormatException(path + ": 중복 " + key + " " + row[key]);
            output.Add(row);
        }
        return output;
    }
    public static int NonNegative(string value, string label)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number) || number < 0) throw new FormatException(label + ": 0 이상의 정수가 필요합니다.");
        return number;
    }
}
