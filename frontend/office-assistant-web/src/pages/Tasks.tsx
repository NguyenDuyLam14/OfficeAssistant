import { useCallback, useEffect, useMemo, useState } from "react";
import {
  CalendarDays,
  CheckCircle2,
  ChevronLeft,
  ChevronRight,
  Clock,
  RefreshCw,
  X,
} from "lucide-react";

import api from "../services/api";

// =========================================================
// KIỂU DỮ LIỆU
// =========================================================

type CalendarEvent = {
  id: number;
  title: string;
  description: string | null;
  eventType: string;
  startAtUtc: string | null;
  dueAtUtc: string | null;
  status: "Confirmed" | "Completed" | string;
};

// =========================================================
// HÀM HỖ TRỢ
// =========================================================

// Backend trả UTC không có "Z" → thêm vào để JS hiểu đúng.
function parseUtc(value: string): Date {
  const hasTimeZone = /([zZ]|[+-]\d{2}:\d{2})$/.test(value);
  return new Date(hasTimeZone ? value : value + "Z");
}

// Mốc thời gian dùng để đặt lên lịch.
function getEventDate(event: CalendarEvent): Date | null {
  const value = event.startAtUtc ?? event.dueAtUtc;
  if (!value) return null;

  const date = parseUtc(value);
  return Number.isNaN(date.getTime()) ? null : date;
}

// Event chỉ có hạn chót (không có giờ bắt đầu).
function isDeadlineOnly(event: CalendarEvent) {
  return !event.startAtUtc && !!event.dueAtUtc;
}

function dateKey(date: Date) {
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, "0");
  const d = String(date.getDate()).padStart(2, "0");
  return `${y}-${m}-${d}`;
}

function formatTime(date: Date) {
  return date.toLocaleTimeString("vi-VN", {
    hour: "2-digit",
    minute: "2-digit",
  });
}

function formatFull(date: Date) {
  return date.toLocaleString("vi-VN", {
    dateStyle: "full",
    timeStyle: "short",
  });
}

// Màu theo loại sự kiện (giống màu event của Google Calendar).
const typeStyles: Record<string, { chip: string; dot: string; label: string }> =
  {
    Meeting: {
      chip: "bg-blue-100 text-blue-800 hover:bg-blue-200",
      dot: "bg-blue-500",
      label: "Cuộc họp",
    },
    Deadline: {
      chip: "bg-red-100 text-red-800 hover:bg-red-200",
      dot: "bg-red-500",
      label: "Hạn chót",
    },
    Appointment: {
      chip: "bg-emerald-100 text-emerald-800 hover:bg-emerald-200",
      dot: "bg-emerald-500",
      label: "Lịch hẹn",
    },
    Task: {
      chip: "bg-amber-100 text-amber-800 hover:bg-amber-200",
      dot: "bg-amber-500",
      label: "Công việc",
    },
    Other: {
      chip: "bg-slate-200 text-slate-700 hover:bg-slate-300",
      dot: "bg-slate-500",
      label: "Khác",
    },
  };

function getStyle(type: string) {
  return typeStyles[type] ?? typeStyles.Other;
}

const WEEKDAYS = ["T2", "T3", "T4", "T5", "T6", "T7", "CN"];

// =========================================================
// TRANG LỊCH CÔNG VIỆC
// =========================================================

function Tasks() {
  const today = new Date();

  // Tháng đang xem (luôn là ngày 1 của tháng).
  const [viewMonth, setViewMonth] = useState(
    new Date(today.getFullYear(), today.getMonth(), 1),
  );

  const [events, setEvents] = useState<CalendarEvent[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [selectedEvent, setSelectedEvent] = useState<CalendarEvent | null>(
    null,
  );
  const [completingId, setCompletingId] = useState<number | null>(null);

  // -------------------------------------------------------
  // Tạo 42 ô (6 tuần) của lưới tháng, bắt đầu từ Thứ Hai.
  // -------------------------------------------------------
  const days = useMemo(() => {
    const firstDay = new Date(viewMonth);
    const offset = (firstDay.getDay() + 6) % 7; // T2 = 0
    const start = new Date(firstDay);
    start.setDate(firstDay.getDate() - offset);

    return Array.from({ length: 42 }, (_, i) => {
      const d = new Date(start);
      d.setDate(start.getDate() + i);
      return d;
    });
  }, [viewMonth]);

  // -------------------------------------------------------
  // Tải sự kiện trong khoảng của lưới.
  // -------------------------------------------------------
  const loadEvents = useCallback(async () => {
    setLoading(true);
    setError("");

    try {
      const from = days[0];
      const to = new Date(days[41]);
      to.setDate(to.getDate() + 1);

      const response = await api.get<CalendarEvent[]>(
        "/DetectedWorkEvents/calendar",
        {
          params: {
            from: from.toISOString(),
            to: to.toISOString(),
          },
        },
      );

      setEvents(response.data);
    } catch (err) {
      console.error("Không thể tải lịch công việc:", err);
      setError("Không thể tải lịch công việc. Hãy kiểm tra backend.");
    } finally {
      setLoading(false);
    }
  }, [days]);

  useEffect(() => {
    async function fetchEvents() {
      await loadEvents();
    }

    void fetchEvents();
  }, [loadEvents]);

  // -------------------------------------------------------
  // Gom sự kiện theo ngày.
  // -------------------------------------------------------
  const eventsByDay = useMemo(() => {
    const map = new Map<string, CalendarEvent[]>();

    for (const event of events) {
      const date = getEventDate(event);
      if (!date) continue;

      const key = dateKey(date);
      const list = map.get(key) ?? [];
      list.push(event);
      map.set(key, list);
    }

    return map;
  }, [events]);

  // Danh sách sắp tới (bên phải).
  const upcoming = useMemo(() => {
    const now = new Date();

    return events
      .filter((e) => e.status === "Confirmed")
      .map((e) => ({ event: e, date: getEventDate(e) }))
      .filter((x): x is { event: CalendarEvent; date: Date } => x.date !== null)
      .filter((x) => x.date >= now)
      .sort((a, b) => a.date.getTime() - b.date.getTime())
      .slice(0, 6);
  }, [events]);

  // -------------------------------------------------------
  // Điều hướng tháng.
  // -------------------------------------------------------
  const changeMonth = (delta: number) => {
    setViewMonth(
      (current) =>
        new Date(current.getFullYear(), current.getMonth() + delta, 1),
    );
  };

  const goToday = () => {
    const now = new Date();
    setViewMonth(new Date(now.getFullYear(), now.getMonth(), 1));
  };

  // -------------------------------------------------------
  // Đánh dấu hoàn thành.
  // -------------------------------------------------------
  const completeEvent = async (event: CalendarEvent) => {
    setCompletingId(event.id);

    try {
      await api.patch(`/DetectedWorkEvents/${event.id}/complete`);

      setEvents((current) =>
        current.map((e) =>
          e.id === event.id ? { ...e, status: "Completed" } : e,
        ),
      );
      setSelectedEvent((current) =>
        current && current.id === event.id
          ? { ...current, status: "Completed" }
          : current,
      );
    } catch (err) {
      console.error("Không thể hoàn thành công việc:", err);
      setError("Không thể cập nhật công việc. Vui lòng thử lại.");
    } finally {
      setCompletingId(null);
    }
  };

  const monthTitle = viewMonth.toLocaleDateString("vi-VN", {
    month: "long",
    year: "numeric",
  });

  const todayKey = dateKey(today);

  // =======================================================
  // GIAO DIỆN
  // =======================================================

  return (
    <main className="p-6">
      {/* Tiêu đề */}
      <div className="mb-6 flex flex-col gap-4 md:flex-row md:items-center md:justify-between">
        <div className="flex items-center gap-3">
          <div className="rounded-xl bg-blue-100 p-3 text-blue-600">
            <CalendarDays size={26} />
          </div>

          <div>
            <h1 className="text-2xl font-bold text-slate-800">
              Lịch công việc
            </h1>
            <p className="text-sm text-slate-500">
              Các công việc từ Gmail sau khi bạn xác nhận sẽ hiện tại đây.
            </p>
          </div>
        </div>

        <div className="flex items-center gap-2">
          <button
            type="button"
            onClick={goToday}
            className="rounded-lg border border-slate-200 bg-white px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50"
          >
            Hôm nay
          </button>

          <button
            type="button"
            onClick={() => changeMonth(-1)}
            className="rounded-lg border border-slate-200 bg-white p-2 text-slate-600 hover:bg-slate-50"
            title="Tháng trước"
          >
            <ChevronLeft size={18} />
          </button>

          <button
            type="button"
            onClick={() => changeMonth(1)}
            className="rounded-lg border border-slate-200 bg-white p-2 text-slate-600 hover:bg-slate-50"
            title="Tháng sau"
          >
            <ChevronRight size={18} />
          </button>

          <button
            type="button"
            onClick={() => void loadEvents()}
            disabled={loading}
            className="rounded-lg border border-slate-200 bg-white p-2 text-slate-600 hover:bg-slate-50 disabled:opacity-50"
            title="Làm mới"
          >
            <RefreshCw size={18} className={loading ? "animate-spin" : ""} />
          </button>
        </div>
      </div>

      {error && (
        <div className="mb-5 rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
          {error}
        </div>
      )}

      <div className="grid gap-6 xl:grid-cols-[1fr_320px]">
        {/* ===================== LƯỚI THÁNG ===================== */}
        <section className="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
          <div className="border-b border-slate-200 px-5 py-4">
            <h2 className="text-lg font-semibold capitalize text-slate-800">
              {monthTitle}
            </h2>
          </div>

          {/* Tên thứ */}
          <div className="grid grid-cols-7 border-b border-slate-200 bg-slate-50">
            {WEEKDAYS.map((name) => (
              <div
                key={name}
                className="py-2 text-center text-xs font-semibold text-slate-500"
              >
                {name}
              </div>
            ))}
          </div>

          {/* Các ô ngày */}
          <div className="grid grid-cols-7">
            {days.map((day) => {
              const key = dateKey(day);
              const inMonth = day.getMonth() === viewMonth.getMonth();
              const isToday = key === todayKey;
              const dayEvents = eventsByDay.get(key) ?? [];

              return (
                <div
                  key={key}
                  className={`min-h-28 border-b border-r border-slate-100 p-1.5 ${
                    inMonth ? "bg-white" : "bg-slate-50/70"
                  }`}
                >
                  <div className="mb-1 flex justify-center">
                    <span
                      className={`flex h-7 w-7 items-center justify-center rounded-full text-xs font-medium ${
                        isToday
                          ? "bg-blue-600 text-white"
                          : inMonth
                            ? "text-slate-700"
                            : "text-slate-400"
                      }`}
                    >
                      {day.getDate()}
                    </span>
                  </div>

                  <div className="space-y-1">
                    {dayEvents.slice(0, 3).map((event) => {
                      const date = getEventDate(event)!;
                      const done = event.status === "Completed";

                      return (
                        <button
                          type="button"
                          key={event.id}
                          onClick={() => setSelectedEvent(event)}
                          className={`block w-full truncate rounded px-1.5 py-0.5 text-left text-[11px] font-medium transition ${
                            getStyle(event.eventType).chip
                          } ${done ? "line-through opacity-60" : ""}`}
                          title={event.title}
                        >
                          {!isDeadlineOnly(event) && (
                            <span className="mr-1">{formatTime(date)}</span>
                          )}
                          {event.title}
                        </button>
                      );
                    })}

                    {dayEvents.length > 3 && (
                      <p className="px-1 text-[11px] font-medium text-slate-500">
                        +{dayEvents.length - 3} nữa
                      </p>
                    )}
                  </div>
                </div>
              );
            })}
          </div>
        </section>

        {/* ===================== SẮP TỚI ===================== */}
        <aside className="h-fit rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
          <h2 className="font-semibold text-slate-800">Sắp tới</h2>
          <p className="mt-1 text-sm text-slate-500">
            Công việc đã xác nhận, chưa hoàn thành
          </p>

          <div className="mt-4 space-y-3">
            {upcoming.length === 0 ? (
              <p className="rounded-xl bg-slate-50 p-4 text-center text-sm text-slate-500">
                Không có công việc sắp tới.
              </p>
            ) : (
              upcoming.map(({ event, date }) => (
                <button
                  type="button"
                  key={event.id}
                  onClick={() => setSelectedEvent(event)}
                  className="flex w-full items-start gap-3 rounded-xl bg-slate-50 p-3 text-left hover:bg-slate-100"
                >
                  <span
                    className={`mt-1.5 h-2.5 w-2.5 shrink-0 rounded-full ${
                      getStyle(event.eventType).dot
                    }`}
                  />
                  <span className="min-w-0">
                    <span className="block truncate text-sm font-medium text-slate-700">
                      {event.title}
                    </span>
                    <span className="mt-1 block text-xs text-slate-500">
                      {date.toLocaleDateString("vi-VN")}
                      {!isDeadlineOnly(event) && ` • ${formatTime(date)}`}
                      {isDeadlineOnly(event) && " • Hạn chót"}
                    </span>
                  </span>
                </button>
              ))
            )}
          </div>

          {/* Chú thích màu */}
          <div className="mt-6 border-t border-slate-100 pt-4">
            <p className="mb-2 text-xs font-semibold uppercase tracking-wider text-slate-400">
              Loại sự kiện
            </p>
            <div className="grid grid-cols-2 gap-2">
              {Object.entries(typeStyles).map(([type, style]) => (
                <div
                  key={type}
                  className="flex items-center gap-2 text-xs text-slate-600"
                >
                  <span className={`h-2.5 w-2.5 rounded-full ${style.dot}`} />
                  {style.label}
                </div>
              ))}
            </div>
          </div>
        </aside>
      </div>

      {/* ===================== CHI TIẾT SỰ KIỆN ===================== */}
      {selectedEvent && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/40 px-4"
          onClick={() => setSelectedEvent(null)}
        >
          <div
            className="w-full max-w-md rounded-2xl bg-white p-6 shadow-2xl"
            onClick={(event) => event.stopPropagation()}
          >
            <div className="flex items-start justify-between gap-3">
              <div className="flex items-start gap-3">
                <span
                  className={`mt-1.5 h-3 w-3 shrink-0 rounded-full ${
                    getStyle(selectedEvent.eventType).dot
                  }`}
                />
                <div>
                  <h3 className="text-lg font-bold text-slate-800">
                    {selectedEvent.title}
                  </h3>
                  <p className="mt-1 text-sm text-slate-500">
                    {getStyle(selectedEvent.eventType).label}
                    {selectedEvent.status === "Completed" && " • Đã hoàn thành"}
                  </p>
                </div>
              </div>

              <button
                type="button"
                onClick={() => setSelectedEvent(null)}
                className="rounded-lg p-1.5 text-slate-400 hover:bg-slate-100"
              >
                <X size={18} />
              </button>
            </div>

            <dl className="mt-5 space-y-3 text-sm">
              {selectedEvent.startAtUtc && (
                <div className="flex items-start gap-2 text-slate-600">
                  <Clock size={16} className="mt-0.5 shrink-0 text-slate-400" />
                  <span>
                    Bắt đầu: {formatFull(parseUtc(selectedEvent.startAtUtc))}
                  </span>
                </div>
              )}

              {selectedEvent.dueAtUtc && (
                <div className="flex items-start gap-2 text-slate-600">
                  <Clock size={16} className="mt-0.5 shrink-0 text-slate-400" />
                  <span>
                    Hạn chót: {formatFull(parseUtc(selectedEvent.dueAtUtc))}
                  </span>
                </div>
              )}

              <p className="whitespace-pre-wrap rounded-xl bg-slate-50 p-3 text-slate-600">
                {selectedEvent.description || "Không có mô tả."}
              </p>
            </dl>

            <div className="mt-6 flex justify-end gap-3">
              <button
                type="button"
                onClick={() => setSelectedEvent(null)}
                className="rounded-xl border border-slate-200 px-4 py-2 text-sm font-medium text-slate-600 hover:bg-slate-50"
              >
                Đóng
              </button>

              {selectedEvent.status === "Confirmed" && (
                <button
                  type="button"
                  onClick={() => void completeEvent(selectedEvent)}
                  disabled={completingId === selectedEvent.id}
                  className="inline-flex items-center gap-2 rounded-xl bg-emerald-600 px-4 py-2 text-sm font-semibold text-white hover:bg-emerald-700 disabled:opacity-60"
                >
                  <CheckCircle2 size={16} />
                  Hoàn thành
                </button>
              )}
            </div>
          </div>
        </div>
      )}
    </main>
  );
}

export default Tasks;
