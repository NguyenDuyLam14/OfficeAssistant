import { useState } from "react";
import {
  CheckCircle2,
  LoaderCircle,
  MessageSquareReply,
  RefreshCw,
  Sparkles,
} from "lucide-react";

import ComposeForm from "./ComposeForm";
import {
  getApiMessage,
  suggestReply,
  summarizeEmail,
} from "../../services/emailService";
import type { ReplyTone } from "../../services/emailService";

type PanelMessage = {
  id: string;
  from?: string | null;
  subject?: string | null;
};

type EmailAssistantPanelProps = {
  message: PanelMessage;
};

// "Nguyen A <a@x.com>" -> "a@x.com"
function extractAddress(from?: string | null): string {
  if (!from) return "";

  const match = from.match(/<([^>]+)>/);
  return (match ? match[1] : from).trim();
}

const TONE_LABEL: Record<ReplyTone, string> = {
  formal: "Trang trọng",
  friendly: "Thân thiện",
  short: "Ngắn gọn",
};

// Dùng với key={message.id} để tự reset khi đổi email.
function EmailAssistantPanel({ message }: EmailAssistantPanelProps) {
  const [summary, setSummary] = useState("");
  const [summaryCached, setSummaryCached] = useState(false);
  const [summaryLoading, setSummaryLoading] = useState(false);
  const [summaryError, setSummaryError] = useState("");

  const [tone, setTone] = useState<ReplyTone>("formal");
  const [note, setNote] = useState("");
  const [replyLoading, setReplyLoading] = useState(false);
  const [replyError, setReplyError] = useState("");

  // Nội dung gợi ý + khóa để form soạn thư nạp lại nội dung mới.
  const [draft, setDraft] = useState("");
  const [composeKey, setComposeKey] = useState(0);
  const [composeOpen, setComposeOpen] = useState(false);
  const [sent, setSent] = useState(false);

  const originalSubject = message.subject?.trim() ?? "";
  const replySubject = /^re:/i.test(originalSubject)
    ? originalSubject
    : `Re: ${originalSubject}`.trim();

  const handleSummarize = async (refresh: boolean) => {
    setSummaryLoading(true);
    setSummaryError("");

    try {
      const result = await summarizeEmail(message.id, refresh);

      setSummary(result.summary);
      setSummaryCached(result.cached);
    } catch (err) {
      setSummaryError(getApiMessage(err, "Không thể tóm tắt email."));
    } finally {
      setSummaryLoading(false);
    }
  };

  const handleSuggest = async () => {
    setReplyLoading(true);
    setReplyError("");
    setSent(false);

    try {
      const reply = await suggestReply(message.id, tone, note);

      setDraft(reply);
      setComposeKey((key) => key + 1);
      setComposeOpen(true);
    } catch (err) {
      setReplyError(getApiMessage(err, "Không thể gợi ý nội dung trả lời."));
    } finally {
      setReplyLoading(false);
    }
  };

  const handleWriteManually = () => {
    setDraft("");
    setComposeKey((key) => key + 1);
    setComposeOpen(true);
    setSent(false);
  };

  return (
    <div className="mt-8 space-y-4 rounded-xl border border-blue-100 bg-blue-50/70 p-4">
      {/* ================= TÓM TẮT ================= */}
      <section>
        <div className="flex flex-wrap items-center justify-between gap-2">
          <h3 className="flex items-center gap-2 text-sm font-semibold text-slate-800">
            <Sparkles size={16} className="text-blue-600" />
            Tóm tắt bằng AI
          </h3>

          {summary ? (
            <button
              type="button"
              onClick={() => void handleSummarize(true)}
              disabled={summaryLoading}
              className="inline-flex items-center gap-1.5 text-xs font-medium text-blue-700 hover:underline disabled:opacity-50"
            >
              <RefreshCw
                size={13}
                className={summaryLoading ? "animate-spin" : ""}
              />
              Tóm tắt lại
            </button>
          ) : (
            <button
              type="button"
              onClick={() => void handleSummarize(false)}
              disabled={summaryLoading}
              className="inline-flex items-center gap-2 rounded-lg bg-blue-600 px-3 py-1.5 text-xs font-semibold text-white hover:bg-blue-700 disabled:opacity-60"
            >
              {summaryLoading && (
                <LoaderCircle size={14} className="animate-spin" />
              )}
              {summaryLoading ? "Đang tóm tắt..." : "Tóm tắt email"}
            </button>
          )}
        </div>

        {summaryError && (
          <p className="mt-3 text-sm text-red-600">{summaryError}</p>
        )}

        {summary && (
          <div className="mt-3 whitespace-pre-wrap rounded-lg bg-white p-3 text-sm leading-6 text-slate-700">
            {summary}
            {summaryCached && (
              <p className="mt-2 text-xs text-slate-400">
                Bản tóm tắt đã lưu từ lần trước.
              </p>
            )}
          </div>
        )}
      </section>

      <div className="border-t border-blue-100" />

      {/* ================= TRẢ LỜI ================= */}
      <section>
        <h3 className="flex items-center gap-2 text-sm font-semibold text-slate-800">
          <MessageSquareReply size={16} className="text-blue-600" />
          Trả lời email
        </h3>

        <div className="mt-3 grid gap-2 sm:grid-cols-[160px_1fr]">
          <select
            value={tone}
            onChange={(event) => setTone(event.target.value as ReplyTone)}
            disabled={replyLoading}
            className="rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100"
          >
            {(Object.keys(TONE_LABEL) as ReplyTone[]).map((key) => (
              <option key={key} value={key}>
                {TONE_LABEL[key]}
              </option>
            ))}
          </select>

          <input
            type="text"
            value={note}
            onChange={(event) => setNote(event.target.value)}
            disabled={replyLoading}
            maxLength={1000}
            placeholder="Ý chính muốn trả lời (không bắt buộc), ví dụ: đồng ý họp, đề nghị dời sang 15h"
            className="rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100"
          />
        </div>

        <div className="mt-3 flex flex-wrap gap-2">
          <button
            type="button"
            onClick={() => void handleSuggest()}
            disabled={replyLoading}
            className="inline-flex items-center gap-2 rounded-lg bg-blue-600 px-3 py-2 text-xs font-semibold text-white hover:bg-blue-700 disabled:opacity-60"
          >
            {replyLoading ? (
              <LoaderCircle size={14} className="animate-spin" />
            ) : (
              <Sparkles size={14} />
            )}
            {replyLoading ? "AI đang soạn..." : "Gợi ý trả lời bằng AI"}
          </button>

          <button
            type="button"
            onClick={handleWriteManually}
            disabled={replyLoading}
            className="rounded-lg border border-slate-200 bg-white px-3 py-2 text-xs font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-50"
          >
            Tự soạn trả lời
          </button>
        </div>

        {replyError && (
          <p className="mt-3 text-sm text-red-600">{replyError}</p>
        )}

        {sent && (
          <p className="mt-3 inline-flex items-center gap-2 text-sm font-medium text-emerald-700">
            <CheckCircle2 size={16} />
            Đã gửi thư trả lời.
          </p>
        )}

        {composeOpen && (
          <div className="mt-4 rounded-lg bg-white p-4">
            <p className="mb-3 text-xs text-slate-500">
              Hãy kiểm tra và sửa nội dung trước khi gửi. Phần [Tên của bạn]
              cần được thay bằng tên thật.
            </p>

            <ComposeForm
              key={composeKey}
              initialTo={extractAddress(message.from)}
              initialSubject={replySubject}
              initialBody={draft}
              replyToMessageId={message.id}
              onSent={() => {
                setComposeOpen(false);
                setSent(true);
              }}
              onCancel={() => setComposeOpen(false)}
            />
          </div>
        )}
      </section>
    </div>
  );
}

export default EmailAssistantPanel;
