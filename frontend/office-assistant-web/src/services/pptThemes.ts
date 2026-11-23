export type HeaderStyle = "bar" | "line" | "side";

export interface PptTheme {
  id: string;
  name: string;
  description: string;
  fontTitle: string;
  fontBody: string;
  headerStyle: HeaderStyle;
  coverBg: string;
  coverTitle: string;
  coverSubtitle: string;
  accent: string;
  bg: string;
  headerBg: string;
  title: string;
  text: string;
  number: string;
}

export const PPT_THEMES: PptTheme[] = [
  {
    id: "blue", name: "Xanh doanh nghiệp", description: "Thanh tiêu đề xanh, trang trọng",
    fontTitle: "Calibri", fontBody: "Calibri", headerStyle: "bar",
    coverBg: "1E3A8A", coverTitle: "FFFFFF", coverSubtitle: "BFDBFE", accent: "3B82F6",
    bg: "FFFFFF", headerBg: "1E3A8A", title: "FFFFFF", text: "334155", number: "94A3B8",
  },
  {
    id: "minimal", name: "Tối giản", description: "Nền trắng, gạch chân nhấn nhẹ",
    fontTitle: "Arial", fontBody: "Arial", headerStyle: "line",
    coverBg: "FFFFFF", coverTitle: "0F172A", coverSubtitle: "64748B", accent: "0EA5E9",
    bg: "FFFFFF", headerBg: "FFFFFF", title: "0F172A", text: "334155", number: "94A3B8",
  },
  {
    id: "admin", name: "Hành chính", description: "Đỏ đô – vàng, phù hợp báo cáo cơ quan",
    fontTitle: "Times New Roman", fontBody: "Times New Roman", headerStyle: "side",
    coverBg: "7F1D1D", coverTitle: "FFFFFF", coverSubtitle: "FDE68A", accent: "F59E0B",
    bg: "FFFBEB", headerBg: "FFFBEB", title: "7F1D1D", text: "292524", number: "A8A29E",
  },
  {
    id: "dark", name: "Hiện đại tối", description: "Nền tối, điểm nhấn xanh ngọc",
    fontTitle: "Segoe UI", fontBody: "Segoe UI", headerStyle: "bar",
    coverBg: "0F172A", coverTitle: "F8FAFC", coverSubtitle: "94A3B8", accent: "22D3EE",
    bg: "1E293B", headerBg: "0F172A", title: "F8FAFC", text: "E2E8F0", number: "64748B",
  },
];