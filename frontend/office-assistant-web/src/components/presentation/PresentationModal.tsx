import { useState } from "react";
import { LoaderCircle, Presentation, X } from "lucide-react";

import { getApiMessage } from "../../services/emailService";
import {
  downloadPptx,
  requestOutline,
} from "../../services/presentationService";
import type { SlideOutline } from "../../services/presentationService";

type Props = {
  title: string;
  documentId?: number;
  content?: string; // dùng khi chưa có documentId (dữ liệu khác)
  onClose: () => void;
};

const inputClass =
  "w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100";

export default function PresentationModal({
  title,
  documentId,
  content,
  onClose,
}: Props) {
  const [slideCount, setSlideCount] = useState(6);
  const [audience, setAudience] = useState("Lãnh đạo, đồng nghiệp");
  const [outline, setOutline] = useState<SlideOutline | null>(null);
  const [loading, setLoading] = useState(false);
  const [exporting, setExporting] = useState(false);
  const [error, setError] = useState("");

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
      await downloadPptx(outline, `${safe}.pptx`);
    } catch {
      setError("Không thể tạo file PowerPoint.");
    } finally {
      setExporting(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/40 px-4">
      <div className="max-h-[90vh] w-full max-w-3xl overflow-y-auto rounded-2xl bg-white p-6 shadow-2xl">
        <div className="flex items-start justify-between gap-3">
          <div className="min-w-0">
            <h3 className="flex items-center gap-2 text-lg font-bold text-slate-800">
              <Presentation size={18} className="text-blue-600" />
              Tạo PowerPoint báo cáo
            </h3>
            <p className="mt-1 truncate text-sm text-slate-500">
              Nguồn: {title}
            </p>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="rounded-lg p-1.5 text-slate-400 hover:bg-slate-100"
          >
            <X size={18} />
          </button>
        </div>

        {error && (
          <div className="mt-4 rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
            {error}
          </div>
        )}

        <div className="mt-4 grid gap-3 sm:grid-cols-[120px_1fr_auto] sm:items-end">
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
              Kiểm tra và chỉnh sửa dàn ý trước khi tải. Mỗi dòng là một gạch
              đầu dòng.
            </p>

            <input
              value={outline.title}
              onChange={(e) =>
                setOutline({ ...outline, title: e.target.value })
              }
              className={`${inputClass} font-semibold`}
            />

            {outline.slides.map((slide, index) => (
              <div
                key={index}
                className="rounded-xl border border-slate-200 p-4"
              >
                <p className="mb-2 text-xs font-semibold text-slate-400">
                  Slide {index + 1}
                </p>
                <input
                  value={slide.title}
                  onChange={(e) =>
                    updateSlide(index, { title: e.target.value })
                  }
                  className={`${inputClass} font-medium`}
                />
                <textarea
                  rows={4}
                  value={slide.bullets.join("\n")}
                  onChange={(e) =>
                    updateSlide(index, {
                      bullets: e.target.value
                        .split("\n")
                        .filter((l) => l.trim()),
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
                {exporting && (
                  <LoaderCircle size={16} className="animate-spin" />
                )}
                Tải file .pptx
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
