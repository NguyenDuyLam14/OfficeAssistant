import { useState } from "react";
import { Check, LoaderCircle } from "lucide-react";

import { getApiMessage } from "../../services/emailService";
import {
  downloadPptx,
  requestOutline,
} from "../../services/presentationService";
import type { SlideOutline } from "../../services/presentationService";
import { PPT_THEMES } from "../../services/pptThemes";

type Props = {
  title: string;
  documentId?: number;
  content?: string;
};

const inputClass =
  "w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100";

export default function PresentationBuilder({
  title,
  documentId,
  content,
}: Props) {
  const [themeId, setThemeId] = useState(PPT_THEMES[0].id);
  const [slideCount, setSlideCount] = useState(6);
  const [audience, setAudience] = useState("Lãnh đạo, đồng nghiệp");
  const [outline, setOutline] = useState<SlideOutline | null>(null);
  const [loading, setLoading] = useState(false);
  const [exporting, setExporting] = useState(false);
  const [error, setError] = useState("");

  const theme = PPT_THEMES.find((t) => t.id === themeId) ?? PPT_THEMES[0];

  const handleGenerate = async () => {
    setLoading(true);
    setError("");
    try {
      setOutline(
        await requestOutline({
          documentId,
          title,
          content,
          slideCount,
          audience,
        }),
      );
    } catch (err) {
      setError(getApiMessage(err, "Không thể tạo dàn ý."));
    } finally {
      setLoading(false);
    }
  };

  const updateSlide = (
    index: number,
    patch: Partial<SlideOutline["slides"][number]>,
  ) => {
    if (!outline) return;
    setOutline({
      ...outline,
      slides: outline.slides.map((s, i) =>
        i === index ? { ...s, ...patch } : s,
      ),
    });
  };

  const handleDownload = async () => {
    if (!outline) return;
    setExporting(true);
    try {
      const safe = title.replace(/[\\/:*?"<>|]/g, "_");
      await downloadPptx(outline, `${safe}.pptx`, theme);
    } catch {
      setError("Không thể tạo file PowerPoint.");
    } finally {
      setExporting(false);
    }
  };

  return (
    <div>
      {error && (
        <div className="mb-4 rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
          {error}
        </div>
      )}

      {/* ===== Chọn mẫu ===== */}
      <p className="mb-2 text-sm font-medium text-slate-700">
        Chọn mẫu PowerPoint
      </p>
      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        {PPT_THEMES.map((t) => {
          const active = t.id === themeId;
          return (
            <button
              key={t.id}
              type="button"
              onClick={() => setThemeId(t.id)}
              className={`rounded-xl border-2 p-2 text-left transition ${
                active
                  ? "border-blue-600 ring-2 ring-blue-100"
                  : "border-slate-200 hover:border-slate-300"
              }`}
            >
              {/* Xem trước thu nhỏ: slide tiêu đề + slide nội dung */}
              <div className="flex gap-1.5">
                <div
                  className="flex aspect-video flex-1 flex-col justify-center rounded px-2"
                  style={{
                    background: `#${t.coverBg}`,
                    border: "1px solid #e2e8f0",
                  }}
                >
                  <div
                    className="h-1.5 w-3/4 rounded"
                    style={{ background: `#${t.coverTitle}` }}
                  />
                  <div
                    className="mt-1 h-0.5 w-1/4 rounded"
                    style={{ background: `#${t.accent}` }}
                  />
                  <div
                    className="mt-1 h-1 w-1/2 rounded opacity-70"
                    style={{ background: `#${t.coverSubtitle}` }}
                  />
                </div>
                <div
                  className="relative aspect-video flex-1 overflow-hidden rounded"
                  style={{
                    background: `#${t.bg}`,
                    border: "1px solid #e2e8f0",
                  }}
                >
                  {t.headerStyle === "bar" && (
                    <div
                      className="h-3"
                      style={{
                        background: `#${t.headerBg}`,
                        borderBottom: `2px solid #${t.accent}`,
                      }}
                    />
                  )}
                  {t.headerStyle === "side" && (
                    <div
                      className="absolute inset-y-0 left-0 w-1"
                      style={{ background: `#${t.accent}` }}
                    />
                  )}
                  {t.headerStyle === "line" && (
                    <div
                      className="absolute left-2 top-3 h-0.5 w-5"
                      style={{ background: `#${t.accent}` }}
                    />
                  )}
                  <div className="mt-3 space-y-1 px-2">
                    <div
                      className="h-1 w-5/6 rounded opacity-70"
                      style={{ background: `#${t.text}` }}
                    />
                    <div
                      className="h-1 w-2/3 rounded opacity-70"
                      style={{ background: `#${t.text}` }}
                    />
                  </div>
                </div>
              </div>
              <div className="mt-2 flex items-center justify-between px-1">
                <span className="text-sm font-semibold text-slate-800">
                  {t.name}
                </span>
                {active && <Check size={16} className="text-blue-600" />}
              </div>
              <p className="px-1 pb-1 text-xs text-slate-500">
                {t.description}
              </p>
            </button>
          );
        })}
      </div>

      {/* ===== Dàn ý ===== */}
      <div className="mt-5 grid gap-3 sm:grid-cols-[120px_1fr_auto] sm:items-end">
        <label className="text-xs font-medium text-slate-600">
          Số slide
          <input
            type="number"
            min={3}
            max={12}
            value={slideCount}
            onChange={(e) => setSlideCount(Number(e.target.value))}
            className={`${inputClass} mt-1`}
          />
        </label>
        <label className="text-xs font-medium text-slate-600">
          Đối tượng nghe
          <input
            value={audience}
            onChange={(e) => setAudience(e.target.value)}
            className={`${inputClass} mt-1`}
          />
        </label>
        <button
          type="button"
          onClick={() => void handleGenerate()}
          disabled={loading}
          className="inline-flex items-center justify-center gap-2 rounded-xl bg-blue-600 px-4 py-2.5 text-sm font-semibold text-white hover:bg-blue-700 disabled:opacity-60"
        >
          {loading && <LoaderCircle size={16} className="animate-spin" />}
          {outline ? "Tạo lại dàn ý" : "Tạo dàn ý bằng AI"}
        </button>
      </div>

      {outline && (
        <div className="mt-5 space-y-4">
          <p className="text-xs text-slate-500">
            Kiểm tra và chỉnh sửa dàn ý trước khi tải. Mỗi dòng là một gạch đầu
            dòng. Đổi mẫu ở trên không cần tạo lại dàn ý.
          </p>

          <input
            value={outline.title}
            onChange={(e) => setOutline({ ...outline, title: e.target.value })}
            className={`${inputClass} font-semibold`}
          />

          {outline.slides.map((slide, index) => (
            <div key={index} className="rounded-xl border border-slate-200 p-4">
              <p className="mb-2 text-xs font-semibold text-slate-400">
                Slide {index + 1}
              </p>
              <input
                value={slide.title}
                onChange={(e) => updateSlide(index, { title: e.target.value })}
                className={`${inputClass} font-medium`}
              />
              <textarea
                rows={4}
                value={slide.bullets.join("\n")}
                onChange={(e) =>
                  updateSlide(index, {
                    bullets: e.target.value.split("\n").filter((l) => l.trim()),
                  })
                }
                className={`${inputClass} mt-2 resize-y`}
              />
            </div>
          ))}

          <div className="flex justify-end">
            <button
              type="button"
              onClick={() => void handleDownload()}
              disabled={exporting}
              className="inline-flex items-center gap-2 rounded-xl bg-green-600 px-5 py-2.5 text-sm font-semibold text-white hover:bg-green-700 disabled:opacity-60"
            >
              {exporting && <LoaderCircle size={16} className="animate-spin" />}
              Tải file .pptx ({theme.name})
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
