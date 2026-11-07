import { useEffect, useState } from "react";
import {
  Download,
  FileSpreadsheet,
  FileText,
  FolderSearch,
  LoaderCircle,
  Search,
  Sparkles,
  X,
} from "lucide-react";

import SummaryModal from "../components/common/SummaryModal";
import { getApiMessage } from "../services/emailService";
import {
  SUMMARIZABLE_EXTENSIONS,
  downloadLocalFile,
  getRoots,
  searchFiles,
  summarizeLocalFile,
  askFile,
} from "../services/fileService";
import type {
  FileCategory,
  LocalFileItem,
  LocalFileSearchResponse,
  RootInfo,
  SummaryResult,
  AnswerResult,
} from "../services/fileService";

const CATEGORY_LABEL: Record<string, string> = {
  word: "Word",
  excel: "Excel / CSV",
  pdf: "PDF",
  powerpoint: "PowerPoint",
  text: "Văn bản thuần",
  other: "Khác",
};

const inputClass =
  "w-full rounded-xl border border-slate-300 bg-white px-4 py-3 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100";

function formatSize(bytes: number) {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
}

function formatDate(value: string) {
  // Backend trả UTC không có "Z" → thêm vào để JS hiểu đúng.
  const hasTimeZone = /([zZ]|[+-]\d{2}:\d{2})$/.test(value);

  return new Date(hasTimeZone ? value : value + "Z").toLocaleString("vi-VN", {
    dateStyle: "short",
    timeStyle: "short",
  });
}

function FileSearch() {
  const [roots, setRoots] = useState<RootInfo[]>([]);
  const [rootsLoaded, setRootsLoaded] = useState(false);

  const [keyword, setKeyword] = useState("");
  const [category, setCategory] = useState<FileCategory>("");
  const [fromDate, setFromDate] = useState("");
  const [toDate, setToDate] = useState("");
  const [searchContent, setSearchContent] = useState(false);
  const [rootIndex, setRootIndex] = useState<number | null>(null);

  const [result, setResult] = useState<LocalFileSearchResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  const [summaryTarget, setSummaryTarget] = useState<LocalFileItem | null>(
    null,
  );
  const [loadSummary, setLoadSummary] = useState<
    (() => Promise<SummaryResult>) | null
  >(null);

  const [askFn, setAskFn] = useState<
    ((question: string) => Promise<AnswerResult>) | null
  >(null);

  useEffect(() => {
    async function fetchRoots() {
      try {
        setRoots(await getRoots());
      } catch (err) {
        setError(getApiMessage(err, "Không thể tải danh sách thư mục."));
      } finally {
        setRootsLoaded(true);
      }
    }

    void fetchRoots();
  }, []);

  const handleSearch = async (event?: React.FormEvent) => {
    event?.preventDefault();

    if (fromDate && toDate && toDate < fromDate) {
      setError("Ngày kết thúc phải sau hoặc bằng ngày bắt đầu.");
      return;
    }

    // Chuyển ngày chọn (giờ địa phương) sang ISO; "đến ngày" gồm cả ngày đó.
    const from = fromDate
      ? new Date(`${fromDate}T00:00:00`).toISOString()
      : null;

    let to: string | null = null;

    if (toDate) {
      const end = new Date(`${toDate}T00:00:00`);
      end.setDate(end.getDate() + 1);
      to = end.toISOString();
    }

    try {
      setLoading(true);
      setError("");

      setResult(
        await searchFiles({
          keyword,
          category,
          from,
          to,
          searchContent,
          rootIndex,
        }),
      );
    } catch (err) {
      setResult(null);
      setError(getApiMessage(err, "Không thể tìm kiếm file."));
    } finally {
      setLoading(false);
    }
  };

  const handleReset = () => {
    setKeyword("");
    setCategory("");
    setFromDate("");
    setToDate("");
    setSearchContent(false);
    setRootIndex(null);
    setResult(null);
    setError("");
  };

  const openSummary = (item: LocalFileItem) => {
    setSummaryTarget(item);
    setLoadSummary(() => () => summarizeLocalFile(item));
    setAskFn(() => (question: string) => askFile(item, question));
  };

  const closeSummary = () => {
    setSummaryTarget(null);
    setLoadSummary(null);
    setAskFn(null);
  };

  const handleDownload = async (item: LocalFileItem) => {
    try {
      setError("");
      await downloadLocalFile(item);
    } catch (err) {
      setError(getApiMessage(err, "Không thể tải file."));
    }
  };

  return (
    <main className="p-6">
      {/* Tiêu đề */}
      <div className="mb-6 flex items-center gap-3">
        <div className="rounded-xl bg-blue-100 p-3 text-blue-600">
          <FolderSearch size={26} />
        </div>

        <div>
          <h1 className="text-2xl font-bold text-slate-800">
            Tìm văn bản trên máy
          </h1>
          <p className="text-sm text-slate-500">
            Tìm file theo tên, loại file, ngày chỉnh sửa hoặc nội dung.
          </p>
        </div>
      </div>

      {rootsLoaded && roots.length === 0 && (
        <div className="mb-5 rounded-xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800">
          Chưa cấu hình thư mục để tìm. Hãy thêm <b>FileSearch:Roots</b> trong
          cấu hình backend rồi chạy lại.
        </div>
      )}

      {error && (
        <div
          role="alert"
          className="mb-5 flex items-center justify-between rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700"
        >
          <span>{error}</span>
          <button type="button" onClick={() => setError("")}>
            <X size={17} />
          </button>
        </div>
      )}

      {/* Bộ lọc */}
      <form
        onSubmit={(event) => void handleSearch(event)}
        className="mb-6 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm"
      >
        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
          <div className="md:col-span-2">
            <label className="mb-1.5 block text-xs font-medium text-slate-600">
              Từ khóa (không cần gõ dấu)
            </label>
            <div className="relative">
              <Search
                size={18}
                className="absolute left-3 top-1/2 -translate-y-1/2 text-slate-400"
              />
              <input
                type="text"
                value={keyword}
                onChange={(event) => setKeyword(event.target.value)}
                maxLength={200}
                placeholder="Ví dụ: cong van nghi le"
                className={`${inputClass} pl-10`}
              />
            </div>
          </div>

          <div>
            <label className="mb-1.5 block text-xs font-medium text-slate-600">
              Loại file
            </label>
            <select
              value={category}
              onChange={(event) =>
                setCategory(event.target.value as FileCategory)
              }
              className={inputClass}
            >
              <option value="">Tất cả</option>
              <option value="word">Word</option>
              <option value="excel">Excel / CSV</option>
              <option value="pdf">PDF</option>
              <option value="powerpoint">PowerPoint</option>
              <option value="text">Văn bản thuần (.txt, .md)</option>
            </select>
          </div>

          {roots.length > 1 && (
            <div>
              <label className="mb-1.5 block text-xs font-medium text-slate-600">
                Thư mục
              </label>
              <select
                value={rootIndex ?? ""}
                onChange={(event) =>
                  setRootIndex(
                    event.target.value === ""
                      ? null
                      : Number(event.target.value),
                  )
                }
                className={inputClass}
              >
                <option value="">Tất cả thư mục</option>
                {roots.map((root) => (
                  <option key={root.index} value={root.index}>
                    {root.name}
                  </option>
                ))}
              </select>
            </div>
          )}

          <div>
            <label className="mb-1.5 block text-xs font-medium text-slate-600">
              Sửa đổi từ ngày
            </label>
            <input
              type="date"
              value={fromDate}
              onChange={(event) => setFromDate(event.target.value)}
              className={inputClass}
            />
          </div>

          <div>
            <label className="mb-1.5 block text-xs font-medium text-slate-600">
              Đến ngày
            </label>
            <input
              type="date"
              value={toDate}
              onChange={(event) => setToDate(event.target.value)}
              className={inputClass}
            />
          </div>

          <label className="flex cursor-pointer items-center gap-2 self-end pb-3 md:col-span-2">
            <input
              type="checkbox"
              checked={searchContent}
              onChange={(event) => setSearchContent(event.target.checked)}
              className="h-4 w-4 rounded border-slate-300"
            />
            <span className="text-sm text-slate-700">
              Tìm cả trong nội dung (.docx, .txt, .md, .csv — chậm hơn)
            </span>
          </label>
        </div>

        <div className="mt-5 flex justify-end gap-3">
          <button
            type="button"
            onClick={handleReset}
            disabled={loading}
            className="rounded-xl border border-slate-200 px-5 py-2.5 text-sm font-medium text-slate-600 hover:bg-slate-50 disabled:opacity-50"
          >
            Xóa bộ lọc
          </button>

          <button
            type="submit"
            disabled={loading || roots.length === 0}
            className="inline-flex items-center gap-2 rounded-xl bg-blue-600 px-5 py-2.5 text-sm font-semibold text-white hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-60"
          >
            {loading ? (
              <LoaderCircle size={16} className="animate-spin" />
            ) : (
              <Search size={16} />
            )}
            {loading ? "Đang tìm..." : "Tìm kiếm"}
          </button>
        </div>
      </form>

      {/* Kết quả */}
      {result && (
        <section className="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
          <div className="border-b border-slate-200 px-5 py-4">
            <h2 className="font-semibold text-slate-800">Kết quả</h2>
            <p className="mt-1 text-sm text-slate-500">
              {result.totalMatched} file phù hợp
              {result.totalMatched > result.items.length &&
                ` (hiển thị ${result.items.length} file mới sửa nhất)`}
            </p>

            {result.truncated && (
              <p className="mt-2 text-xs text-amber-700">
                Thư mục quá lớn nên quá trình tìm đã dừng sớm. Hãy thu hẹp bộ
                lọc để có kết quả đầy đủ.
              </p>
            )}
          </div>

          {result.items.length === 0 ? (
            <div className="py-14 text-center text-sm text-slate-500">
              Không tìm thấy file phù hợp.
            </div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full min-w-[800px] text-left text-sm">
                <thead>
                  <tr className="border-b border-slate-200 bg-slate-50 text-slate-500">
                    <th className="px-5 py-3 font-medium">Tên file</th>
                    <th className="px-5 py-3 font-medium">Loại</th>
                    <th className="px-5 py-3 font-medium">Dung lượng</th>
                    <th className="px-5 py-3 font-medium">Sửa đổi</th>
                    <th className="px-5 py-3 text-right font-medium">
                      Thao tác
                    </th>
                  </tr>
                </thead>

                <tbody>
                  {result.items.map((item) => {
                    const canSummarize = SUMMARIZABLE_EXTENSIONS.includes(
                      item.extension,
                    );

                    return (
                      <tr
                        key={`${item.rootIndex}:${item.relativePath}`}
                        className="border-b border-slate-100 last:border-0 hover:bg-slate-50"
                      >
                        <td className="px-5 py-4">
                          <div className="flex items-center gap-3">
                            <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-blue-50 text-blue-600">
                              {item.category === "excel" ? (
                                <FileSpreadsheet size={18} />
                              ) : (
                                <FileText size={18} />
                              )}
                            </div>

                            <div className="min-w-0">
                              <p className="truncate font-medium text-slate-700">
                                {item.name}
                              </p>
                              <p className="mt-0.5 truncate text-xs text-slate-400">
                                {item.folder || "(thư mục gốc)"}
                              </p>
                            </div>
                          </div>
                        </td>

                        <td className="px-5 py-4">
                          <span className="rounded-full bg-blue-50 px-3 py-1 text-xs font-medium text-blue-700">
                            {CATEGORY_LABEL[item.category] ?? "Khác"}
                          </span>
                        </td>

                        <td className="px-5 py-4 text-slate-500">
                          {formatSize(item.size)}
                        </td>

                        <td className="px-5 py-4 text-slate-500">
                          {formatDate(item.modifiedAtUtc)}
                        </td>

                        <td className="px-5 py-4">
                          <div className="flex justify-end gap-2">
                            <button
                              type="button"
                              onClick={() => openSummary(item)}
                              disabled={!canSummarize}
                              title={
                                canSummarize
                                  ? "Tóm tắt ý chính bằng AI"
                                  : "Chỉ tóm tắt được .docx, .txt, .md, .csv"
                              }
                              className="inline-flex items-center gap-1.5 rounded-lg border border-blue-200 bg-blue-50 px-3 py-1.5 text-xs font-medium text-blue-700 hover:bg-blue-100 disabled:cursor-not-allowed disabled:opacity-40"
                            >
                              <Sparkles size={14} />
                              Tóm tắt
                            </button>

                            <button
                              type="button"
                              onClick={() => void handleDownload(item)}
                              className="rounded-lg p-2 text-slate-500 hover:bg-blue-50 hover:text-blue-600"
                              title="Tải về"
                            >
                              <Download size={17} />
                            </button>
                          </div>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
        </section>
      )}

      {summaryTarget && loadSummary && (
        <SummaryModal
          title={summaryTarget.name}
          load={loadSummary}
          onClose={closeSummary}
          ask={askFn ?? undefined}
        />
      )}
    </main>
  );
}

export default FileSearch;
