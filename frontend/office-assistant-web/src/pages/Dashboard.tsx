import { useEffect, useState } from "react";

import {
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

type DashboardStats = {
  totalDocuments: number;
  totalEmails: number;
  totalTasks: number;
  upcomingTasks: number;
};
function Dashboard() {
  const [stats, setStats] = useState<DashboardStats>({
    totalDocuments: 0,
    totalEmails: 0,
    totalTasks: 0,
    upcomingTasks: 0,
  });

  const [loading, setLoading] = useState(true);

  const [error, setError] = useState("");

  useEffect(() => {
    const loadDashboardStats = async () => {
      try {
        setLoading(true);
        setError("");

        const response = await api.get<DashboardStats>("/dashboard/stats");

        setStats(response.data);
      } catch (error) {
        console.error("Không thể tải thống kê Dashboard:", error);

        setError("Không thể tải dữ liệu thống kê.");
      } finally {
        setLoading(false);
      }
    };

    loadDashboardStats();
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
          <div className="sm:col-span-2 xl:col-span-4 rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
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
          title="Email"
          value={loading ? "..." : stats.totalEmails.toString()}
          description="Đã xử lý"
          icon={<Mail size={22} />}
        />

        <StatCard
          title="Công việc"
          value={loading ? "..." : stats.totalTasks.toString()}
          description="Đang thực hiện"
          icon={<CalendarDays size={22} />}
        />

        <StatCard
          title="Phân tích"
          value="0"
          description="File Excel đã xử lý"
          icon={<BarChart3 size={22} />}
        />
      </section>

      {/* =========================
          MAIN GRID
          ========================= */}
      <section className="mt-6 grid gap-6 xl:grid-cols-3">
        {/* =========================
            QUICK ACTIONS
            ========================= */}
        <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm xl:col-span-2">
          <div className="mb-5 flex items-center justify-between">
            <div>
              <h3 className="font-semibold text-slate-800">Thao tác nhanh</h3>

              <p className="mt-1 text-sm text-slate-500">
                Truy cập nhanh các nghiệp vụ thường dùng
              </p>
            </div>
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            <QuickAction
              icon={<Sparkles size={22} />}
              title="Soạn văn bản bằng AI"
              description="Tạo nội dung văn bản từ yêu cầu bằng ngôn ngữ tự nhiên"
            />

            <QuickAction
              icon={<Mail size={22} />}
              title="Xử lý email"
              description="Tóm tắt email và hỗ trợ tạo nội dung trả lời"
            />

            <QuickAction
              icon={<CalendarDays size={22} />}
              title="Thêm công việc"
              description="Tạo công việc và thiết lập thời gian nhắc hạn"
            />

            <QuickAction
              icon={<Upload size={22} />}
              title="Phân tích Excel"
              description="Tải dữ liệu Excel để thống kê và trực quan hóa"
            />
          </div>
        </div>

        {/* =========================
            UPCOMING TASKS
            ========================= */}
        <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
          <div className="mb-5 flex items-center justify-between">
            <div>
              <h3 className="font-semibold text-slate-800">
                Công việc sắp đến hạn
              </h3>

              <p className="mt-1 text-sm text-slate-500">
                Theo dõi công việc cần xử lý
              </p>
            </div>

            <button type="button" className="text-blue-600 hover:text-blue-700">
              <ChevronRight size={20} />
            </button>
          </div>

          <div className="space-y-3">
            <TaskItem
              title="Hoàn thiện báo cáo tháng"
              deadline="Còn 6 giờ"
              urgent
            />

            <TaskItem title="Gửi thông báo cuộc họp" deadline="Còn 12 giờ" />

            <TaskItem title="Kiểm tra hồ sơ văn bản" deadline="Ngày mai" />

            <TaskItem title="Tổng hợp dữ liệu Excel" deadline="18/09/2026" />
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
            Lịch sử thao tác trên hệ thống
          </p>
        </div>

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
              <ActivityRow
                activity="Tạo văn bản bằng AI"
                type="Văn bản"
                time="10 phút trước"
                status="Hoàn thành"
              />

              <ActivityRow
                activity="Phân tích dữ liệu doanh thu"
                type="Excel"
                time="35 phút trước"
                status="Hoàn thành"
              />

              <ActivityRow
                activity="Tóm tắt email"
                type="Email"
                time="1 giờ trước"
                status="Hoàn thành"
              />

              <ActivityRow
                activity="Tạo công việc mới"
                type="Công việc"
                time="2 giờ trước"
                status="Hoàn thành"
              />
            </tbody>
          </table>
        </div>
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
      <div className="flex items-center gap-3">
        <div
          className={`h-2.5 w-2.5 rounded-full ${
            urgent ? "bg-red-500" : "bg-blue-500"
          }`}
        />

        <div>
          <p className="text-sm font-medium text-slate-700">{title}</p>

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
  return (
    <tr className="border-b border-slate-100 last:border-0">
      <td className="py-4 font-medium text-slate-700">{activity}</td>

      <td className="py-4 text-slate-500">{type}</td>

      <td className="py-4 text-slate-500">{time}</td>

      <td className="py-4">
        <span className="rounded-full bg-green-50 px-3 py-1 text-xs font-medium text-green-700">
          {status}
        </span>
      </td>
    </tr>
  );
}

export default Dashboard;
