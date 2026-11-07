import { useState } from "react";
import type { FormEvent } from "react";
import { LoaderCircle, X } from "lucide-react";

import api from "../../services/api";
import { getApiMessage } from "../../services/emailService";

export type EditableEvent = {
  id: number;
  title: string;
  description: string | null;
  eventType: string;
  startAtUtc: string | null;
  dueAtUtc: string | null;
};

type TaskFormModalProps = {
  initial: EditableEvent | null; // null = thêm mới
  onClose: () => void;
  onSaved: () => void;
};

const inputClass =
  "w-full rounded-xl border border-slate-300 bg-white px-4 py-3 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100 disabled:bg-slate-100";

const EVENT_TYPES = [
  { value: "Task", label: "Công việc" },
  { value: "Meeting", label: "Cuộc họp" },
  { value: "Deadline", label: "Hạn chót" },
  { value: "Appointment", label: "Lịch hẹn" },
  { value: "Other", label: "Khác" },
];

// UTC từ backend -> giá trị cho <input type="datetime-local"> (giờ máy).
function toLocalInput(value: string | null): string {
  if (!value) return "";

  const hasTimeZone = /([zZ]|[+-]\d{2}:\d{2})$/.test(value);
  const date = new Date(hasTimeZone ? value : value + "Z");

  if (Number.isNaN(date.getTime())) return "";

  const p = (n: number) => String(n).padStart(2, "0");

  return (
    `${date.getFullYear()}-${p(date.getMonth() + 1)}-${p(date.getDate())}` +
    `T${p(date.getHours())}:${p(date.getMinutes())}`
  );
}

// Giá trị datetime-local (giờ máy) -> ISO UTC gửi lên backend.
function toIso(local: string): string | null {
  return local ? new Date(local).toISOString() : null;
}

function TaskFormModal({ initial, onClose, onSaved }: TaskFormModalProps) {
  const [title, setTitle] = useState(initial?.title ?? "");
  const [description, setDescription] = useState(initial?.description ?? "");
  const [eventType, setEventType] = useState(initial?.eventType ?? "Task");
  const [startAt, setStartAt] = useState(
    toLocalInput(initial?.startAtUtc ?? null),
  );
  const [dueAt, setDueAt] = useState(toLocalInput(initial?.dueAtUtc ?? null));

  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setError("");

    if (!title.trim()) {
      setError("Vui lòng nhập tiêu đề công việc.");
      return;
    }

    if (!startAt && !dueAt) {
      setError("Vui lòng chọn thời gian bắt đầu hoặc hạn chót.");
      return;
    }

    if (startAt && dueAt && new Date(dueAt) < new Date(startAt)) {
      setError("Hạn chót phải sau thời gian bắt đầu.");
      return;
    }

    const payload = {
      title: title.trim(),
      description: description.trim() || null,
      eventType,
      startAt: toIso(startAt),
      dueAt: toIso(dueAt),
    };

    try {
      setSaving(true);

      if (initial) {
        await api.put(`/DetectedWorkEvents/${initial.id}/manual`, payload);
      } else {
        await api.post("/DetectedWorkEvents/manual", payload);
      }

      onSaved();
    } catch (err) {
      setError(getApiMessage(err, "Không thể lưu công việc."));
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/40 px-4">
      <div className="max-h-[90vh] w-full max-w-lg overflow-y-auto rounded-2xl bg-white shadow-2xl">
        <div className="flex items-center justify-between border-b border-slate-200 px-6 py-4">
          <h2 className="text-lg font-semibold text-slate-800">
            {initial ? "Sửa công việc" : "Thêm công việc"}
          </h2>

          <button
            type="button"
            onClick={onClose}
            disabled={saving}
            className="rounded-lg p-2 text-slate-500 hover:bg-slate-100 disabled:opacity-50"
          >
            <X size={20} />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="space-y-4 p-6">
          {error && (
            <div
              role="alert"
              className="rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700"
            >
              {error}
            </div>
          )}

          <div>
            <label className="mb-2 block text-sm font-medium text-slate-700">
              Tiêu đề
            </label>
            <input
              type="text"
              value={title}
              onChange={(e) => setTitle(e.target.value)}
              disabled={saving}
              maxLength={300}
              placeholder="Ví dụ: Nộp báo cáo quý 4"
              className={inputClass}
            />
          </div>

          <div>
            <label className="mb-2 block text-sm font-medium text-slate-700">
              Loại
            </label>
            <select
              value={eventType}
              onChange={(e) => setEventType(e.target.value)}
              disabled={saving}
              className={inputClass}
            >
              {EVENT_TYPES.map((item) => (
                <option key={item.value} value={item.value}>
                  {item.label}
                </option>
              ))}
            </select>
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            <div>
              <label className="mb-2 block text-sm font-medium text-slate-700">
                Bắt đầu
              </label>
              <input
                type="datetime-local"
                value={startAt}
                onChange={(e) => setStartAt(e.target.value)}
                disabled={saving}
                className={inputClass}
              />
            </div>

            <div>
              <label className="mb-2 block text-sm font-medium text-slate-700">
                Hạn chót
              </label>
              <input
                type="datetime-local"
                value={dueAt}
                onChange={(e) => setDueAt(e.target.value)}
                disabled={saving}
                className={inputClass}
              />
            </div>
          </div>

          <p className="text-xs text-slate-500">
            Chọn ít nhất một trong hai mốc thời gian.
          </p>

          <div>
            <label className="mb-2 block text-sm font-medium text-slate-700">
              Mô tả (không bắt buộc)
            </label>
            <textarea
              rows={4}
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              disabled={saving}
              maxLength={4000}
              className={`${inputClass} resize-y`}
            />
          </div>

          <div className="flex justify-end gap-3 border-t border-slate-200 pt-5">
            <button
              type="button"
              onClick={onClose}
              disabled={saving}
              className="rounded-xl border border-slate-300 px-5 py-2.5 text-sm font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-50"
            >
              Hủy
            </button>

            <button
              type="submit"
              disabled={saving}
              className="inline-flex items-center gap-2 rounded-xl bg-blue-600 px-5 py-2.5 text-sm font-semibold text-white hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-60"
            >
              {saving && <LoaderCircle size={16} className="animate-spin" />}
              {saving ? "Đang lưu..." : "Lưu"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

export default TaskFormModal;
