import { useCallback, useEffect, useMemo, useState } from "react";

import { FileText, Plus, Search, Pencil, Trash2, X } from "lucide-react";

import api from "../services/api";

import axios from "axios";

// =========================================================
// DỮ LIỆU API AI
// =========================================================

// Dữ liệu gửi từ React lên API AI.
interface GenerateDocumentRequest {
  documentType: string;
  templateName: string;
  userPrompt: string;
}

// Dữ liệu API AI trả về cho React.
interface GenerateDocumentResponse {
  message: string;
  content: string;
}

type DocumentItem = {
  documentId: number;
  title: string;
  documentType: string;
  content: string | null;
  userId: number;
  documentTemplateId: number | null;
  createdAt: string;
  updatedAt: string | null;
};

type DocumentForm = {
  title: string;
  documentType: string;
  content: string;
};

const documentTypes = [
  "Thông báo",
  "Công văn",
  "Báo cáo",
  "Biên bản",
  "Quyết định",
  "Khác",
];

function Documents() {
  // =========================================================
  // STATE
  // =========================================================

  // Danh sách văn bản.
  const [documents, setDocuments] = useState<DocumentItem[]>([]);

  // Trạng thái tải danh sách.
  const [loading, setLoading] = useState(true);

  // Thông báo lỗi.
  const [error, setError] = useState("");

  // Từ khóa tìm kiếm.
  const [searchText, setSearchText] = useState("");

  // Loại văn bản đang lọc.
  const [filterType, setFilterType] = useState("");

  // Điều khiển modal.
  const [modalOpen, setModalOpen] = useState(false);

  // ID văn bản đang chỉnh sửa.
  // null = đang tạo mới.
  const [editingId, setEditingId] = useState<number | null>(null);

  // Trạng thái lưu dữ liệu.
  const [saving, setSaving] = useState(false);

  // Dữ liệu form.
  const [form, setForm] = useState<DocumentForm>({
    title: "",
    documentType: "Thông báo",
    content: "",
  });

  // =========================================================
  // STATE CHO CHỨC NĂNG SOẠN THẢO BẰNG AI
  // =========================================================

  // Loại văn bản mà người dùng muốn AI soạn.
  const [aiDocumentType, setAiDocumentType] = useState("Thông báo");

  // Tên mẫu văn bản mà người dùng muốn sử dụng.
  const [aiTemplateName, setAiTemplateName] = useState("Thông báo hành chính");

  // Yêu cầu cụ thể gửi cho AI.
  const [aiPrompt, setAiPrompt] = useState("");

  // Nội dung văn bản do Gemini tạo ra.
  const [aiContent, setAiContent] = useState("");

  // Trạng thái đang gọi API Gemini.
  const [isGeneratingAI, setIsGeneratingAI] = useState(false);

  // Lỗi riêng của chức năng AI.
  const [aiError, setAiError] = useState("");

  /**
   * Gọi API backend để Gemini sinh nội dung văn bản.
   *
   * Frontend không gọi Gemini trực tiếp.
   * Frontend chỉ gọi ASP.NET Core API.
   *
   * Luồng:
   *
   * React
   *   ↓
   * ASP.NET Core API
   *   ↓
   * Gemini API
   *   ↓
   * ASP.NET Core
   *   ↓
   * React
   */
  const handleGenerateAI = async () => {
    // Kiểm tra yêu cầu của người dùng.
    if (!aiPrompt.trim()) {
      setAiError("Vui lòng nhập yêu cầu cần AI soạn thảo.");

      return;
    }

    // Xóa lỗi cũ.
    setAiError("");

    // Hiển thị trạng thái đang xử lý.
    setIsGeneratingAI(true);

    try {
      // Gọi API backend.
      //
      // Không cần tự lấy token ở đây.
      // File services/api.ts của bạn đã có interceptor
      // tự động thêm:
      //
      // Authorization: Bearer <JWT>
      const response = await api.post<GenerateDocumentResponse>(
        "/AI/generate-document",
        {
          documentType: aiDocumentType,
          templateName: aiTemplateName,
          userPrompt: aiPrompt,
        } satisfies GenerateDocumentRequest,
      );

      // Lấy nội dung Gemini trả về.
      setAiContent(response.data.content);

      // Xóa lỗi nếu trước đó có lỗi.
      setAiError("");
    } catch (error) {
      console.error("Lỗi khi gọi API AI:", error);

      // AxiosError có response nếu backend thực sự trả về HTTP error.
      if (axios.isAxiosError(error)) {
        const message = error.response?.data?.message;

        setAiError(message ?? "Không thể kết nối tới dịch vụ AI.");
      } else if (error instanceof Error) {
        setAiError(error.message);
      } else {
        setAiError("Đã xảy ra lỗi khi gọi AI.");
      }
    } finally {
      // Tắt loading dù thành công hay thất bại.
      setIsGeneratingAI(false);
    }
  };

  // =========================================================
  // LẤY DANH SÁCH VĂN BẢN
  // =========================================================

  /*
   * Đưa hàm loadDocuments ra ngoài useEffect.
   *
   * useCallback giúp giữ ổn định tham chiếu của hàm giữa
   * các lần render và cho phép useEffect sử dụng hàm này
   * mà không gây cảnh báo dependency.
   */
  const loadDocuments = useCallback(async () => {
    try {
      setLoading(true);

      const response = await api.get<DocumentItem[]>("/documents");

      setDocuments(response.data);
      setError("");
    } catch (error) {
      console.error("Không thể tải danh sách văn bản:", error);

      setError("Không thể tải danh sách văn bản.");
    } finally {
      setLoading(false);
    }
  }, []);

  // =========================================================
  // TẢI DỮ LIỆU KHI MỞ TRANG
  // =========================================================

  /*
   * useEffect chỉ có nhiệm vụ gọi API khi trang được mở.
   *
   * Hàm loadDocuments đã được useCallback nên có thể đưa
   * vào dependency một cách an toàn.
   */
  /* eslint-disable react-hooks/set-state-in-effect */
  useEffect(() => {
    void loadDocuments();
  }, [loadDocuments]);
  /* eslint-enable react-hooks/set-state-in-effect */

  // =========================================================
  // LỌC VĂN BẢN
  // =========================================================

  const filteredDocuments = useMemo(() => {
    const keyword = searchText.trim().toLowerCase();

    return documents.filter((document) => {
      const matchesSearch = document.title.toLowerCase().includes(keyword);

      const matchesType = !filterType || document.documentType === filterType;

      return matchesSearch && matchesType;
    });
  }, [documents, searchText, filterType]);

  // =========================================================
  // MỞ MODAL TẠO VĂN BẢN
  // =========================================================

  const handleCreate = () => {
    setEditingId(null);

    setForm({
      title: "",
      documentType: "Thông báo",
      content: "",
    });

    setError("");
    setModalOpen(true);
  };

  // =========================================================
  // MỞ MODAL CHỈNH SỬA
  // =========================================================

  const handleEdit = (document: DocumentItem) => {
    setEditingId(document.documentId);

    setForm({
      title: document.title,
      documentType: document.documentType,
      content: document.content || "",
    });

    setError("");
    setModalOpen(true);
  };

  // =========================================================
  // ĐÓNG MODAL
  // =========================================================

  const handleCloseModal = () => {
    if (saving) {
      return;
    }

    setModalOpen(false);
    setEditingId(null);

    setForm({
      title: "",
      documentType: "Thông báo",
      content: "",
    });
  };

  // =========================================================
  // LƯU VĂN BẢN
  // =========================================================

  const handleSubmit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    // Kiểm tra tiêu đề.
    if (!form.title.trim()) {
      setError("Vui lòng nhập tiêu đề văn bản.");
      return;
    }

    // Kiểm tra loại văn bản.
    if (!form.documentType.trim()) {
      setError("Vui lòng chọn loại văn bản.");
      return;
    }

    try {
      setSaving(true);
      setError("");

      // Dữ liệu gửi lên API.
      const requestData = {
        title: form.title.trim(),
        documentType: form.documentType.trim(),
        content: form.content.trim() || null,
        documentTemplateId: null,
      };

      // =====================================================
      // TẠO MỚI
      // =====================================================

      if (editingId === null) {
        await api.post("/documents", requestData);
      }

      // =====================================================
      // CẬP NHẬT
      // =====================================================
      else {
        await api.put(`/documents/${editingId}`, requestData);
      }

      // Đóng modal.
      setModalOpen(false);

      // Reset trạng thái chỉnh sửa.
      setEditingId(null);

      // Reset form.
      setForm({
        title: "",
        documentType: "Thông báo",
        content: "",
      });

      // Tải lại danh sách sau khi lưu.
      await loadDocuments();
    } catch (error) {
      console.error("Không thể lưu văn bản:", error);

      setError("Không thể lưu văn bản. Vui lòng thử lại.");
    } finally {
      setSaving(false);
    }
  };

  // =========================================================
  // XÓA VĂN BẢN
  // =========================================================

  const handleDelete = async (document: DocumentItem) => {
    const confirmed = window.confirm(
      `Bạn có chắc muốn xóa văn bản "${document.title}" không?`,
    );

    if (!confirmed) {
      return;
    }

    try {
      setError("");

      // Gọi API xóa.
      await api.delete(`/documents/${document.documentId}`);

      // Tải lại danh sách.
      await loadDocuments();
    } catch (error) {
      console.error("Không thể xóa văn bản:", error);

      setError("Không thể xóa văn bản. Vui lòng thử lại.");
    }
  };

  // =========================================================
  // ĐỊNH DẠNG NGÀY
  // =========================================================

  const formatDate = (dateString: string) => {
    const date = new Date(dateString);

    return date.toLocaleDateString("vi-VN", {
      day: "2-digit",
      month: "2-digit",
      year: "numeric",
    });
  };

  // =========================================================
  // GIAO DIỆN
  // =========================================================

  return (
    <main className="p-6">
      {/* =====================================================
          HEADER
          ===================================================== */}

      <div className="mb-6 flex flex-col justify-between gap-4 md:flex-row md:items-center">
        <div>
          <div className="flex items-center gap-3">
            <div className="flex h-11 w-11 items-center justify-center rounded-xl bg-blue-100 text-blue-700">
              <FileText size={23} />
            </div>

            <div>
              <h1 className="text-2xl font-bold text-slate-800">
                Quản lý văn bản
              </h1>

              <p className="mt-1 text-sm text-slate-500">
                Quản lý và hỗ trợ soạn thảo văn bản bằng AI.
              </p>
            </div>
          </div>
        </div>

        <button
          type="button"
          onClick={handleCreate}
          className="flex items-center justify-center gap-2 rounded-xl bg-blue-600 px-5 py-3 text-sm font-semibold text-white shadow-sm transition hover:bg-blue-700"
        >
          <Plus size={19} />
          Tạo văn bản
        </button>
      </div>

      {/* =====================================================
          ERROR
          ===================================================== */}

      {error && (
        <div className="mb-5 rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
          {error}
        </div>
      )}

      {/* =====================================================
          SOẠN THẢO VĂN BẢN BẰNG AI
          ===================================================== */}

      <section className="mb-6 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
        {/* Tiêu đề khu vực AI */}
        <div className="mb-5 flex items-center gap-3">
          <div className="flex h-11 w-11 items-center justify-center rounded-xl bg-blue-100 text-xl">
            ✨
          </div>

          <div>
            <h2 className="text-lg font-bold text-slate-800">
              Soạn thảo văn bản bằng AI
            </h2>

            <p className="mt-1 text-sm text-slate-500">
              Nhập yêu cầu để Gemini hỗ trợ soạn thảo nội dung văn bản.
            </p>
          </div>
        </div>

        {/* Loại văn bản + mẫu văn bản */}
        <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
          {/* Loại văn bản */}
          <div>
            <label
              htmlFor="ai-document-type"
              className="mb-2 block text-sm font-medium text-slate-700"
            >
              Loại văn bản
            </label>

            <select
              id="ai-document-type"
              value={aiDocumentType}
              onChange={(event) => setAiDocumentType(event.target.value)}
              disabled={isGeneratingAI}
              className="w-full rounded-xl border border-slate-300 bg-white px-4 py-3 text-sm outline-none transition focus:border-blue-500 focus:ring-2 focus:ring-blue-100 disabled:bg-slate-100"
            >
              {documentTypes.map((type) => (
                <option key={type} value={type}>
                  {type}
                </option>
              ))}
            </select>
          </div>

          {/* Mẫu văn bản */}
          <div>
            <label
              htmlFor="ai-template-name"
              className="mb-2 block text-sm font-medium text-slate-700"
            >
              Mẫu văn bản
            </label>

            <select
              id="ai-template-name"
              value={aiTemplateName}
              onChange={(event) => setAiTemplateName(event.target.value)}
              disabled={isGeneratingAI}
              className="w-full rounded-xl border border-slate-300 bg-white px-4 py-3 text-sm outline-none transition focus:border-blue-500 focus:ring-2 focus:ring-blue-100 disabled:bg-slate-100"
            >
              <option value="Thông báo hành chính">Thông báo hành chính</option>

              <option value="Công văn hành chính">Công văn hành chính</option>

              <option value="Báo cáo hành chính">Báo cáo hành chính</option>

              <option value="Biên bản hành chính">Biên bản hành chính</option>

              <option value="Quyết định hành chính">
                Quyết định hành chính
              </option>
            </select>
          </div>
        </div>

        {/* Yêu cầu cho AI */}
        <div className="mt-4">
          <label
            htmlFor="ai-prompt"
            className="mb-2 block text-sm font-medium text-slate-700"
          >
            Yêu cầu soạn thảo
          </label>

          <textarea
            id="ai-prompt"
            value={aiPrompt}
            onChange={(event) => setAiPrompt(event.target.value)}
            disabled={isGeneratingAI}
            rows={5}
            placeholder="Ví dụ: Soạn thông báo về việc nghỉ lễ Quốc khánh 02/9, yêu cầu cán bộ nhân viên hoàn thành công việc trước kỳ nghỉ..."
            className="w-full resize-y rounded-xl border border-slate-300 px-4 py-3 text-sm outline-none transition focus:border-blue-500 focus:ring-2 focus:ring-blue-100 disabled:bg-slate-100"
          />
        </div>

        {/* Lỗi AI */}
        {aiError && (
          <div className="mt-4 rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
            {aiError}
          </div>
        )}

        {/* Nút tạo AI */}
        <div className="mt-4 flex justify-end">
          <button
            type="button"
            onClick={handleGenerateAI}
            disabled={isGeneratingAI}
            className="rounded-xl bg-blue-600 px-5 py-3 text-sm font-semibold text-white shadow-sm transition hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-60"
          >
            {isGeneratingAI ? "Đang tạo văn bản..." : "✨ Tạo văn bản bằng AI"}
          </button>
        </div>
      </section>

      {/* =====================================================
          KẾT QUẢ AI
          ===================================================== */}

      {aiContent && (
        <section className="mb-6 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
          <div className="mb-4 flex flex-col justify-between gap-3 md:flex-row md:items-center">
            <div>
              <h2 className="text-lg font-bold text-slate-800">
                Nội dung do AI tạo
              </h2>

              <p className="mt-1 text-sm text-slate-500">
                Bạn có thể kiểm tra và chỉnh sửa nội dung trước khi lưu.
              </p>
            </div>

            <button
              type="button"
              onClick={() => navigator.clipboard.writeText(aiContent)}
              className="rounded-xl border border-slate-300 px-4 py-2 text-sm font-medium text-slate-700 transition hover:bg-slate-50"
            >
              Sao chép
            </button>
          </div>

          <textarea
            value={aiContent}
            onChange={(event) => setAiContent(event.target.value)}
            rows={20}
            className="w-full resize-y rounded-xl border border-slate-300 px-4 py-3 font-mono text-sm leading-6 outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100"
          />
        </section>
      )}

      {/* =====================================================
          SEARCH + FILTER
          ===================================================== */}

      {/* =====================================================
          SEARCH + FILTER
          ===================================================== */}

      <section className="mb-6 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
        <div className="grid gap-4 md:grid-cols-[1fr_220px]">
          {/* Tìm kiếm */}
          <div className="relative">
            <Search
              size={18}
              className="absolute left-3 top-1/2 -translate-y-1/2 text-slate-400"
            />

            <input
              type="text"
              value={searchText}
              onChange={(event) => setSearchText(event.target.value)}
              placeholder="Tìm kiếm theo tiêu đề văn bản..."
              className="w-full rounded-xl border border-slate-300 bg-white py-3 pl-10 pr-4 text-sm outline-none transition focus:border-blue-500 focus:ring-2 focus:ring-blue-100"
            />
          </div>

          {/* Lọc loại văn bản */}
          <select
            value={filterType}
            onChange={(event) => setFilterType(event.target.value)}
            className="rounded-xl border border-slate-300 bg-white px-4 py-3 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100"
          >
            <option value="">Tất cả loại văn bản</option>

            {documentTypes.map((type) => (
              <option key={type} value={type}>
                {type}
              </option>
            ))}
          </select>
        </div>
      </section>

      {/* =====================================================
          DOCUMENT TABLE
          ===================================================== */}

      <section className="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
        <div className="border-b border-slate-200 px-5 py-4">
          <h2 className="font-semibold text-slate-800">Danh sách văn bản</h2>

          <p className="mt-1 text-sm text-slate-500">
            {filteredDocuments.length} văn bản
          </p>
        </div>

        {/* ===================================================
            LOADING
            =================================================== */}

        {loading ? (
          <div className="flex items-center justify-center px-5 py-16 text-sm text-slate-500">
            Đang tải danh sách văn bản...
          </div>
        ) : filteredDocuments.length === 0 ? (
          /* =================================================
             EMPTY
             ================================================= */

          <div className="flex flex-col items-center justify-center px-5 py-16 text-center">
            <div className="mb-4 flex h-14 w-14 items-center justify-center rounded-full bg-slate-100 text-slate-400">
              <FileText size={26} />
            </div>

            <h3 className="font-semibold text-slate-700">Chưa có văn bản</h3>

            <p className="mt-1 text-sm text-slate-500">
              Hãy tạo văn bản đầu tiên để bắt đầu.
            </p>

            <button
              type="button"
              onClick={handleCreate}
              className="mt-4 flex items-center gap-2 rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700"
            >
              <Plus size={17} />
              Tạo văn bản
            </button>
          </div>
        ) : (
          /* =================================================
             TABLE
             ================================================= */

          <div className="overflow-x-auto">
            <table className="w-full min-w-[800px] text-left text-sm">
              <thead>
                <tr className="border-b border-slate-200 bg-slate-50 text-slate-500">
                  <th className="px-5 py-3 font-medium">Tiêu đề</th>

                  <th className="px-5 py-3 font-medium">Loại</th>

                  <th className="px-5 py-3 font-medium">Ngày tạo</th>

                  <th className="px-5 py-3 font-medium">Cập nhật</th>

                  <th className="px-5 py-3 text-right font-medium">Thao tác</th>
                </tr>
              </thead>

              <tbody>
                {filteredDocuments.map((document) => (
                  <tr
                    key={document.documentId}
                    className="border-b border-slate-100 last:border-0 hover:bg-slate-50"
                  >
                    {/* Tiêu đề */}
                    <td className="px-5 py-4">
                      <div className="flex items-center gap-3">
                        <div className="flex h-9 w-9 items-center justify-center rounded-lg bg-blue-50 text-blue-600">
                          <FileText size={18} />
                        </div>

                        <div>
                          <p className="font-medium text-slate-700">
                            {document.title}
                          </p>

                          <p className="mt-1 text-xs text-slate-400">
                            ID: {document.documentId}
                          </p>
                        </div>
                      </div>
                    </td>

                    {/* Loại */}
                    <td className="px-5 py-4">
                      <span className="rounded-full bg-blue-50 px-3 py-1 text-xs font-medium text-blue-700">
                        {document.documentType}
                      </span>
                    </td>

                    {/* Ngày tạo */}
                    <td className="px-5 py-4 text-slate-500">
                      {formatDate(document.createdAt)}
                    </td>

                    {/* Ngày cập nhật */}
                    <td className="px-5 py-4 text-slate-500">
                      {document.updatedAt
                        ? formatDate(document.updatedAt)
                        : "—"}
                    </td>

                    {/* Thao tác */}
                    <td className="px-5 py-4">
                      <div className="flex justify-end gap-2">
                        <button
                          type="button"
                          onClick={() => handleEdit(document)}
                          className="rounded-lg p-2 text-slate-500 hover:bg-blue-50 hover:text-blue-600"
                          title="Sửa văn bản"
                        >
                          <Pencil size={17} />
                        </button>

                        <button
                          type="button"
                          onClick={() => handleDelete(document)}
                          className="rounded-lg p-2 text-slate-500 hover:bg-red-50 hover:text-red-600"
                          title="Xóa văn bản"
                        >
                          <Trash2 size={17} />
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      {/* =====================================================
          CREATE / EDIT MODAL
          ===================================================== */}

      {modalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/40 px-4">
          <div className="max-h-[90vh] w-full max-w-2xl overflow-y-auto rounded-2xl bg-white shadow-2xl">
            {/* =================================================
                MODAL HEADER
                ================================================= */}

            <div className="flex items-center justify-between border-b border-slate-200 px-6 py-4">
              <div>
                <h2 className="text-lg font-semibold text-slate-800">
                  {editingId === null ? "Tạo văn bản" : "Chỉnh sửa văn bản"}
                </h2>

                <p className="mt-1 text-sm text-slate-500">
                  Nhập thông tin văn bản.
                </p>
              </div>

              <button
                type="button"
                onClick={handleCloseModal}
                disabled={saving}
                className="rounded-lg p-2 text-slate-500 hover:bg-slate-100 disabled:opacity-50"
              >
                <X size={20} />
              </button>
            </div>

            {/* =================================================
                MODAL FORM
                ================================================= */}

            <form onSubmit={handleSubmit} className="space-y-5 p-6">
              {/* Tiêu đề */}
              <div>
                <label
                  htmlFor="document-title"
                  className="mb-2 block text-sm font-medium text-slate-700"
                >
                  Tiêu đề
                </label>

                <input
                  id="document-title"
                  type="text"
                  value={form.title}
                  onChange={(event) =>
                    setForm({
                      ...form,
                      title: event.target.value,
                    })
                  }
                  placeholder="Nhập tiêu đề văn bản"
                  disabled={saving}
                  className="w-full rounded-xl border border-slate-300 px-4 py-3 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100 disabled:bg-slate-100"
                />
              </div>

              {/* Loại văn bản */}
              <div>
                <label
                  htmlFor="document-type"
                  className="mb-2 block text-sm font-medium text-slate-700"
                >
                  Loại văn bản
                </label>

                <select
                  id="document-type"
                  value={form.documentType}
                  onChange={(event) =>
                    setForm({
                      ...form,
                      documentType: event.target.value,
                    })
                  }
                  disabled={saving}
                  className="w-full rounded-xl border border-slate-300 px-4 py-3 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100 disabled:bg-slate-100"
                >
                  {documentTypes.map((type) => (
                    <option key={type} value={type}>
                      {type}
                    </option>
                  ))}
                </select>
              </div>

              {/* Nội dung */}
              <div>
                <label
                  htmlFor="document-content"
                  className="mb-2 block text-sm font-medium text-slate-700"
                >
                  Nội dung
                </label>

                <textarea
                  id="document-content"
                  value={form.content}
                  onChange={(event) =>
                    setForm({
                      ...form,
                      content: event.target.value,
                    })
                  }
                  placeholder="Nhập nội dung văn bản..."
                  rows={10}
                  disabled={saving}
                  className="w-full resize-y rounded-xl border border-slate-300 px-4 py-3 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100 disabled:bg-slate-100"
                />
              </div>

              {/* =================================================
                  MODAL BUTTONS
                  ================================================= */}

              <div className="flex justify-end gap-3 border-t border-slate-200 pt-5">
                <button
                  type="button"
                  onClick={handleCloseModal}
                  disabled={saving}
                  className="rounded-xl border border-slate-300 px-5 py-2.5 text-sm font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-50"
                >
                  Hủy
                </button>

                <button
                  type="submit"
                  disabled={saving}
                  className="rounded-xl bg-blue-600 px-5 py-2.5 text-sm font-semibold text-white hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-60"
                >
                  {saving
                    ? "Đang lưu..."
                    : editingId === null
                      ? "Tạo văn bản"
                      : "Lưu thay đổi"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </main>
  );
}

export default Documents;
