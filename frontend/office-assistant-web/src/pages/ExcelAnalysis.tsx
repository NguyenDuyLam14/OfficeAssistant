import { useMemo, useRef, useState } from "react";
import type { DragEvent } from "react";
import axios from "axios";
import {
  BarChart3,
  Download,
  FileSpreadsheet,
  LoaderCircle,
  Sparkles,
  Upload,
  X,
} from "lucide-react";
import {
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  Legend,
  Line,
  LineChart,
  Pie,
  PieChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";

import api from "../services/api";

// =========================================================
// KIỂU DỮ LIỆU
// =========================================================

type CellValue = string | number | null;

type ValueCount = { value: string; count: number };

type ColumnStats = {
  name: string;
  type: "Number" | "Date" | "Text";
  count: number;
  missing: number;
  unique: number;
  sum: number | null;
  mean: number | null;
  min: number | null;
  max: number | null;
  median: number | null;
  stdDev: number | null;
  minText: string | null;
  maxText: string | null;
  topValues: ValueCount[];
};

type AnalysisResult = {
  fileName: string;
  sheetName: string;
  sheetNames: string[];
  totalRows: number;
  truncated: boolean;
  maxRows: number;
  columns: ColumnStats[];
  rows: CellValue[][];
};

type ChartType = "bar" | "line" | "pie";
type Aggregation = "sum" | "avg" | "count";

const COLORS = [
  "#2563eb",
  "#16a34a",
  "#f59e0b",
  "#dc2626",
  "#7c3aed",
  "#0891b2",
  "#db2777",
  "#65a30d",
  "#ea580c",
  "#475569",
];

const TYPE_LABEL: Record<string, string> = {
  Number: "Số",
  Date: "Ngày",
  Text: "Văn bản",
};

const MAX_FILE_SIZE = 10 * 1024 * 1024;

const formatNumber = (value: number | null | undefined) =>
  value === null || value === undefined
    ? "—"
    : value.toLocaleString("vi-VN", { maximumFractionDigits: 2 });

// =========================================================
// TRANG PHÂN TÍCH EXCEL
// =========================================================

function ExcelAnalysis() {
  const inputRef = useRef<HTMLInputElement | null>(null);
  const chartRef = useRef<HTMLDivElement | null>(null);

  const [sourceFile, setSourceFile] = useState<File | null>(null);
  const [result, setResult] = useState<AnalysisResult | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [dragging, setDragging] = useState(false);

  // Cấu hình biểu đồ.
  const [chartType, setChartType] = useState<ChartType>("bar");
  const [xIndex, setXIndex] = useState(-1);
  const [yIndex, setYIndex] = useState(-1); // -1 = đếm số dòng
  const [aggregation, setAggregation] = useState<Aggregation>("sum");
  const [dateGroup, setDateGroup] = useState<"day" | "month">("month");

  const [aiInsight, setAiInsight] = useState("");
  const [aiLoading, setAiLoading] = useState(false);
  const [aiError, setAiError] = useState("");
  const [exporting, setExporting] = useState(false);

  // -------------------------------------------------------
  // UPLOAD + PHÂN TÍCH
  // -------------------------------------------------------
  const uploadFile = async (file: File, sheetName?: string, save = true) => {
    const name = file.name.toLowerCase();

    if (!name.endsWith(".xlsx") && !name.endsWith(".xlsm")) {
      setError("Chỉ hỗ trợ file Excel định dạng .xlsx.");
      return;
    }

    if (file.size > MAX_FILE_SIZE) {
      setError("File không được vượt quá 10 MB.");
      return;
    }

    try {
      setLoading(true);
      setError("");

      const formData = new FormData();
      formData.append("file", file);

      const response = await api.post<AnalysisResult>(
        "/Excel/analyze",
        formData,
        { params: { ...(sheetName ? { sheetName } : {}), save } },
      );

      const data = response.data;

      setSourceFile(file);
      setResult(data);

      // Chọn mặc định: cột X là cột không phải số đầu tiên,
      // cột Y là cột số đầu tiên.
      const firstCategory = data.columns.findIndex((c) => c.type !== "Number");
      const firstNumber = data.columns.findIndex((c) => c.type === "Number");

      setXIndex(firstCategory >= 0 ? firstCategory : 0);
      setYIndex(firstNumber);
      setAggregation(firstNumber >= 0 ? "sum" : "count");
    } catch (err) {
      console.error("Lỗi phân tích Excel:", err);

      if (axios.isAxiosError(err)) {
        setError(
          err.response?.data?.message ?? "Không thể phân tích file Excel.",
        );
      } else {
        setError("Không thể phân tích file Excel.");
      }
    } finally {
      setLoading(false);

      if (inputRef.current) {
        inputRef.current.value = "";
      }
    }
  };

  const handleDrop = (event: DragEvent<HTMLDivElement>) => {
    event.preventDefault();
    setDragging(false);

    const file = event.dataTransfer.files?.[0];
    if (file) void uploadFile(file);
  };

  const handleReset = () => {
    setSourceFile(null);
    setResult(null);
    setError("");
    setAiInsight("");
    setAiError("");
  };

  const handleAiInsight = async () => {
    if (!result) return;

    try {
      setAiLoading(true);
      setAiError("");

      const response = await api.post<{ insight: string }>("/Excel/insights", {
        fileName: result.fileName,
        sheetName: result.sheetName,
        totalRows: result.totalRows,
        columns: result.columns,
      });

      setAiInsight(response.data.insight);
    } catch (err) {
      console.error("Lỗi AI nhận xét dữ liệu:", err);

      setAiError(
        axios.isAxiosError(err)
          ? (err.response?.data?.message ??
              "Không thể nhờ AI nhận xét dữ liệu.")
          : "Không thể nhờ AI nhận xét dữ liệu.",
      );
    } finally {
      setAiLoading(false);
    }
  };

  // Tên file gốc, bỏ đuôi .xlsx
  const baseName = result?.fileName.replace(/\.[^.]+$/, "") ?? "dulieu";

  // -------------------------------------------------------
  // XUẤT BẢNG THỐNG KÊ RA EXCEL
  // -------------------------------------------------------
  const handleExportStats = async () => {
    if (!result) return;

    try {
      setExporting(true);
      setError("");

      const response = await api.post(
        "/Excel/export-stats",
        {
          fileName: result.fileName,
          sheetName: result.sheetName,
          totalRows: result.totalRows,
          columns: result.columns,
        },
        { responseType: "blob" },
      );

      downloadBlob(response.data, `ThongKe_${baseName}.xlsx`);
    } catch (err) {
      console.error("Lỗi xuất thống kê:", err);
      setError("Không thể xuất file thống kê.");
    } finally {
      setExporting(false);
    }
  };

  // -------------------------------------------------------
  // XUẤT BIỂU ĐỒ RA ẢNH PNG
  // -------------------------------------------------------
  const handleExportChartPng = () => {
    const surfaces = Array.from(
      chartRef.current?.querySelectorAll<SVGSVGElement>(
        "svg.recharts-surface",
      ) ?? [],
    );

    if (surfaces.length === 0) {
      setError("Không tìm thấy biểu đồ để xuất.");
      return;
    }

    // Chọn SVG lớn nhất (biểu đồ chính, bỏ qua icon nhỏ của chú thích).
    const svg = surfaces.reduce((best, current) => {
      const a = best.getBoundingClientRect();
      const b = current.getBoundingClientRect();
      return b.width * b.height > a.width * a.height ? current : best;
    });

    const { width, height } = svg.getBoundingClientRect();

    const clone = svg.cloneNode(true) as SVGSVGElement;
    clone.setAttribute("xmlns", "http://www.w3.org/2000/svg");
    clone.setAttribute("width", String(width));
    clone.setAttribute("height", String(height));

    const svgText = new XMLSerializer().serializeToString(clone);

    const url = URL.createObjectURL(
      new Blob([svgText], { type: "image/svg+xml;charset=utf-8" }),
    );

    const image = new Image();

    image.onload = () => {
      const scale = 2; // ảnh nét hơn
      const canvas = document.createElement("canvas");

      canvas.width = width * scale;
      canvas.height = height * scale;

      const context = canvas.getContext("2d");

      if (!context) {
        URL.revokeObjectURL(url);
        return;
      }

      // Nền trắng (SVG gốc trong suốt).
      context.fillStyle = "#ffffff";
      context.fillRect(0, 0, canvas.width, canvas.height);

      context.scale(scale, scale);
      context.drawImage(image, 0, 0, width, height);

      URL.revokeObjectURL(url);

      canvas.toBlob((blob) => {
        if (blob) downloadBlob(blob, `BieuDo_${baseName}.png`);
      }, "image/png");
    };

    image.onerror = () => {
      URL.revokeObjectURL(url);
      setError("Không thể xuất biểu đồ thành ảnh.");
    };

    image.src = url;
  };

  // -------------------------------------------------------
  // DỮ LIỆU BIỂU ĐỒ (gom nhóm ở frontend)
  // -------------------------------------------------------
  const chartData = useMemo(() => {
    if (!result || xIndex < 0) return [];

    const xColumn = result.columns[xIndex];
    const groups = new Map<string, { sum: number; count: number }>();

    for (const row of result.rows) {
      const raw = row[xIndex];
      if (raw === null || raw === "") continue;

      let label = String(raw);

      if (xColumn.type === "Date" && dateGroup === "month") {
        label = label.slice(0, 7); // yyyy-MM
      }

      const group = groups.get(label) ?? { sum: 0, count: 0 };

      if (yIndex >= 0) {
        const value = row[yIndex];

        if (typeof value === "number") {
          group.sum += value;
          group.count += 1;
        }
      } else {
        group.count += 1;
      }

      groups.set(label, group);
    }

    let data = [...groups].map(([name, group]) => {
      let value = group.count;

      if (yIndex >= 0 && aggregation === "sum") value = group.sum;
      if (yIndex >= 0 && aggregation === "avg") {
        value = group.count > 0 ? group.sum / group.count : 0;
      }

      return { name, value: Math.round(value * 100) / 100 };
    });

    if (xColumn.type === "Date") {
      data.sort((a, b) => a.name.localeCompare(b.name));
    } else {
      data.sort((a, b) => b.value - a.value);
      data = data.slice(0, chartType === "pie" ? 10 : 20);
    }

    return data;
  }, [result, xIndex, yIndex, aggregation, dateGroup, chartType]);

  const valueLabel =
    yIndex < 0
      ? "Số dòng"
      : `${
          aggregation === "sum"
            ? "Tổng"
            : aggregation === "avg"
              ? "Trung bình"
              : "Số lượng"
        } ${result?.columns[yIndex]?.name ?? ""}`;

  // -------------------------------------------------------
  // TỔNG QUAN
  // -------------------------------------------------------
  const summary = useMemo(() => {
    if (!result) return null;

    return {
      numericColumns: result.columns.filter((c) => c.type === "Number").length,
      missingCells: result.columns.reduce((sum, c) => sum + c.missing, 0),
    };
  }, [result]);

  const numberColumns = result
    ? result.columns
        .map((column, index) => ({ column, index }))
        .filter((x) => x.column.type === "Number")
    : [];

  // =======================================================
  // GIAO DIỆN
  // =======================================================

  return (
    <main className="p-6">
      {/* Input file ẩn */}
      <input
        ref={inputRef}
        type="file"
        accept=".xlsx,.xlsm,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        className="hidden"
        onChange={(event) => {
          const file = event.target.files?.[0];
          if (file) void uploadFile(file);
        }}
      />

      {/* Tiêu đề */}
      <div className="mb-6 flex flex-col gap-4 md:flex-row md:items-center md:justify-between">
        <div className="flex items-center gap-3">
          <div className="rounded-xl bg-blue-100 p-3 text-blue-600">
            <BarChart3 size={26} />
          </div>

          <div>
            <h1 className="text-2xl font-bold text-slate-800">
              Phân tích Excel
            </h1>
            <p className="text-sm text-slate-500">
              Nhập dữ liệu từ file Excel để thống kê, phân tích và trực quan
              hóa.
            </p>
          </div>
        </div>

        {result && (
          <button
            type="button"
            onClick={handleReset}
            className="inline-flex items-center justify-center gap-2 rounded-xl border border-slate-200 bg-white px-4 py-2.5 text-sm font-medium text-slate-700 hover:bg-slate-50"
          >
            <X size={16} />
            Chọn file khác
          </button>
        )}
      </div>

      {error && (
        <div className="mb-5 flex items-center justify-between rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
          <span>{error}</span>
          <button type="button" onClick={() => setError("")}>
            <X size={17} />
          </button>
        </div>
      )}

      {/* ================= KHU VỰC UPLOAD ================= */}
      {!result && (
        <div
          onDragOver={(event) => {
            event.preventDefault();
            setDragging(true);
          }}
          onDragLeave={() => setDragging(false)}
          onDrop={handleDrop}
          className={`flex flex-col items-center justify-center rounded-2xl border-2 border-dashed bg-white px-6 py-16 text-center transition ${
            dragging ? "border-blue-500 bg-blue-50" : "border-slate-300"
          }`}
        >
          {loading ? (
            <>
              <LoaderCircle size={36} className="animate-spin text-blue-600" />
              <p className="mt-4 text-sm text-slate-600">
                Đang đọc và phân tích file...
              </p>
            </>
          ) : (
            <>
              <div className="mb-4 flex h-16 w-16 items-center justify-center rounded-2xl bg-green-50 text-green-600">
                <FileSpreadsheet size={32} />
              </div>

              <h2 className="text-lg font-semibold text-slate-800">
                Kéo thả file Excel vào đây
              </h2>

              <p className="mt-2 max-w-md text-sm text-slate-500">
                Hỗ trợ .xlsx, tối đa 10 MB. Dòng đầu tiên của sheet được xem là
                tiêu đề cột.
              </p>

              <button
                type="button"
                onClick={() => inputRef.current?.click()}
                className="mt-5 inline-flex items-center gap-2 rounded-xl bg-blue-600 px-5 py-3 text-sm font-semibold text-white hover:bg-blue-700"
              >
                <Upload size={18} />
                Chọn file Excel
              </button>
            </>
          )}
        </div>
      )}

      {/* ================= KẾT QUẢ ================= */}
      {result && summary && (
        <div className="space-y-6">
          {/* Thông tin file + chọn sheet */}
          <section className="flex flex-col gap-3 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm sm:flex-row sm:items-center sm:justify-between">
            <div className="flex items-center gap-3">
              <div className="flex h-11 w-11 items-center justify-center rounded-xl bg-green-50 text-green-600">
                <FileSpreadsheet size={22} />
              </div>

              <div>
                <p className="font-semibold text-slate-800">
                  {result.fileName}
                </p>
                <p className="text-xs text-slate-500">
                  Sheet: {result.sheetName}
                </p>
              </div>
            </div>

            {result.sheetNames.length > 1 && sourceFile && (
              <select
                value={result.sheetName}
                disabled={loading}
                onChange={(event) =>
                  void uploadFile(sourceFile, event.target.value, false)
                }
                className="rounded-xl border border-slate-300 bg-white px-4 py-2.5 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100"
              >
                {result.sheetNames.map((name) => (
                  <option key={name} value={name}>
                    {name}
                  </option>
                ))}
              </select>
            )}
          </section>

          {result.truncated && (
            <div className="rounded-xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800">
              File có nhiều dòng hơn giới hạn. Hệ thống chỉ phân tích{" "}
              {result.maxRows.toLocaleString("vi-VN")} dòng đầu tiên.
            </div>
          )}

          {/* Thẻ tổng quan */}
          <section className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
            <SummaryCard
              label="Số dòng dữ liệu"
              value={result.totalRows.toLocaleString("vi-VN")}
            />
            <SummaryCard
              label="Số cột"
              value={result.columns.length.toString()}
            />
            <SummaryCard
              label="Cột dạng số"
              value={summary.numericColumns.toString()}
            />
            <SummaryCard
              label="Ô dữ liệu trống"
              value={summary.missingCells.toLocaleString("vi-VN")}
            />
          </section>

          {/* ===== AI NHẬN XÉT ===== */}
          <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
            <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
              <div>
                <h2 className="font-semibold text-slate-800">
                  AI nhận xét dữ liệu
                </h2>
                <p className="mt-1 text-sm text-slate-500">
                  AI đọc bảng thống kê (không gửi các dòng dữ liệu) và đưa ra
                  nhận xét, gợi ý phân tích.
                </p>
              </div>

              <button
                type="button"
                onClick={() => void handleAiInsight()}
                disabled={aiLoading}
                className="inline-flex items-center justify-center gap-2 rounded-xl bg-blue-600 px-5 py-2.5 text-sm font-semibold text-white hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-60"
              >
                {aiLoading ? (
                  <LoaderCircle size={17} className="animate-spin" />
                ) : (
                  <Sparkles size={17} />
                )}
                {aiLoading ? "AI đang phân tích..." : "AI nhận xét"}
              </button>
            </div>

            {aiError && (
              <div className="mt-4 rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
                {aiError}
              </div>
            )}

            {aiInsight && (
              <div className="mt-4 whitespace-pre-wrap rounded-xl bg-slate-50 p-4 text-sm leading-6 text-slate-700">
                {aiInsight}
              </div>
            )}
          </section>

          {/* ===== BIỂU ĐỒ ===== */}
          <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
            <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
              <div>
                <h2 className="font-semibold text-slate-800">Trực quan hóa</h2>
                <p className="mt-1 text-sm text-slate-500">
                  Chọn cột nhóm và cột giá trị để vẽ biểu đồ.
                </p>
              </div>

              <button
                type="button"
                onClick={handleExportChartPng}
                disabled={chartData.length === 0}
                className="inline-flex items-center justify-center gap-2 rounded-xl border border-slate-200 bg-white px-4 py-2.5 text-sm font-medium text-slate-700 hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-50"
              >
                <Download size={16} />
                Xuất ảnh PNG
              </button>
            </div>

            <div className="mt-4 grid gap-3 md:grid-cols-2 xl:grid-cols-5">
              <Field label="Loại biểu đồ">
                <select
                  value={chartType}
                  onChange={(e) => setChartType(e.target.value as ChartType)}
                  className={selectClass}
                >
                  <option value="bar">Cột</option>
                  <option value="line">Đường</option>
                  <option value="pie">Tròn</option>
                </select>
              </Field>

              <Field label="Nhóm theo (trục X)">
                <select
                  value={xIndex}
                  onChange={(e) => setXIndex(Number(e.target.value))}
                  className={selectClass}
                >
                  {result.columns.map((column, index) => (
                    <option key={column.name} value={index}>
                      {column.name} ({TYPE_LABEL[column.type]})
                    </option>
                  ))}
                </select>
              </Field>

              <Field label="Giá trị (trục Y)">
                <select
                  value={yIndex}
                  onChange={(e) => {
                    const value = Number(e.target.value);
                    setYIndex(value);
                    if (value < 0) setAggregation("count");
                    else if (aggregation === "count") setAggregation("sum");
                  }}
                  className={selectClass}
                >
                  <option value={-1}>Số dòng</option>
                  {numberColumns.map(({ column, index }) => (
                    <option key={column.name} value={index}>
                      {column.name}
                    </option>
                  ))}
                </select>
              </Field>

              <Field label="Cách tính">
                <select
                  value={aggregation}
                  disabled={yIndex < 0}
                  onChange={(e) =>
                    setAggregation(e.target.value as Aggregation)
                  }
                  className={`${selectClass} disabled:bg-slate-100`}
                >
                  <option value="sum">Tổng</option>
                  <option value="avg">Trung bình</option>
                  <option value="count">Đếm</option>
                </select>
              </Field>

              {result.columns[xIndex]?.type === "Date" && (
                <Field label="Gom ngày theo">
                  <select
                    value={dateGroup}
                    onChange={(e) =>
                      setDateGroup(e.target.value as "day" | "month")
                    }
                    className={selectClass}
                  >
                    <option value="month">Tháng</option>
                    <option value="day">Ngày</option>
                  </select>
                </Field>
              )}
            </div>

            <div ref={chartRef} className="mt-6 h-96">
              {chartData.length === 0 ? (
                <div className="flex h-full items-center justify-center text-sm text-slate-500">
                  Không có dữ liệu để vẽ biểu đồ.
                </div>
              ) : (
                <ResponsiveContainer width="100%" height="100%">
                  {chartType === "bar" ? (
                    <BarChart data={chartData}>
                      <CartesianGrid strokeDasharray="3 3" />
                      <XAxis dataKey="name" />
                      <YAxis />
                      <Tooltip />
                      <Legend />
                      <Bar
                        dataKey="value"
                        name={valueLabel}
                        fill="#2563eb"
                        radius={[4, 4, 0, 0]}
                      />
                    </BarChart>
                  ) : chartType === "line" ? (
                    <LineChart data={chartData}>
                      <CartesianGrid strokeDasharray="3 3" />
                      <XAxis dataKey="name" />
                      <YAxis />
                      <Tooltip />
                      <Legend />
                      <Line
                        type="monotone"
                        dataKey="value"
                        name={valueLabel}
                        stroke="#2563eb"
                        strokeWidth={2}
                      />
                    </LineChart>
                  ) : (
                    <PieChart>
                      <Tooltip />
                      <Legend />
                      <Pie
                        data={chartData}
                        dataKey="value"
                        nameKey="name"
                        outerRadius={140}
                        label
                      >
                        {chartData.map((item, index) => (
                          <Cell
                            key={item.name}
                            fill={COLORS[index % COLORS.length]}
                          />
                        ))}
                      </Pie>
                    </PieChart>
                  )}
                </ResponsiveContainer>
              )}
            </div>
          </section>

          {/* ===== THỐNG KÊ TỪNG CỘT ===== */}
          <section className="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
            <div className="flex flex-col gap-3 border-b border-slate-200 px-5 py-4 sm:flex-row sm:items-center sm:justify-between">
              <h2 className="font-semibold text-slate-800">
                Thống kê theo cột
              </h2>

              <button
                type="button"
                onClick={() => void handleExportStats()}
                disabled={exporting}
                className="inline-flex items-center justify-center gap-2 rounded-xl border border-slate-200 bg-white px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-60"
              >
                {exporting ? (
                  <LoaderCircle size={16} className="animate-spin" />
                ) : (
                  <Download size={16} />
                )}
                Xuất Excel
              </button>
            </div>

            <div className="overflow-x-auto">
              <table className="w-full min-w-[900px] text-left text-sm">
                <thead>
                  <tr className="border-b border-slate-200 bg-slate-50 text-slate-500">
                    <th className="px-4 py-3 font-medium">Cột</th>
                    <th className="px-4 py-3 font-medium">Kiểu</th>
                    <th className="px-4 py-3 font-medium">Có dữ liệu</th>
                    <th className="px-4 py-3 font-medium">Trống</th>
                    <th className="px-4 py-3 font-medium">Giá trị khác nhau</th>
                    <th className="px-4 py-3 font-medium">Thống kê</th>
                  </tr>
                </thead>

                <tbody>
                  {result.columns.map((column) => (
                    <tr
                      key={column.name}
                      className="border-b border-slate-100 align-top last:border-0"
                    >
                      <td className="px-4 py-3 font-medium text-slate-700">
                        {column.name}
                      </td>
                      <td className="px-4 py-3">
                        <span className="rounded-full bg-blue-50 px-2.5 py-1 text-xs font-medium text-blue-700">
                          {TYPE_LABEL[column.type]}
                        </span>
                      </td>
                      <td className="px-4 py-3 text-slate-600">
                        {column.count.toLocaleString("vi-VN")}
                      </td>
                      <td className="px-4 py-3 text-slate-600">
                        {column.missing.toLocaleString("vi-VN")}
                      </td>
                      <td className="px-4 py-3 text-slate-600">
                        {column.unique.toLocaleString("vi-VN")}
                      </td>
                      <td className="px-4 py-3 text-xs leading-5 text-slate-600">
                        {column.type === "Number" && (
                          <>
                            Tổng: {formatNumber(column.sum)} · TB:{" "}
                            {formatNumber(column.mean)}
                            <br />
                            Min: {formatNumber(column.min)} · Max:{" "}
                            {formatNumber(column.max)}
                            <br />
                            Trung vị: {formatNumber(column.median)} · Độ lệch
                            chuẩn: {formatNumber(column.stdDev)}
                          </>
                        )}

                        {column.type === "Date" && (
                          <>
                            Từ {column.minText} đến {column.maxText}
                          </>
                        )}

                        {column.type === "Text" &&
                          column.topValues.map((item) => (
                            <div key={item.value}>
                              {item.value}: {item.count}
                            </div>
                          ))}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </section>

          {/* ===== XEM TRƯỚC DỮ LIỆU ===== */}
          <section className="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
            <div className="border-b border-slate-200 px-5 py-4">
              <h2 className="font-semibold text-slate-800">
                Xem trước dữ liệu
              </h2>
              <p className="mt-1 text-sm text-slate-500">
                Hiển thị 50 dòng đầu tiên.
              </p>
            </div>

            <div className="max-h-96 overflow-auto">
              <table className="w-full text-left text-sm">
                <thead className="sticky top-0 bg-slate-50">
                  <tr className="border-b border-slate-200 text-slate-500">
                    {result.columns.map((column) => (
                      <th
                        key={column.name}
                        className="whitespace-nowrap px-4 py-3 font-medium"
                      >
                        {column.name}
                      </th>
                    ))}
                  </tr>
                </thead>

                <tbody>
                  {result.rows.slice(0, 50).map((row, rowIndex) => (
                    <tr
                      key={rowIndex}
                      className="border-b border-slate-100 last:border-0"
                    >
                      {row.map((cell, cellIndex) => (
                        <td
                          key={cellIndex}
                          className="whitespace-nowrap px-4 py-2.5 text-slate-600"
                        >
                          {typeof cell === "number"
                            ? formatNumber(cell)
                            : (cell ?? "")}
                        </td>
                      ))}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </section>
        </div>
      )}
    </main>
  );
}

// =========================================================
// COMPONENT PHỤ
// =========================================================

const selectClass =
  "w-full rounded-xl border border-slate-300 bg-white px-3 py-2.5 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100";

function SummaryCard({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
      <p className="text-sm text-slate-500">{label}</p>
      <p className="mt-2 text-3xl font-bold text-slate-800">{value}</p>
    </div>
  );
}

function Field({
  label,
  children,
}: {
  label: string;
  children: React.ReactNode;
}) {
  return (
    <div>
      <label className="mb-1.5 block text-xs font-medium text-slate-600">
        {label}
      </label>
      {children}
    </div>
  );
}

function downloadBlob(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");

  link.href = url;
  link.download = fileName;

  document.body.appendChild(link);
  link.click();
  link.remove();

  URL.revokeObjectURL(url);
}

export default ExcelAnalysis;
