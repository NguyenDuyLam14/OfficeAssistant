import { useCallback, useEffect, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { AlarmClock, Bell, X } from "lucide-react";

import api from "../../services/api";
import { getCurrentUser } from "../../services/authService";

type NotificationItem = {
  id: number;
  title: string;
  message: string;
  type: string;
  isRead: boolean;
  createdAtUtc: string;
};

const POLL_INTERVAL_MS = 20_000; // kiểm tra mỗi 20 giây
const TOAST_DURATION_MS = 8_000; // tự tắt sau 8 giây
const MAX_TOASTS = 4;

const isReminder = (type: string) => type.startsWith("Reminder");

// ============================================================
// MỘT TOAST
// ============================================================
function ToastItem({
  item,
  onClose,
  onOpen,
}: {
  item: NotificationItem;
  onClose: (id: number) => void;
  onOpen: (item: NotificationItem) => void;
}) {
  const [paused, setPaused] = useState(false);

  // Tự tắt; rê chuột vào thì tạm dừng đếm.
  useEffect(() => {
    if (paused) return;

    const timer = setTimeout(() => onClose(item.id), TOAST_DURATION_MS);
    return () => clearTimeout(timer);
  }, [paused, item.id, onClose]);

  const urgent = item.type === "Reminder1h";

  return (
    <div
      onMouseEnter={() => setPaused(true)}
      onMouseLeave={() => setPaused(false)}
      onClick={() => onOpen(item)}
      className="toast-slide-in pointer-events-auto relative w-80 cursor-pointer overflow-hidden rounded-xl border border-slate-200 bg-white shadow-2xl transition hover:shadow-xl"
      role="alert"
    >
      <div
        className={`absolute left-0 top-0 h-full w-1 ${
          urgent ? "bg-red-500" : "bg-blue-600"
        }`}
      />

      <div className="flex items-start gap-3 py-3 pl-4 pr-3">
        <div
          className={`mt-0.5 flex h-10 w-10 shrink-0 items-center justify-center rounded-full ${
            urgent ? "bg-red-50 text-red-600" : "bg-blue-50 text-blue-600"
          }`}
        >
          {isReminder(item.type) ? (
            <AlarmClock size={20} />
          ) : (
            <Bell size={20} />
          )}
        </div>

        <div className="min-w-0 flex-1">
          <p className="truncate text-sm font-semibold text-slate-800">
            {item.title}
          </p>
          <p className="mt-0.5 line-clamp-3 text-xs leading-5 text-slate-600">
            {item.message}
          </p>
          <p className="mt-1 text-[11px] text-slate-400">OfficeAssistant</p>
        </div>

        <button
          type="button"
          onClick={(event) => {
            event.stopPropagation();
            onClose(item.id);
          }}
          className="rounded-md p-1 text-slate-400 hover:bg-slate-100 hover:text-slate-600"
          title="Đóng"
        >
          <X size={16} />
        </button>
      </div>
    </div>
  );
}

// ============================================================
// KHU VỰC HIỂN THỊ TOAST
// ============================================================
export default function NotificationToaster() {
  const navigate = useNavigate();
  const [toasts, setToasts] = useState<NotificationItem[]>([]);

  const userId = getCurrentUser()?.userId;
  const storageKey = `lastToastNotificationId:${userId ?? 0}`;

  // ID lớn nhất đã xử lý (để không hiện lại thông báo cũ).
  const lastIdRef = useRef<number | null>(null);

  const closeToast = useCallback((id: number) => {
    setToasts((current) => current.filter((t) => t.id !== id));
  }, []);

  const openToast = useCallback(
    (item: NotificationItem) => {
      void api
        .patch(`/Notifications/${item.id}/read`)
        .then(() => window.dispatchEvent(new Event("notifications:changed")))
        .catch(() => {});

      closeToast(item.id);
      navigate(isReminder(item.type) ? "/tasks" : "/notifications");
    },
    [closeToast, navigate],
  );

  // Xin quyền thông báo hệ điều hành ở lần click đầu tiên của người dùng.
  useEffect(() => {
    if (!("Notification" in window) || Notification.permission !== "default") {
      return;
    }

    const ask = () => {
      void Notification.requestPermission();
      document.removeEventListener("click", ask);
    };

    document.addEventListener("click", ask);
    return () => document.removeEventListener("click", ask);
  }, []);

  // Kiểm tra thông báo mới định kỳ.
  useEffect(() => {
    let cancelled = false;

    const stored = localStorage.getItem(storageKey);
    lastIdRef.current = stored ? Number(stored) : null;

    async function check() {
      try {
        const response = await api.get<{ items?: NotificationItem[] }>(
          "/Notifications",
          { params: { page: 1, pageSize: 10 } },
        );

        if (cancelled) return;

        const items = response.data.items ?? [];
        const maxId = items.reduce((m, x) => Math.max(m, x.id), 0);

        // Lần đầu tiên chưa có mốc: chỉ ghi nhận, không hiện thông báo cũ.
        if (lastIdRef.current === null) {
          lastIdRef.current = maxId;
          localStorage.setItem(storageKey, String(maxId));
          return;
        }

        const fresh = items
          .filter((x) => x.id > lastIdRef.current! && !x.isRead)
          .sort((a, b) => a.id - b.id);

        if (maxId > lastIdRef.current) {
          lastIdRef.current = maxId;
          localStorage.setItem(storageKey, String(maxId));
        }

        if (fresh.length === 0) return;

        setToasts((current) => [...current, ...fresh].slice(-MAX_TOASTS));

        // Báo cho Header cập nhật số chưa đọc ngay.
        window.dispatchEvent(new Event("notifications:changed"));

        // Tab đang ẩn/thu nhỏ: bắn thêm thông báo của hệ điều hành.
        if (
          document.hidden &&
          "Notification" in window &&
          Notification.permission === "granted"
        ) {
          for (const item of fresh) {
            const popup = new Notification(item.title, {
              body: item.message,
              tag: `office-assistant-${item.id}`,
            });

            popup.onclick = () => {
              window.focus();
              navigate(isReminder(item.type) ? "/tasks" : "/notifications");
              popup.close();
            };
          }
        }
      } catch {
        // Lỗi tạm thời: lần sau thử lại.
      }
    }

    void check();
    const timer = setInterval(() => void check(), POLL_INTERVAL_MS);

    return () => {
      cancelled = true;
      clearInterval(timer);
    };
  }, [storageKey, navigate]);

  if (toasts.length === 0) return null;

  return (
    <div className="pointer-events-none fixed bottom-5 right-5 z-[100] flex flex-col gap-3">
      {toasts.map((item) => (
        <ToastItem
          key={item.id}
          item={item}
          onClose={closeToast}
          onOpen={openToast}
        />
      ))}
    </div>
  );
}
