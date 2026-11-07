import { useEffect, useRef, useState } from "react";
import type { FormEvent } from "react";
import { Check, Copy, LoaderCircle, Send, Sparkles, X } from "lucide-react";

import { getApiMessage } from "../../services/emailService";
import type { AnswerResult, SummaryResult } from "../../services/fileService";

type SummaryModalProps = {
  title: string;
  load: () => Promise<SummaryResult>;
  // Nếu có, hiển thị khung hỏi đáp bên dưới bản tóm tắt.
  ask?: (question: string) => Promise<AnswerResult>;
  onClose: () => void;
};

type QaItem = { question: string; answer: string };

// Component được render có điều kiện nên mỗi lần mở là một lần gọi AI.
function SummaryModal({ title, load, ask, onClose }: SummaryModalProps) {
  const [result, setResult] = useState<SummaryResult | null>(null);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);
  const [copied, setCopied] = useState(false);

  const [question, setQuestion] = useState("");
  const [qaItems, setQaItems] = useState<QaItem[]>([]);
  const [asking, setAsking] = useState(false);
  const [askError, setAskError] = useState("");

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

  const handleAsk = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const text = question.trim();

    if (!ask || !text || asking) return;

    setAsking(true);
    setAskError("");

    try {
      const response = await ask(text);

      setQaItems((current) => [
        ...current,
        { question: text, answer: response.answer },
      ]);
      setQuestion("");
    } catch (err) {
      setAskError(getApiMessage(err, "Không thể trả lời câu hỏi."));
    } finally {
      setAsking(false);
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

        {/* ================= HỎI ĐÁP ================= */}
        {ask && result && (
          <div className="mt-6 border-t border-slate-200 pt-5">
            <h4 className="text-sm font-semibold text-slate-800">
              Hỏi AI về văn bản này
            </h4>

            {qaItems.length > 0 && (
              <div className="mt-3 space-y-3">
                {qaItems.map((item, index) => (
                  <div key={index} className="space-y-2">
                    <p className="ml-auto w-fit max-w-[85%] rounded-xl bg-blue-600 px-3 py-2 text-sm text-white">
                      {item.question}
                    </p>
                    <p className="max-w-[90%] whitespace-pre-wrap rounded-xl bg-slate-100 px-3 py-2 text-sm leading-6 text-slate-700">
                      {item.answer}
                    </p>
                  </div>
                ))}
              </div>
            )}

            {askError && (
              <p className="mt-3 text-sm text-red-600">{askError}</p>
            )}

            <form onSubmit={handleAsk} className="mt-3 flex gap-2">
              <input
                type="text"
                value={question}
                onChange={(event) => setQuestion(event.target.value)}
                disabled={asking}
                maxLength={500}
                placeholder="Ví dụ: Hạn nộp là khi nào? Ai chịu trách nhiệm?"
                className="min-w-0 flex-1 rounded-xl border border-slate-300 px-4 py-2.5 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100 disabled:bg-slate-100"
              />

              <button
                type="submit"
                disabled={asking || !question.trim()}
                className="inline-flex items-center gap-2 rounded-xl bg-blue-600 px-4 py-2.5 text-sm font-semibold text-white hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-60"
              >
                {asking ? (
                  <LoaderCircle size={16} className="animate-spin" />
                ) : (
                  <Send size={16} />
                )}
                Hỏi
              </button>
            </form>
          </div>
        )}

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
