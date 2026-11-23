import { useRef, useState } from "react";
import { FileUp, LoaderCircle, Trash2, X } from "lucide-react";

import api from "../../services/api";
import { getApiMessage } from "../../services/emailService";
import { toIso, toLocalInput } from "../../utils/datetime";

type Row = {
  key: number;
  selected: boolean;
  title: string;
  description: string;
  eventType: string;
  startAt: string; // datetime-local
  dueAt: string;
};

type PreviewResponse = {
  fileName: string;
  events: {
    title: string;
    description: string | null;
    eventType: string;
    startAtUtc: string | null;
    dueAtUtc: string | null;
  }[];
};

const EVENT_TYPES = [
  { value: "Task", label: "Công việc" },
  { value: "Meeting", label: "Cuộc họp" },
  { value: "Deadline", label: "Hạn chót" },
  { value: "Appointment", label: "Lịch hẹn" },
  { value: "Other", label: "Khác" },
];

const cell =
  "w-full rounded-lg border border-slate-300 bg-white px-2 py-1.5 text-xs outline-none focus:border-blue-500";

type Props = { onClose: () => void; onSaved: () => void };

export default function ImportWordModal({ onClose, onSaved }: Props) {
  const inputRef = useRef<HTMLInputElement | null>(null);
  const [fileName, setFileName] = useState("");
  const [rows, setRows] = useState<Row[]>([]);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [analyzed, setAnalyzed] = useState(false);

  const update = (key: number, patch: Partial<Row>) =>
    setRows((cur) => cur.map((r) => (r.key === key ? { ...r, ...patch } : r)));

  const handleFile = async (file: File) => {
    if (!file.name.toLowerCase().endsWith(".docx")) {
      setError("Chỉ hỗ trợ file Word .docx.");
      return;
    }

    setLoading(true);
    setError("");

    try {
      const formData = new FormData();
      formData.append("file", file);

      const response = await api.post<PreviewResponse>(
        "/WorkEventImport/preview",
        formData,
      );

      setFileName(response.data.fileName);
      setRows(
        response.data.events.map((e, i) => ({
          key: i,
          selected: true,
          title: e.title,
          description: e.description ?? "",
          eventType: e.eventType,
          startAt: toLocalInput(e.startAtUtc),
          dueAt: toLocalInput(e.dueAtUtc),
        })),
      );
      setAnalyzed(true);
    } catch (err) {
      setError(getApiMessage(err, "Không thể đọc lịch từ file."));
    } finally {
      setLoading(false);
      if (inputRef.current) inputRef.current.value = "";
    }
  };

  const selectedRows = rows.filter((r) => r.selected);

  const handleSave = async () => {
    setError("");

    for (const r of selectedRows) {
      if (!r.title.trim()) return setError("Có dòng chưa có tiêu đề.");
      if (!r.startAt && !r.dueAt)
        return setError(
          `"${r.title}": cần chọn thời gian bắt đầu hoặc hạn chót.`,
        );
    }

    try {
      setSaving(true);
      await api.post("/WorkEventImport/confirm", {
        events: selectedRows.map((r) => ({
          title: r.title.trim(),
          description: r.description.trim() || null,
          eventType: r.eventType,
          startAt: toIso(r.startAt),
          dueAt: toIso(r.dueAt),
        })),
      });
      onSaved();
    } catch (err) {
      setError(getApiMessage(err, "Không thể lưu công việc."));
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/40 px-4">
      <div className="max-h-[90vh] w-full max-w-5xl overflow-y-auto rounded-2xl bg-white p-6 shadow-2xl">
        <div className="flex items-start justify-between gap-3">
          <div>
            <h3 className="text-lg font-bold text-slate-800">
              Nhập lịch từ file Word
            </h3>
            <p className="mt-1 text-sm text-slate-500">
              AI đọc file (kể cả bảng lịch) và đề xuất công việc. Hãy kiểm tra
              trước khi thêm vào lịch.
            </p>
          </div>
          <button
            type="button"
            onClick={onClose}
            disabled={saving}
            className="rounded-lg p-1.5 text-slate-400 hover:bg-slate-100"
          >
            <X size={18} />
          </button>
        </div>

        <input
          ref={inputRef}
          type="file"
          accept=".docx"
          className="hidden"
          onChange={(e) => {
            const f = e.target.files?.[0];
            if (f) void handleFile(f);
          }}
        />

        {error && (
          <div
            role="alert"
            className="mt-4 rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700"
          >
            {error}
          </div>
        )}

        <div className="mt-4 flex flex-wrap items-center gap-3">
          <button
            type="button"
            onClick={() => inputRef.current?.click()}
            disabled={loading || saving}
            className="inline-flex items-center gap-2 rounded-xl bg-blue-600 px-5 py-2.5 text-sm font-semibold text-white hover:bg-blue-700 disabled:opacity-60"
          >
            {loading ? (
              <LoaderCircle size={16} className="animate-spin" />
            ) : (
              <FileUp size={16} />
            )}
            {loading
              ? "AI đang đọc file..."
              : analyzed
                ? "Chọn file khác"
                : "Chọn file .docx"}
          </button>
          {fileName && (
            <span className="text-sm text-slate-500">{fileName}</span>
          )}
        </div>

        {analyzed && rows.length === 0 && (
          <p className="mt-5 rounded-xl bg-slate-50 p-4 text-center text-sm text-slate-500">
            AI không tìm thấy lịch nào trong file này.
          </p>
        )}

        {rows.length > 0 && (
          <>
            <div className="mt-5 overflow-x-auto">
              <table className="w-full min-w-[900px] text-left text-xs">
                <thead>
                  <tr className="border-b border-slate-200 text-slate-500">
                    <th className="w-8 py-2" />
                    <th className="py-2 pr-2 font-medium">Tiêu đề</th>
                    <th className="py-2 pr-2 font-medium">Loại</th>
                    <th className="py-2 pr-2 font-medium">Bắt đầu</th>
                    <th className="py-2 pr-2 font-medium">Hạn chót</th>
                    <th className="py-2 pr-2 font-medium">Ghi chú</th>
                    <th className="w-8 py-2" />
                  </tr>
                </thead>
                <tbody>
                  {rows.map((r) => (
                    <tr
                      key={r.key}
                      className={`border-b border-slate-100 align-top ${r.selected ? "" : "opacity-40"}`}
                    >
                      <td className="py-2">
                        <input
                          type="checkbox"
                          checked={r.selected}
                          onChange={(e) =>
                            update(r.key, { selected: e.target.checked })
                          }
                          className="h-4 w-4"
                        />
                      </td>
                      <td className="py-2 pr-2">
                        <input
                          value={r.title}
                          className={cell}
                          onChange={(e) =>
                            update(r.key, { title: e.target.value })
                          }
                        />
                      </td>
                      <td className="py-2 pr-2">
                        <select
                          value={r.eventType}
                          className={cell}
                          onChange={(e) =>
                            update(r.key, { eventType: e.target.value })
                          }
                        >
                          {EVENT_TYPES.map((t) => (
                            <option key={t.value} value={t.value}>
                              {t.label}
                            </option>
                          ))}
                        </select>
                      </td>
                      <td className="py-2 pr-2">
                        <input
                          type="datetime-local"
                          value={r.startAt}
                          className={cell}
                          onChange={(e) =>
                            update(r.key, { startAt: e.target.value })
                          }
                        />
                      </td>
                      <td className="py-2 pr-2">
                        <input
                          type="datetime-local"
                          value={r.dueAt}
                          className={cell}
                          onChange={(e) =>
                            update(r.key, { dueAt: e.target.value })
                          }
                        />
                      </td>
                      <td className="py-2 pr-2">
                        <input
                          value={r.description}
                          className={cell}
                          onChange={(e) =>
                            update(r.key, { description: e.target.value })
                          }
                        />
                      </td>
                      <td className="py-2">
                        <button
                          type="button"
                          title="Bỏ dòng này"
                          onClick={() =>
                            setRows((cur) => cur.filter((x) => x.key !== r.key))
                          }
                          className="rounded p-1 text-slate-400 hover:text-red-600"
                        >
                          <Trash2 size={14} />
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <div className="mt-5 flex items-center justify-between gap-3 border-t border-slate-200 pt-4">
              <span className="text-sm text-slate-500">
                Đã chọn {selectedRows.length}/{rows.length} công việc
              </span>
              <div className="flex gap-3">
                <button
                  type="button"
                  onClick={onClose}
                  disabled={saving}
                  className="rounded-xl border border-slate-300 px-5 py-2.5 text-sm font-medium text-slate-700 hover:bg-slate-50"
                >
                  Hủy
                </button>
                <button
                  type="button"
                  onClick={() => void handleSave()}
                  disabled={saving || selectedRows.length === 0}
                  className="inline-flex items-center gap-2 rounded-xl bg-blue-600 px-5 py-2.5 text-sm font-semibold text-white hover:bg-blue-700 disabled:opacity-60"
                >
                  {saving && (
                    <LoaderCircle size={16} className="animate-spin" />
                  )}
                  Thêm vào lịch
                </button>
              </div>
            </div>
          </>
        )}
      </div>
    </div>
  );
}
