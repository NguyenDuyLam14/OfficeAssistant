import { useCallback, useEffect, useMemo, useState } from "react";
import { AxiosError } from "axios";
import {
  FileText,
  Plus,
  Search,
  Pencil,
  Trash2,
  X,
  CheckCircle,
  XCircle,
} from "lucide-react";

import api from "../services/api";

/*
 * Kiểu dữ liệu của một Template.
 * Các thuộc tính này tương ứng với DocumentTemplate
 * ở Backend ASP.NET Core.
 */
interface DocumentTemplate {
  documentTemplateId: number;
  templateName: string;
  documentType: string;
  filePath: string | null;
  description: string | null;
  isActive: boolean;
  createdAt: string;
}

/*
 * Dữ liệu dùng khi tạo Template mới.
 */
interface CreateTemplateForm {
  templateName: string;
  documentType: string;
  filePath: string;
  description: string;
}

/*
 * Dữ liệu dùng khi chỉnh sửa Template.
 */
interface EditTemplateForm extends CreateTemplateForm {
  isActive: boolean;
}

function Templates() {
  // Danh sách Template lấy từ API.
  const [templates, setTemplates] = useState<DocumentTemplate[]>([]);

  // Trạng thái đang tải dữ liệu.
  const [loading, setLoading] = useState(true);

  // Thông báo lỗi.
  const [error, setError] = useState("");

  // Nội dung tìm kiếm.
  const [searchText, setSearchText] = useState("");

  // Điều khiển modal.
  const [showModal, setShowModal] = useState(false);

  // Xác định modal đang dùng để thêm hay sửa.
  const [editingId, setEditingId] = useState<number | null>(null);

  // Form thêm Template.
  const [createForm, setCreateForm] = useState<CreateTemplateForm>({
    templateName: "",
    documentType: "",
    filePath: "",
    description: "",
  });

  // Form sửa Template.
  const [editForm, setEditForm] = useState<EditTemplateForm>({
    templateName: "",
    documentType: "",
    filePath: "",
    description: "",
    isActive: true,
  });

  // Trạng thái đang lưu dữ liệu.
  const [saving, setSaving] = useState(false);

  /*
   * Lấy danh sách Template từ Backend.
   */
  const loadTemplates = useCallback(async () => {
    try {
      setLoading(true);
      setError("");

      const response = await api.get<DocumentTemplate[]>("/DocumentTemplates");

      setTemplates(response.data);
    } catch (err) {
      console.error(err);

      setError(
        "Không thể tải danh sách mẫu văn bản. Vui lòng kiểm tra Backend.",
      );
    } finally {
      setLoading(false);
    }
  }, []);

  /*
   * Khi trang được mở,
   * gọi API lấy danh sách Template.
   */
  /* eslint-disable react-hooks/set-state-in-effect */
  useEffect(() => {
    void loadTemplates();
  }, [loadTemplates]);
  /* eslint-enable react-hooks/set-state-in-effect */

  /*
   * Lọc Template theo nội dung tìm kiếm.
   *
   * Có thể tìm theo:
   * - Tên Template
   * - Loại văn bản
   * - Mô tả
   */
  const filteredTemplates = useMemo(() => {
    const keyword = searchText.trim().toLowerCase();

    if (!keyword) {
      return templates;
    }

    return templates.filter((template) =>
      [template.templateName, template.documentType, template.description ?? ""]
        .join(" ")
        .toLowerCase()
        .includes(keyword),
    );
  }, [templates, searchText]);

  /*
   * Mở modal thêm Template.
   */
  const openCreateModal = () => {
    setEditingId(null);

    setCreateForm({
      templateName: "",
      documentType: "",
      filePath: "",
      description: "",
    });

    setError("");
    setShowModal(true);
  };

  /*
   * Mở modal chỉnh sửa Template.
   */
  const openEditModal = (template: DocumentTemplate) => {
    setEditingId(template.documentTemplateId);

    setEditForm({
      templateName: template.templateName,
      documentType: template.documentType,
      filePath: template.filePath ?? "",
      description: template.description ?? "",
      isActive: template.isActive,
    });

    setError("");
    setShowModal(true);
  };

  /*
   * Đóng modal.
   */
  const closeModal = () => {
    if (saving) return;

    setShowModal(false);
    setEditingId(null);
    setError("");
  };

  /*
   * Xử lý thay đổi Form thêm Template.
   */
  const handleCreateChange = (
    field: keyof CreateTemplateForm,
    value: string,
  ) => {
    setCreateForm((current) => ({
      ...current,
      [field]: value,
    }));
  };

  /*
   * Xử lý thay đổi Form sửa Template.
   */
  const handleEditChange = (
    field: keyof EditTemplateForm,
    value: string | boolean,
  ) => {
    setEditForm((current) => ({
      ...current,
      [field]: value,
    }));
  };

  /*
   * Gửi API tạo Template.
   */
  const handleCreate = async () => {
    if (!createForm.templateName.trim()) {
      setError("Vui lòng nhập tên mẫu văn bản.");
      return;
    }

    if (!createForm.documentType.trim()) {
      setError("Vui lòng nhập loại văn bản.");
      return;
    }

    try {
      setSaving(true);
      setError("");

      await api.post("/DocumentTemplates", {
        templateName: createForm.templateName.trim(),
        documentType: createForm.documentType.trim(),
        filePath: createForm.filePath.trim() || null,
        description: createForm.description.trim() || null,
      });

      setShowModal(false);

      await loadTemplates();
    } catch (err) {
      console.error(err);

      const axiosError = err as AxiosError<{
        message?: string;
      }>;

      const message =
        axiosError.response?.data?.message ?? "Không thể tạo mẫu văn bản.";

      setError(message);
    } finally {
      setSaving(false);
    }
  };

  /*
   * Gửi API cập nhật Template.
   */
  const handleUpdate = async () => {
    if (editingId === null) return;

    if (!editForm.templateName.trim()) {
      setError("Vui lòng nhập tên mẫu văn bản.");
      return;
    }

    if (!editForm.documentType.trim()) {
      setError("Vui lòng nhập loại văn bản.");
      return;
    }

    try {
      setSaving(true);
      setError("");

      await api.put(`/DocumentTemplates/${editingId}`, {
        templateName: editForm.templateName.trim(),
        documentType: editForm.documentType.trim(),
        filePath: editForm.filePath.trim() || null,
        description: editForm.description.trim() || null,
        isActive: editForm.isActive,
      });

      setShowModal(false);
      setEditingId(null);

      await loadTemplates();
    } catch (err) {
      console.error(err);

      const axiosError = err as AxiosError<{
        message?: string;
      }>;

      const message =
        axiosError.response?.data?.message ?? "Không thể cập nhật mẫu văn bản.";

      setError(message);
    } finally {
      setSaving(false);
    }
  };

  /*
   * Xóa Template.
   */
  const handleDelete = async (template: DocumentTemplate) => {
    const confirmed = window.confirm(
      `Bạn có chắc muốn xóa mẫu "${template.templateName}" không?`,
    );

    if (!confirmed) return;

    try {
      setError("");

      await api.delete(`/DocumentTemplates/${template.documentTemplateId}`);

      await loadTemplates();
    } catch (err) {
      console.error(err);

      const axiosError = err as AxiosError<{
        message?: string;
      }>;

      const message =
        axiosError.response?.data?.message ?? "Không thể xóa mẫu văn bản.";

      setError(message);
    }
  };

  return (
    <main className="p-6">
      {/* =========================
          TIÊU ĐỀ TRANG
          ========================= */}
      <div className="mb-6 flex flex-col gap-4 md:flex-row md:items-center md:justify-between">
        <div>
          <div className="flex items-center gap-3">
            <div className="rounded-xl bg-blue-100 p-3 text-blue-600">
              <FileText size={26} />
            </div>

            <div>
              <h1 className="text-2xl font-bold text-slate-800">Mẫu văn bản</h1>

              <p className="text-sm text-slate-500">
                Quản lý các mẫu văn bản sử dụng trong hệ thống
              </p>
            </div>
          </div>
        </div>

        <button
          type="button"
          onClick={openCreateModal}
          className="flex items-center justify-center gap-2 rounded-xl bg-blue-600 px-5 py-3 font-medium text-white shadow-sm transition hover:bg-blue-700"
        >
          <Plus size={20} />
          Thêm mẫu văn bản
        </button>
      </div>

      {/* =========================
          THÔNG BÁO LỖI
          ========================= */}
      {error && !showModal && (
        <div className="mb-5 flex items-center justify-between rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
          <span>{error}</span>

          <button
            type="button"
            onClick={() => setError("")}
            className="text-red-500 hover:text-red-700"
          >
            <X size={18} />
          </button>
        </div>
      )}

      {/* =========================
          THANH TÌM KIẾM
          ========================= */}
      <div className="mb-6 rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
        <div className="relative">
          <Search
            size={20}
            className="absolute left-3 top-1/2 -translate-y-1/2 text-slate-400"
          />

          <input
            type="text"
            value={searchText}
            onChange={(event) => setSearchText(event.target.value)}
            placeholder="Tìm kiếm theo tên mẫu, loại văn bản hoặc mô tả..."
            className="w-full rounded-xl border border-slate-200 py-3 pl-10 pr-4 outline-none transition focus:border-blue-500 focus:ring-2 focus:ring-blue-100"
          />
        </div>
      </div>

      {/* =========================
          DANH SÁCH TEMPLATE
          ========================= */}
      <div className="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
        <div className="border-b border-slate-200 px-5 py-4">
          <h2 className="font-semibold text-slate-800">
            Danh sách mẫu văn bản
          </h2>

          <p className="mt-1 text-sm text-slate-500">
            {filteredTemplates.length} mẫu văn bản
          </p>
        </div>

        {loading ? (
          <div className="flex items-center justify-center py-16 text-slate-500">
            Đang tải dữ liệu...
          </div>
        ) : filteredTemplates.length === 0 ? (
          <div className="flex flex-col items-center justify-center px-6 py-16 text-center">
            <div className="mb-4 rounded-full bg-slate-100 p-4 text-slate-400">
              <FileText size={32} />
            </div>

            <h3 className="font-semibold text-slate-700">
              Chưa có mẫu văn bản
            </h3>

            <p className="mt-1 text-sm text-slate-500">
              Hãy thêm mẫu văn bản đầu tiên để bắt đầu.
            </p>
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[900px]">
              <thead className="bg-slate-50">
                <tr>
                  <th className="px-5 py-4 text-left text-sm font-semibold text-slate-600">
                    Tên mẫu
                  </th>

                  <th className="px-5 py-4 text-left text-sm font-semibold text-slate-600">
                    Loại văn bản
                  </th>

                  <th className="px-5 py-4 text-left text-sm font-semibold text-slate-600">
                    Mô tả
                  </th>

                  <th className="px-5 py-4 text-center text-sm font-semibold text-slate-600">
                    Trạng thái
                  </th>

                  <th className="px-5 py-4 text-left text-sm font-semibold text-slate-600">
                    Ngày tạo
                  </th>

                  <th className="px-5 py-4 text-center text-sm font-semibold text-slate-600">
                    Thao tác
                  </th>
                </tr>
              </thead>

              <tbody className="divide-y divide-slate-100">
                {filteredTemplates.map((template) => (
                  <tr
                    key={template.documentTemplateId}
                    className="transition hover:bg-slate-50"
                  >
                    <td className="px-5 py-4">
                      <div className="flex items-center gap-3">
                        <div className="rounded-lg bg-blue-50 p-2 text-blue-600">
                          <FileText size={18} />
                        </div>

                        <span className="font-medium text-slate-800">
                          {template.templateName}
                        </span>
                      </div>
                    </td>

                    <td className="px-5 py-4 text-sm text-slate-600">
                      {template.documentType}
                    </td>

                    <td className="max-w-xs px-5 py-4 text-sm text-slate-500">
                      {template.description || "Không có mô tả"}
                    </td>

                    <td className="px-5 py-4 text-center">
                      {template.isActive ? (
                        <span className="inline-flex items-center gap-1 rounded-full bg-green-100 px-3 py-1 text-xs font-medium text-green-700">
                          <CheckCircle size={14} />
                          Đang sử dụng
                        </span>
                      ) : (
                        <span className="inline-flex items-center gap-1 rounded-full bg-slate-100 px-3 py-1 text-xs font-medium text-slate-500">
                          <XCircle size={14} />
                          Tạm ngưng
                        </span>
                      )}
                    </td>

                    <td className="px-5 py-4 text-sm text-slate-500">
                      {new Date(template.createdAt).toLocaleDateString("vi-VN")}
                    </td>

                    <td className="px-5 py-4">
                      <div className="flex items-center justify-center gap-2">
                        <button
                          type="button"
                          onClick={() => openEditModal(template)}
                          className="rounded-lg p-2 text-blue-600 transition hover:bg-blue-50"
                          title="Chỉnh sửa"
                        >
                          <Pencil size={18} />
                        </button>

                        <button
                          type="button"
                          onClick={() => void handleDelete(template)}
                          className="rounded-lg p-2 text-red-600 transition hover:bg-red-50"
                          title="Xóa"
                        >
                          <Trash2 size={18} />
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* =========================
          MODAL THÊM / SỬA
          ========================= */}
      {showModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
          <div className="w-full max-w-2xl rounded-2xl bg-white shadow-2xl">
            {/* Header modal */}
            <div className="flex items-center justify-between border-b border-slate-200 px-6 py-4">
              <div>
                <h2 className="text-xl font-bold text-slate-800">
                  {editingId === null
                    ? "Thêm mẫu văn bản"
                    : "Chỉnh sửa mẫu văn bản"}
                </h2>

                <p className="mt-1 text-sm text-slate-500">
                  Nhập thông tin mẫu văn bản
                </p>
              </div>

              <button
                type="button"
                onClick={closeModal}
                className="rounded-lg p-2 text-slate-400 hover:bg-slate-100 hover:text-slate-600"
              >
                <X size={22} />
              </button>
            </div>

            {/* Nội dung modal */}
            <div className="space-y-5 px-6 py-6">
              {error && (
                <div className="rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
                  {error}
                </div>
              )}

              {editingId === null ? (
                <>
                  {/* Tên mẫu */}
                  <div>
                    <label className="mb-2 block text-sm font-medium text-slate-700">
                      Tên mẫu văn bản
                    </label>

                    <input
                      type="text"
                      value={createForm.templateName}
                      onChange={(event) =>
                        handleCreateChange("templateName", event.target.value)
                      }
                      placeholder="Ví dụ: Mẫu thông báo hành chính"
                      className="w-full rounded-xl border border-slate-200 px-4 py-3 outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100"
                    />
                  </div>

                  {/* Loại văn bản */}
                  <div>
                    <label className="mb-2 block text-sm font-medium text-slate-700">
                      Loại văn bản
                    </label>

                    <input
                      type="text"
                      value={createForm.documentType}
                      onChange={(event) =>
                        handleCreateChange("documentType", event.target.value)
                      }
                      placeholder="Ví dụ: Thông báo"
                      className="w-full rounded-xl border border-slate-200 px-4 py-3 outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100"
                    />
                  </div>

                  {/* Đường dẫn file */}
                  <div>
                    <label className="mb-2 block text-sm font-medium text-slate-700">
                      Đường dẫn file template
                    </label>

                    <input
                      type="text"
                      value={createForm.filePath}
                      onChange={(event) =>
                        handleCreateChange("filePath", event.target.value)
                      }
                      placeholder="Có thể để trống ở giai đoạn hiện tại"
                      className="w-full rounded-xl border border-slate-200 px-4 py-3 outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100"
                    />
                  </div>

                  {/* Mô tả */}
                  <div>
                    <label className="mb-2 block text-sm font-medium text-slate-700">
                      Mô tả
                    </label>

                    <textarea
                      rows={4}
                      value={createForm.description}
                      onChange={(event) =>
                        handleCreateChange("description", event.target.value)
                      }
                      placeholder="Mô tả mục đích sử dụng của mẫu..."
                      className="w-full resize-none rounded-xl border border-slate-200 px-4 py-3 outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100"
                    />
                  </div>
                </>
              ) : (
                <>
                  {/* Tên mẫu */}
                  <div>
                    <label className="mb-2 block text-sm font-medium text-slate-700">
                      Tên mẫu văn bản
                    </label>

                    <input
                      type="text"
                      value={editForm.templateName}
                      onChange={(event) =>
                        handleEditChange("templateName", event.target.value)
                      }
                      className="w-full rounded-xl border border-slate-200 px-4 py-3 outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100"
                    />
                  </div>

                  {/* Loại văn bản */}
                  <div>
                    <label className="mb-2 block text-sm font-medium text-slate-700">
                      Loại văn bản
                    </label>

                    <input
                      type="text"
                      value={editForm.documentType}
                      onChange={(event) =>
                        handleEditChange("documentType", event.target.value)
                      }
                      className="w-full rounded-xl border border-slate-200 px-4 py-3 outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100"
                    />
                  </div>

                  {/* Đường dẫn file */}
                  <div>
                    <label className="mb-2 block text-sm font-medium text-slate-700">
                      Đường dẫn file template
                    </label>

                    <input
                      type="text"
                      value={editForm.filePath}
                      onChange={(event) =>
                        handleEditChange("filePath", event.target.value)
                      }
                      className="w-full rounded-xl border border-slate-200 px-4 py-3 outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100"
                    />
                  </div>

                  {/* Mô tả */}
                  <div>
                    <label className="mb-2 block text-sm font-medium text-slate-700">
                      Mô tả
                    </label>

                    <textarea
                      rows={4}
                      value={editForm.description}
                      onChange={(event) =>
                        handleEditChange("description", event.target.value)
                      }
                      className="w-full resize-none rounded-xl border border-slate-200 px-4 py-3 outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100"
                    />
                  </div>

                  {/* Trạng thái */}
                  <label className="flex cursor-pointer items-center gap-3">
                    <input
                      type="checkbox"
                      checked={editForm.isActive}
                      onChange={(event) =>
                        handleEditChange("isActive", event.target.checked)
                      }
                      className="h-4 w-4 rounded border-slate-300"
                    />

                    <span className="text-sm font-medium text-slate-700">
                      Cho phép sử dụng mẫu văn bản
                    </span>
                  </label>
                </>
              )}
            </div>

            {/* Footer modal */}
            <div className="flex justify-end gap-3 border-t border-slate-200 px-6 py-4">
              <button
                type="button"
                onClick={closeModal}
                disabled={saving}
                className="rounded-xl border border-slate-200 px-5 py-2.5 font-medium text-slate-600 hover:bg-slate-50 disabled:opacity-50"
              >
                Hủy
              </button>

              <button
                type="button"
                disabled={saving}
                onClick={() =>
                  editingId === null ? void handleCreate() : void handleUpdate()
                }
                className="rounded-xl bg-blue-600 px-5 py-2.5 font-medium text-white hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-50"
              >
                {saving
                  ? "Đang lưu..."
                  : editingId === null
                    ? "Thêm mẫu"
                    : "Lưu thay đổi"}
              </button>
            </div>
          </div>
        </div>
      )}
    </main>
  );
}

export default Templates;
