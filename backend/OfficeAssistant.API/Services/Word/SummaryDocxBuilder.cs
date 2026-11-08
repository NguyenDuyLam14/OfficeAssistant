using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using System.Text.RegularExpressions;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace OfficeAssistant.API.Services.Word;

public static class SummaryDocxBuilder
{
    public static void Build(string path, string title, string content)
    {
        using var doc = WordprocessingDocument.Create(
            path, WordprocessingDocumentType.Document);

        var main = doc.AddMainDocumentPart();
        main.Document = new W.Document(new W.Body());
        var body = main.Document.Body!;

        body.Append(Para(title.ToUpperInvariant(), bold: true, size: 32, center: true));
        body.Append(Para($"Ngày lập: {DateTime.Now:dd/MM/yyyy HH:mm}", italic: true, center: true));

        foreach (var raw in content.Replace("\r", "").Split('\n'))
        {
            var line = raw.TrimEnd();
            if (line.Length == 0) continue;

            // Dòng dạng "1. Tổng quan" hoặc "Tóm tắt nhanh:" được in đậm.
            var isHeading =
                Regex.IsMatch(line, @"^\d+\.\s+\S") && line.Length < 80
                || line.EndsWith(':') && line.Length < 60;

            body.Append(Para(line, bold: isHeading));
        }

        main.Document.Save();
    }

    private static W.Paragraph Para(
        string text, bool bold = false, bool italic = false,
        int size = 26, bool center = false)
    {
        // Thứ tự phần tử phải đúng schema: RunFonts, Bold, Italic, FontSize.
        var props = new W.RunProperties();
        props.Append(new W.RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman" });
        if (bold) props.Append(new W.Bold());
        if (italic) props.Append(new W.Italic());
        props.Append(new W.FontSize { Val = size.ToString() });

        var pPr = new W.ParagraphProperties(
            new W.SpacingBetweenLines { After = "120", Line = "300", LineRule = W.LineSpacingRuleValues.Auto });
        if (center) pPr.Append(new W.Justification { Val = W.JustificationValues.Center });

        return new W.Paragraph(
            pPr,
            new W.Run(props,
                new W.Text(text) { Space = SpaceProcessingModeValues.Preserve }));
    }
}