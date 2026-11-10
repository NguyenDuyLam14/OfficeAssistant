import { useRef, useState } from "react";
import {
  FileUp,
  LoaderCircle,
  Presentation,
  Sparkles,
  FileDown,
  X,
} from "lucide-react";

import PresentationModal from "../presentation/PresentationModal";
import { getApiMessage } from "../../services/emailService";
import { summarizeUploadedFile } from "../../services/fileService";
import type { UploadSummaryResult } from "../../services/fileService";
import { exportSummaryDocx } from "../../services/presentationService";

type Props = {
  onSaved: () => void | Promise<void>;
};

type PptSource = {
  title: string;
  documentId?: number;
  content?: string;
};

function downloadBlob(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = fileName;
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
}

export default function UploadSummaryPanel({ onSaved }: Props) {
  const inputRef = useRef<HTMLInputElement | null>(null);

  const [result, setResult] = useState<UploadSummaryResult | null>(null);
  const [loading, setLoading] = useState(false);
  const [exporting, setExporting] = useState(false);
  const [error, setError] = useState("");

  // Id văn bản Word tóm tắt sau khi xuất (để làm PPT từ chính file đó).
  const [summaryDocId, setSummaryDocId] = useState<number | null>(null);

  // Dữ liệu khác để làm PPT.
  const [otherData, setOtherData] = useState("");

  const [ppt, setPpt] = useState<PptSource | null>(null);

  const baseName = result?.fileName.replace(/\.[^.]+$/, "") ?? "tai_lieu";

  const handleFile = async (file: File) => {
    setLoading(true);
    setError("");
    setResult(null);
    setSummaryDocId(null);

    try {
      setResult(await summarizeUploadedFile(file));
    } catch (err) {
      setError(getApiMessage(err, "Không thể tóm tắt file."));
    } finally {
      setLoading(false);
      if (inputRef.current) inputRef.current.value = "";
    }
  };

  const handleExportWord = async () => {
    if (!result) return;

    try {
      setExporting(true);
      setError("");

      const { blob, documentId } = await exportSummaryDocx(
        `Tóm tắt ${baseName}`,
        result.summary,
      );

      setSummaryDocId(documentId);
      downloadBlob(blob, `TomTat_${baseName}.docx`);
      await onSaved(); // văn bản mới xuất hiện trong danh sách
    } catch (err) {
      setError(getApiMessage(err, "Không thể xuất file Word."));
    } finally {
      setExporting(false);
    }
  };

  return (
    <section className="mb-6 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
      <div className="flex items-center gap-3">
        <div className="flex h-11 w-11 items-center justify-center rounded-xl bg-blue-100 text-blue-700">
          <Sparkles size={22} />
        </div>
        <div>
          <h2 className="text-lg font-bold text-slate-800">
            Tóm tắt file & tạo báo cáo PowerPoint
          </h2>
          <p className="mt-1 text-sm text-slate-500">
            Tải file lên → AI tóm tắt → xuất Word → tạo PPT báo cáo.
          </p>
        </div>
      </div>

      <input
        ref={inputRef}
        type="file"
        accept=".docx,.pdf,.txt,.md,.csv"
        className="hidden"
        onChange={(e) => {
          const file = e.target.files?.[0];
          if (file) void handleFile(file);
        }}
      />

      {error && (
        <div className="mt-4 flex items-center justify-between rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
          <span>{error}</span>
          <button type="button" onClick={() => setError("")}>
            <X size={16} />
          </button>
        </div>
      )}

      {/* Bước 1: chọn file */}
      <div className="mt-4 flex flex-wrap items-center gap-3">
        <button
          type="button"
          onClick={() => inputRef.current?.click()}
          disabled={loading}
          className="inline-flex items-center gap-2 rounded-xl bg-blue-600 px-5 py-2.5 text-sm font-semibold text-white hover:bg-blue-700 disabled:opacity-60"
        >
          {loading ? (
            <LoaderCircle size={16} className="animate-spin" />
          ) : (
            <FileUp size={16} />
          )}
          {loading ? "AI đang đọc và tóm tắt..." : "Chọn file để tóm tắt"}
        </button>
        <span className="text-xs text-slate-500">
          Hỗ trợ .docx, .pdf, .txt, .md, .csv — tối đa 10 MB
        </span>
      </div>

      {/* Bước 2: kết quả tóm tắt */}
      {result && (
        <div className="mt-5">
          <p className="mb-2 text-sm font-medium text-slate-700">
            Bản tóm tắt: {result.fileName}
          </p>

          <div className="whitespace-pre-wrap rounded-xl bg-slate-50 p-4 text-sm leading-6 text-slate-700">
            {result.summary}
          </div>

          {result.truncated && (
            <p className="mt-2 rounded-lg bg-amber-50 p-3 text-xs text-amber-800">
              File dài nên AI chỉ tóm tắt phần đầu (khoảng 12.000 ký tự).
            </p>
          )}

          {summaryDocId && (
            <p className="mt-2 text-xs text-green-700">
              Đã lưu vào danh sách văn bản (ID: {summaryDocId}).
            </p>
          )}

          <div className="mt-4 flex flex-wrap gap-2">
            <button
              type="button"
              onClick={() => void handleExportWord()}
              disabled={exporting}
              className="inline-flex items-center gap-2 rounded-xl border border-slate-200 px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-60"
            >
              {exporting ? (
                <LoaderCircle size={16} className="animate-spin" />
              ) : (
                <FileDown size={16} />
              )}
              Xuất Word tóm tắt
            </button>

            <button
              type="button"
              onClick={() =>
                setPpt(
                  summaryDocId
                    ? {
                        title: `Tóm tắt ${baseName}`,
                        documentId: summaryDocId,
                      }
                    : {
                        title: `Tóm tắt ${baseName}`,
                        content: result.summary,
                      },
                )
              }
              className="inline-flex items-center gap-2 rounded-xl bg-blue-600 px-4 py-2 text-sm font-semibold text-white hover:bg-blue-700"
            >
              <Presentation size={16} />
              Tạo PPT từ bản tóm tắt
            </button>

            <button
              type="button"
              onClick={() => setPpt({ title: baseName, content: result.text })}
              className="inline-flex items-center gap-2 rounded-xl border border-blue-200 bg-blue-50 px-4 py-2 text-sm font-medium text-blue-700 hover:bg-blue-100"
            >
              <Presentation size={16} />
              Tạo PPT từ nội dung gốc
            </button>
          </div>
        </div>
      )}

      {/* PPT từ dữ liệu khác */}
      <div className="mt-6 border-t border-slate-200 pt-5">
        <p className="text-sm font-medium text-slate-700">
          Hoặc tạo PPT từ dữ liệu khác
        </p>
        <textarea
          rows={4}
          value={otherData}
          onChange={(e) => setOtherData(e.target.value)}
          placeholder="Dán số liệu, ghi chú, nội dung báo cáo... (tối thiểu 50 ký tự)"
          className="mt-2 w-full resize-y rounded-xl border border-slate-300 px-4 py-3 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100"
        />
        <button
          type="button"
          disabled={otherData.trim().length < 50}
          onClick={() =>
            setPpt({ title: "Báo cáo", content: otherData.trim() })
          }
          className="mt-2 inline-flex items-center gap-2 rounded-xl border border-slate-200 px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-50"
        >
          <Presentation size={16} />
          Tạo PPT từ dữ liệu này
        </button>
      </div>

      {ppt && (
        <PresentationModal
          key={`${ppt.documentId ?? "c"}-${ppt.title}`}
          title={ppt.title}
          documentId={ppt.documentId}
          content={ppt.content}
          onClose={() => setPpt(null)}
        />
      )}
    </section>
  );
}
