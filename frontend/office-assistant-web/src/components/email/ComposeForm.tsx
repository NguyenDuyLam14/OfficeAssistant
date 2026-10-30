import { useState } from "react";
import type { FormEvent } from "react";
import { LoaderCircle, Send } from "lucide-react";

import {
  getApiMessage,
  isReconnectRequired,
  sendEmail,
} from "../../services/emailService";

type ComposeFormProps = {
  initialTo?: string;
  initialSubject?: string;
  initialBody?: string;
  replyToMessageId?: string;
  onSent: () => void;
  onCancel?: () => void;
};

const inputClass =
  "w-full rounded-lg border border-slate-300 bg-white px-3 py-2.5 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100 disabled:bg-slate-100";

// Form soạn thư: dùng cho thư mới và thư trả lời.
function ComposeForm({
  initialTo = "",
  initialSubject = "",
  initialBody = "",
  replyToMessageId,
  onSent,
  onCancel,
}: ComposeFormProps) {
  const [to, setTo] = useState(initialTo);
  const [cc, setCc] = useState("");
  const [subject, setSubject] = useState(initialSubject);
  const [body, setBody] = useState(initialBody);

  const [sending, setSending] = useState(false);
  const [error, setError] = useState("");

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setError("");

    if (!to.trim()) {
      setError("Vui lòng nhập địa chỉ người nhận.");
      return;
    }

    if (!subject.trim()) {
      setError("Vui lòng nhập tiêu đề thư.");
      return;
    }

    if (!body.trim()) {
      setError("Vui lòng nhập nội dung thư.");
      return;
    }

    try {
      setSending(true);

      await sendEmail({
        to: to.trim(),
        cc: cc.trim() || undefined,
        subject: subject.trim(),
        body,
        replyToMessageId,
      });

      onSent();
    } catch (err) {
      setError(
        isReconnectRequired(err)
          ? "Kết nối Gmail không còn hiệu lực. Hãy kết nối lại Gmail ở đầu trang."
          : getApiMessage(err, "Không thể gửi thư. Vui lòng thử lại."),
      );
    } finally {
      setSending(false);
    }
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-3">
      {error && (
        <div
          role="alert"
          className="rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700"
        >
          {error}
        </div>
      )}

      <div>
        <label className="mb-1 block text-xs font-medium text-slate-600">
          Đến
        </label>
        <input
          type="text"
          value={to}
          onChange={(event) => setTo(event.target.value)}
          placeholder="nguoinhan@example.com (nhiều địa chỉ cách nhau bằng dấu phẩy)"
          disabled={sending}
          className={inputClass}
        />
      </div>

      <div>
        <label className="mb-1 block text-xs font-medium text-slate-600">
          Cc (không bắt buộc)
        </label>
        <input
          type="text"
          value={cc}
          onChange={(event) => setCc(event.target.value)}
          disabled={sending}
          className={inputClass}
        />
      </div>

      <div>
        <label className="mb-1 block text-xs font-medium text-slate-600">
          Tiêu đề
        </label>
        <input
          type="text"
          value={subject}
          onChange={(event) => setSubject(event.target.value)}
          disabled={sending}
          className={inputClass}
        />
      </div>

      <div>
        <label className="mb-1 block text-xs font-medium text-slate-600">
          Nội dung
        </label>
        <textarea
          rows={10}
          value={body}
          onChange={(event) => setBody(event.target.value)}
          disabled={sending}
          className={`${inputClass} resize-y leading-6`}
        />
      </div>

      <div className="flex justify-end gap-2">
        {onCancel && (
          <button
            type="button"
            onClick={onCancel}
            disabled={sending}
            className="rounded-lg border border-slate-200 px-4 py-2 text-sm font-medium text-slate-600 hover:bg-slate-50 disabled:opacity-50"
          >
            Hủy
          </button>
        )}

        <button
          type="submit"
          disabled={sending}
          className="inline-flex items-center gap-2 rounded-lg bg-blue-600 px-4 py-2 text-sm font-semibold text-white hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-60"
        >
          {sending ? (
            <LoaderCircle size={16} className="animate-spin" />
          ) : (
            <Send size={16} />
          )}
          {sending ? "Đang gửi..." : "Gửi thư"}
        </button>
      </div>
    </form>
  );
}

export default ComposeForm;
