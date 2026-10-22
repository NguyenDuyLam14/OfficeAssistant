import { useCallback, useEffect, useState } from "react";
import {
  Bell,
  Check,
  CheckCheck,
  Clock,
  RefreshCw,
  AlertCircle,
} from "lucide-react";
import api from "../services/api";

type NotificationItem = {
  id: number;
  title: string;
  message: string;
  type: string;
  detectedWorkEventId: number | null;
  isRead: boolean;
  createdAtUtc: string;
};

type DetectedEvent = {
  id: number;
  title: string;
  description: string;
  eventType: string;
  startAtUtc: string | null;
  dueAtUtc: string | null;
  status: string;
  detectedAtUtc: string;
};

type NotificationResponse = {
  items?: NotificationItem[];
  notifications?: NotificationItem[];
  unreadCount?: number;
};

type EventResponse = {
  items?: DetectedEvent[];
  events?: DetectedEvent[];
};

function formatDate(value: string | null | undefined) {
  if (!value) return "Chưa xác định thời gian";

  // Backend trả UTC nhưng không có "Z" → thêm vào để JS hiểu đúng.
  const hasTimeZone = /([zZ]|[+-]\d{2}:\d{2})$/.test(value);
  const date = new Date(hasTimeZone ? value : value + "Z");

  if (Number.isNaN(date.getTime())) return "Chưa xác định thời gian";

  return date.toLocaleString("vi-VN", {
    dateStyle: "short",
    timeStyle: "short",
  });
}

function Notifications() {
  const [notifications, setNotifications] = useState<NotificationItem[]>([]);
  const [events, setEvents] = useState<DetectedEvent[]>([]);
  const [unreadCount, setUnreadCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [actionId, setActionId] = useState<number | null>(null);
  const [activeTab, setActiveTab] = useState<"notifications" | "events">(
    "notifications",
  );

  const loadData = useCallback(async () => {
    setError("");

    try {
      const [notificationResult, eventResult] = await Promise.all([
        api.get<NotificationResponse>("/Notifications", {
          params: { page: 1, pageSize: 50 },
        }),
        api.get<EventResponse>("/DetectedWorkEvents", {
          params: { status: "PendingReview", page: 1, pageSize: 50 },
        }),
      ]);

      const notificationData = notificationResult.data;
      const eventData = eventResult.data;

      setNotifications(
        notificationData.items ?? notificationData.notifications ?? [],
      );

      setUnreadCount(
        notificationData.unreadCount ??
          (
            notificationData.items ??
            notificationData.notifications ??
            []
          ).filter((item) => !item.isRead).length,
      );

      setEvents(eventData.items ?? eventData.events ?? []);
    } catch (err) {
      console.error("Không thể tải thông báo:", err);
      setError(
        "Không thể tải dữ liệu. Hãy kiểm tra backend đang chạy và đăng nhập còn hiệu lực.",
      );
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    async function fetchNotifications() {
      await loadData();
    }

    void fetchNotifications();
  }, [loadData]);

  const markAsRead = async (id: number) => {
    setActionId(id);

    try {
      await api.patch(`/Notifications/${id}/read`);

      setNotifications((current) =>
        current.map((item) =>
          item.id === id ? { ...item, isRead: true } : item,
        ),
      );

      setUnreadCount((count) => Math.max(0, count - 1));
    } catch (err) {
      console.error("Không thể đánh dấu đã đọc:", err);
      setError("Không thể cập nhật trạng thái thông báo.");
    } finally {
      setActionId(null);
    }
  };

  const markAllAsRead = async () => {
    setActionId(-1);

    try {
      await api.patch("/Notifications/read-all");

      setNotifications((current) =>
        current.map((item) => ({ ...item, isRead: true })),
      );

      setUnreadCount(0);
    } catch (err) {
      console.error("Không thể đánh dấu tất cả đã đọc:", err);
      setError("Không thể cập nhật trạng thái thông báo.");
    } finally {
      setActionId(null);
    }
  };

  const updateEvent = async (
    id: number,
    action: "confirm" | "dismiss" | "complete",
  ) => {
    setActionId(id);

    try {
      await api.patch(`/DetectedWorkEvents/${id}/${action}`);

      // Xóa khỏi danh sách chờ duyệt sau khi thao tác thành công.
      setEvents((current) => current.filter((event) => event.id !== id));
    } catch (err) {
      console.error("Không thể cập nhật sự kiện:", err);
      setError("Không thể cập nhật sự kiện. Vui lòng thử lại.");
    } finally {
      setActionId(null);
    }
  };

  return (
    <main className="min-h-[calc(100vh-4rem)] p-6">
      <div className="mx-auto max-w-6xl space-y-6">
        <div className="flex flex-col justify-between gap-4 sm:flex-row sm:items-center">
          <div>
            <h1 className="text-2xl font-bold text-slate-800">
              Thông báo và nhắc việc
            </h1>
            <p className="mt-1 text-sm text-slate-500">
              Theo dõi thông báo và các sự kiện được phát hiện từ email Gmail.
            </p>
          </div>

          <button
            type="button"
            onClick={() => void loadData()}
            disabled={loading}
            className="inline-flex items-center justify-center gap-2 rounded-lg border border-slate-200 bg-white px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-50"
          >
            <RefreshCw size={16} />
            Làm mới
          </button>
        </div>

        <div className="grid gap-4 sm:grid-cols-3">
          <div className="rounded-xl border border-slate-200 bg-white p-5 shadow-sm">
            <div className="flex items-center justify-between">
              <span className="text-sm text-slate-500">Thông báo chưa đọc</span>
              <Bell className="text-blue-600" size={21} />
            </div>
            <p className="mt-3 text-3xl font-bold text-slate-800">
              {unreadCount}
            </p>
          </div>

          <div className="rounded-xl border border-slate-200 bg-white p-5 shadow-sm">
            <div className="flex items-center justify-between">
              <span className="text-sm text-slate-500">
                Sự kiện chờ xác nhận
              </span>
              <Clock className="text-amber-600" size={21} />
            </div>
            <p className="mt-3 text-3xl font-bold text-slate-800">
              {events.length}
            </p>
          </div>

          <div className="rounded-xl border border-slate-200 bg-white p-5 shadow-sm">
            <div className="flex items-center justify-between">
              <span className="text-sm text-slate-500">Thông báo đã tải</span>
              <CheckCheck className="text-emerald-600" size={21} />
            </div>
            <p className="mt-3 text-3xl font-bold text-slate-800">
              {notifications.length}
            </p>
          </div>
        </div>

        <div className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-200">
          <div className="flex gap-2">
            <button
              type="button"
              onClick={() => setActiveTab("notifications")}
              className={`border-b-2 px-4 py-3 text-sm font-medium ${
                activeTab === "notifications"
                  ? "border-blue-600 text-blue-700"
                  : "border-transparent text-slate-500 hover:text-slate-800"
              }`}
            >
              Thông báo
            </button>

            <button
              type="button"
              onClick={() => setActiveTab("events")}
              className={`border-b-2 px-4 py-3 text-sm font-medium ${
                activeTab === "events"
                  ? "border-blue-600 text-blue-700"
                  : "border-transparent text-slate-500 hover:text-slate-800"
              }`}
            >
              Sự kiện Gmail ({events.length})
            </button>
          </div>

          {activeTab === "notifications" && unreadCount > 0 && (
            <button
              type="button"
              onClick={() => void markAllAsRead()}
              disabled={actionId !== null}
              className="mb-2 inline-flex items-center gap-2 rounded-lg px-3 py-2 text-sm text-blue-700 hover:bg-blue-50 disabled:opacity-50"
            >
              <CheckCheck size={16} />
              Đánh dấu tất cả đã đọc
            </button>
          )}
        </div>

        {error && (
          <div className="flex items-start gap-3 rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700">
            <AlertCircle className="mt-0.5 shrink-0" size={18} />
            <p>{error}</p>
          </div>
        )}

        {loading ? (
          <div className="rounded-xl border border-slate-200 bg-white p-10 text-center text-slate-500">
            Đang tải dữ liệu...
          </div>
        ) : activeTab === "notifications" ? (
          <section className="overflow-hidden rounded-xl border border-slate-200 bg-white shadow-sm">
            {notifications.length === 0 ? (
              <div className="p-10 text-center">
                <Bell className="mx-auto text-slate-300" size={36} />
                <p className="mt-3 font-medium text-slate-700">
                  Chưa có thông báo
                </p>
                <p className="mt-1 text-sm text-slate-500">
                  Thông báo mới sẽ xuất hiện tại đây.
                </p>
              </div>
            ) : (
              <div className="divide-y divide-slate-100">
                {notifications.map((item) => (
                  <article
                    key={item.id}
                    className={`flex flex-col gap-4 p-5 sm:flex-row sm:items-start sm:justify-between ${
                      item.isRead ? "bg-white" : "bg-blue-50/50"
                    }`}
                  >
                    <div className="flex gap-3">
                      <div
                        className={`mt-1 flex h-10 w-10 shrink-0 items-center justify-center rounded-full ${
                          item.isRead
                            ? "bg-slate-100 text-slate-500"
                            : "bg-blue-100 text-blue-700"
                        }`}
                      >
                        <Bell size={18} />
                      </div>

                      <div>
                        <h2 className="font-semibold text-slate-800">
                          {item.title}
                        </h2>
                        <p className="mt-1 whitespace-pre-wrap text-sm text-slate-600">
                          {item.message}
                        </p>
                        <p className="mt-2 text-xs text-slate-400">
                          {formatDate(item.createdAtUtc)}
                        </p>
                      </div>
                    </div>

                    {!item.isRead && (
                      <button
                        type="button"
                        onClick={() => void markAsRead(item.id)}
                        disabled={actionId !== null}
                        className="inline-flex shrink-0 items-center justify-center gap-2 self-start rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700 hover:bg-slate-50 disabled:opacity-50"
                      >
                        <Check size={15} />
                        Đã đọc
                      </button>
                    )}
                  </article>
                ))}
              </div>
            )}
          </section>
        ) : (
          <section className="space-y-4">
            {events.length === 0 ? (
              <div className="rounded-xl border border-slate-200 bg-white p-10 text-center">
                <Clock className="mx-auto text-slate-300" size={36} />
                <p className="mt-3 font-medium text-slate-700">
                  Không có sự kiện chờ xác nhận
                </p>
                <p className="mt-1 text-sm text-slate-500">
                  Các sự kiện được phát hiện từ Gmail sẽ xuất hiện tại đây.
                </p>
              </div>
            ) : (
              events.map((event) => (
                <article
                  key={event.id}
                  className="rounded-xl border border-slate-200 bg-white p-5 shadow-sm"
                >
                  <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
                    <div className="min-w-0">
                      <div className="flex flex-wrap items-center gap-2">
                        <h2 className="font-semibold text-slate-800">
                          {event.title}
                        </h2>
                        <span className="rounded-full bg-amber-100 px-2.5 py-1 text-xs font-medium text-amber-800">
                          Chờ xác nhận
                        </span>
                      </div>

                      <p className="mt-2 whitespace-pre-wrap text-sm text-slate-600">
                        {event.description || "Không có mô tả."}
                      </p>

                      <dl className="mt-4 grid gap-2 text-sm text-slate-500 sm:grid-cols-2">
                        <div>
                          <dt className="inline font-medium">Loại sự kiện: </dt>
                          <dd className="inline">{event.eventType}</dd>
                        </div>
                        <div>
                          <dt className="inline font-medium">Bắt đầu: </dt>
                          <dd className="inline">
                            {formatDate(event.startAtUtc)}
                          </dd>
                        </div>
                        <div>
                          <dt className="inline font-medium">Hạn xử lý: </dt>
                          <dd className="inline">
                            {formatDate(event.dueAtUtc)}
                          </dd>
                        </div>
                        <div>
                          <dt className="inline font-medium">Phát hiện: </dt>
                          <dd className="inline">
                            {formatDate(event.detectedAtUtc)}
                          </dd>
                        </div>
                      </dl>
                    </div>

                    <div className="flex shrink-0 flex-wrap gap-2">
                      <button
                        type="button"
                        onClick={() => void updateEvent(event.id, "confirm")}
                        disabled={actionId !== null}
                        className="rounded-lg bg-blue-600 px-3 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50"
                      >
                        Xác nhận
                      </button>

                      <button
                        type="button"
                        onClick={() => void updateEvent(event.id, "dismiss")}
                        disabled={actionId !== null}
                        className="rounded-lg border border-slate-200 px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-50"
                      >
                        Bỏ qua
                      </button>

                      {event.status === "Confirmed" && (
                        <button
                          type="button"
                          onClick={() => void updateEvent(event.id, "complete")}
                          disabled={actionId !== null}
                          className="rounded-lg bg-emerald-600 px-3 py-2 text-sm font-medium text-white hover:bg-emerald-700 disabled:opacity-50"
                        >
                          Hoàn thành
                        </button>
                      )}
                    </div>
                  </div>
                </article>
              ))
            )}
          </section>
        )}
      </div>
    </main>
  );
}

export default Notifications;
