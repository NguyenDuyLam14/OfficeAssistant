import { useCallback, useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import {
  ArrowLeft,
  Eye,
  FileSpreadsheet,
  LoaderCircle,
  RefreshCw,
  Send,
  Trash2,
  Upload,
  X,
} from "lucide-react";

import {
  cancelCampaign,
  createCampaign,
  deleteCampaign,
  getApiMessage,
  getCampaign,
  getCampaigns,
  isReconnectRequired,
  parseRecipients,
  parseUtc,
  previewCampaign,
  startCampaign,
} from "../services/emailService";
import type {
  CampaignDetail,
  CampaignSummary,
  ParsedRecipients,
} from "../services/emailService";

const STATUS_LABEL: Record<string, { text: string; className: string }> = {
  Draft: { text: "Bản nháp", className: "bg-slate-100 text-slate-600" },
  Queued: { text: "Chờ gửi", className: "bg-amber-100 text-amber-800" },
  Running: { text: "Đang gửi", className: "bg-blue-100 text-blue-700" },
  Completed: { text: "Hoàn thành", className: "bg-green-100 text-green-700" },
  Cancelled: { text: "Đã hủy", className: "bg-slate-100 text-slate-500" },
  Failed: { text: "Lỗi", className: "bg-red-100 text-red-700" },
  Pending: { text: "Chờ gửi", className: "bg-amber-100 text-amber-800" },
  Sent: { text: "Đã gửi", className: "bg-green-100 text-green-700" },
};

const inputClass =
  "w-full rounded-xl border border-slate-300 bg-white px-4 py-3 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100 disabled:bg-slate-100";

function StatusBadge({ status }: { status: string }) {
  const item = STATUS_LABEL[status] ?? {
    text: status,
    className: "bg-slate-100 text-slate-600",
  };

  return (
    <span
      className={`rounded-full px-2.5 py-1 text-xs font-medium ${item.className}`}
    >
      {item.text}
    </span>
  );
}

function formatDateTime(value: string | null) {
  if (!value) return "—";

  return parseUtc(value).toLocaleString("vi-VN", {
    dateStyle: "short",
    timeStyle: "short",
  });
}

function EmailCampaigns() {
  // ---------------- Danh sách chiến dịch ----------------
  const [campaigns, setCampaigns] = useState<CampaignSummary[]>([]);
  const [listLoading, setListLoading] = useState(true);

  // ---------------- Form tạo chiến dịch ----------------
  const fileRef = useRef<HTMLInputElement | null>(null);
  const bodyRef = useRef<HTMLTextAreaElement | null>(null);

  const [fileName, setFileName] = useState("");
  const [parsed, setParsed] = useState<ParsedRecipients | null>(null);
  const [parsing, setParsing] = useState(false);

  const [name, setName] = useState("");
  const [subject, setSubject] = useState("");
  const [body, setBody] = useState("");

  const [preview, setPreview] = useState<{
    subject: string;
    body: string;
  } | null>(null);
  const [previewing, setPreviewing] = useState(false);

  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState("");
  const [reconnect, setReconnect] = useState(false);

  // ---------------- Chi tiết ----------------
  const [detail, setDetail] = useState<CampaignDetail | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);

  const hasActive = campaigns.some(
    (c) => c.status === "Queued" || c.status === "Running",
  );

  const loadCampaigns = useCallback(async () => {
    try {
      setCampaigns(await getCampaigns());
    } catch (err) {
      console.error("Không thể tải danh sách chiến dịch:", err);
      setError("Không thể tải danh sách chiến dịch.");
    } finally {
      setListLoading(false);
    }
  }, []);

  useEffect(() => {
    async function fetchCampaigns() {
      await loadCampaigns();
    }

    void fetchCampaigns();
  }, [loadCampaigns]);

  // Có chiến dịch đang chạy thì tự làm mới tiến độ mỗi 5 giây.
  useEffect(() => {
    if (!hasActive) return;

    const timer = setInterval(() => void loadCampaigns(), 5000);
    return () => clearInterval(timer);
  }, [hasActive, loadCampaigns]);

  // ---------------- Chọn file người nhận ----------------
  const handleFile = async (file: File) => {
    setParsing(true);
    setError("");
    setPreview(null);

    try {
      setParsed(await parseRecipients(file));
      setFileName(file.name);
    } catch (err) {
      setParsed(null);
      setFileName("");
      setError(getApiMessage(err, "Không thể đọc file người nhận."));
    } finally {
      setParsing(false);

      if (fileRef.current) {
        fileRef.current.value = "";
      }
    }
  };

  // Chèn biến {{...}} tại vị trí con trỏ trong nội dung thư.
  const insertVariable = (variable: string) => {
    const token = `{{${variable}}}`;
    const element = bodyRef.current;

    if (!element) {
      setBody((current) => current + token);
      return;
    }

    const start = element.selectionStart;
    const end = element.selectionEnd;

    setBody(body.slice(0, start) + token + body.slice(end));

    requestAnimationFrame(() => {
      element.focus();
      element.setSelectionRange(start + token.length, start + token.length);
    });
  };

  const handlePreview = async () => {
    if (!parsed || parsed.recipients.length === 0) return;

    setPreviewing(true);
    setError("");

    try {
      setPreview(await previewCampaign(subject, body, parsed.recipients[0]));
    } catch (err) {
      setError(getApiMessage(err, "Không thể xem trước thư."));
    } finally {
      setPreviewing(false);
    }
  };

  const resetForm = () => {
    setParsed(null);
    setFileName("");
    setName("");
    setSubject("");
    setBody("");
    setPreview(null);
  };

  // ---------------- Tạo và gửi ----------------
  const handleSubmit = async () => {
    setError("");
    setReconnect(false);

    if (!parsed || parsed.recipients.length === 0) {
      setError("Vui lòng chọn file có danh sách người nhận hợp lệ.");
      return;
    }

    if (!name.trim() || !subject.trim() || !body.trim()) {
      setError("Vui lòng nhập tên chiến dịch, tiêu đề và nội dung thư.");
      return;
    }

    const confirmed = window.confirm(
      `Gửi thư "${subject.trim()}" đến ${parsed.recipients.length} người từ ` +
        "tài khoản Gmail của bạn?\n\nThư sẽ được gửi dần trong nền. " +
        "Bạn có thể hủy giữa chừng, nhưng thư đã gửi không thể thu hồi.",
    );

    if (!confirmed) return;

    setSubmitting(true);

    try {
      const id = await createCampaign({
        name: name.trim(),
        subject: subject.trim(),
        bodyTemplate: body.trim(),
        recipients: parsed.recipients,
      });

      await startCampaign(id);

      resetForm();
      await loadCampaigns();
    } catch (err) {
      setReconnect(isReconnectRequired(err));
      setError(getApiMessage(err, "Không thể tạo chiến dịch."));
    } finally {
      setSubmitting(false);
    }
  };

  // ---------------- Thao tác trên chiến dịch ----------------
  const openDetail = async (id: number) => {
    setDetailLoading(true);

    try {
      setDetail(await getCampaign(id));
    } catch (err) {
      setError(getApiMessage(err, "Không thể tải chi tiết chiến dịch."));
    } finally {
      setDetailLoading(false);
    }
  };

  const handleCancel = async (campaign: CampaignSummary) => {
    if (!window.confirm(`Hủy chiến dịch "${campaign.name}"?`)) return;

    try {
      await cancelCampaign(campaign.id);
      await loadCampaigns();
    } catch (err) {
      setError(getApiMessage(err, "Không thể hủy chiến dịch."));
    }
  };

  const handleDelete = async (campaign: CampaignSummary) => {
    if (!window.confirm(`Xóa chiến dịch "${campaign.name}"?`)) return;

    try {
      await deleteCampaign(campaign.id);
      await loadCampaigns();
    } catch (err) {
      setError(getApiMessage(err, "Không thể xóa chiến dịch."));
    }
  };

  // =======================================================
  // GIAO DIỆN
  // =======================================================

  return (
    <main className="p-6">
      {/* Tiêu đề */}
      <div className="mb-6 flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
        <div className="flex items-center gap-3">
          <div className="rounded-xl bg-blue-100 p-3 text-blue-600">
            <Send size={26} />
          </div>

          <div>
            <h1 className="text-2xl font-bold text-slate-800">
              Gửi email hàng loạt
            </h1>
            <p className="text-sm text-slate-500">
              Gửi thư cá nhân hóa đến nhiều người từ danh sách Excel hoặc CSV.
            </p>
          </div>
        </div>

        <Link
          to="/emails"
          className="inline-flex items-center justify-center gap-2 rounded-xl border border-slate-200 bg-white px-4 py-2.5 text-sm font-medium text-slate-700 hover:bg-slate-50"
        >
          <ArrowLeft size={16} />
          Về hộp thư
        </Link>
      </div>

      {error && (
        <div
          role="alert"
          className="mb-5 flex items-start justify-between gap-3 rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700"
        >
          <span>
            {error}
            {reconnect && (
              <>
                {" "}
                <Link to="/emails" className="font-semibold underline">
                  Kết nối Gmail
                </Link>
              </>
            )}
          </span>

          <button type="button" onClick={() => setError("")}>
            <X size={17} />
          </button>
        </div>
      )}

      {/* ================= TẠO CHIẾN DỊCH ================= */}
      <section className="mb-6 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
        <h2 className="text-lg font-bold text-slate-800">Tạo chiến dịch mới</h2>

        <input
          ref={fileRef}
          type="file"
          accept=".xlsx,.xlsm,.csv"
          className="hidden"
          onChange={(event) => {
            const file = event.target.files?.[0];
            if (file) void handleFile(file);
          }}
        />

        {/* Bước 1: danh sách người nhận */}
        <div className="mt-4 rounded-xl border border-dashed border-slate-300 p-4">
          <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
            <div className="flex items-center gap-3">
              <div className="flex h-11 w-11 items-center justify-center rounded-xl bg-green-50 text-green-600">
                <FileSpreadsheet size={22} />
              </div>

              <div>
                <p className="text-sm font-semibold text-slate-800">
                  {fileName || "Danh sách người nhận"}
                </p>
                <p className="text-xs text-slate-500">
                  Dòng đầu là tiêu đề. Cột có chữ "email" là địa chỉ nhận; cột
                  "Tên" hoặc "Họ tên" (nếu có) là tên người nhận.
                </p>
              </div>
            </div>

            <button
              type="button"
              onClick={() => fileRef.current?.click()}
              disabled={parsing || submitting}
              className="inline-flex items-center justify-center gap-2 rounded-xl border border-slate-200 bg-white px-4 py-2.5 text-sm font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-60"
            >
              {parsing ? (
                <LoaderCircle size={16} className="animate-spin" />
              ) : (
                <Upload size={16} />
              )}
              {parsed ? "Chọn file khác" : "Chọn file"}
            </button>
          </div>

          {parsed && (
            <div className="mt-4 text-sm text-slate-600">
              <p>
                <span className="font-semibold text-green-700">
                  {parsed.recipients.length} người nhận hợp lệ
                </span>
                {parsed.invalid > 0 && (
                  <span> · {parsed.invalid} email không hợp lệ bị bỏ</span>
                )}
                {parsed.duplicates > 0 && (
                  <span> · {parsed.duplicates} email trùng bị bỏ</span>
                )}
              </p>

              {parsed.truncated && (
                <p className="mt-1 text-amber-700">
                  File vượt giới hạn số người nhận, chỉ lấy phần đầu danh sách.
                </p>
              )}

              <p className="mt-3 text-xs text-slate-500">
                Bấm vào biến để chèn vào nội dung thư:
              </p>

              <div className="mt-2 flex flex-wrap gap-2">
                {parsed.variables.map((variable) => (
                  <button
                    key={variable}
                    type="button"
                    onClick={() => insertVariable(variable)}
                    className="rounded-full bg-blue-50 px-3 py-1 text-xs font-medium text-blue-700 hover:bg-blue-100"
                  >
                    {`{{${variable}}}`}
                  </button>
                ))}
              </div>
            </div>
          )}
        </div>

        {/* Bước 2: nội dung thư */}
        <div className="mt-4 grid gap-4">
          <div>
            <label className="mb-2 block text-sm font-medium text-slate-700">
              Tên chiến dịch
            </label>
            <input
              type="text"
              value={name}
              onChange={(event) => setName(event.target.value)}
              disabled={submitting}
              maxLength={200}
              placeholder="Ví dụ: Thông báo họp tổng kết quý 4"
              className={inputClass}
            />
          </div>

          <div>
            <label className="mb-2 block text-sm font-medium text-slate-700">
              Tiêu đề thư
            </label>
            <input
              type="text"
              value={subject}
              onChange={(event) => setSubject(event.target.value)}
              disabled={submitting}
              maxLength={300}
              placeholder="Có thể dùng biến, ví dụ: Thư mời {{Ten}} dự họp"
              className={inputClass}
            />
          </div>

          <div>
            <label className="mb-2 block text-sm font-medium text-slate-700">
              Nội dung thư
            </label>
            <textarea
              ref={bodyRef}
              rows={10}
              value={body}
              onChange={(event) => setBody(event.target.value)}
              disabled={submitting}
              placeholder={"Kính gửi {{Ten}},\n\n..."}
              className={`${inputClass} resize-y leading-6`}
            />
          </div>
        </div>

        {/* Xem trước */}
        {preview && (
          <div className="mt-4 rounded-xl border border-slate-200 bg-slate-50 p-4">
            <p className="mb-2 text-xs text-slate-500">
              Xem trước với người nhận đầu tiên ({parsed?.recipients[0]?.email}
              ):
            </p>
            <p className="text-sm font-semibold text-slate-800">
              {preview.subject}
            </p>
            <p className="mt-2 whitespace-pre-wrap text-sm leading-6 text-slate-700">
              {preview.body}
            </p>
          </div>
        )}

        <div className="mt-5 flex flex-col gap-3 sm:flex-row sm:justify-end">
          <button
            type="button"
            onClick={() => void handlePreview()}
            disabled={
              previewing ||
              submitting ||
              !parsed ||
              !subject.trim() ||
              !body.trim()
            }
            className="inline-flex items-center justify-center gap-2 rounded-xl border border-slate-200 bg-white px-5 py-3 text-sm font-medium text-slate-700 hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-50"
          >
            {previewing ? (
              <LoaderCircle size={16} className="animate-spin" />
            ) : (
              <Eye size={16} />
            )}
            Xem trước
          </button>

          <button
            type="button"
            onClick={() => void handleSubmit()}
            disabled={submitting || !parsed}
            className="inline-flex items-center justify-center gap-2 rounded-xl bg-blue-600 px-5 py-3 text-sm font-semibold text-white hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-60"
          >
            {submitting ? (
              <LoaderCircle size={16} className="animate-spin" />
            ) : (
              <Send size={16} />
            )}
            {submitting ? "Đang tạo..." : "Tạo và bắt đầu gửi"}
          </button>
        </div>
      </section>

      {/* ================= DANH SÁCH CHIẾN DỊCH ================= */}
      <section className="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
        <div className="flex items-center justify-between border-b border-slate-200 px-5 py-4">
          <div>
            <h2 className="font-semibold text-slate-800">Chiến dịch đã tạo</h2>
            <p className="mt-1 text-sm text-slate-500">
              {campaigns.length} chiến dịch
              {hasActive && " · đang tự cập nhật tiến độ"}
            </p>
          </div>

          <button
            type="button"
            onClick={() => void loadCampaigns()}
            className="rounded-lg border border-slate-200 p-2 text-slate-600 hover:bg-slate-50"
            title="Làm mới"
          >
            <RefreshCw size={17} />
          </button>
        </div>

        {listLoading ? (
          <div className="px-5 py-12 text-center text-sm text-slate-500">
            Đang tải...
          </div>
        ) : campaigns.length === 0 ? (
          <div className="px-5 py-12 text-center text-sm text-slate-500">
            Chưa có chiến dịch nào. Chọn file danh sách người nhận ở trên để
            bắt đầu.
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[800px] text-left text-sm">
              <thead>
                <tr className="border-b border-slate-200 bg-slate-50 text-slate-500">
                  <th className="px-5 py-3 font-medium">Chiến dịch</th>
                  <th className="px-5 py-3 font-medium">Trạng thái</th>
                  <th className="px-5 py-3 font-medium">Tiến độ</th>
                  <th className="px-5 py-3 font-medium">Ngày tạo</th>
                  <th className="px-5 py-3 text-right font-medium">Thao tác</th>
                </tr>
              </thead>

              <tbody>
                {campaigns.map((campaign) => {
                  const done = campaign.sent + campaign.failed;
                  const percent =
                    campaign.total > 0
                      ? Math.round((done / campaign.total) * 100)
                      : 0;
                  const active =
                    campaign.status === "Queued" ||
                    campaign.status === "Running";

                  return (
                    <tr
                      key={campaign.id}
                      className="border-b border-slate-100 align-top last:border-0"
                    >
                      <td className="px-5 py-4">
                        <p className="font-medium text-slate-700">
                          {campaign.name}
                        </p>
                        <p className="mt-1 max-w-xs truncate text-xs text-slate-400">
                          {campaign.subject}
                        </p>
                        {campaign.note && (
                          <p className="mt-1 max-w-xs text-xs text-amber-700">
                            {campaign.note}
                          </p>
                        )}
                      </td>

                      <td className="px-5 py-4">
                        <StatusBadge status={campaign.status} />
                      </td>

                      <td className="px-5 py-4">
                        <div className="h-2 w-40 overflow-hidden rounded-full bg-slate-100">
                          <div
                            className="h-full rounded-full bg-blue-600"
                            style={{ width: `${percent}%` }}
                          />
                        </div>
                        <p className="mt-1 text-xs text-slate-500">
                          {campaign.sent} đã gửi
                          {campaign.failed > 0 &&
                            ` · ${campaign.failed} lỗi`}
                          {" · "}
                          {campaign.pending} chờ / {campaign.total}
                        </p>
                      </td>

                      <td className="px-5 py-4 text-slate-500">
                        {formatDateTime(campaign.createdAtUtc)}
                      </td>

                      <td className="px-5 py-4">
                        <div className="flex justify-end gap-2">
                          <button
                            type="button"
                            onClick={() => void openDetail(campaign.id)}
                            className="rounded-lg border border-slate-200 px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50"
                          >
                            Chi tiết
                          </button>

                          {active ? (
                            <button
                              type="button"
                              onClick={() => void handleCancel(campaign)}
                              className="rounded-lg border border-red-200 px-3 py-1.5 text-xs font-medium text-red-600 hover:bg-red-50"
                            >
                              Hủy
                            </button>
                          ) : (
                            <button
                              type="button"
                              onClick={() => void handleDelete(campaign)}
                              className="rounded-lg p-2 text-slate-500 hover:bg-red-50 hover:text-red-600"
                              title="Xóa chiến dịch"
                            >
                              <Trash2 size={16} />
                            </button>
                          )}
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

      {/* ================= CHI TIẾT ================= */}
      {(detail || detailLoading) && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/40 px-4"
          onClick={() => setDetail(null)}
        >
          <div
            className="max-h-[90vh] w-full max-w-3xl overflow-y-auto rounded-2xl bg-white p-6 shadow-2xl"
            onClick={(event) => event.stopPropagation()}
          >
            {detailLoading || !detail ? (
              <div className="flex items-center justify-center py-16">
                <LoaderCircle
                  size={28}
                  className="animate-spin text-blue-600"
                />
              </div>
            ) : (
              <>
                <div className="flex items-start justify-between gap-3">
                  <div>
                    <h3 className="text-lg font-bold text-slate-800">
                      {detail.name}
                    </h3>
                    <p className="mt-1 text-sm text-slate-500">
                      {detail.subject}
                    </p>
                  </div>

                  <button
                    type="button"
                    onClick={() => setDetail(null)}
                    className="rounded-lg p-1.5 text-slate-400 hover:bg-slate-100"
                  >
                    <X size={18} />
                  </button>
                </div>

                <div className="mt-4 flex flex-wrap items-center gap-3 text-sm text-slate-600">
                  <StatusBadge status={detail.status} />
                  <span>{detail.sent} đã gửi</span>
                  <span>{detail.failed} lỗi</span>
                  <span>{detail.pending} chờ</span>
                  <span>
                    Bắt đầu: {formatDateTime(detail.startedAtUtc)} · Kết thúc:{" "}
                    {formatDateTime(detail.finishedAtUtc)}
                  </span>
                </div>

                {detail.note && (
                  <p className="mt-3 rounded-lg bg-amber-50 p-3 text-sm text-amber-800">
                    {detail.note}
                  </p>
                )}

                <div className="mt-4 overflow-x-auto">
                  <table className="w-full text-left text-sm">
                    <thead>
                      <tr className="border-b border-slate-200 text-slate-500">
                        <th className="py-2 pr-4 font-medium">Người nhận</th>
                        <th className="py-2 pr-4 font-medium">Trạng thái</th>
                        <th className="py-2 font-medium">Ghi chú</th>
                      </tr>
                    </thead>

                    <tbody>
                      {detail.recipients.map((recipient) => (
                        <tr
                          key={recipient.id}
                          className="border-b border-slate-100 last:border-0"
                        >
                          <td className="py-2 pr-4 text-slate-700">
                            {recipient.name
                              ? `${recipient.name} <${recipient.email}>`
                              : recipient.email}
                          </td>
                          <td className="py-2 pr-4">
                            <StatusBadge status={recipient.status} />
                          </td>
                          <td className="py-2 text-xs text-slate-500">
                            {recipient.error ??
                              (recipient.sentAtUtc
                                ? formatDateTime(recipient.sentAtUtc)
                                : "")}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>

                  {detail.total > detail.recipients.length && (
                    <p className="mt-3 text-xs text-slate-400">
                      Hiển thị {detail.recipients.length}/{detail.total} người
                      nhận (ưu tiên các thư lỗi).
                    </p>
                  )}
                </div>

                <div className="mt-5 flex justify-end">
                  <button
                    type="button"
                    onClick={() => void openDetail(detail.id)}
                    className="inline-flex items-center gap-2 rounded-xl border border-slate-200 px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50"
                  >
                    <RefreshCw size={15} />
                    Làm mới
                  </button>
                </div>
              </>
            )}
          </div>
        </div>
      )}
    </main>
  );
}

export default EmailCampaigns;
