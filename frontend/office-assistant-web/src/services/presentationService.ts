import PptxGenJS from "pptxgenjs";
import api from "./api";
import { PPT_THEMES } from "./pptThemes"; import type { PptTheme } from "./pptThemes";

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
export async function downloadPptx(
  outline: SlideOutline,
  fileName: string,
  theme: PptTheme = PPT_THEMES[0],
) {
  const pptx = new PptxGenJS();
  pptx.layout = "LAYOUT_WIDE"; // 13.33 x 7.5 inch
  pptx.title = outline.title;

  // ---------- Slide master cho các slide nội dung ----------
  const objects = [] as NonNullable<
    Parameters<typeof pptx.defineSlideMaster>[0]["objects"]
  >;

  if (theme.headerStyle === "bar") {
    objects.push({ rect: { x: 0, y: 0, w: 13.33, h: 1.2, fill: { color: theme.headerBg } } });
    objects.push({ rect: { x: 0, y: 1.2, w: 13.33, h: 0.06, fill: { color: theme.accent } } });
  } else if (theme.headerStyle === "line") {
    objects.push({ rect: { x: 0.8, y: 1.25, w: 1.6, h: 0.06, fill: { color: theme.accent } } });
  } else {
    objects.push({ rect: { x: 0, y: 0, w: 0.35, h: 7.5, fill: { color: theme.accent } } });
  }

  pptx.defineSlideMaster({
    title: "CONTENT",
    background: { color: theme.bg },
    objects,
    slideNumber: { x: 12.3, y: 6.9, w: 0.7, h: 0.4, fontSize: 12, color: theme.number, align: "right" },
  });

  // ---------- Slide tiêu đề ----------
  const cover = pptx.addSlide();
  cover.background = { color: theme.coverBg };
  cover.addText(outline.title, {
    x: 0.8, y: 1.9, w: 11.7, h: 1.9,
    fontSize: 40, bold: true, color: theme.coverTitle,
    fontFace: theme.fontTitle, valign: "bottom",
  });
  cover.addShape(pptx.ShapeType.rect, {
    x: 0.8, y: 3.95, w: 2, h: 0.08, fill: { color: theme.accent },
  });
  cover.addText(outline.subtitle, {
    x: 0.8, y: 4.2, w: 11.7, h: 0.9,
    fontSize: 20, color: theme.coverSubtitle, fontFace: theme.fontBody,
  });

  // ---------- Các slide nội dung ----------
  outline.slides.forEach((item) => {
    const slide = pptx.addSlide({ masterName: "CONTENT" });

    slide.addText(item.title, {
      x: theme.headerStyle === "bar" ? 0.6 : 0.8, y: 0.2, w: 12.1, h: 0.9,
      fontSize: 28, bold: true, color: theme.title,
      fontFace: theme.fontTitle, valign: "middle",
    });

    slide.addText(
      item.bullets.map((text) => ({ text, options: { bullet: true, breakLine: true } })),
      {
        x: 0.8, y: 1.7, w: 11.7, h: 4.9,
        fontSize: 22, color: theme.text, fontFace: theme.fontBody,
        valign: "top", paraSpaceAfter: 12,
      },
    );

    if (item.notes) slide.addNotes(item.notes);
  });

  await pptx.writeFile({ fileName });
}