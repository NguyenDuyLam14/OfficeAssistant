import PptxGenJS from "pptxgenjs";
import api from "./api";

export interface SlideItem {
  title: string;
  bullets: string[];
  notes: string;
}

export interface SlideOutline {
  title: string;
  subtitle: string;
  slides: SlideItem[];
}

export async function requestOutline(payload: {
  documentId?: number;
  title?: string;
  content?: string;
  slideCount: number;
  audience: string;
}): Promise<SlideOutline> {
  const response = await api.post<SlideOutline>("/Presentation/outline", payload);
  return response.data;
}

export async function exportSummaryDocx(
  title: string,
  content: string,
): Promise<{ blob: Blob; documentId: number | null }> {
  const response = await api.post(
    "/Export/summary-docx",
    { title, content },
    { responseType: "blob" },
  );

  const id = Number(response.headers["x-document-id"]);
  return { blob: response.data as Blob, documentId: Number.isNaN(id) ? null : id };
}

// Dựng file .pptx hoàn toàn ở trình duyệt, không tốn quota AI.
export async function downloadPptx(outline: SlideOutline, fileName: string) {
  const pptx = new PptxGenJS();
  pptx.layout = "LAYOUT_WIDE"; // 13.33 x 7.5 inch
  pptx.title = outline.title;

  // Slide tiêu đề
  const cover = pptx.addSlide();
  cover.background = { color: "1E3A8A" };
  cover.addText(outline.title, {
    x: 0.8, y: 2.3, w: 11.7, h: 1.6,
    fontSize: 40, bold: true, color: "FFFFFF", fontFace: "Calibri", valign: "middle",
  });
  cover.addText(outline.subtitle, {
    x: 0.8, y: 4.0, w: 11.7, h: 0.8,
    fontSize: 20, color: "BFDBFE", fontFace: "Calibri",
  });

  // Các slide nội dung
  outline.slides.forEach((item, index) => {
    const slide = pptx.addSlide();

    slide.addShape(pptx.ShapeType.rect, {
      x: 0, y: 0, w: 13.33, h: 1.2, fill: { color: "1E3A8A" },
    });
    slide.addText(item.title, {
      x: 0.6, y: 0.15, w: 12.1, h: 0.9,
      fontSize: 28, bold: true, color: "FFFFFF", fontFace: "Calibri", valign: "middle",
    });

    slide.addText(
      item.bullets.map((text) => ({
        text,
        options: { bullet: true, breakLine: true },
      })),
      {
        x: 0.8, y: 1.6, w: 11.7, h: 5.0,
        fontSize: 22, color: "334155", fontFace: "Calibri",
        valign: "top", paraSpaceAfter: 12,
      },
    );

    slide.addText(String(index + 1), {
      x: 12.3, y: 6.9, w: 0.7, h: 0.4, fontSize: 12, color: "94A3B8", align: "right",
    });

    if (item.notes) slide.addNotes(item.notes);
  });

  await pptx.writeFile({ fileName });
}