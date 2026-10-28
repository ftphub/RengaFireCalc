using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security;
using System.Text;
using System.Xml;
using FireCalc.Core;

namespace FireCalc.Plugin;

/// <summary>
/// Minimal standalone OOXML XLSX export (three worksheets, no Excel/Office dependency).
/// Contains values, not linked macros or workbook recalculation.
/// </summary>
internal static class ExcelExporter
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static string Esc(string text) => SecurityElement.Escape(text) ?? "";

    public static void Write(string filePath, string scenario, ReserveInputs input,
                             IReadOnlyList<MainWindow.MetricRow> metrics, IReadOnlyList<PipeSegment> pipes)
    {
        var inputs = new List<object?[]> {
            new object?[] {"Сценарий",scenario},
            new object?[] {"Источник расчёта","ЭФЭР: Расчет запаса воды и ПО.xlsx (формулы)"},
            new object?[] {"Время тушения, мин",input.SuppressionMinutes},
            new object?[] {"Расход раствора, л/с",input.SolutionFlowLitresPerSecond},
            new object?[] {"Концентрация, %",input.ConcentrationPercent},
            new object?[] {"Время охлаждения, мин",input.CoolingMinutes},
            new object?[] {"Расход охлаждения, л/с",input.CoolingFlowLitresPerSecond},
            new object?[] {"Вместимость Wтр, м3 (ручное)",input.FoamPipeCapacityCubicMetres},
            new object?[] {"Вместимость сухотруба, м3",input.DryPipeCapacityCubicMetres},
            new object?[] {"Объем бака, м3",input.DoserTankCapacityCubicMetres},
            new object?[] {"Предупреждение","Предварительный расчёт. Не является утверждённой проектной документацией."}};
        var outputs = new List<object?[]> {new object?[]{"Показатель","Значение","Единицы"}};
        outputs.AddRange(metrics.Select(r=>new object?[]{r.Name,
            double.TryParse(r.Value, NumberStyles.Float, CultureInfo.GetCultureInfo("ru-RU"), out var numeric) ? numeric : r.Value, r.Units}));
        var pipeRows = new List<object?[]> {new object?[]{"ID Renga","Имя","Длина, м","Внутренний диаметр, мм","Вместимость, м3","Состояние","Источник Dвн"}};
        pipeRows.AddRange(pipes.Select(p=>new object?[]{p.RengaObjectId,p.Name,p.LengthM,p.InnerDiameterMm,p.InternalVolumeM3,p.State,p.DiameterSource}));
        using var zip = new ZipArchive(new FileStream(filePath, FileMode.Create, FileAccess.Write), ZipArchiveMode.Create);
        WriteEntry(zip,"[Content_Types].xml","<?xml version=\"1.0\" encoding=\"utf-8\"?>"+
            "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">"+
            "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>"+
            "<Default Extension=\"xml\" ContentType=\"application/xml\"/>"+
            "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>"+
            "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>"+
            "<Override PartName=\"/xl/worksheets/sheet2.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>"+
            "<Override PartName=\"/xl/worksheets/sheet3.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>"+
            "</Types>");
        WriteEntry(zip,"_rels/.rels","<?xml version=\"1.0\" encoding=\"utf-8\"?>"+
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"+
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>"+
            "</Relationships>");
        WriteEntry(zip,"xl/workbook.xml","<?xml version=\"1.0\" encoding=\"utf-8\"?>"+
            "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">"+
            "<sheets><sheet name=\"Результаты\" sheetId=\"1\" r:id=\"rId1\"/>"+
            "<sheet name=\"Исходные данные\" sheetId=\"2\" r:id=\"rId2\"/>"+
            "<sheet name=\"Трубы Renga\" sheetId=\"3\" r:id=\"rId3\"/></sheets></workbook>");
        WriteEntry(zip,"xl/_rels/workbook.xml.rels","<?xml version=\"1.0\" encoding=\"utf-8\"?>"+
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"+
            string.Concat(Enumerable.Range(1,3).Select(n=>$"<Relationship Id=\"rId{n}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet{n}.xml\"/>")) +
            "</Relationships>");
        WriteEntry(zip,"xl/worksheets/sheet1.xml",SheetXml(outputs));
        WriteEntry(zip,"xl/worksheets/sheet2.xml",SheetXml(inputs));
        WriteEntry(zip,"xl/worksheets/sheet3.xml",SheetXml(pipeRows));
    }

    // Independent 2-sheet xlsx report, no Microsoft Excel installation or remote services.
    public static void WriteHydraulic(string filePath, string scenario, HydraulicRouteResult result)
    {
        var rows = new List<object?[]> {new object?[] {"Расчётный путь", "Участок", "Длина, м", "Q, л/с", "Кт", "Dвн, мм", "v, м/с", "Линейные потери, МПа", "Линейные потери, м"}};
        int i = 0;
        foreach (var s in result.Sections)
            rows.Add(new object?[] {scenario, ++i + ". " + s.Name, s.LengthM, s.FlowLitresPerSecond,
                s.ResistanceKt, s.InnerDiameterMm, s.VelocityMs,
                s.LinearLossMpa, s.LinearLossMeters});
        var summary = new List<object?[]> {
            new object?[] {"Показатель", "Значение", "Ед."},
            new object?[] {"Путь", scenario, ""},
            new object?[] {"Участков", result.Sections.Count, "шт."},
            new object?[] {"Длина трассы", result.LengthMeters, "м"},
            new object?[] {"Линейные потери", result.LinearLossMpa, "МПа"},
            new object?[] {"Линейные потери", result.LinearLossMeters, "м"},
            new object?[] {"Местные потери", result.LocalLossMeters, "м"},
            new object?[] {"Потери оборудования", result.EquipmentLossMeters, "м"},
            new object?[] {"Потери по трассе", result.TotalCircuitLossMeters, "м"},
            new object?[] {"Напор у потребителя", result.RequiredConsumerHeadMeters, "м"},
            new object?[] {"Перепад отметок", result.ElevationDifferenceMeters, "м"},
            new object?[] {"Требуемый напор на входе", result.RequiredSourceHeadMeters, "м"},
            new object?[] {"ОГРАНИЧЕНИЕ", "Выбор диктующей трассы и Q/Kт выполнен вручную. Не проверено гидравлическое распределение по сети.", ""}
        };
        using var zip = new ZipArchive(new FileStream(filePath, FileMode.Create, FileAccess.Write), ZipArchiveMode.Create);
        WriteEntry(zip, "[Content_Types].xml", "<?xml version=\"1.0\" encoding=\"utf-8\"?>"+
            "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">"+
            "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>"+
            "<Default Extension=\"xml\" ContentType=\"application/xml\"/>"+
            "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>"+
            "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>"+
            "<Override PartName=\"/xl/worksheets/sheet2.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>"+
            "</Types>");
        WriteEntry(zip, "_rels/.rels", "<?xml version=\"1.0\" encoding=\"utf-8\"?>"+
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"+
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>"+
            "</Relationships>");
        WriteEntry(zip, "xl/workbook.xml", "<?xml version=\"1.0\" encoding=\"utf-8\"?>"+
            "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">"+
            "<sheets><sheet name=\"Гидравлика по участкам\" sheetId=\"1\" r:id=\"rId1\"/>"+
            "<sheet name=\"Итоги\" sheetId=\"2\" r:id=\"rId2\"/></sheets></workbook>");
        WriteEntry(zip, "xl/_rels/workbook.xml.rels", "<?xml version=\"1.0\" encoding=\"utf-8\"?>"+
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"+
            string.Concat(Enumerable.Range(1,2).Select(n=>$"<Relationship Id=\"rId{n}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet{n}.xml\"/>"))+
            "</Relationships>");
        WriteEntry(zip, "xl/worksheets/sheet1.xml", SheetXml(rows));
        WriteEntry(zip, "xl/worksheets/sheet2.xml", SheetXml(summary));
    }

    private static void WriteEntry(ZipArchive zip,string path,string xml)
    {
        using var writer=new StreamWriter(zip.CreateEntry(path,CompressionLevel.Optimal).Open(),new UTF8Encoding(false));
        writer.Write(xml);
    }

    private static string ColumnName(int c)
    {
        var r="";
        do { r=(char)('A'+ c%26)+r; c=c/26-1; } while (c>=0);
        return r;
    }

    private static string SheetXml(IEnumerable<object?[]> rows)
    {
        var b = new StringBuilder("<?xml version=\"1.0\" encoding=\"utf-8\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");
        int rowNum=0;
        foreach(var row in rows)
        {
            rowNum++;b.Append($"<row r=\"{rowNum}\">");
            for(int j=0;j<row.Length;j++)
            {
                object? value=row[j];if(value is null) continue;
                string cell=$"{ColumnName(j)}{rowNum}";
                if(value is int or long or double or decimal or float)
                    b.Append($"<c r=\"{cell}\"><v>{Convert.ToString(value,Inv)}</v></c>");
                else
                    b.Append($"<c r=\"{cell}\" t=\"inlineStr\"><is><t>{Esc(value.ToString()??"")}</t></is></c>");
            }
            b.Append("</row>");
        }
        return b.Append("</sheetData></worksheet>").ToString();
    }
}
