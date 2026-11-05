import { useEffect, useRef, useState } from "react";
import { Check, Copy, LoaderCircle, Sparkles, X } from "lucide-react";

import { getApiMessage } from "../../services/emailService";
import type { SummaryResult } from "../../services/fileService";

type SummaryModalProps = {
  title: string;
  load: () => Promise<SummaryResult>;
  onClose: () => void;
};

// Component được render có điều kiện nên mỗi lần mở là một lần gọi AI.
function SummaryModal({ title, load, onClose }: SummaryModalProps) {
  const [result, setResult] = useState<SummaryResult | null>(null);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);
  const [copied, setCopied] = useState(false);

  const loadRef = useRef(load);
  const startedRef = useRef(false);

  useEffect(() => {
    // Chặn gọi AI hai lần do React StrictMode (tiết kiệm quota Gemini).
    if (startedRef.current) return;
    startedRef.current = true;

    async function run() {
      try {
        setResult(await loadRef.current());
      } catch (err) {
        setError(getApiMessage(err, "Không thể tóm tắt văn bản."));
      } finally {
        setLoading(false);
      }
    }

    void run();
  }, []);

  const handleCopy = async () => {
    if (!result) return;

    try {
      await navigator.clipboard.writeText(result.summary);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      setError("Không thể sao chép. Hãy chọn nội dung và nhấn Ctrl + C.");
    }
  };

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/40 px-4"
      onClick={onClose}
    >
      <div
        className="max-h-[90vh] w-full max-w-2xl overflow-y-auto rounded-2xl bg-white p-6 shadow-2xl"
        onClick={(event) => event.stopPropagation()}
      >
        <div className="flex items-start justify-between gap-3">
          <div className="min-w-0">
            <h3 className="flex items-center gap-2 text-lg font-bold text-slate-800">
              <Sparkles size={18} className="text-blue-600" />
              Tóm tắt các ý chính
            </h3>
            <p className="mt-1 truncate text-sm text-slate-500">{title}</p>
          </div>

          <button
            type="button"
            onClick={onClose}
            className="rounded-lg p-1.5 text-slate-400 hover:bg-slate-100"
          >
            <X size={18} />
          </button>
        </div>

        <div className="mt-5">
          {loading ? (
            <div className="flex items-center justify-center gap-2 py-12 text-sm text-slate-500">
              <LoaderCircle size={22} className="animate-spin text-blue-600" />
              AI đang đọc và tóm tắt...
            </div>
          ) : error ? (
            <div className="rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
              {error}
            </div>
          ) : (
            result && (
              <>
                <div className="whitespace-pre-wrap rounded-xl bg-slate-50 p-4 text-sm leading-6 text-slate-700">
                  {result.summary}
                </div>

                {result.truncated && (
                  <p className="mt-3 rounded-lg bg-amber-50 p-3 text-xs text-amber-800">
                    Văn bản dài nên AI chỉ tóm tắt phần đầu (khoảng 12.000 ký
                    tự).
                  </p>
                )}

                <p className="mt-3 text-xs text-slate-400">
                  Bản tóm tắt do AI tạo, hãy đối chiếu với văn bản gốc khi cần
                  độ chính xác.
                </p>
              </>
            )
          )}
        </div>

        <div className="mt-6 flex justify-end gap-3">
          {result && (
            <button
              type="button"
              onClick={() => void handleCopy()}
              className="inline-flex items-center gap-2 rounded-xl border border-slate-200 px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50"
            >
              {copied ? <Check size={16} /> : <Copy size={16} />}
              {copied ? "Đã sao chép" : "Sao chép"}
            </button>
          )}

          <button
            type="button"
            onClick={onClose}
            className="rounded-xl bg-blue-600 px-5 py-2 text-sm font-semibold text-white hover:bg-blue-700"
          >
            Đóng
          </button>
        </div>
      </div>
    </div>
  );
}

export default SummaryModal;
