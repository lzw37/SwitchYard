using System.Globalization;
using System.IO.Compression;
using ClosedXML.Excel;
using SwitchYard.Service.Models;

namespace SwitchYard.Service.Services;

/// <summary>Reads all three characteristic tables before returning any replacement data.</summary>
public static class TrainCharacteristicsExcel
{
    public const int MaxFileBytes = 2 * 1024 * 1024;

    public static TrainCharacteristicsImport Read(Stream stream, CancellationToken cancellationToken = default)
    {
        XLWorkbook workbook;
        try
        {
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true))
            {
                if (archive.Entries.Count > 256 || archive.Entries.Sum(entry => entry.Length) > 16 * 1024 * 1024)
                    throw new InvalidDataException();
            }
            stream.Position = 0;
            workbook = new XLWorkbook(stream);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new TrainCharacteristicsExcelException("无法读取 Excel 文件，请使用下载的 .xlsx 模板，并确认文件未损坏或加密。");
        }

        using (workbook)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (workbook.Worksheets.Count != 3)
                throw new TrainCharacteristicsExcelException("工作簿须包含且仅包含“牵引特性”“制动特性”“阻力特性”三个 Sheet。");
            return new TrainCharacteristicsImport(
                ReadCurve(workbook, "牵引特性", "牵引力 (kN)", true, cancellationToken),
                ReadCurve(workbook, "制动特性", "制动力 (kN)", false, cancellationToken),
                ReadCurve(workbook, "阻力特性", "基本阻力 (kN)", false, cancellationToken));
        }
    }

    private static IReadOnlyList<SpeedProfileForceInput> ReadCurve(
        XLWorkbook workbook, string sheetName, string forceHeader, bool traction, CancellationToken cancellationToken)
    {
        if (!workbook.Worksheets.TryGetWorksheet(sheetName, out var sheet))
            throw new TrainCharacteristicsExcelException($"缺少 Sheet“{sheetName}”，请保留模板中的 Sheet 名称。");
        if (sheet.Cell(1, 1).HasFormula || sheet.Cell(1, 2).HasFormula ||
            NormalizeHeader(sheet.Cell(1, 1).GetString()) != NormalizeHeader("速度 (km/h)") ||
            NormalizeHeader(sheet.Cell(1, 2).GetString()) != NormalizeHeader(forceHeader))
            throw new TrainCharacteristicsExcelException($"Sheet“{sheetName}”第 1 行表头须为“速度 (km/h)”和“{forceHeader}”，请使用模板表头。");

        var points = new List<SpeedProfileForceInput>();
        foreach (var row in sheet.RowsUsed(XLCellsUsedOptions.Contents))
        {
            if (row.RowNumber() == 1) continue;
            cancellationToken.ThrowIfCancellationRequested();
            var speedCell = row.Cell(1);
            var forceCell = row.Cell(2);
            if (IsBlank(speedCell) && IsBlank(forceCell)) continue;
            var location = $"Sheet“{sheetName}”第 {row.RowNumber()} 行";
            if (points.Count >= 128)
                throw new TrainCharacteristicsExcelException($"{location}：每个特性表最多允许 128 个数据点。");
            var speed = ReadNumber(speedCell, location, "速度", 10000);
            var force = ReadNumber(forceCell, location, "力", 10000000);
            if ((points.Count == 0 && speed != 0) || (points.Count > 0 && speed <= points[^1].SpeedKmh))
                throw new TrainCharacteristicsExcelException($"{location}：首速度须为 0，后续速度须严格递增，不可重复。");
            if (traction && points.Count > 0 && force > points[^1].ForceKn)
                throw new TrainCharacteristicsExcelException($"{location}：牵引力须随速度保持不变或减小，不可增大。");
            points.Add(new SpeedProfileForceInput { SpeedKmh = speed, ForceKn = force });
        }
        if (points.Count == 0)
            throw new TrainCharacteristicsExcelException($"Sheet“{sheetName}”没有数据，请从第 2 行开始填写 1–128 个速度、力数据点。");
        return points;
    }

    private static string NormalizeHeader(string value) =>
        string.Concat(value.Replace('（', '(').Replace('）', ')').Where(character => !char.IsWhiteSpace(character))).ToLowerInvariant();

    private static bool IsBlank(IXLCell cell) => !cell.HasFormula &&
        (cell.IsEmpty(XLCellsUsedOptions.Contents) || (cell.DataType == XLDataType.Text && string.IsNullOrWhiteSpace(cell.GetString())));

    private static double ReadNumber(IXLCell cell, string location, string field, double maximum)
    {
        if (cell.HasFormula)
            throw new TrainCharacteristicsExcelException($"{location}：{field}须填写数值，请将公式结果粘贴为数值后导入。");
        double value = double.NaN;
        var valid = cell.DataType == XLDataType.Number
            ? cell.TryGetValue(out value)
            : cell.DataType == XLDataType.Text && double.TryParse(cell.GetString().Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        if (!valid || !double.IsFinite(value) || value < 0 || value > maximum)
            throw new TrainCharacteristicsExcelException($"{location}：{field}须为 0–{maximum} 的数值，不可留空。");
        return value;
    }
}

public sealed class TrainCharacteristicsExcelException(string message) : Exception(message);
