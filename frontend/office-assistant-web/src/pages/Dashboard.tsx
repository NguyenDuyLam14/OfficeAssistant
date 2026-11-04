import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";

import {
  AlarmClock,
  BarChart3,
  CalendarDays,
  ChevronRight,
  FileText,
  Mail,
  Sparkles,
  Upload,
} from "lucide-react";

import StatCard from "../components/common/StatCard";
import QuickAction from "../components/common/QuickAction";

import api from "../services/api";

// =========================================================
// KIỂU DỮ LIỆU
// =========================================================

type DashboardStats = {
  totalDocuments: number;
  totalEmails: number;
  totalTasks: number;
  upcomingTasks: number;
  totalAnalyses: number;
};

type CalendarEvent = {
  id: number;
  title: string;
  eventType: string;
  startAtUtc: string | null;
  dueAtUtc: string | null;
  status: string;
};

type DocumentItem = {
  documentId: number;
  title: string;
  createdAt: string;
};

type NotificationItem = {
  id: number;
  title: string;
  message: string;
  isRead: boolean;
  createdAtUtc: string;
};

type UpcomingTask = {
  id: number;
  title: string;
  date: Date;
  deadlineOnly: boolean;
  urgent: boolean;
};

type Activity = {
  key: string;
  activity: string;
  type: string;
  time: Date;
  status: string;
};

// =========================================================
// HÀM HỖ TRỢ
// =========================================================

// Backend trả UTC không có "Z" → thêm vào để JS hiểu đúng.
function parseUtc(value: string): Date {
  const hasTimeZone = /([zZ]|[+-]\d{2}:\d{2})$/.test(value);
  return new Date(hasTimeZone ? value : value + "Z");
}

// "Còn 6 giờ", "Còn 2 ngày"...
function formatRemaining(date: Date, nowMs: number): string {
  const diffMinutes = Math.round((date.getTime() - nowMs) / 60000);

  if (diffMinutes < 1) return "Đến hạn ngay";
  if (diffMinutes < 60) return `Còn ${diffMinutes} phút`;

  const hours = Math.floor(diffMinutes / 60);
  if (hours < 24) return `Còn ${hours} giờ`;

  const days = Math.floor(hours / 24);
  return `Còn ${days} ngày`;
}

// "10 phút trước", "2 giờ trước"...
function formatAgo(date: Date, nowMs: number): string {
  const diffMinutes = Math.round((nowMs - date.getTime()) / 60000);

  if (diffMinutes < 1) return "Vừa xong";
  if (diffMinutes < 60) return `${diffMinutes} phút trước`;

  const hours = Math.floor(diffMinutes / 60);
  if (hours < 24) return `${hours} giờ trước`;

  const days = Math.floor(hours / 24);
  if (days < 7) return `${days} ngày trước`;

  return date.toLocaleDateString("vi-VN");
}

const EMPTY_STATS: DashboardStats = {
  totalDocuments: 0,
  totalEmails: 0,
  totalTasks: 0,
  upcomingTasks: 0,
  totalAnalyses: 0,
};

// =========================================================
// TRANG DASHBOARD
// =========================================================

function Dashboard() {
  const navigate = useNavigate();

  const [stats, setStats] = useState<DashboardStats>(EMPTY_STATS);
  const [upcoming, setUpcoming] = useState<UpcomingTask[]>([]);
  const [activities, setActivities] = useState<Activity[]>([]);

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const [nowMs, setNowMs] = useState(0);

  useEffect(() => {
    async function loadDashboard() {
      setLoading(true);
      setError("");

      const now = new Date();
      setNowMs(now.getTime());
      const weekLater = new Date(now.getTime() + 7 * 24 * 60 * 60 * 1000);

      // Gọi song song; một API lỗi không làm hỏng cả trang.
      const [
        statsResult,
        calendarResult,
        documentsResult,
        notificationsResult,
      ] = await Promise.allSettled([
        api.get<DashboardStats>("/dashboard/stats"),
        api.get<CalendarEvent[]>("/DetectedWorkEvents/calendar", {
          params: {
            from: now.toISOString(),
            to: weekLater.toISOString(),
          },
        }),
        api.get<DocumentItem[]>("/documents"),
        api.get<{ items?: NotificationItem[] }>("/Notifications", {
          params: { page: 1, pageSize: 5 },
        }),
      ]);

      // ---------- Thống kê ----------
      if (statsResult.status === "fulfilled") {
        setStats(statsResult.value.data);
      } else {
        console.error("Không thể tải thống kê Dashboard:", statsResult.reason);
        setError("Không thể tải dữ liệu thống kê.");
      }

      // ---------- Công việc sắp đến hạn ----------
      if (calendarResult.status === "fulfilled") {
        const tasks = calendarResult.value.data
          .filter((event) => event.status === "Confirmed")
          .map((event): UpcomingTask | null => {
            const value = event.startAtUtc ?? event.dueAtUtc;
            if (!value) return null;

            const date = parseUtc(value);
            if (Number.isNaN(date.getTime())) return null;

            return {
              id: event.id,
              title: event.title,
              date,
              deadlineOnly: !event.startAtUtc,
              urgent: date.getTime() - now.getTime() < 24 * 60 * 60 * 1000,
            };
          })
          .filter((task): task is UpcomingTask => task !== null)
          .filter((task) => task.date.getTime() >= now.getTime())
          .sort((a, b) => a.date.getTime() - b.date.getTime())
          .slice(0, 5);

        setUpcoming(tasks);
      } else {
        console.error("Không thể tải công việc:", calendarResult.reason);
      }

      // ---------- Hoạt động gần đây ----------
      const items: Activity[] = [];

      if (documentsResult.status === "fulfilled") {
        for (const document of documentsResult.value.data.slice(0, 5)) {
          items.push({
            key: `doc-${document.documentId}`,
            activity: `Tạo văn bản: ${document.title}`,
            type: "Văn bản",
            time: parseUtc(document.createdAt),
            status: "Hoàn thành",
          });
        }
      }

      if (notificationsResult.status === "fulfilled") {
        for (const item of notificationsResult.value.data.items ?? []) {
          items.push({
            key: `noti-${item.id}`,
            activity: item.title,
            type: "Thông báo",
            time: parseUtc(item.createdAtUtc),
            status: item.isRead ? "Đã đọc" : "Chưa đọc",
          });
        }
      }

      items.sort((a, b) => b.time.getTime() - a.time.getTime());
      setActivities(items.slice(0, 6));

      setLoading(false);
    }

    void loadDashboard();
  }, []);

  return (
    <main className="p-6">
      {/* =========================
          WELCOME
          ========================= */}
      <section className="mb-6 rounded-2xl bg-gradient-to-r from-blue-700 to-indigo-700 p-6 text-white shadow-lg">
        <div className="flex flex-col justify-between gap-5 md:flex-row md:items-center">
          <div>
            <div className="mb-2 flex items-center gap-2">
              <Sparkles size={20} />

              <span className="text-sm font-medium text-blue-100">
                Trợ lý AI văn phòng
              </span>
            </div>

            <h1 className="text-2xl font-bold md:text-3xl">
              Chào mừng bạn đến với OfficeAssistant
            </h1>

            <p className="mt-2 max-w-2xl text-sm text-blue-100">
              Hỗ trợ soạn thảo văn bản, xử lý email, quản lý công việc và phân
              tích dữ liệu bằng AI.
            </p>
          </div>

          <button
            type="button"
            onClick={() => navigate("/documents")}
            className="flex items-center justify-center gap-2 rounded-xl bg-white px-5 py-3 text-sm font-semibold text-blue-700 shadow hover:bg-blue-50"
          >
            <Sparkles size={18} />
            Soạn văn bản bằng AI
          </button>
        </div>
      </section>

      {/* =========================
          STATISTICS
          ========================= */}
      <section className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        {error && (
          <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700 sm:col-span-2 xl:col-span-4">
            {error}
          </div>
        )}

        <StatCard
          title="Văn bản"
          value={loading ? "..." : stats.totalDocuments.toString()}
          description="Tổng số văn bản"
          icon={<FileText size={22} />}
        />

        <StatCard
          title="Công việc"
          value={loading ? "..." : stats.totalTasks.toString()}
          description="Đã xác nhận"
          icon={<CalendarDays size={22} />}
        />

        <StatCard
          title="Sắp đến hạn"
          value={loading ? "..." : stats.upcomingTasks.toString()}
          description="Trong 7 ngày tới"
          icon={<AlarmClock size={22} />}
        />

        <StatCard
          title="Phân tích"
          value={loading ? "..." : stats.totalAnalyses.toString()}
          description="File Excel đã xử lý"
          icon={<BarChart3 size={22} />}
        />
      </section>

      {/* =========================
          MAIN GRID
          ========================= */}
      <section className="mt-6 grid gap-6 xl:grid-cols-3">
        {/* QUICK ACTIONS */}
        <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm xl:col-span-2">
          <div className="mb-5">
            <h3 className="font-semibold text-slate-800">Thao tác nhanh</h3>

            <p className="mt-1 text-sm text-slate-500">
              Truy cập nhanh các nghiệp vụ thường dùng
            </p>
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            <QuickAction
              icon={<Sparkles size={22} />}
              title="Soạn văn bản bằng AI"
              description="Tạo nội dung văn bản từ yêu cầu bằng ngôn ngữ tự nhiên"
              onClick={() => navigate("/documents")}
            />

            <QuickAction
              icon={<Mail size={22} />}
              title="Xử lý email"
              description="Tóm tắt email và hỗ trợ tạo nội dung trả lời"
              onClick={() => navigate("/emails")}
            />

            <QuickAction
              icon={<CalendarDays size={22} />}
              title="Xem lịch công việc"
              description="Theo dõi công việc đã xác nhận trên lịch tháng"
              onClick={() => navigate("/tasks")}
            />

            <QuickAction
              icon={<Upload size={22} />}
              title="Phân tích Excel"
              description="Tải dữ liệu Excel để thống kê và trực quan hóa"
              onClick={() => navigate("/excel")}
            />
          </div>
        </div>

        {/* UPCOMING TASKS */}
        <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
          <div className="mb-5 flex items-center justify-between">
            <div>
              <h3 className="font-semibold text-slate-800">
                Công việc sắp đến hạn
              </h3>

              <p className="mt-1 text-sm text-slate-500">Trong 7 ngày tới</p>
            </div>

            <button
              type="button"
              onClick={() => navigate("/tasks")}
              className="text-blue-600 hover:text-blue-700"
              title="Xem lịch công việc"
            >
              <ChevronRight size={20} />
            </button>
          </div>

          <div className="space-y-3">
            {loading ? (
              <p className="rounded-xl bg-slate-50 p-4 text-center text-sm text-slate-500">
                Đang tải...
              </p>
            ) : upcoming.length === 0 ? (
              <p className="rounded-xl bg-slate-50 p-4 text-center text-sm text-slate-500">
                Không có công việc sắp đến hạn.
              </p>
            ) : (
              upcoming.map((task) => (
                <TaskItem
                  key={task.id}
                  title={task.title}
                  deadline={`${formatRemaining(task.date, nowMs)} • ${task.date.toLocaleString(
                    "vi-VN",
                    task.deadlineOnly
                      ? { dateStyle: "short" }
                      : { dateStyle: "short", timeStyle: "short" },
                  )}`}
                  urgent={task.urgent}
                />
              ))
            )}
          </div>
        </div>
      </section>

      {/* =========================
          RECENT ACTIVITIES
          ========================= */}
      <section className="mt-6 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
        <div className="mb-5">
          <h3 className="font-semibold text-slate-800">Hoạt động gần đây</h3>

          <p className="mt-1 text-sm text-slate-500">
            Văn bản mới tạo và thông báo gần nhất
          </p>
        </div>

        {loading ? (
          <p className="py-8 text-center text-sm text-slate-500">Đang tải...</p>
        ) : activities.length === 0 ? (
          <p className="py-8 text-center text-sm text-slate-500">
            Chưa có hoạt động nào.
          </p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[600px] text-left text-sm">
              <thead>
                <tr className="border-b border-slate-200 text-slate-500">
                  <th className="pb-3 font-medium">Hoạt động</th>
                  <th className="pb-3 font-medium">Loại</th>
                  <th className="pb-3 font-medium">Thời gian</th>
                  <th className="pb-3 font-medium">Trạng thái</th>
                </tr>
              </thead>

              <tbody>
                {activities.map((item) => (
                  <ActivityRow
                    key={item.key}
                    activity={item.activity}
                    type={item.type}
                    time={formatAgo(item.time, nowMs)}
                    status={item.status}
                  />
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </main>
  );
}

/* =========================
   TASK ITEM
   ========================= */

type TaskItemProps = {
  title: string;
  deadline: string;
  urgent?: boolean;
};

function TaskItem({ title, deadline, urgent = false }: TaskItemProps) {
  return (
    <div className="flex items-center justify-between rounded-xl bg-slate-50 p-3">
      <div className="flex min-w-0 items-center gap-3">
        <div
          className={`h-2.5 w-2.5 shrink-0 rounded-full ${
            urgent ? "bg-red-500" : "bg-blue-500"
          }`}
        />

        <div className="min-w-0">
          <p className="truncate text-sm font-medium text-slate-700">{title}</p>

          <p
            className={`mt-1 text-xs ${
              urgent ? "font-medium text-red-600" : "text-slate-500"
            }`}
          >
            {deadline}
          </p>
        </div>
      </div>
    </div>
  );
}

/* =========================
   ACTIVITY ROW
   ========================= */

type ActivityRowProps = {
  activity: string;
  type: string;
  time: string;
  status: string;
};

function ActivityRow({ activity, type, time, status }: ActivityRowProps) {
  const isUnread = status === "Chưa đọc";

  return (
    <tr className="border-b border-slate-100 last:border-0">
      <td className="max-w-xs truncate py-4 font-medium text-slate-700">
        {activity}
      </td>

      <td className="py-4 text-slate-500">{type}</td>

      <td className="py-4 text-slate-500">{time}</td>

      <td className="py-4">
        <span
          className={`rounded-full px-3 py-1 text-xs font-medium ${
            isUnread
              ? "bg-amber-50 text-amber-700"
              : "bg-green-50 text-green-700"
          }`}
        >
          {status}
        </span>
      </td>
    </tr>
  );
}

export default Dashboard;
