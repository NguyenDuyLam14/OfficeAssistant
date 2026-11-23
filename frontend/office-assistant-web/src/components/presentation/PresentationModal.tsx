import { Presentation, X } from "lucide-react";
import PresentationBuilder from "./PresentationBuilder";

type Props = {
  title: string;
  documentId?: number;
  content?: string;
  onClose: () => void;
};

export default function PresentationModal({
  title,
  documentId,
  content,
  onClose,
}: Props) {
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/40 px-4">
      <div className="max-h-[90vh] w-full max-w-4xl overflow-y-auto rounded-2xl bg-white p-6 shadow-2xl">
        <div className="mb-4 flex items-start justify-between gap-3">
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
        <PresentationBuilder
          title={title}
          documentId={documentId}
          content={content}
        />
      </div>
    </div>
  );
}
