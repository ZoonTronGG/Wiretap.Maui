using System.Globalization;
using System.Text;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Fonts;
using Wiretap.Maui.Core;
using Color = MigraDoc.DocumentObjectModel.Color;

namespace Wiretap.Maui.Services;

/// <summary>
/// Exports HTTP records as PDF documents for formal documentation.
/// </summary>
public static class PdfExporter
{
    // Color scheme
    private static readonly Color PrimaryColor = new(0, 122, 204);      // Blue
    private static readonly Color SuccessColor = new(40, 167, 69);      // Green
    private static readonly Color ErrorColor = new(220, 53, 69);        // Red
    private static readonly Color WarningColor = new(255, 193, 7);      // Yellow/Orange
    private static readonly Color HeaderBgColor = new(248, 249, 250);   // Light gray
    private static readonly Color BorderColor = new(222, 226, 230);     // Border gray

    private static bool _fontResolverInitialized;
    private static readonly object _initLock = new();

    /// <summary>
    /// Generates a PDF document from an HTTP record.
    /// </summary>
    /// <param name="record">The HTTP record to export.</param>
    /// <returns>PDF document as a byte array.</returns>
    public static byte[] ToPdf(HttpRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        EnsureFontResolverInitialized();

        var document = CreateDocument(record);
        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();

        using var stream = new MemoryStream();
        renderer.PdfDocument.Save(stream, false);
        return stream.ToArray();
    }

    /// <summary>
    /// Generates a PDF document and saves it to a file.
    /// </summary>
    /// <param name="record">The HTTP record to export.</param>
    /// <param name="filePath">The path to save the PDF file.</param>
    public static void ToPdfFile(HttpRecord record, string filePath)
    {
        var pdfBytes = ToPdf(record);
        File.WriteAllBytes(filePath, pdfBytes);
    }

    /// <summary>
    /// Generates a PDF document and saves it to a stream.
    /// </summary>
    /// <param name="record">The HTTP record to export.</param>
    /// <param name="stream">The stream to write the PDF to.</param>
    public static void ToPdfStream(HttpRecord record, Stream stream)
    {
        var pdfBytes = ToPdf(record);
        stream.Write(pdfBytes, 0, pdfBytes.Length);
    }

    /// <summary>
    /// Generates a suggested filename for the PDF export.
    /// </summary>
    /// <param name="record">The HTTP record.</param>
    /// <returns>A filename with timestamp and method.</returns>
    public static string GenerateFileName(HttpRecord record)
    {
        var timestamp = record.Timestamp.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture);
        var method = record.Method.ToUpperInvariant();
        return $"wiretap_{method}_{timestamp}.pdf";
    }

    private static Document CreateDocument(HttpRecord record)
    {
        var document = new Document();
        document.Info.Title = $"HTTP Request Report - {record.Method} {record.DisplayUrl}";
        document.Info.Author = "Wiretap.Maui";
        document.Info.Subject = "HTTP Traffic Report";

        DefineStyles(document);

        var section = document.AddSection();
        section.PageSetup.TopMargin = Unit.FromCentimeter(2);
        section.PageSetup.BottomMargin = Unit.FromCentimeter(2);
        section.PageSetup.LeftMargin = Unit.FromCentimeter(2);
        section.PageSetup.RightMargin = Unit.FromCentimeter(2);

        AddHeader(section, record);
        AddSummary(section, record);
        AddRequestSection(section, record);
        AddResponseSection(section, record);
        AddFooter(section);

        return document;
    }

    private static void DefineStyles(Document document)
    {
        // Normal style - use Helvetica (PDF Base-14 font, no font files needed)
        var style = document.Styles["Normal"]!;
        style.Font.Name = "Helvetica";
        style.Font.Size = 10;
        style.ParagraphFormat.SpaceAfter = 6;

        // Title style
        style = document.Styles.AddStyle("Title", "Normal");
        style.Font.Size = 18;
        style.Font.Bold = true;
        style.Font.Color = PrimaryColor;
        style.ParagraphFormat.SpaceAfter = 12;

        // Heading1 style
        style = document.Styles.AddStyle("Heading1", "Normal");
        style.Font.Size = 14;
        style.Font.Bold = true;
        style.Font.Color = PrimaryColor;
        style.ParagraphFormat.SpaceBefore = 18;
        style.ParagraphFormat.SpaceAfter = 8;
        style.ParagraphFormat.Borders.Bottom.Width = 1;
        style.ParagraphFormat.Borders.Bottom.Color = PrimaryColor;

        // Heading2 style
        style = document.Styles.AddStyle("Heading2", "Normal");
        style.Font.Size = 12;
        style.Font.Bold = true;
        style.ParagraphFormat.SpaceBefore = 12;
        style.ParagraphFormat.SpaceAfter = 6;

        // Code style (for bodies) - use Courier (PDF Base-14 font)
        style = document.Styles.AddStyle("Code", "Normal");
        style.Font.Name = "Courier";
        style.Font.Size = 9;
        style.ParagraphFormat.SpaceAfter = 0;

        // Status success
        style = document.Styles.AddStyle("StatusSuccess", "Normal");
        style.Font.Bold = true;
        style.Font.Color = SuccessColor;

        // Status error
        style = document.Styles.AddStyle("StatusError", "Normal");
        style.Font.Bold = true;
        style.Font.Color = ErrorColor;

        // Status warning
        style = document.Styles.AddStyle("StatusWarning", "Normal");
        style.Font.Bold = true;
        style.Font.Color = WarningColor;

        // Label style
        style = document.Styles.AddStyle("Label", "Normal");
        style.Font.Bold = true;
        style.Font.Size = 10;

        // Value style
        style = document.Styles.AddStyle("Value", "Normal");
        style.Font.Size = 10;
    }

    private static void AddHeader(Section section, HttpRecord record)
    {
        var paragraph = section.AddParagraph("HTTP Request Report", "Title");

        // Method badge with status
        var methodColor = GetMethodColor(record.Method);
        var statusStyle = GetStatusStyle(record.StatusCode);

        paragraph = section.AddParagraph();
        paragraph.Format.SpaceAfter = 12;

        var methodText = paragraph.AddFormattedText($"{record.Method} ", TextFormat.Bold);
        methodText.Color = methodColor;

        paragraph.AddFormattedText(record.Url);
    }

    private static void AddSummary(Section section, HttpRecord record)
    {
        section.AddParagraph("Summary", "Heading1");

        var table = section.AddTable();
        table.Borders.Width = 0;
        table.Format.SpaceAfter = 12;

        // Define columns
        table.AddColumn(Unit.FromCentimeter(4));
        table.AddColumn(Unit.FromCentimeter(12));

        // Timestamp
        AddSummaryRow(table, "Timestamp:", record.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff UTC", CultureInfo.InvariantCulture));

        // Method
        AddSummaryRow(table, "Method:", record.Method, GetMethodColor(record.Method));

        // Status
        var statusText = record.IsComplete
            ? $"{record.StatusCode} {record.ReasonPhrase}"
            : record.ErrorMessage ?? "Failed";
        AddSummaryRow(table, "Status:", statusText, GetStatusColor(record));

        // Duration
        AddSummaryRow(table, "Duration:", record.DurationDisplay);

        // Request Size
        AddSummaryRow(table, "Request Size:", FormatBytes(record.RequestSize));

        // Response Size
        AddSummaryRow(table, "Response Size:", FormatBytes(record.ResponseSize));

        // Total Size
        AddSummaryRow(table, "Total Size:", FormatBytes(record.TotalSize));

        // Error message if failed
        if (!string.IsNullOrEmpty(record.ErrorMessage))
        {
            AddSummaryRow(table, "Error:", record.ErrorMessage, ErrorColor);
        }
    }

    private static void AddSummaryRow(Table table, string label, string value, Color? valueColor = null)
    {
        var row = table.AddRow();
        row.Cells[0].AddParagraph(label).Style = "Label";

        var valueParagraph = row.Cells[1].AddParagraph(value);
        valueParagraph.Style = "Value";
        if (valueColor.HasValue)
        {
            valueParagraph.Format.Font.Color = valueColor.Value;
        }
    }

    private static void AddRequestSection(Section section, HttpRecord record)
    {
        section.AddParagraph("Request", "Heading1");
        AddHeadersSubsection(section, record.RequestHeaders);
        AddBodySubsection(section, record.RequestBody, record.RequestBodyTruncated, "request");
    }

    private static void AddResponseSection(Section section, HttpRecord record)
    {
        section.AddParagraph("Response", "Heading1");

        if (!record.IsComplete)
        {
            AddErrorMessage(section, record.ErrorMessage ?? "Request failed - no response received");
            return;
        }

        AddHeadersSubsection(section, record.ResponseHeaders);
        AddBodySubsection(section, record.ResponseBody, record.ResponseBodyTruncated, "response");
    }

    private static void AddHeadersSubsection(Section section, Dictionary<string, string[]> headers)
    {
        section.AddParagraph("Headers", "Heading2");

        if (headers.Count == 0)
        {
            var noHeaders = section.AddParagraph("[No headers]");
            noHeaders.Format.Font.Italic = true;
            return;
        }

        AddHeadersTable(section, headers);
    }

    private static void AddBodySubsection(Section section, string? body, bool truncated, string bodyType)
    {
        section.AddParagraph("Body", "Heading2");

        if (string.IsNullOrEmpty(body))
        {
            var noBody = section.AddParagraph($"[No {bodyType} body]");
            noBody.Format.Font.Italic = true;
            return;
        }

        if (truncated)
        {
            var warning = section.AddParagraph("[Body truncated due to size limits]");
            warning.Format.Font.Italic = true;
            warning.Format.Font.Color = WarningColor;
        }

        AddCodeBlock(section, body);
    }

    private static void AddErrorMessage(Section section, string message)
    {
        var errorParagraph = section.AddParagraph(message);
        errorParagraph.Format.Font.Color = ErrorColor;
        errorParagraph.Format.Font.Italic = true;
    }

    private static void AddHeadersTable(Section section, Dictionary<string, string[]> headers)
    {
        var table = section.AddTable();
        table.Borders.Width = 0.5;
        table.Borders.Color = BorderColor;
        table.Format.SpaceAfter = 12;

        // Define columns
        table.AddColumn(Unit.FromCentimeter(5));
        table.AddColumn(Unit.FromCentimeter(11));

        // Header row
        var headerRow = table.AddRow();
        headerRow.Shading.Color = HeaderBgColor;
        headerRow.HeadingFormat = true;
        headerRow.Format.Font.Bold = true;

        headerRow.Cells[0].AddParagraph("Header");
        headerRow.Cells[1].AddParagraph("Value");

        // Data rows
        foreach (var (name, values) in headers.OrderBy(h => h.Key))
        {
            var row = table.AddRow();
            row.Cells[0].AddParagraph(name);

            // Wrap long values (e.g., Bearer tokens) to prevent overflow
            var combinedValue = string.Join(", ", values);
            row.Cells[1].AddParagraph(WrapLongText(combinedValue));
        }
    }

    private static void AddCodeBlock(Section section, string code)
    {
        // Add a bordered frame for the code
        var paragraph = section.AddParagraph();
        paragraph.Format.Borders.Width = 0.5;
        paragraph.Format.Borders.Color = BorderColor;
        paragraph.Format.Shading.Color = HeaderBgColor;
        paragraph.Format.LeftIndent = Unit.FromCentimeter(0.3);
        paragraph.Format.RightIndent = Unit.FromCentimeter(0.3);
        paragraph.Format.SpaceBefore = 6;
        paragraph.Format.SpaceAfter = 6;

        // Limit code length for PDF (very long bodies can cause issues)
        const int maxCodeLength = 10000;
        var displayCode = code.Length > maxCodeLength
            ? code[..maxCodeLength] + "\n\n[... truncated for PDF display ...]"
            : code;

        // Split into lines and wrap long lines to prevent overflow
        var lines = displayCode.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');

            // Break long lines without spaces (e.g., base64, JWT tokens)
            var wrappedLine = WrapLongText(line, 100);
            var wrappedParts = wrappedLine.Split('\n');

            for (var j = 0; j < wrappedParts.Length; j++)
            {
                if (i > 0 || j > 0)
                {
                    paragraph.AddLineBreak();
                }

                var text = paragraph.AddFormattedText(wrappedParts[j]);
                text.Font.Name = "Courier";
                text.Font.Size = 8;
            }
        }
    }

    /// <summary>
    /// Wraps long text by inserting line breaks at regular intervals.
    /// Useful for preventing overflow of long strings without spaces (e.g., Bearer tokens, base64).
    /// </summary>
    private static string WrapLongText(string text, int maxCharsPerSegment = 80)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxCharsPerSegment)
            return text;

        var result = new StringBuilder();
        for (var i = 0; i < text.Length; i += maxCharsPerSegment)
        {
            if (i > 0) result.Append('\n');
            result.Append(text.Substring(i, Math.Min(maxCharsPerSegment, text.Length - i)));
        }
        return result.ToString();
    }

    private static void AddFooter(Section section)
    {
        var paragraph = section.AddParagraph();
        paragraph.Format.SpaceBefore = 24;
        paragraph.Format.Borders.Top.Width = 0.5;
        paragraph.Format.Borders.Top.Color = BorderColor;

        paragraph = section.AddParagraph();
        paragraph.Format.Font.Size = 8;
        paragraph.Format.Font.Italic = true;
        paragraph.Format.Alignment = ParagraphAlignment.Center;
        paragraph.AddText($"Generated by Wiretap.Maui on {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
    }

    private static Color GetMethodColor(string method)
    {
        return method.ToUpperInvariant() switch
        {
            "GET" => new Color(0, 123, 255),    // Blue
            "POST" => new Color(40, 167, 69),   // Green
            "PUT" => new Color(255, 193, 7),    // Yellow
            "PATCH" => new Color(23, 162, 184), // Cyan
            "DELETE" => new Color(220, 53, 69), // Red
            _ => new Color(108, 117, 125)       // Gray
        };
    }

    private static string GetStatusStyle(int statusCode)
    {
        return statusCode switch
        {
            >= 200 and < 300 => "StatusSuccess",
            >= 400 and < 500 => "StatusWarning",
            >= 500 => "StatusError",
            _ => "Value"
        };
    }

    private static Color GetStatusColor(HttpRecord record)
    {
        if (!record.IsComplete)
            return ErrorColor;

        return record.StatusCode switch
        {
            >= 200 and < 300 => SuccessColor,
            >= 400 and < 500 => WarningColor,
            >= 500 => ErrorColor,
            _ => new Color(0, 0, 0)
        };
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes == 0) return "0 B";
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F2} MB";
    }

    private static void EnsureFontResolverInitialized()
    {
        if (_fontResolverInitialized)
            return;

        lock (_initLock)
        {
            if (_fontResolverInitialized)
                return;

            if (GlobalFontSettings.FontResolver == null)
            {
                GlobalFontSettings.FontResolver = new WiretapFontResolver();
            }

            _fontResolverInitialized = true;
        }
    }
}

/// <summary>
/// Font resolver for cross-platform PDF generation.
/// Maps font requests to available system fonts on macOS, Windows, iOS, and Android.
/// </summary>
internal sealed class WiretapFontResolver : IFontResolver
{
    private readonly Dictionary<string, byte[]> _fontCache = new();
    private readonly object _cacheLock = new();

    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        // Create a unique face name based on family and style
        var faceName = $"{familyName}#{(isBold ? "B" : "R")}{(isItalic ? "I" : "N")}";
        return new FontResolverInfo(faceName, isBold, isItalic);
    }

    public byte[]? GetFont(string faceName)
    {
        lock (_cacheLock)
        {
            if (_fontCache.TryGetValue(faceName, out var cached))
            {
                return cached;
            }
        }

        // Parse face name to get family and style
        var parts = faceName.Split('#');
        var familyName = parts[0];
        var isBold = parts.Length > 1 && parts[1].Contains('B');
        var isItalic = parts.Length > 1 && parts[1].Contains('I');

        var fontData = LoadFontData(familyName, isBold, isItalic);

        if (fontData != null)
        {
            lock (_cacheLock)
            {
                _fontCache[faceName] = fontData;
            }
        }

        return fontData;
    }

    private static byte[]? LoadFontData(string familyName, bool isBold, bool isItalic)
    {
        // Get font file paths based on family name and style
        var paths = GetFontPaths(familyName, isBold, isItalic);

        foreach (var path in paths)
        {
            if (File.Exists(path))
            {
                try
                {
                    return File.ReadAllBytes(path);
                }
                catch
                {
                    // Ignore read errors, try next path
                }
            }
        }

        // Fallback: try to find any available font
        var fallbackPaths = GetFallbackFontPaths();
        foreach (var path in fallbackPaths)
        {
            if (File.Exists(path))
            {
                try
                {
                    return File.ReadAllBytes(path);
                }
                catch
                {
                    // Ignore
                }
            }
        }

        return null;
    }

    private static IEnumerable<string> GetFontPaths(string familyName, bool isBold, bool isItalic)
    {
        var paths = new List<string>();
        var normalizedName = familyName.ToLowerInvariant().Replace(" ", "").Replace("-", "");

        // Determine which font family to use
        var isMonospace = normalizedName.Contains("courier") || normalizedName.Contains("consolas") ||
                          normalizedName.Contains("menlo") || normalizedName.Contains("monaco");
        var isSerif = normalizedName.Contains("times") || normalizedName.Contains("serif");

        // macOS paths - IMPORTANT: Use .ttf files, not .ttc (PDFsharp doesn't handle .ttc well)
        if (isMonospace)
        {
            // Courier New is most reliable on macOS (individual .ttf files)
            if (isBold && isItalic)
                paths.Add("/System/Library/Fonts/Supplemental/Courier New Bold Italic.ttf");
            else if (isBold)
                paths.Add("/System/Library/Fonts/Supplemental/Courier New Bold.ttf");
            else if (isItalic)
                paths.Add("/System/Library/Fonts/Supplemental/Courier New Italic.ttf");
            else
                paths.Add("/System/Library/Fonts/Supplemental/Courier New.ttf");
        }
        else if (isSerif)
        {
            // Times New Roman (individual .ttf files)
            if (isBold && isItalic)
                paths.Add("/System/Library/Fonts/Supplemental/Times New Roman Bold Italic.ttf");
            else if (isBold)
                paths.Add("/System/Library/Fonts/Supplemental/Times New Roman Bold.ttf");
            else if (isItalic)
                paths.Add("/System/Library/Fonts/Supplemental/Times New Roman Italic.ttf");
            else
                paths.Add("/System/Library/Fonts/Supplemental/Times New Roman.ttf");
        }
        else
        {
            // Sans-serif: Arial (individual .ttf files - Helvetica.ttc doesn't work)
            if (isBold && isItalic)
                paths.Add("/System/Library/Fonts/Supplemental/Arial Bold Italic.ttf");
            else if (isBold)
                paths.Add("/System/Library/Fonts/Supplemental/Arial Bold.ttf");
            else if (isItalic)
                paths.Add("/System/Library/Fonts/Supplemental/Arial Italic.ttf");
            else
                paths.Add("/System/Library/Fonts/Supplemental/Arial.ttf");
        }

        // Windows paths
        var winFonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        if (!string.IsNullOrEmpty(winFonts))
        {
            if (isMonospace)
            {
                if (isBold && isItalic)
                    paths.Add(Path.Combine(winFonts, "courbi.ttf"));
                else if (isBold)
                    paths.Add(Path.Combine(winFonts, "courbd.ttf"));
                else if (isItalic)
                    paths.Add(Path.Combine(winFonts, "couri.ttf"));
                else
                    paths.Add(Path.Combine(winFonts, "cour.ttf"));
            }
            else if (isSerif)
            {
                if (isBold && isItalic)
                    paths.Add(Path.Combine(winFonts, "timesbi.ttf"));
                else if (isBold)
                    paths.Add(Path.Combine(winFonts, "timesbd.ttf"));
                else if (isItalic)
                    paths.Add(Path.Combine(winFonts, "timesi.ttf"));
                else
                    paths.Add(Path.Combine(winFonts, "times.ttf"));
            }
            else
            {
                if (isBold && isItalic)
                    paths.Add(Path.Combine(winFonts, "arialbi.ttf"));
                else if (isBold)
                    paths.Add(Path.Combine(winFonts, "arialbd.ttf"));
                else if (isItalic)
                    paths.Add(Path.Combine(winFonts, "ariali.ttf"));
                else
                    paths.Add(Path.Combine(winFonts, "arial.ttf"));
            }
        }

        // Android paths (Roboto for sans-serif, Droid Sans Mono for monospace)
        if (isMonospace)
        {
            paths.Add("/system/fonts/DroidSansMono.ttf");
            paths.Add("/system/fonts/RobotoMono-Regular.ttf");
        }
        else
        {
            if (isBold && isItalic)
                paths.Add("/system/fonts/Roboto-BoldItalic.ttf");
            else if (isBold)
                paths.Add("/system/fonts/Roboto-Bold.ttf");
            else if (isItalic)
                paths.Add("/system/fonts/Roboto-Italic.ttf");
            else
                paths.Add("/system/fonts/Roboto-Regular.ttf");
        }

        return paths;
    }

    private static IEnumerable<string> GetFallbackFontPaths()
    {
        return new[]
        {
            // macOS - use .ttf files only
            "/System/Library/Fonts/Supplemental/Arial.ttf",
            "/System/Library/Fonts/Supplemental/Courier New.ttf",
            "/System/Library/Fonts/Supplemental/Times New Roman.ttf",
            // Windows
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "cour.ttf"),
            // Android
            "/system/fonts/Roboto-Regular.ttf",
            "/system/fonts/DroidSans.ttf"
        };
    }
}
