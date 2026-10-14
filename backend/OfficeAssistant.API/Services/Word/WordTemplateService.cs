using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Text.RegularExpressions;

namespace OfficeAssistant.API.Services.Word;

/// <summary>
/// Service xử lý việc thay thế nội dung trong file Word template.
///
/// Mục tiêu:
/// - Giữ nguyên định dạng Word của template.
/// - Thay thế các placeholder như [TIEU_DE].
/// - Không gom toàn bộ paragraph vào một Run duy nhất.
/// - Hỗ trợ marker tự nhiên như … trong template.
/// </summary>
public class WordTemplateService
{
    /// <summary>
    /// Đọc file Word template và tìm tất cả placeholder
    /// có dạng [TEN_PLACEHOLDER].
    /// </summary>
    public List<string> GetPlaceholders(
        string templateFilePath)
    {
        if (!File.Exists(templateFilePath))
        {
            throw new FileNotFoundException(
                "Không tìm thấy file Word mẫu.",
                templateFilePath
            );
        }

        var placeholders =
            new HashSet<string>(
                StringComparer.Ordinal
            );

        using var document =
            WordprocessingDocument.Open(
                templateFilePath,
                false
            );

        var mainPart =
            document.MainDocumentPart;

        if (mainPart == null)
        {
            throw new InvalidOperationException(
                "File Word không có MainDocumentPart hợp lệ."
            );
        }

        var documentElement =
            mainPart.Document;

        if (documentElement == null)
        {
            throw new InvalidOperationException(
                "File Word không có Document hợp lệ."
            );
        }

        var body =
            documentElement.Body;

        if (body == null)
        {
            throw new InvalidOperationException(
                "File Word không có Body."
            );
        }

        var paragraphs =
            body
                .Descendants<Paragraph>()
                .ToList();

        foreach (var paragraph in paragraphs)
        {
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

            var matches =
                Regex.Matches(
                    paragraphText,
                    @"\[[^\[\]]+\]"
                );

            foreach (Match match in matches)
            {
                placeholders.Add(match.Value);
            }
        }

        return placeholders.ToList();
    }

    /// <summary>
    /// Đọc toàn bộ nội dung text trong file Word template.
    ///
    /// Dùng để gửi nội dung mẫu cho AI phân tích.
    /// Không thay đổi file Word gốc.
    /// </summary>
    public string GetTemplateText(
        string templateFilePath)
    {
        if (!File.Exists(templateFilePath))
        {
            throw new FileNotFoundException(
                "Không tìm thấy file Word mẫu.",
                templateFilePath
            );
        }

        using var document =
            WordprocessingDocument.Open(
                templateFilePath,
                false
            );

        var mainPart =
            document.MainDocumentPart;

        if (mainPart == null)
        {
            throw new InvalidOperationException(
                "File Word không có MainDocumentPart."
            );
        }

        var documentElement =
            mainPart.Document;

        if (documentElement == null)
        {
            throw new InvalidOperationException(
                "File Word không có Document."
            );
        }

        var body =
            documentElement.Body;

        if (body == null)
        {
            throw new InvalidOperationException(
                "File Word không có Body."
            );
        }

        var paragraphs =
            body
                .Descendants<Paragraph>()
                .ToList();

        var lines =
            new List<string>();

        foreach (var paragraph in paragraphs)
        {
            var text =
                string.Concat(
                    paragraph
                        .Descendants<Text>()
                        .Select(
                            x =>
                                x.Text ?? string.Empty
                        )
                );

            if (!string.IsNullOrWhiteSpace(text))
            {
                lines.Add(text);
            }
        }

        return string.Join(
            Environment.NewLine,
            lines
        );
    }

    /// <summary>
    /// Phát hiện các vùng cần điền trong template.
    ///
    /// Hỗ trợ:
    /// - …
    /// - ………
    /// - …………………
    /// - {{PLACEHOLDER}}
    /// - [PLACEHOLDER]
    ///
    /// Không sửa file Word gốc.
    /// </summary>
    public List<string> DetectFillableMarkers(
        string templateFilePath)
    {
        var templateText =
            GetTemplateText(templateFilePath);

        var markers =
            new List<string>();

        // ---------------------------------------------------------
        // 1. Placeholder dạng [ABC]
        // ---------------------------------------------------------

        var squareBracketMatches =
            Regex.Matches(
                templateText,
                @"\[[^\[\]]+\]"
            );

        foreach (Match match in squareBracketMatches)
        {
            if (!markers.Contains(match.Value))
            {
                markers.Add(match.Value);
            }
        }

        // ---------------------------------------------------------
        // 2. Placeholder dạng {{ABC}}
        // ---------------------------------------------------------

        var curlyBracketMatches =
            Regex.Matches(
                templateText,
                @"\{\{[^{}]+\}\}"
            );

        foreach (Match match in curlyBracketMatches)
        {
            if (!markers.Contains(match.Value))
            {
                markers.Add(match.Value);
            }
        }

        // ---------------------------------------------------------
        // 3. Dấu ba chấm Unicode …
        // ---------------------------------------------------------

        var ellipsisMatches =
            Regex.Matches(
                templateText,
                @"…+"
            );

        foreach (Match match in ellipsisMatches)
        {
            if (!markers.Contains(match.Value))
            {
                markers.Add(match.Value);
            }
        }

        return markers;
    }

    /// <summary>
    /// Tạo nội dung trung gian cho AI.
    ///
    /// Các vùng "…" sẽ được thay bằng:
    /// [F001], [F002], [F003]...
    ///
    /// File Word gốc không bị thay đổi.
    ///
    /// Lưu ý:
    /// Method này chỉ tạo chuỗi text trung gian.
    /// Không thay đổi file Word.
    /// </summary>
    public TemplateAnalysisResult BuildAITemplate(
        string templateFilePath)
    {
        var templateText =
            GetTemplateText(templateFilePath);

        var fieldPattern =
            @"…+|\[[^\[\]]+\]|\{\{[^{}]+\}\}";

        var matches =
            Regex.Matches(
                templateText,
                fieldPattern
            );

        var fields =
            new List<TemplateField>();

        var resultText =
            templateText;

        for (
            int i = matches.Count - 1;
            i >= 0;
            i--)
        {
            var match =
                matches[i];

            var fieldId =
                $"[F{i + 1:000}]";

            var field =
                new TemplateField
                {
                    FieldId = fieldId,
                    OriginalText = match.Value
                };

            fields.Insert(
                0,
                field
            );

            resultText =
                resultText.Remove(
                    match.Index,
                    match.Length
                );

            resultText =
                resultText.Insert(
                    match.Index,
                    fieldId
                );
        }

        return new TemplateAnalysisResult
        {
            TemplateText = resultText,
            Fields = fields
        };
    }

    /// <summary>
    /// Tạo một file Word trung gian để AI nhận diện
    /// các vùng cần điền dưới dạng [F001], [F002], ...
    ///
    /// File Word gốc không bị thay đổi.
    ///
    /// QUAN TRỌNG:
    /// WordprocessingDocument phải được đóng hoàn toàn
    /// trước khi gọi GetTemplateText().
    /// </summary>
    public TemplateAnalysisResult PrepareNaturalTemplateForAI(
        string sourceTemplatePath,
        string preparedTemplatePath)
    {
        // =====================================================
        // 1. KIỂM TRA FILE GỐC
        // =====================================================

        if (!File.Exists(sourceTemplatePath))
        {
            throw new FileNotFoundException(
                "Không tìm thấy file Word mẫu.",
                sourceTemplatePath
            );
        }

        // =====================================================
        // 2. TẠO THƯ MỤC CHỨA FILE TẠM
        // =====================================================

        var directory =
            Path.GetDirectoryName(
                preparedTemplatePath
            );

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // =====================================================
        // 3. XÓA FILE TẠM CŨ NẾU CÓ
        // =====================================================

        if (File.Exists(preparedTemplatePath))
        {
            File.Delete(preparedTemplatePath);
        }

        // =====================================================
        // 4. COPY FILE GỐC
        // =====================================================

        File.Copy(
            sourceTemplatePath,
            preparedTemplatePath
        );

        var fields =
            new List<TemplateField>();

        var fieldCounter = 1;

        // =====================================================
        // 5. MỞ FILE COPY ĐỂ CHỈNH SỬA
        //
        // Dùng block { } thay vì "using var" để đảm bảo
        // document được đóng trước khi GetTemplateText().
        // =====================================================

        using (var document =
            WordprocessingDocument.Open(
                preparedTemplatePath,
                true
            ))
        {
            var mainPart =
                document.MainDocumentPart;

            if (mainPart == null)
            {
                throw new InvalidOperationException(
                    "File Word không có MainDocumentPart."
                );
            }

            var documentElement =
                mainPart.Document;

            if (documentElement == null)
            {
                throw new InvalidOperationException(
                    "File Word không có Document."
                );
            }

            var body =
                documentElement.Body;

            if (body == null)
            {
                throw new InvalidOperationException(
                    "File Word không có Body."
                );
            }

            // =================================================
            // 6. DUYỆT TỪNG PARAGRAPH
            // =================================================

            var paragraphs =
                body
                    .Descendants<Paragraph>()
                    .ToList();

            foreach (var paragraph in paragraphs)
            {
                var runs =
                    paragraph
                        .Elements<Run>()
                        .ToList();

                if (runs.Count == 0)
                {
                    continue;
                }

                var fullText =
                    string.Concat(
                        runs.Select(
                            run =>
                                string.Concat(
                                    run
                                        .Elements<Text>()
                                        .Select(
                                            text =>
                                                text.Text ??
                                                string.Empty
                                        )
                                )
                        )
                    );

                if (string.IsNullOrEmpty(fullText))
                {
                    continue;
                }

                // ---------------------------------------------
                // Tìm marker Unicode …
                // ---------------------------------------------

                var matches =
                    Regex.Matches(
                        fullText,
                        @"…+"
                    );

                if (matches.Count == 0)
                {
                    continue;
                }

                var occurrenceReplacements =
                    new List<OccurrenceReplacement>();

                foreach (Match match in matches)
                {
                    var fieldId =
                        $"[F{fieldCounter:000}]";

                    occurrenceReplacements.Add(
                        new OccurrenceReplacement
                        {
                            StartIndex = match.Index,
                            Length = match.Length,
                            Replacement = fieldId
                        }
                    );

                    fields.Add(
                        new TemplateField
                        {
                            FieldId = fieldId,
                            OriginalText = match.Value
                        }
                    );

                    fieldCounter++;
                }

                // ---------------------------------------------
                // Thay marker trực tiếp trong Word.
                // ---------------------------------------------

                ReplaceParagraphOccurrences(
                    paragraph,
                    occurrenceReplacements
                );
            }

            // =================================================
            // 7. LƯU FILE
            // =================================================

            documentElement.Save();
        }

        // =====================================================
        // 8. ĐẾN ĐÂY FILE WORD ĐÃ ĐƯỢC ĐÓNG
        //
        // Bây giờ mới mở lại file bằng GetTemplateText().
        // =====================================================

        var preparedText =
            GetTemplateText(
                preparedTemplatePath
            );

        // =====================================================
        // 9. TRẢ KẾT QUẢ CHO AI
        // =====================================================

        return new TemplateAnalysisResult
        {
            TemplateText = preparedText,
            Fields = fields
        };
    }

    /// <summary>
    /// Tạo file Word mới từ file template.
    /// </summary>
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
            Path.GetDirectoryName(
                outputFilePath
            );

        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(
                outputDirectory
            );
        }

        // =====================================================
        // 3. XÓA FILE CŨ
        // =====================================================

        if (File.Exists(outputFilePath))
        {
            File.Delete(outputFilePath);
        }

        // =====================================================
        // 4. COPY TEMPLATE
        // =====================================================

        File.Copy(
            templateFilePath,
            outputFilePath
        );

        // =====================================================
        // 5. MỞ FILE WORD
        // =====================================================

        using (var document =
            WordprocessingDocument.Open(
                outputFilePath,
                true
            ))
        {
            // =================================================
            // 6. LẤY MAIN DOCUMENT PART
            // =================================================

            var mainPart =
                document.MainDocumentPart;

            if (mainPart == null)
            {
                throw new InvalidOperationException(
                    "File Word không có MainDocumentPart hợp lệ."
                );
            }

            // =================================================
            // 7. LẤY DOCUMENT
            // =================================================

            var documentElement =
                mainPart.Document;

            if (documentElement == null)
            {
                throw new InvalidOperationException(
                    "File Word không có Document hợp lệ."
                );
            }

            // =================================================
            // 8. LẤY BODY
            // =================================================

            var body =
                documentElement.Body;

            if (body == null)
            {
                throw new InvalidOperationException(
                    "File Word không có nội dung Body."
                );
            }

            // =================================================
            // 9. LẤY TẤT CẢ PARAGRAPH
            // =================================================

            var paragraphs =
                body
                    .Descendants<Paragraph>()
                    .ToList();

            // =================================================
            // 10. XỬ LÝ TỪNG PARAGRAPH
            // =================================================

            foreach (var paragraph in paragraphs)
            {
                ReplaceParagraphText(
                    paragraph,
                    replacements
                );
            }

            // =================================================
            // 11. LƯU FILE
            // =================================================

            documentElement.Save();
        }
    }

    /// <summary>
    /// Thay placeholder trong một paragraph
    /// nhưng cố gắng giữ nguyên định dạng của từng Run.
    /// </summary>
    private void ReplaceParagraphText(
    Paragraph paragraph,
    Dictionary<string, string> replacements)
    {
        var runs =
            paragraph
                .Elements<Run>()
                .ToList();

        if (runs.Count == 0)
        {
            return;
        }

        var runInfos =
            new List<RunInfo>();

        var fullText =
            string.Empty;

        foreach (var run in runs)
        {
            var text =
                string.Concat(
                    run
                        .Elements<Text>()
                        .Select(
                            x => x.Text ?? string.Empty
                        )
                );

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

            fullText += text;
        }

        if (string.IsNullOrEmpty(fullText))
        {
            return;
        }

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

        var segments =
            BuildTextSegments(
                fullText,
                runInfos,
                replacements
            );

        // Xóa Run cũ
        foreach (var run in runs)
        {
            run.Remove();
        }

        // Tạo Run mới
        foreach (var segment in segments)
        {
            var newRun =
                new Run();

            if (segment.RunProperties != null)
            {
                newRun.RunProperties =
                    (RunProperties)
                    segment.RunProperties.CloneNode(true);
            }

            var text =
                new Text(segment.Text)
                {
                    Space =
                        DocumentFormat.OpenXml
                            .SpaceProcessingModeValues
                            .Preserve
                };

            newRun.AppendChild(text);

            paragraph.AppendChild(newRun);
        }
    }

    /// <summary>
    /// Thay các vùng text theo vị trí cụ thể trong paragraph.
    /// </summary>
    private void ReplaceParagraphOccurrences(
        Paragraph paragraph,
        List<OccurrenceReplacement> replacements)
    {
        var runs =
            paragraph
                .Elements<Run>()
                .ToList();

        if (runs.Count == 0)
        {
            return;
        }

        var runInfos =
            new List<RunInfo>();

        var fullText =
            string.Empty;

        foreach (var run in runs)
        {
            var text =
                string.Concat(
                    run
                        .Elements<Text>()
                        .Select(
                            x =>
                                x.Text ?? string.Empty
                        )
                );

            runInfos.Add(
                new RunInfo
                {
                    Run = run,
                    Text = text,
                    StartIndex = fullText.Length,
                    EndIndex =
                        fullText.Length +
                        text.Length
                }
            );

            fullText += text;
        }

        if (string.IsNullOrEmpty(fullText))
        {
            return;
        }

        var orderedReplacements =
            replacements
                .OrderBy(x => x.StartIndex)
                .ToList();

        var segments =
            new List<TextSegment>();

        var currentIndex = 0;

        foreach (var replacement in orderedReplacements)
        {
            if (replacement.StartIndex < currentIndex)
            {
                continue;
            }

            if (replacement.StartIndex > currentIndex)
            {
                var textBefore =
                    fullText.Substring(
                        currentIndex,
                        replacement.StartIndex -
                        currentIndex
                    );

                AddTextSegment(
                    segments,
                    textBefore,
                    currentIndex,
                    runInfos
                );
            }

            var runInfo =
                FindRunInfo(
                    replacement.StartIndex,
                    runInfos
                );

            segments.Add(
                new TextSegment
                {
                    Text =
                        replacement.Replacement,
                    RunProperties =
                        runInfo?.Run.RunProperties
                }
            );

            currentIndex =
                replacement.StartIndex +
                replacement.Length;
        }

        if (currentIndex < fullText.Length)
        {
            AddTextSegment(
                segments,
                fullText.Substring(
                    currentIndex
                ),
                currentIndex,
                runInfos
            );
        }

        foreach (var run in runs)
        {
            run.Remove();
        }

        foreach (var segment in segments)
        {
            var newRun =
                new Run();

            if (segment.RunProperties != null)
            {
                newRun.RunProperties =
                    (RunProperties)
                    segment.RunProperties.CloneNode(
                        true
                    );
            }

            var text =
                new Text(segment.Text)
                {
                    Space =
                        DocumentFormat.OpenXml
                            .SpaceProcessingModeValues
                            .Preserve
                };

            newRun.AppendChild(text);

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

            // Không còn placeholder.
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

            // -----------------------------------------------------
            // TEXT TRƯỚC PLACEHOLDER
            // -----------------------------------------------------

            if (foundIndex > currentIndex)
            {
                var textBefore =
                    fullText.Substring(
                        currentIndex,
                        foundIndex - currentIndex
                    );

                AddTextSegment(
                    segments,
                    textBefore,
                    currentIndex,
                    runInfos
                );
            }

            // -----------------------------------------------------
            // GIÁ TRỊ THAY THẾ
            // -----------------------------------------------------

            var replacementText =
                replacements[foundPlaceholder] ??
                string.Empty;

            replacementText =
                NormalizeReplacementValue(
                    replacementText
                );

            // -----------------------------------------------------
            // XÁC ĐỊNH RUN CỦA PLACEHOLDER
            // -----------------------------------------------------

            var placeholderRun =
                FindRunInfo(
                    foundIndex,
                    runInfos
                );

            // -----------------------------------------------------
            // QUAN TRỌNG:
            // BẢO ĐẢM KHOẢNG TRẮNG GIỮA TEXT VÀ GIÁ TRỊ AI
            // -----------------------------------------------------

            var textBeforePlaceholder =
                foundIndex > 0
                    ? fullText[foundIndex - 1]
                    : '\0';

            var placeholderEnd =
                foundIndex +
                foundPlaceholder.Length;

            var textAfterPlaceholder =
                placeholderEnd < fullText.Length
                    ? fullText[placeholderEnd]
                    : '\0';

            // Nếu trước placeholder là chữ/số
            // và giá trị AI bắt đầu bằng chữ/số,
            // thêm khoảng trắng.
            if (NeedsSpaceBetween(
                    textBeforePlaceholder,
                    replacementText.FirstOrDefault()))
            {
                replacementText =
                    " " +
                    replacementText;
            }

            // Nếu sau placeholder là chữ/số
            // và giá trị AI kết thúc bằng chữ/số,
            // thêm khoảng trắng.
            if (NeedsSpaceBetween(
                    replacementText.LastOrDefault(),
                    textAfterPlaceholder))
            {
                replacementText =
                    replacementText +
                    " ";
            }

            segments.Add(
                new TextSegment
                {
                    Text = replacementText,
                    RunProperties =
                        placeholderRun?
                            .Run
                            .RunProperties
                }
            );

            currentIndex =
                placeholderEnd;
        }

        return segments;
    }

    /// <summary>
    /// Kiểm tra xem giữa hai ký tự có cần thêm khoảng trắng hay không.
    /// </summary>
    private static bool NeedsSpaceBetween(
        char left,
        char right)
    {
        if (left == '\0' || right == '\0')
        {
            return false;
        }

        // Đã có khoảng trắng thì không thêm.
        if (char.IsWhiteSpace(left) ||
            char.IsWhiteSpace(right))
        {
            return false;
        }

        // Không thêm khoảng trắng trước/sau dấu câu.
        if (char.IsPunctuation(left) ||
            char.IsPunctuation(right))
        {
            return false;
        }

        // Không thêm khoảng trắng giữa các thành phần ngày tháng.
        if (left == '/' || right == '/')
        {
            return false;
        }

        // Không thêm khoảng trắng giữa số và dấu chấm.
        if (left == '.' || right == '.')
        {
            return false;
        }

        // Nếu hai bên đều là chữ hoặc số
        // thì khả năng cao cần khoảng trắng.
        return
            (char.IsLetterOrDigit(left) &&
             char.IsLetterOrDigit(right));
    }

    /// <summary>
    /// Chuẩn hóa giá trị do AI trả về trước khi chèn vào Word.
    /// </summary>
    /// <summary>
    /// Chuẩn hóa giá trị AI trả về trước khi chèn vào Word.
    /// Chủ yếu xử lý lỗi định dạng ngày tháng.
    /// </summary>
    private static string NormalizeReplacementValue(
        string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        value = value.Trim();

        // ---------------------------------------------------------
        // 1. Sửa ngày dạng:
        //
        // 03/09./2026
        // 03./09/2026
        // 03/09/./2026
        //
        // Thành:
        //
        // 03/09/2026
        // ---------------------------------------------------------

        value =
            Regex.Replace(
                value,
                @"(?<=\d)\s*\.\s*(?=/)",
                ""
            );

        value =
            Regex.Replace(
                value,
                @"(?<=/)\s*\.\s*(?=\d)",
                ""
            );

        // ---------------------------------------------------------
        // 2. Chuẩn hóa ngày đầy đủ:
        //
        // 01/09/2026.
        // 02/09/2026..
        //
        // Thành:
        //
        // 01/09/2026
        // 02/09/2026
        // ---------------------------------------------------------

        value =
            Regex.Replace(
                value,
                @"(\d{1,2}/\d{1,2}/\d{4})\.+",
                "$1"
            );

        return value;
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

        var currentIndex =
            startIndex;

        while (
            currentIndex <
            startIndex + text.Length)
        {
            var runInfo =
                FindRunInfo(
                    currentIndex,
                    runInfos
                );

            if (runInfo == null)
            {
                break;
            }

            var offsetInRun =
                currentIndex -
                runInfo.StartIndex;

            var remainingInRun =
                runInfo.Text.Length -
                offsetInRun;

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

            var segmentText =
                text.Substring(
                    currentIndex -
                    startIndex,
                    takeLength
                );

            segments.Add(
                new TextSegment
                {
                    Text =
                        segmentText,
                    RunProperties =
                        runInfo.Run
                            .RunProperties
                }
            );

            currentIndex +=
                takeLength;
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
    /// Đại diện cho một lần xuất hiện cụ thể
    /// của vùng cần thay thế.
    /// </summary>
    private class OccurrenceReplacement
    {
        public int StartIndex { get; set; }

        public int Length { get; set; }

        public string Replacement { get; set; } =
            string.Empty;
    }

    /// <summary>
    /// Thông tin của một Run trong Word.
    /// </summary>
    private class RunInfo
    {
        public Run Run { get; set; } = null!;

        public string Text { get; set; } =
            string.Empty;

        public int StartIndex { get; set; }

        public int EndIndex { get; set; }
    }

    /// <summary>
    /// Một đoạn text mới cùng với định dạng của nó.
    /// </summary>
    private class TextSegment
    {
        public string Text { get; set; } =
            string.Empty;

        public RunProperties? RunProperties { get; set; }
    }

    public class TemplateAnalysisResult
    {
        public string TemplateText { get; set; } =
            string.Empty;

        public List<TemplateField> Fields { get; set; } =
            new();
    }

    public class TemplateField
    {
        public string FieldId { get; set; } =
            string.Empty;

        public string OriginalText { get; set; } =
            string.Empty;
    }
}