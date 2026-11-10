import { useCallback, useEffect, useMemo, useState } from "react";
import UploadSummaryPanel from "../components/documents/UploadSummaryPanel";

import {
  FileText,
  Plus,
  Search,
  Pencil,
  Trash2,
  X,
  Sparkles,
  Download,
  Presentation,
} from "lucide-react";

import SummaryModal from "../components/common/SummaryModal";

import PresentationModal from "../components/presentation/PresentationModal";

import {
  askDocument,
  askText,
  summarizeDocument,
  summarizeText,
} from "../services/fileService";

import type { AnswerResult, SummaryResult } from "../services/fileService";

import api from "../services/api";

import axios from "axios";

// =========================================================
// DỮ LIỆU API AI
// =========================================================

// Dữ liệu gửi từ React lên API AI.
interface GenerateDocumentRequest {
  documentType: string;
  documentTemplateId: number;
  userPrompt: string;
}

// Dữ liệu API AI trả về cho React.
interface GenerateDocumentResponse {
  message: string;
  content: string;
}

// =========================================================
// DỮ LIỆU MẪU VĂN BẢN
// =========================================================

// Đại diện cho một mẫu văn bản lấy từ API.
// Dữ liệu này tương ứng với bảng DocumentTemplates
// trong SQL Server.
type DocumentTemplate = {
  documentTemplateId: number;
  templateName: string;
  documentType: string;
  filePath: string | null;
  description: string | null;
  isActive: boolean;
  createdAt: string;
};

type DocumentItem = {
  documentId: number;
  title: string;
  documentType: string;
  content: string | null;
  userId: number;
  documentTemplateId: number | null;
  createdAt: string;
  updatedAt: string | null;
  hasFile?: boolean;
};

type DocumentForm = {
  title: string;
  documentType: string;
  content: string;
};

type TemplateFieldInfo = {
  fieldId: string;
  originalText: string;
  context: string;
};

const documentTypes = [
  "Thông báo",
  "Công văn",
  "Báo cáo",
  "Biên bản",
  "Quyết định",
  "Khác",
];

// Gợi ý nội dung cần nêu và ví dụ mẫu cho từng loại văn bản.
const PROMPT_GUIDES: Record<string, { hints: string[]; example: string }> = {
  "Thông báo": {
    hints: [
      "Đơn vị ban hành",
      "Nội dung chính",
      "Thời gian",
      "Địa điểm",
      "Đối tượng áp dụng",
      "Yêu cầu / lưu ý",
      "Người ký",
    ],
    example:
      "Soạn thông báo nghỉ lễ Quốc khánh 02/9 cho toàn thể CBNV. " +
      "Nghỉ từ 30/8 đến hết 03/9, đi làm lại ngày 04/9. " +
      "Bộ phận trực: Bảo vệ, IT. Người ký: Giám đốc Nguyễn Văn A.",
  },
  "Công văn": {
    hints: [
      "Cơ quan nhận",
      "Trích yếu (V/v)",
      "Căn cứ / lý do",
      "Nội dung đề nghị",
      "Thời hạn phản hồi",
      "Người ký",
    ],
    example:
      "Soạn công văn gửi Sở Tài chính đề nghị gia hạn nộp báo cáo quý 3 " +
      "đến 20/10 vì lý do chuyển đổi phần mềm kế toán.",
  },
  "Báo cáo": {
    hints: [
      "Kỳ báo cáo",
      "Đơn vị báo cáo",
      "Kết quả đạt được",
      "Số liệu chính",
      "Tồn tại / khó khăn",
      "Kiến nghị",
    ],
    example:
      "Báo cáo kết quả hoạt động tháng 10 của phòng Hành chính: " +
      "xử lý 120 văn bản, tổ chức 3 cuộc họp, còn tồn 5 hồ sơ.",
  },
  "Biên bản": {
    hints: [
      "Thời gian",
      "Địa điểm",
      "Thành phần tham dự",
      "Nội dung thảo luận",
      "Kết luận / phân công",
      "Chủ trì & thư ký",
    ],
    example:
      "Biên bản họp giao ban sáng 05/11 tại phòng họp tầng 2, " +
      "chủ trì: Trưởng phòng; thư ký: Lê Thị B. " +
      "Kết luận: hoàn thành báo cáo tuần trước thứ Sáu.",
  },
  "Quyết định": {
    hints: [
      "Căn cứ pháp lý",
      "Nội dung quyết định",
      "Đối tượng",
      "Hiệu lực từ ngày",
      "Người ký",
    ],
    example:
      "Quyết định bổ nhiệm ông Trần Văn C làm Trưởng phòng Kinh doanh, " +
      "có hiệu lực từ 01/12/2026.",
  },
  Khác: {
    hints: [
      "Mục đích văn bản",
      "Người nhận",
      "Nội dung chính",
      "Thời gian",
      "Người ký",
    ],
    example:
      "Nêu ngắn gọn mục đích, người nhận, nội dung chính và người ký của văn bản.",
  },
};

// Huy hiệu số bước.
function StepBadge({ n }: { n: number }) {
  return (
    <span className="mr-2 inline-flex h-5 w-5 items-center justify-center rounded-full bg-blue-600 text-xs font-bold text-white">
      {n}
    </span>
  );
}

function Documents() {
  // =========================================================
  // STATE
  // =========================================================

  const [pptDoc, setPptDoc] = useState<DocumentItem | null>(null);

  // Danh sách văn bản.
  const [documents, setDocuments] = useState<DocumentItem[]>([]);

  // Danh sách mẫu văn bản lấy từ SQL Server.
  const [templates, setTemplates] = useState<DocumentTemplate[]>([]);

  // Trạng thái tải danh sách mẫu.
  const [templatesLoading, setTemplatesLoading] = useState(true);

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

  // Hộp thoại tóm tắt ý chính.
  const [summaryTarget, setSummaryTarget] = useState<{
    title: string;
    load: () => Promise<SummaryResult>;
    ask?: (question: string) => Promise<AnswerResult>;
  } | null>(null);

  // =========================================================
  // STATE CHO CHỨC NĂNG SOẠN THẢO BẰNG AI
  // =========================================================

  // Loại văn bản mà người dùng muốn AI soạn.
  const [aiDocumentType, setAiDocumentType] = useState("Thông báo");

  // ID mẫu văn bản mà người dùng muốn sử dụng.
  // null = chưa chọn mẫu.
  const [aiTemplateId, setAiTemplateId] = useState<number | null>(null);

  // Yêu cầu cụ thể gửi cho AI.
  const [aiPrompt, setAiPrompt] = useState("");

  // Nội dung văn bản do AI tạo ra.
  const [aiContent, setAiContent] = useState("");

  // Trạng thái đang gọi API AI.
  const [isGeneratingAI, setIsGeneratingAI] = useState(false);

  // Lỗi riêng của chức năng AI.
  const [aiError, setAiError] = useState("");

  // =========================================================
  // STATE CHO CHỨC NĂNG AI SOẠN VÀ XUẤT WORD
  // =========================================================

  // Trạng thái đang gọi API tạo file Word.
  const [isGeneratingWord, setIsGeneratingWord] = useState(false);

  // Lỗi riêng của chức năng xuất Word.
  const [wordError, setWordError] = useState("");

  // Danh sách trường của mẫu đang chọn.
  const [templateFields, setTemplateFields] = useState<TemplateFieldInfo[]>([]);

  // Giá trị người dùng nhập: key = "[F001]", value = nội dung.
  const [fieldValues, setFieldValues] = useState<Record<string, string>>({});

  // Đang tải danh sách trường.
  const [fieldsLoading, setFieldsLoading] = useState(false);

  // Chỉ giữ những trường người dùng thực sự đã nhập.
  const filledFieldValues = Object.fromEntries(
    Object.entries(fieldValues).filter(([, value]) => value.trim()),
  );

  // Có nhập ít nhất một trường hay không.
  const hasFieldValues = Object.keys(filledFieldValues).length > 0;

  // Xóa danh sách trường và giá trị đã nhập.
  const clearTemplateFields = () => {
    setTemplateFields([]);
    setFieldValues({});
  };

  // Hiển thị "Đã sao chép" trong 2 giây.
  const [copied, setCopied] = useState(false);

  // ---------- 2.2 BIẾN + HÀM MỚI ----------

  // Gợi ý theo loại văn bản đang chọn.
  const guide = PROMPT_GUIDES[aiDocumentType] ?? PROMPT_GUIDES["Khác"];

  // Mẫu đang chọn (để lấy tên mẫu).
  const selectedTemplate = templates.find(
    (template) => template.documentTemplateId === aiTemplateId,
  );

  // Điều kiện bật nút.
  const hasPrompt = aiPrompt.trim().length > 0;
  const canGenerateText = aiTemplateId !== null && hasPrompt;
  const canExportWord = aiTemplateId !== null && (hasPrompt || hasFieldValues);

  // Lý do nút bị khóa (hiển thị cho người dùng hiểu).
  const textHelp =
    aiTemplateId === null
      ? "Chọn mẫu văn bản ở bước 1 để tiếp tục."
      : !hasPrompt
        ? "Nhập yêu cầu ở bước 3 để tạo nội dung."
        : "";

  const wordHelp =
    aiTemplateId === null
      ? "Chọn mẫu văn bản ở bước 1 để xuất Word."
      : !hasPrompt && !hasFieldValues
        ? "Nhập yêu cầu (bước 3) hoặc điền ít nhất một trường (bước 2)."
        : "";

  const isBusy = isGeneratingAI || isGeneratingWord;

  // Chèn một dòng gợi ý vào ô yêu cầu: "Thời gian: ".
  const appendHint = (hint: string) => {
    setAiPrompt((current) => {
      const prefix = current.trim() ? current.replace(/\s+$/, "") + "\n" : "";
      return `${prefix}${hint}: `;
    });

    // Đưa con trỏ về ô nhập sau khi chèn.
    setTimeout(() => {
      const element = document.getElementById(
        "ai-prompt",
      ) as HTMLTextAreaElement | null;

      if (element) {
        element.focus();
        element.setSelectionRange(element.value.length, element.value.length);
      }
    }, 0);
  };

  // Xóa yêu cầu, các trường đã nhập và kết quả.
  const handleResetAI = () => {
    setAiPrompt("");
    setAiContent("");
    setAiError("");
    setWordError("");
    setFieldValues({});
  };

  // Sao chép nội dung AI tạo ra.
  const handleCopyAI = async () => {
    try {
      await navigator.clipboard.writeText(aiContent);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      setAiError("Không thể sao chép. Hãy chọn nội dung và nhấn Ctrl + C.");
    }
  };

  // Lưu nội dung AI vào danh sách văn bản (mở modal tạo văn bản có sẵn nội dung).
  const handleSaveAiContent = () => {
    setEditingId(null);

    setForm({
      title: selectedTemplate?.templateName ?? "",
      documentType: aiDocumentType,
      content: aiContent,
    });

    setError("");
    setModalOpen(true);
  };

  // Khi người dùng chọn mẫu: lưu ID rồi tải các trường của mẫu.
  const handleSelectTemplate = async (value: string) => {
    const id = value ? Number(value) : null;

    setAiTemplateId(id);
    clearTemplateFields();

    if (id === null) return;

    try {
      setFieldsLoading(true);

      const response = await api.get<TemplateFieldInfo[]>(
        `/Word/template-fields/${id}`,
      );

      setTemplateFields(response.data);
    } catch (error) {
      console.error("Không thể tải các trường của mẫu:", error);
      // Không chặn người dùng: vẫn có thể để AI tự điền hết.
    } finally {
      setFieldsLoading(false);
    }
  };

  /**
   * Gọi API backend để AI sinh nội dung văn bản.
   *
   * Frontend không gọi AI trực tiếp.
   * Frontend chỉ gọi ASP.NET Core API.
   *
   * Luồng:
   *
   * React
   *   ↓
   * ASP.NET Core API
   *   ↓
   * AI API
   *   ↓
   * ASP.NET Core
   *   ↓
   * React
   */
  // Tạo nội dung dạng văn bản để xem trước và chỉnh sửa.
  const handleGenerateAI = async () => {
    if (aiTemplateId === null) {
      setAiError("Vui lòng chọn mẫu văn bản.");
      return;
    }

    if (!aiPrompt.trim()) {
      setAiError("Vui lòng nhập yêu cầu soạn thảo.");
      return;
    }

    setAiError("");
    setWordError("");
    setIsGeneratingAI(true);

    try {
      const response = await api.post<GenerateDocumentResponse>(
        "/AI/generate-document",
        {
          documentType: aiDocumentType,
          documentTemplateId: aiTemplateId,
          userPrompt: aiPrompt.trim(),
        } satisfies GenerateDocumentRequest,
      );

      setAiContent(response.data.content);
    } catch (error) {
      console.error("Lỗi khi gọi API AI:", error);

      if (axios.isAxiosError(error)) {
        const message = error.response?.data?.message;

        setAiError(message ?? "Không thể kết nối tới dịch vụ AI.");
      } else if (error instanceof Error) {
        setAiError(error.message);
      } else {
        setAiError("Đã xảy ra lỗi khi gọi AI.");
      }
    } finally {
      setIsGeneratingAI(false);
    }
  };

  // =========================================================
  // AI SOẠN NỘI DUNG + XUẤT FILE WORD
  // =========================================================

  /**
   * Gọi backend để:
   *
   * React
   *   ↓
   * ASP.NET Core
   *   ↓
   * AI
   *   ↓
   * Lấy placeholder từ template
   *   ↓
   * OpenXML thay nội dung
   *   ↓
   * Trả file .docx
   *
   * Frontend nhận file dạng Blob
   * và tự động tải xuống máy.
   */
  const handleGenerateAIWord = async () => {
    if (aiTemplateId === null) {
      setWordError("Vui lòng chọn mẫu văn bản trước khi xuất Word.");
      return;
    }

    // Khớp với backend: chấp nhận khi có yêu cầu HOẶC có ít nhất một trường.
    if (!aiPrompt.trim() && !hasFieldValues) {
      setWordError("Vui lòng nhập yêu cầu hoặc điền ít nhất một trường.");
      return;
    }

    try {
      setIsGeneratingWord(true);
      setWordError("");
      setAiError("");

      const response = await api.post(
        "/Word/generate-from-ai",
        {
          documentTemplateId: aiTemplateId,
          documentType: aiDocumentType,
          userPrompt: aiPrompt.trim(),
          fieldValues: filledFieldValues,
        },
        {
          responseType: "blob",
        },
      );

      const blob = new Blob([response.data], {
        type: "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
      });

      const url = window.URL.createObjectURL(blob);
      const link = document.createElement("a");

      link.href = url;
      link.download = `AI_${aiDocumentType}_${new Date()
        .toISOString()
        .slice(0, 10)}.docx`;

      document.body.appendChild(link);
      link.click();
      link.remove();

      window.URL.revokeObjectURL(url);
      await loadDocuments();
    } catch (error) {
      console.error("Lỗi AI soạn và xuất Word:", error);

      let message = "Không thể tạo file Word bằng AI.";

      if (axios.isAxiosError(error)) {
        const responseData = error.response?.data;

        // API trả lỗi dạng Blob nên phải đọc thành text.
        if (responseData instanceof Blob) {
          try {
            const errorText = await responseData.text();
            const errorJson = JSON.parse(errorText);

            if (errorJson.message) {
              message = errorJson.message;
            }
          } catch {
            // Giữ message mặc định.
          }
        }
      }

      setWordError(message);
    } finally {
      setIsGeneratingWord(false);
    }
  };

  // =========================================================
  // LẤY DANH SÁCH MẪU VĂN BẢN
  // =========================================================

  /**
   * Lấy danh sách mẫu văn bản từ backend.
   *
   * Backend:
   * GET /api/DocumentTemplates
   *
   * API này lấy dữ liệu từ bảng DocumentTemplates
   * trong SQL Server.
   */
  const loadTemplates = useCallback(async () => {
    try {
      // Hiển thị trạng thái đang tải.
      setTemplatesLoading(true);

      // Gọi API backend.
      const response = await api.get<DocumentTemplate[]>("/DocumentTemplates");

      // Chỉ lấy các template đang hoạt động.
      const activeTemplates = response.data.filter(
        (template) => template.isActive,
      );

      // Lưu danh sách template.
      setTemplates(activeTemplates);
    } catch (error) {
      console.error("Không thể tải danh sách mẫu văn bản:", error);

      // Không làm hỏng toàn bộ trang nếu API template lỗi.
      setTemplates([]);
    } finally {
      setTemplatesLoading(false);
    }
  }, []);

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
    void loadTemplates();
  }, [loadDocuments, loadTemplates]);
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
  // LỌC MẪU VĂN BẢN THEO LOẠI VĂN BẢN
  // =========================================================

  /**
   * Chỉ hiển thị những mẫu đang hoạt động
   * và phù hợp với loại văn bản đang được chọn.
   *
   * Ví dụ:
   * - Chọn "Thông báo" → chỉ hiện template "Thông báo".
   * - Chọn "Công văn" → chỉ hiện template "Công văn".
   *
   * Riêng "Khác":
   * - Hiển thị các mẫu không thuộc những loại
   *   được định nghĩa trong danh sách documentTypes.
   */
  const filteredTemplates = useMemo(() => {
    return templates.filter((template) => {
      // Nếu người dùng chọn "Khác",
      // lấy các template không thuộc 5 loại chính.
      if (aiDocumentType === "Khác") {
        return !documentTypes
          .filter((type) => type !== "Khác")
          .includes(template.documentType);
      }

      // Với các loại thông thường,
      // chỉ lấy template cùng loại.
      return template.documentType === aiDocumentType;
    });
  }, [templates, aiDocumentType]);

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

  const handleDownloadFile = async (item: DocumentItem) => {
    try {
      setError("");

      const response = await api.get(`/documents/${item.documentId}/file`, {
        responseType: "blob",
      });

      const url = window.URL.createObjectURL(response.data as Blob);
      const link = window.document.createElement("a");

      link.href = url;
      link.download = `${item.title.replace(/[\\/:*?"<>|]/g, "_")}.docx`;

      window.document.body.appendChild(link);
      link.click();
      link.remove();

      window.URL.revokeObjectURL(url);
    } catch (err) {
      console.error("Không thể tải file Word:", err);
      setError("Không thể tải file Word. File có thể đã bị xóa.");
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
        {/* Tiêu đề */}
        <div className="mb-5 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div className="flex items-center gap-3">
            <div className="flex h-11 w-11 items-center justify-center rounded-xl bg-blue-100 text-xl">
              ✨
            </div>

            <div>
              <h2 className="text-lg font-bold text-slate-800">
                Soạn thảo văn bản bằng AI
              </h2>

              <p className="mt-1 text-sm text-slate-500">
                Làm theo 3 bước: chọn mẫu, (tùy chọn) điền trường, mô tả yêu
                cầu.
              </p>
            </div>
          </div>

          <button
            type="button"
            onClick={handleResetAI}
            disabled={isBusy || (!aiPrompt && !aiContent && !hasFieldValues)}
            className="self-start rounded-lg border border-slate-200 px-3 py-2 text-xs font-medium text-slate-600 transition hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-50 sm:self-auto"
          >
            Làm lại từ đầu
          </button>
        </div>

        {/* BƯỚC 1: loại văn bản + mẫu */}
        <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
          <div>
            <label
              htmlFor="ai-document-type"
              className="mb-2 flex items-center text-sm font-medium text-slate-700"
            >
              <StepBadge n={1} />
              Loại văn bản
            </label>

            <select
              id="ai-document-type"
              value={aiDocumentType}
              onChange={(event) => {
                const newDocumentType = event.target.value;

                setAiDocumentType(newDocumentType);

                if (aiTemplateId !== null) {
                  const currentTemplate = templates.find(
                    (template) => template.documentTemplateId === aiTemplateId,
                  );

                  if (!currentTemplate) {
                    setAiTemplateId(null);
                    clearTemplateFields();
                  } else if (newDocumentType === "Khác") {
                    const isOtherTemplate = !documentTypes
                      .filter((type) => type !== "Khác")
                      .includes(currentTemplate.documentType);

                    if (!isOtherTemplate) {
                      setAiTemplateId(null);
                      clearTemplateFields();
                    }
                  } else if (currentTemplate.documentType !== newDocumentType) {
                    setAiTemplateId(null);
                    clearTemplateFields();
                  }
                }
              }}
              disabled={isBusy}
              className="w-full rounded-xl border border-slate-300 bg-white px-4 py-3 text-sm outline-none transition focus:border-blue-500 focus:ring-2 focus:ring-blue-100 disabled:bg-slate-100"
            >
              {documentTypes.map((type) => (
                <option key={type} value={type}>
                  {type}
                </option>
              ))}
            </select>
          </div>

          <div>
            <label
              htmlFor="ai-template-id"
              className="mb-2 flex items-center text-sm font-medium text-slate-700"
            >
              <StepBadge n={1} />
              Mẫu văn bản
            </label>

            <select
              id="ai-template-id"
              value={aiTemplateId === null ? "" : aiTemplateId}
              onChange={(event) =>
                void handleSelectTemplate(event.target.value)
              }
              disabled={isBusy || templatesLoading}
              className="w-full rounded-xl border border-slate-300 bg-white px-4 py-3 text-sm outline-none transition focus:border-blue-500 focus:ring-2 focus:ring-blue-100 disabled:bg-slate-100"
            >
              <option value="">
                {templatesLoading
                  ? "Đang tải mẫu văn bản..."
                  : filteredTemplates.length === 0
                    ? "Chưa có mẫu cho loại văn bản này"
                    : "Chọn mẫu văn bản"}
              </option>

              {filteredTemplates.map((template) => (
                <option
                  key={template.documentTemplateId}
                  value={template.documentTemplateId}
                >
                  {template.templateName}
                </option>
              ))}
            </select>

            {!templatesLoading && filteredTemplates.length === 0 && (
              <p className="mt-1 text-xs text-amber-600">
                Chưa có mẫu nào cho loại này. Nhờ quản trị viên thêm mẫu ở mục
                "Mẫu văn bản", hoặc chọn loại khác.
              </p>
            )}
          </div>
        </div>

        {/* BƯỚC 2: các trường trong mẫu */}
        {aiTemplateId !== null && (
          <div className="mt-4 rounded-xl border border-slate-200 bg-slate-50 p-4">
            <h3 className="flex items-center text-sm font-semibold text-slate-800">
              <StepBadge n={2} />
              Thông tin các trường trong mẫu
              <span className="ml-2 rounded-full bg-slate-200 px-2 py-0.5 text-[11px] font-medium text-slate-600">
                Không bắt buộc
              </span>
            </h3>

            <p className="mt-1 text-xs text-slate-500">
              Biết thông tin nào thì điền vào đó. Trường để trống sẽ do AI tự
              điền dựa trên yêu cầu ở bước 3.
            </p>

            {fieldsLoading ? (
              <p className="mt-3 text-sm text-slate-500">
                Đang đọc mẫu Word...
              </p>
            ) : templateFields.length === 0 ? (
              <p className="mt-3 text-sm text-slate-500">
                Mẫu này không có vùng nào cần điền.
              </p>
            ) : (
              <div className="mt-3 grid grid-cols-1 gap-3 md:grid-cols-2">
                {templateFields.map((field, index) => (
                  <div key={field.fieldId}>
                    <label
                      htmlFor={`field-${field.fieldId}`}
                      className="mb-1 block text-xs font-medium text-slate-600"
                    >
                      Trường {index + 1}
                    </label>

                    <p className="mb-1 text-xs italic text-slate-400">
                      {field.context}
                    </p>

                    <input
                      id={`field-${field.fieldId}`}
                      type="text"
                      value={fieldValues[field.fieldId] ?? ""}
                      onChange={(event) =>
                        setFieldValues((current) => ({
                          ...current,
                          [field.fieldId]: event.target.value,
                        }))
                      }
                      disabled={isBusy}
                      placeholder="Để trống để AI điền"
                      className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100 disabled:bg-slate-100"
                    />
                  </div>
                ))}
              </div>
            )}
          </div>
        )}

        {/* BƯỚC 3: yêu cầu soạn thảo */}
        <div className="mt-4">
          <label
            htmlFor="ai-prompt"
            className="mb-2 flex items-center text-sm font-medium text-slate-700"
          >
            <StepBadge n={3} />
            Yêu cầu soạn thảo
          </label>

          {/* Chip gợi ý nội dung cần nêu */}
          <div className="mb-2 flex flex-wrap items-center gap-2">
            <span className="text-xs text-slate-500">
              Nên nêu rõ (bấm để chèn):
            </span>

            {guide.hints.map((hint) => {
              const used = aiPrompt.includes(`${hint}:`);

              return (
                <button
                  key={hint}
                  type="button"
                  onClick={() => appendHint(hint)}
                  disabled={isBusy}
                  className={`rounded-full px-3 py-1 text-xs font-medium transition disabled:opacity-50 ${
                    used
                      ? "bg-green-50 text-green-700 hover:bg-green-100"
                      : "bg-blue-50 text-blue-700 hover:bg-blue-100"
                  }`}
                >
                  {used ? "✓ " : "+ "}
                  {hint}
                </button>
              );
            })}
          </div>

          <textarea
            id="ai-prompt"
            value={aiPrompt}
            onChange={(event) => setAiPrompt(event.target.value)}
            disabled={isBusy}
            rows={6}
            placeholder={`Ví dụ: ${guide.example}`}
            className="w-full resize-y rounded-xl border border-slate-300 px-4 py-3 text-sm outline-none transition focus:border-blue-500 focus:ring-2 focus:ring-blue-100 disabled:bg-slate-100"
          />

          <div className="mt-2 flex flex-wrap items-center justify-between gap-2">
            <button
              type="button"
              onClick={() => setAiPrompt(guide.example)}
              disabled={isBusy}
              className="text-xs font-medium text-blue-600 hover:underline disabled:opacity-50"
            >
              Dùng ví dụ mẫu cho "{aiDocumentType}"
            </button>

            {templateFields.length > 0 && (
              <p className="text-xs text-slate-500">
                Mẫu có {templateFields.length} trường: chỉ cần mô tả bối cảnh
                chung, AI sẽ tự điền các trường để trống.
              </p>
            )}
          </div>

          {hasPrompt && aiPrompt.trim().length < 30 && (
            <p className="mt-1 text-xs text-amber-600">
              Yêu cầu còn ngắn. Hãy nêu thêm thời gian, đối tượng và nội dung
              chính để AI soạn chính xác hơn.
            </p>
          )}
        </div>

        {/* Thông báo lỗi */}
        {aiError && (
          <div className="mt-4 rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
            {aiError}
          </div>
        )}

        {wordError && (
          <div className="mt-4 rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
            {wordError}
          </div>
        )}

        {/* Thông báo đang xử lý */}
        {isBusy && (
          <div className="mt-4 rounded-xl border border-blue-100 bg-blue-50 px-4 py-3 text-sm text-blue-700">
            AI đang soạn, có thể mất 10–30 giây. Vui lòng không đóng trang.
          </div>
        )}

        {/* BƯỚC 4: hai nút tạo */}
        <div className="mt-5 grid grid-cols-1 gap-3 sm:grid-cols-2">
          <div>
            <button
              type="button"
              onClick={() => void handleGenerateAI()}
              disabled={isBusy || !canGenerateText}
              className="w-full rounded-xl bg-blue-600 px-5 py-3 text-sm font-semibold text-white shadow-sm transition hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-60"
            >
              {isGeneratingAI
                ? "Đang tạo văn bản..."
                : "✨ Tạo nội dung bằng AI"}
            </button>

            <p className="mt-1 text-xs text-slate-500">
              {textHelp ||
                "Xem trước nội dung dạng văn bản, có thể chỉnh sửa và lưu."}
            </p>
          </div>

          <div>
            <button
              type="button"
              onClick={() => void handleGenerateAIWord()}
              disabled={isBusy || !canExportWord}
              className="w-full rounded-xl bg-green-600 px-5 py-3 text-sm font-semibold text-white shadow-sm transition hover:bg-green-700 disabled:cursor-not-allowed disabled:opacity-60"
            >
              {isGeneratingWord
                ? "Đang tạo file Word..."
                : "📄 AI soạn & xuất Word"}
            </button>

            <p className="mt-1 text-xs text-slate-500">
              {wordHelp || "Điền thẳng vào mẫu .docx và tải file về máy."}
            </p>
          </div>
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
                Hãy kiểm tra và chỉnh sửa nội dung trước khi sử dụng. AI có thể
                sai sót hoặc để trống thông tin còn thiếu.
              </p>
            </div>

            <div className="flex flex-wrap gap-2">
              <button
                type="button"
                onClick={() =>
                  setSummaryTarget({
                    title: selectedTemplate?.templateName ?? aiDocumentType,
                    load: () =>
                      summarizeText(
                        selectedTemplate?.templateName ?? aiDocumentType,
                        aiContent,
                      ),
                    ask: (question) =>
                      askText(
                        selectedTemplate?.templateName ?? aiDocumentType,
                        aiContent,
                        question,
                      ),
                  })
                }
                className="inline-flex items-center gap-2 rounded-xl border border-blue-200 bg-blue-50 px-4 py-2 text-sm font-medium text-blue-700 transition hover:bg-blue-100"
              >
                <Sparkles size={16} />
                Tóm tắt ý chính
              </button>
              <button
                type="button"
                onClick={() => void handleCopyAI()}
                className="rounded-xl border border-slate-300 px-4 py-2 text-sm font-medium text-slate-700 transition hover:bg-slate-50"
              >
                {copied ? "✓ Đã sao chép" : "Sao chép"}
              </button>

              <button
                type="button"
                onClick={handleSaveAiContent}
                className="rounded-xl bg-blue-600 px-4 py-2 text-sm font-semibold text-white transition hover:bg-blue-700"
              >
                Lưu vào danh sách văn bản
              </button>
            </div>
          </div>

          <textarea
            value={aiContent}
            onChange={(event) => setAiContent(event.target.value)}
            rows={20}
            className="w-full resize-y rounded-xl border border-slate-300 px-4 py-3 font-mono text-sm leading-6 outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100"
          />
        </section>
      )}

      <UploadSummaryPanel onSaved={loadDocuments} />

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
                          onClick={() =>
                            setSummaryTarget({
                              title: document.title,
                              load: () =>
                                summarizeDocument(document.documentId),
                              ask: (question) =>
                                askDocument(document.documentId, question),
                            })
                          }
                          disabled={!document.content?.trim()}
                          className="rounded-lg p-2 text-slate-500 hover:bg-blue-50 hover:text-blue-600 disabled:cursor-not-allowed disabled:opacity-30"
                          title={
                            document.content?.trim()
                              ? "Tóm tắt ý chính"
                              : "Văn bản chưa có nội dung"
                          }
                        >
                          <Sparkles size={17} />
                        </button>

                        <button
                          type="button"
                          onClick={() => setPptDoc(document)}
                          disabled={
                            !document.content?.trim() && !document.hasFile
                          }
                          className="rounded-lg p-2 text-slate-500 hover:bg-blue-50 hover:text-blue-600 disabled:cursor-not-allowed disabled:opacity-30"
                          title="Tạo PowerPoint"
                        >
                          <Presentation size={17} />
                        </button>

                        <button
                          type="button"
                          onClick={() => handleEdit(document)}
                          className="rounded-lg p-2 text-slate-500 hover:bg-blue-50 hover:text-blue-600"
                          title="Sửa văn bản"
                        >
                          <Pencil size={17} />
                        </button>

                        {document.hasFile && (
                          <button
                            type="button"
                            onClick={() => void handleDownloadFile(document)}
                            className="rounded-lg p-2 text-slate-500 hover:bg-green-50 hover:text-green-600"
                            title="Tải file Word"
                          >
                            <Download size={17} />
                          </button>
                        )}

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
      {summaryTarget && (
        <SummaryModal
          title={summaryTarget.title}
          load={summaryTarget.load}
          ask={summaryTarget.ask}
          onClose={() => setSummaryTarget(null)}
        />
      )}

      {pptDoc && (
        <PresentationModal
          title={pptDoc.title}
          documentId={pptDoc.documentId}
          onClose={() => setPptDoc(null)}
        />
      )}
    </main>
  );
}

export default Documents;
