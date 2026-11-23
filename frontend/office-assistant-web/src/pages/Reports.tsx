import { useEffect, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { Presentation } from "lucide-react";

import api from "../services/api";
import UploadSummaryPanel from "../components/documents/UploadSummaryPanel";
import type { PptSource } from "../components/documents/UploadSummaryPanel";
import PresentationBuilder from "../components/presentation/PresentationBuilder";

type DocItem = {
  documentId: number;
  title: string;
  content: string | null;
  hasFile?: boolean;
};

function Reports() {
  const [params] = useSearchParams();
  const initialId = Number(params.get("documentId")) || null;

  const [docs, setDocs] = useState<DocItem[]>([]);
  const [source, setSource] = useState<PptSource | null>(null);
  const [other, setOther] = useState("");

  const loadDocs = async () => {
    try {
      const response = await api.get<DocItem[]>("/documents");
      setDocs(response.data);
      return response.data;
    } catch {
      return [];
    }
  };

  useEffect(() => {
    async function init() {
      const list = await loadDocs();
      const found = initialId && list.find((d) => d.documentId === initialId);
      if (found)
        setSource({ title: found.title, documentId: found.documentId });
    }
    void init();
  }, [initialId]);

  return (
    <main className="p-6">
      <div className="mb-6 flex items-center gap-3">
        <div className="rounded-xl bg-blue-100 p-3 text-blue-600">
          <Presentation size={26} />
        </div>
        <div>
          <h1 className="text-2xl font-bold text-slate-800">
            Báo cáo PowerPoint
          </h1>
          <p className="text-sm text-slate-500">
            Tóm tắt tài liệu và tạo slide báo cáo bằng AI, chọn mẫu cho đẹp.
          </p>
        </div>
      </div>

      {/* Nguồn 1: file tải lên */}
      <UploadSummaryPanel
        onSaved={async () => {
          await loadDocs();
        }}
        onPickSource={setSource}
      />

      {/* Nguồn 2: văn bản đã lưu */}
      <section className="mb-6 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
        <h2 className="text-lg font-bold text-slate-800">Từ văn bản đã lưu</h2>
        <select
          value={source?.documentId ?? ""}
          onChange={(e) => {
            const d = docs.find((x) => x.documentId === Number(e.target.value));
            if (d) setSource({ title: d.title, documentId: d.documentId });
          }}
          className="mt-3 w-full rounded-xl border border-slate-300 bg-white px-4 py-3 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100"
        >
          <option value="">Chọn văn bản...</option>
          {docs
            .filter((d) => d.content?.trim() || d.hasFile)
            .map((d) => (
              <option key={d.documentId} value={d.documentId}>
                {d.title}
              </option>
            ))}
        </select>
      </section>

      {/* Nguồn 3: dữ liệu khác */}
      <section className="mb-6 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
        <h2 className="text-lg font-bold text-slate-800">Từ dữ liệu khác</h2>
        <textarea
          rows={4}
          value={other}
          onChange={(e) => setOther(e.target.value)}
          placeholder="Dán số liệu, ghi chú, nội dung báo cáo... (tối thiểu 50 ký tự)"
          className="mt-3 w-full resize-y rounded-xl border border-slate-300 px-4 py-3 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100"
        />
        <button
          type="button"
          disabled={other.trim().length < 50}
          onClick={() => setSource({ title: "Báo cáo", content: other.trim() })}
          className="mt-2 rounded-xl border border-slate-200 px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-50"
        >
          Dùng dữ liệu này làm nguồn
        </button>
      </section>

      {/* Tạo PPT */}
      <section className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
        <h2 className="text-lg font-bold text-slate-800">
          Tạo bài thuyết trình
        </h2>
        {source ? (
          <>
            <p className="mb-4 mt-1 text-sm text-slate-500">
              Nguồn: <b>{source.title}</b>
            </p>
            {/* key để reset dàn ý khi đổi nguồn */}
            <PresentationBuilder
              key={`${source.documentId ?? "c"}-${source.title}`}
              title={source.title}
              documentId={source.documentId}
              content={source.content}
            />
          </>
        ) : (
          <p className="mt-2 rounded-xl bg-slate-50 p-4 text-center text-sm text-slate-500">
            Hãy chọn một nguồn ở phía trên để bắt đầu.
          </p>
        )}
      </section>
    </main>
  );
}

export default Reports;
