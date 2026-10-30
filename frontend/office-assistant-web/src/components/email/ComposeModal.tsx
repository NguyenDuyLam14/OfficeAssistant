import { X } from "lucide-react";

import ComposeForm from "./ComposeForm";

type ComposeModalProps = {
  onClose: () => void;
  onSent: () => void;
};

// Cửa sổ soạn thư mới.
function ComposeModal({ onClose, onSent }: ComposeModalProps) {
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/40 px-4">
      <div className="max-h-[90vh] w-full max-w-2xl overflow-y-auto rounded-2xl bg-white shadow-2xl">
        <div className="flex items-center justify-between border-b border-slate-200 px-6 py-4">
          <h2 className="text-lg font-semibold text-slate-800">
            Soạn thư mới
          </h2>

          <button
            type="button"
            onClick={onClose}
            className="rounded-lg p-2 text-slate-500 hover:bg-slate-100"
            title="Đóng"
          >
            <X size={20} />
          </button>
        </div>

        <div className="p-6">
          <ComposeForm onSent={onSent} onCancel={onClose} />
        </div>
      </div>
    </div>
  );
}

export default ComposeModal;
