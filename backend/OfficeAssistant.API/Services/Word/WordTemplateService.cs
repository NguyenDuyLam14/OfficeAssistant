using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace OfficeAssistant.API.Services.Word;

/// <summary>
/// Service xử lý việc thay thế nội dung trong file Word template.
///
/// Mục tiêu:
/// - Giữ nguyên định dạng Word của template.
/// - Thay thế các placeholder như {{TIEU_DE}}.
/// - Không gom toàn bộ paragraph vào một Run duy nhất.
/// </summary>
public class WordTemplateService
{
    /// <summary>
    /// Đọc file Word template và tìm tất cả placeholder
    /// có dạng {{TEN_PLACEHOLDER}}.
    ///
    /// Ví dụ:
    /// {{TIEU_DE}}
    /// {{DON_VI}}
    /// {{NOI_DUNG}}
    /// {{NGUOI_KY}}
    /// </summary>
    public List<string> GetPlaceholders(
        string templateFilePath)
    {
        // Kiểm tra file template có tồn tại không.
        if (!File.Exists(templateFilePath))
        {
            throw new FileNotFoundException(
                "Không tìm thấy file Word mẫu.",
                templateFilePath
            );
        }

        // Danh sách placeholder tìm được.
        var placeholders =
            new HashSet<string>(
                StringComparer.Ordinal
            );

        // Mở file Word ở chế độ chỉ đọc.
        using var document =
            WordprocessingDocument.Open(
                templateFilePath,
                false
            );

        // Lấy MainDocumentPart.
        var mainPart =
            document.MainDocumentPart;

        if (mainPart == null)
        {
            throw new InvalidOperationException(
                "File Word không có MainDocumentPart hợp lệ."
            );
        }

        // Lấy Document.
        var documentElement =
            mainPart.Document;

        if (documentElement == null)
        {
            throw new InvalidOperationException(
                "File Word không có Document hợp lệ."
            );
        }

        // Lấy Body.
        var body =
            documentElement.Body;

        if (body == null)
        {
            throw new InvalidOperationException(
                "File Word không có Body."
            );
        }

        // Descendants<Paragraph>() bao gồm paragraph
        // trong bảng Word.
        var paragraphs =
            body
                .Descendants<Paragraph>()
                .ToList();

        // Duyệt từng paragraph.
        foreach (var paragraph in paragraphs)
        {
            // Ghép toàn bộ Text trong paragraph.
            var paragraphText =
                string.Concat(
                    paragraph
                        .Descendants<Text>()
                        .Select(
                            text =>
                                text.Text ?? string.Empty
                        )
                );

            if (string.IsNullOrWhiteSpace(paragraphText))
            {
                continue;
            }

            // Tìm placeholder dạng {{...}}.
            var matches =
                System.Text.RegularExpressions.Regex.Matches(
                    paragraphText,
                    @"\{\{[^{}]+\}\}"
                );

            foreach (System.Text.RegularExpressions.Match match
                     in matches)
            {
                placeholders.Add(match.Value);
            }
        }

        return placeholders.ToList();
    }

    /// <summary>
    /// Tạo file Word mới từ file template.
    /// </summary>
    /// <param name="templateFilePath">
    /// Đường dẫn file Word mẫu.
    /// </param>
    /// <param name="outputFilePath">
    /// Đường dẫn file Word kết quả.
    /// </param>
    /// <param name="replacements">
    /// Danh sách placeholder và nội dung cần thay thế.
    /// </param>
    public void GenerateDocument(
        string templateFilePath,
        string outputFilePath,
        Dictionary<string, string> replacements)
    {
        // =====================================================
        // 1. KIỂM TRA FILE TEMPLATE
        // =====================================================

        if (!File.Exists(templateFilePath))
        {
            throw new FileNotFoundException(
                "Không tìm thấy file Word mẫu.",
                templateFilePath
            );
        }

        // =====================================================
        // 2. TẠO THƯ MỤC OUTPUT
        // =====================================================

        var outputDirectory =
            Path.GetDirectoryName(outputFilePath);

        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        // =====================================================
        // 3. XÓA FILE CŨ NẾU ĐÃ TỒN TẠI
        // =====================================================

        if (File.Exists(outputFilePath))
        {
            File.Delete(outputFilePath);
        }

        // =====================================================
        // 4. COPY TEMPLATE THÀNH FILE OUTPUT
        // =====================================================

        File.Copy(
            templateFilePath,
            outputFilePath
        );

        // =====================================================
        // 5. MỞ FILE WORD BẰNG OPENXML
        // =====================================================

        using var document =
            WordprocessingDocument.Open(
                outputFilePath,
                true
            );

        // =====================================================
        // 6. LẤY MAIN DOCUMENT PART
        // =====================================================

        var mainPart =
            document.MainDocumentPart;

        if (mainPart == null)
        {
            throw new InvalidOperationException(
                "File Word không có MainDocumentPart hợp lệ."
            );
        }

        // =====================================================
        // 7. LẤY DOCUMENT
        // =====================================================

        var documentElement =
            mainPart.Document;

        if (documentElement == null)
        {
            throw new InvalidOperationException(
                "File Word không có Document hợp lệ."
            );
        }

        // =====================================================
        // 8. LẤY BODY
        // =====================================================

        var body =
            documentElement.Body;

        if (body == null)
        {
            throw new InvalidOperationException(
                "File Word không có nội dung Body."
            );
        }

        // =====================================================
        // 9. LẤY TẤT CẢ PARAGRAPH
        // =====================================================

        var paragraphs =
            body
                .Descendants<Paragraph>()
                .ToList();

        // =====================================================
        // 10. XỬ LÝ TỪNG PARAGRAPH
        // =====================================================

        foreach (var paragraph in paragraphs)
        {
            ReplaceParagraphText(
                paragraph,
                replacements
            );
        }

        // =====================================================
        // 11. LƯU FILE WORD
        // =====================================================

        documentElement.Save();
    }

    /// <summary>
    /// Thay placeholder trong một paragraph
    /// nhưng cố gắng giữ nguyên định dạng của từng Run.
    /// </summary>
    private void ReplaceParagraphText(
        Paragraph paragraph,
        Dictionary<string, string> replacements)
    {
        // =====================================================
        // 1. LẤY CÁC RUN TRỰC TIẾP TRONG PARAGRAPH
        // =====================================================

        var runs =
            paragraph
                .Elements<Run>()
                .ToList();

        if (runs.Count == 0)
        {
            return;
        }

        // =====================================================
        // 2. TẠO DANH SÁCH THÔNG TIN CỦA CÁC RUN
        // =====================================================

        var runInfos =
            new List<RunInfo>();

        var fullText = string.Empty;

        foreach (var run in runs)
        {
            // Lấy toàn bộ Text trong Run.
            var text =
                string.Concat(
                    run
                        .Elements<Text>()
                        .Select(x => x.Text ?? string.Empty)
                );

            // Lưu lại nội dung và định dạng của Run.
            runInfos.Add(
                new RunInfo
                {
                    Run = run,
                    Text = text,
                    StartIndex = fullText.Length,
                    EndIndex =
                        fullText.Length + text.Length
                }
            );

            // Ghép vào chuỗi tổng.
            fullText += text;
        }

        // Không có nội dung text.
        if (string.IsNullOrEmpty(fullText))
        {
            return;
        }

        // =====================================================
        // 3. KIỂM TRA CÓ PLACEHOLDER HAY KHÔNG
        // =====================================================

        var hasReplacement =
            replacements.Keys.Any(
                placeholder =>
                    fullText.Contains(
                        placeholder,
                        StringComparison.Ordinal
                    )
            );

        if (!hasReplacement)
        {
            return;
        }

        // =====================================================
        // 4. TẠO CÁC ĐOẠN TEXT MỚI
        // =====================================================

        var segments =
            BuildTextSegments(
                fullText,
                runInfos,
                replacements
            );

        // =====================================================
        // 5. XÓA CÁC RUN CŨ
        // =====================================================

        foreach (var run in runs)
        {
            run.Remove();
        }

        // =====================================================
        // 6. TẠO LẠI RUN
        // =====================================================

        foreach (var segment in segments)
        {
            var newRun =
                new Run();

            // -------------------------------------------------
            // Sao chép định dạng của Run gốc.
            // -------------------------------------------------

            if (segment.RunProperties != null)
            {
                newRun.RunProperties =
                    (RunProperties)
                    segment.RunProperties.CloneNode(true);
            }

            // -------------------------------------------------
            // Thêm Text mới.
            // -------------------------------------------------

            var text =
                new Text(segment.Text);

            // Giữ khoảng trắng đầu/cuối nếu có.
            text.Space =
                DocumentFormat.OpenXml.SpaceProcessingModeValues.Preserve;

            newRun.AppendChild(text);

            // -------------------------------------------------
            // Thêm Run mới vào Paragraph.
            // -------------------------------------------------

            paragraph.AppendChild(newRun);
        }
    }

    /// <summary>
    /// Xây dựng các đoạn text mới sau khi thay placeholder.
    /// </summary>
    private List<TextSegment> BuildTextSegments(
        string fullText,
        List<RunInfo> runInfos,
        Dictionary<string, string> replacements)
    {
        var segments =
            new List<TextSegment>();

        var currentIndex = 0;

        while (currentIndex < fullText.Length)
        {
            // =================================================
            // TÌM PLACEHOLDER TIẾP THEO
            // =================================================

            string? foundPlaceholder = null;

            var foundIndex = -1;

            foreach (var replacement in replacements)
            {
                var index =
                    fullText.IndexOf(
                        replacement.Key,
                        currentIndex,
                        StringComparison.Ordinal
                    );

                if (index >= 0 &&
                    (foundIndex < 0 ||
                     index < foundIndex))
                {
                    foundIndex = index;
                    foundPlaceholder =
                        replacement.Key;
                }
            }

            // =================================================
            // KHÔNG CÒN PLACEHOLDER
            // =================================================

            if (foundPlaceholder == null)
            {
                AddTextSegment(
                    segments,
                    fullText.Substring(currentIndex),
                    currentIndex,
                    runInfos
                );

                break;
            }

            // =================================================
            // THÊM TEXT TRƯỚC PLACEHOLDER
            // =================================================

            if (foundIndex > currentIndex)
            {
                AddTextSegment(
                    segments,
                    fullText.Substring(
                        currentIndex,
                        foundIndex - currentIndex
                    ),
                    currentIndex,
                    runInfos
                );
            }

            // =================================================
            // LẤY NỘI DUNG THAY THẾ
            // =================================================

            var replacementText =
                replacements[foundPlaceholder] ??
                string.Empty;

            // -------------------------------------------------
            // Placeholder sẽ sử dụng định dạng của Run
            // chứa ký tự đầu tiên của placeholder.
            // -------------------------------------------------

            var placeholderRun =
                FindRunInfo(
                    foundIndex,
                    runInfos
                );

            if (placeholderRun != null)
            {
                segments.Add(
                    new TextSegment
                    {
                        Text = replacementText,
                        RunProperties =
                            placeholderRun.Run
                                .RunProperties
                    }
                );
            }
            else
            {
                segments.Add(
                    new TextSegment
                    {
                        Text = replacementText,
                        RunProperties = null
                    }
                );
            }

            // =================================================
            // DI CHUYỂN QUA PLACEHOLDER
            // =================================================

            currentIndex =
                foundIndex +
                foundPlaceholder.Length;
        }

        return segments;
    }

    /// <summary>
    /// Thêm một đoạn text vào danh sách segment
    /// với định dạng tương ứng với Run gốc.
    /// </summary>
    private void AddTextSegment(
        List<TextSegment> segments,
        string text,
        int startIndex,
        List<RunInfo> runInfos)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var currentIndex = startIndex;

        while (currentIndex < startIndex + text.Length)
        {
            // Tìm Run chứa vị trí hiện tại.
            var runInfo =
                FindRunInfo(
                    currentIndex,
                    runInfos
                );

            if (runInfo == null)
            {
                break;
            }

            // Xác định vị trí bắt đầu trong Run.
            var offsetInRun =
                currentIndex -
                runInfo.StartIndex;

            // Số ký tự còn lại của Run.
            var remainingInRun =
                runInfo.Text.Length -
                offsetInRun;

            // Số ký tự còn lại của segment.
            var remainingInSegment =
                startIndex +
                text.Length -
                currentIndex;

            var takeLength =
                Math.Min(
                    remainingInRun,
                    remainingInSegment
                );

            if (takeLength <= 0)
            {
                break;
            }

            // Lấy phần text tương ứng.
            var segmentText =
                text.Substring(
                    currentIndex - startIndex,
                    takeLength
                );

            // Thêm segment cùng định dạng Run gốc.
            segments.Add(
                new TextSegment
                {
                    Text = segmentText,
                    RunProperties =
                        runInfo.Run.RunProperties
                }
            );

            currentIndex += takeLength;
        }
    }

    /// <summary>
    /// Tìm Run chứa vị trí ký tự.
    /// </summary>
    private RunInfo? FindRunInfo(
        int characterIndex,
        List<RunInfo> runInfos)
    {
        return runInfos.FirstOrDefault(
            x =>
                characterIndex >= x.StartIndex &&
                characterIndex < x.EndIndex
        );
    }

    /// <summary>
    /// Thông tin của một Run trong Word.
    /// </summary>
    private class RunInfo
    {
        public Run Run { get; set; } = null!;

        public string Text { get; set; } = string.Empty;

        public int StartIndex { get; set; }

        public int EndIndex { get; set; }
    }

    /// <summary>
    /// Một đoạn text mới cùng với định dạng của nó.
    /// </summary>
    private class TextSegment
    {
        public string Text { get; set; } = string.Empty;

        public RunProperties? RunProperties { get; set; }
    }
}