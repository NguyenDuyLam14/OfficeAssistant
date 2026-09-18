using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace OfficeAssistant.API.Services.Word;

/// <summary>
/// Service xử lý file Word mẫu (.docx).
///
/// Chức năng:
/// - Mở file Word mẫu.
/// - Tìm placeholder dạng {{TEN_PLACEHOLDER}}.
/// - Thay placeholder bằng nội dung thực tế.
/// - Lưu thành file Word mới.
///
/// Ví dụ:
/// {{TIEU_DE}}
/// {{NOI_DUNG}}
/// {{NGAY}}
/// {{THANG}}
/// {{NAM}}
/// </summary>
public class WordTemplateService
{
    /// <summary>
    /// Tạo file Word mới từ file Word mẫu.
    /// </summary>
    /// <param name="templateFilePath">
    /// Đường dẫn vật lý tới file Word mẫu.
    /// </param>
    /// <param name="outputFilePath">
    /// Đường dẫn vật lý tới file Word kết quả.
    /// </param>
    /// <param name="replacements">
    /// Danh sách placeholder và nội dung thay thế.
    /// </param>
    public void GenerateDocument(
        string templateFilePath,
        string outputFilePath,
        Dictionary<string, string> replacements)
    {
        // =====================================================
        // 1. KIỂM TRA FILE MẪU
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
        // 3. XÓA FILE OUTPUT CŨ NẾU CÓ
        // =====================================================

        if (File.Exists(outputFilePath))
        {
            File.Delete(outputFilePath);
        }

        // =====================================================
        // 4. COPY FILE MẪU
        // =====================================================
        //
        // Không sửa trực tiếp file mẫu.
        //
        // Ví dụ:
        //
        // Template:
        // Storage/Templates/mau.docx
        //
        // Output:
        // Storage/Generated/result.docx
        //
        // =====================================================

        File.Copy(
            templateFilePath,
            outputFilePath
        );

        // =====================================================
        // 5. MỞ FILE WORD KẾT QUẢ BẰNG OPENXML
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

        // Lấy tài liệu Word chính từ MainDocumentPart.
        var documentElement =
            mainPart.Document;

        // Kiểm tra Document có tồn tại hay không.
        if (documentElement == null)
        {
            throw new InvalidOperationException(
                "File Word không có Document hợp lệ."
            );
        }

        // =====================================================
        // 8. LẤY BODY
        // =====================================================

        // Sau khi đã kiểm tra Document,
        // chúng ta mới lấy Body của tài liệu.
        var body =
            documentElement.Body;

        // Kiểm tra Body có tồn tại hay không.
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
            body.Descendants<Paragraph>()
                .ToList();

        // =====================================================
        // 10. DUYỆT TỪNG PARAGRAPH
        // =====================================================

        foreach (var paragraph in paragraphs)
        {
            // -------------------------------------------------
            // Lấy danh sách Run.
            // -------------------------------------------------

            var runs =
                paragraph.Descendants<Run>()
                    .ToList();

            if (runs.Count == 0)
            {
                continue;
            }

            // -------------------------------------------------
            // Ghép toàn bộ Text trong paragraph.
            //
            // Word có thể chia:
            //
            // {{TIEU_DE}}
            //
            // thành nhiều Run.
            //
            // Vì vậy phải ghép chúng lại.
            // -------------------------------------------------

            var textParts =
                new List<string>();

            foreach (var run in runs)
            {
                var texts =
                    run.Elements<Text>();

                foreach (var text in texts)
                {
                    if (text != null)
                    {
                        textParts.Add(text.Text);
                    }
                }
            }

            var fullText =
                string.Concat(textParts);

            if (string.IsNullOrEmpty(fullText))
            {
                continue;
            }

            // =================================================
            // 11. THAY PLACEHOLDER
            // =================================================

            foreach (var replacement in replacements)
            {
                var placeholder =
                    replacement.Key;

                var replacementText =
                    replacement.Value ?? string.Empty;

                if (fullText.Contains(
                        placeholder,
                        StringComparison.Ordinal))
                {
                    fullText =
                        fullText.Replace(
                            placeholder,
                            replacementText,
                            StringComparison.Ordinal
                        );
                }
            }

            // =================================================
            // 12. XÓA TEXT CŨ
            // =================================================

            foreach (var run in runs)
            {
                var textElements =
                    run.Elements<Text>()
                        .ToList();

                foreach (var textElement in textElements)
                {
                    textElement.Text =
                        string.Empty;
                }
            }

            // =================================================
            // 13. LẤY RUN ĐẦU TIÊN
            // =================================================

            var firstRun =
                runs.FirstOrDefault();

            if (firstRun == null)
            {
                continue;
            }

            // =================================================
            // 14. TÌM TEXT ĐẦU TIÊN
            // =================================================

            var firstText =
                firstRun
                    .Elements<Text>()
                    .FirstOrDefault();

            // =================================================
            // 15. CẬP NHẬT TEXT
            // =================================================

            if (firstText != null)
            {
                // Đã có phần tử Text → cập nhật nội dung.
                firstText.Text = fullText;
            }
            else
            {
                // Chưa có Text → tạo một phần tử Text mới.
                var newText = new Text
                {
                    Text = fullText
                };

                firstRun.AppendChild(newText);
            }
        }

        // =====================================================
        // 16. LƯU FILE
        // =====================================================

        // Lưu lại nội dung Word sau khi thay thế placeholder.
        documentElement.Save();
    }
}