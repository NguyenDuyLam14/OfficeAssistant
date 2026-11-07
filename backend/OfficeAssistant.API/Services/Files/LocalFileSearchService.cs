using System.Globalization;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace OfficeAssistant.API.Services.Files;

public sealed record RootInfo(int Index, string Name);

public sealed record LocalFileItem(
    int RootIndex,
    string RelativePath,
    string Name,
    string Extension,
    string Category,
    long Size,
    DateTime ModifiedAtUtc,
    string Folder);

public sealed record LocalFileSearchResult(
    List<LocalFileItem> Items,
    int TotalMatched,
    int Scanned,
    bool Truncated);

public sealed class LocalFileSearchQuery
{
    public string? Keyword { get; set; }
    public string? Category { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public bool SearchContent { get; set; }
    public int? RootIndex { get; set; }
}

/// <summary>
/// Tìm file văn bản trong các thư mục được cấu hình sẵn (FileSearch:Roots).
/// Chỉ làm việc bên trong các thư mục này, không nhận đường dẫn tùy ý từ client.
/// </summary>
public sealed class LocalFileSearchService
{
    public static readonly Dictionary<string, string[]> Categories =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["word"] = new[] { ".doc", ".docx" },
            ["excel"] = new[] { ".xls", ".xlsx", ".xlsm", ".csv" },
            ["pdf"] = new[] { ".pdf" },
            ["powerpoint"] = new[] { ".ppt", ".pptx" },
            ["text"] = new[] { ".txt", ".md" }
        };

    // Các định dạng đọc được nội dung (để tìm theo nội dung và tóm tắt).
    private static readonly HashSet<string> TextExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".docx", ".txt", ".md", ".csv" };

    private const long MaxContentBytes = 5 * 1024 * 1024;
    private const int MaxFilesScanned = 100_000;
    private const int MaxContentScans = 2000;
    private static readonly TimeSpan MaxDuration = TimeSpan.FromSeconds(20);

    private readonly string[] _roots;
    private readonly int _maxResults;

    public LocalFileSearchService(IConfiguration configuration)
    {
        var configured = configuration.GetSection("FileSearch:Roots")
            .Get<string[]>() ?? Array.Empty<string>();

        foreach (var path in configured)
        {
            Console.WriteLine(
                $"[FileSearch] Cấu hình: '{path}' | Tồn tại: {Directory.Exists(path)}");
        }

        if (configured.Length == 0)
        {
            Console.WriteLine("[FileSearch] Không đọc được FileSearch:Roots.");
        }

        _roots = configured
            .Where(path => !string.IsNullOrWhiteSpace(path) &&
                           Directory.Exists(path))
            .Select(Path.GetFullPath)
            .ToArray();

        _maxResults = Math.Clamp(
            configuration.GetValue("FileSearch:MaxResults", 100), 1, 500);
    }

    public IReadOnlyList<RootInfo> GetRoots() =>
        _roots.Select((path, index) =>
        {
            var name = Path.GetFileName(
                path.TrimEnd(Path.DirectorySeparatorChar));

            return new RootInfo(index, string.IsNullOrEmpty(name) ? path : name);
        }).ToList();

    public static bool IsSummarizable(string path) =>
        TextExtensions.Contains(Path.GetExtension(path));

    // ---------------------------------------------------------
    // Chuyển (rootIndex + đường dẫn tương đối) thành đường dẫn thật,
    // bảo đảm file nằm trong thư mục gốc (chống ../).
    // ---------------------------------------------------------
    public string? ResolvePath(int rootIndex, string? relativePath)
    {
        if (rootIndex < 0 || rootIndex >= _roots.Length ||
            string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        var root = _roots[rootIndex];

        var full = Path.GetFullPath(Path.Combine(root, relativePath));

        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;

        if (!full.StartsWith(
                rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return File.Exists(full) ? full : null;
    }

    public Task<LocalFileSearchResult> SearchAsync(
        LocalFileSearchQuery query, CancellationToken cancellationToken) =>
        Task.Run(() => Search(query, cancellationToken), cancellationToken);

    private LocalFileSearchResult Search(
        LocalFileSearchQuery query, CancellationToken cancellationToken)
    {
        var extensions = ResolveExtensions(query.Category);
        var keyword = Normalize(query.Keyword);
        var hasKeyword = keyword.Length > 0;

        var started = DateTime.UtcNow;
        var matches = new List<LocalFileItem>();
        var scanned = 0;
        var contentScans = 0;
        var truncated = false;

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.Hidden |
                               FileAttributes.System |
                               FileAttributes.ReparsePoint
        };

        for (var rootIndex = 0; rootIndex < _roots.Length && !truncated; rootIndex++)
        {
            if (query.RootIndex.HasValue && query.RootIndex.Value != rootIndex)
            {
                continue;
            }

            var root = _roots[rootIndex];

            foreach (var path in Directory.EnumerateFiles(root, "*", options))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (++scanned > MaxFilesScanned ||
                    DateTime.UtcNow - started > MaxDuration)
                {
                    truncated = true;
                    break;
                }

                var name = Path.GetFileName(path);

                // Bỏ file tạm của Word/Excel.
                if (name.StartsWith("~$", StringComparison.Ordinal))
                {
                    continue;
                }

                var extension = Path.GetExtension(path).ToLowerInvariant();

                if (!extensions.Contains(extension))
                {
                    continue;
                }

                var info = new FileInfo(path);
                var modified = info.LastWriteTimeUtc;

                if (query.FromUtc.HasValue && modified < query.FromUtc.Value)
                {
                    continue;
                }

                if (query.ToUtc.HasValue && modified >= query.ToUtc.Value)
                {
                    continue;
                }

                if (hasKeyword)
                {
                    var matched = Normalize(name).Contains(keyword);

                    if (!matched &&
                        query.SearchContent &&
                        TextExtensions.Contains(extension) &&
                        info.Length <= MaxContentBytes &&
                        contentScans < MaxContentScans)
                    {
                        contentScans++;
                        matched = ContentContains(path, keyword);
                    }

                    if (!matched)
                    {
                        continue;
                    }
                }

                var relative = Path.GetRelativePath(root, path);
                var folder = Path.GetDirectoryName(relative) ?? string.Empty;

                matches.Add(new LocalFileItem(
                    rootIndex,
                    relative,
                    name,
                    extension,
                    GetCategory(extension),
                    info.Length,
                    modified,
                    folder));
            }
        }

        var items = matches
            .OrderByDescending(x => x.ModifiedAtUtc)
            .Take(_maxResults)
            .ToList();

        return new LocalFileSearchResult(
            items, matches.Count, scanned, truncated);
    }

    // ---------------------------------------------------------
    // Đọc nội dung văn bản (.docx, .txt, .md, .csv).
    // Mở bằng FileShare.ReadWrite để đọc được cả khi file đang mở trong Word.
    // ---------------------------------------------------------
    public string ExtractText(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();

        using var file = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        if (extension == ".docx")
        {
            using var memory = new MemoryStream();
            file.CopyTo(memory);
            memory.Position = 0;

            using var document = WordprocessingDocument.Open(memory, false);

            var body = document.MainDocumentPart?.Document?.Body;

            if (body == null)
            {
                return string.Empty;
            }

            return string.Join(
                Environment.NewLine,
                body.Descendants<Paragraph>()
                    .Select(p => p.InnerText)
                    .Where(t => !string.IsNullOrWhiteSpace(t)));
        }

        if (extension is ".txt" or ".md" or ".csv")
        {
            using var reader = new StreamReader(file, Encoding.UTF8, true);
            return reader.ReadToEnd();
        }

        throw new NotSupportedException(
            "Định dạng này chưa hỗ trợ đọc nội dung.");
    }

    private bool ContentContains(string path, string keyword)
    {
        try
        {
            return Normalize(ExtractText(path)).Contains(keyword);
        }
        catch
        {
            // File lỗi/bị khóa: bỏ qua, không làm hỏng cả lượt tìm.
            return false;
        }
    }

    private static HashSet<string> ResolveExtensions(string? category)
    {
        if (!string.IsNullOrWhiteSpace(category) &&
            Categories.TryGetValue(category, out var list))
        {
            return new HashSet<string>(list);
        }

        return new HashSet<string>(Categories.Values.SelectMany(x => x));
    }

    private static string GetCategory(string extension) =>
        Categories.FirstOrDefault(x => x.Value.Contains(extension)).Key
        ?? "other";

    // Bỏ dấu tiếng Việt để gõ "cong van" vẫn tìm ra "Công văn".
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decomposed = value.Trim()
            .ToLowerInvariant()
            .Replace('đ', 'd')
            .Normalize(NormalizationForm.FormD);

        var builder = new StringBuilder(decomposed.Length);

        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) !=
                UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}