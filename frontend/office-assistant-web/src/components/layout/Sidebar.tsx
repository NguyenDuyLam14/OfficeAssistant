import {
  BarChart3,
  Bell,
  CalendarDays,
  FileText,
  Files,
  Home,
  Mail,
  Settings,
  Sparkles,
  Users,
} from "lucide-react";

import { NavLink } from "react-router-dom";

type SidebarProps = {
  sidebarOpen: boolean;
};

function Sidebar({ sidebarOpen }: SidebarProps) {
  return (
    <aside
      className={`fixed left-0 top-0 z-40 h-screen bg-slate-900 text-white transition-all duration-300 ${
        sidebarOpen ? "w-64" : "w-20"
      }`}
    >
      {/* Logo và tên hệ thống */}
      <div className="flex h-16 items-center border-b border-slate-700 px-4">
        <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-blue-600">
          <Sparkles size={22} />
        </div>

        {sidebarOpen && (
          <div className="ml-3">
            <h1 className="text-lg font-bold">OfficeAssistant</h1>
            <p className="text-xs text-slate-400">AI Office Assistant</p>
          </div>
        )}
      </div>

      {/* Menu */}
      <nav className="px-3 py-5">
        {/* Nhóm Tổng quan */}
        <p
          className={`mb-2 px-3 text-xs font-semibold uppercase tracking-wider text-slate-500 ${
            !sidebarOpen && "hidden"
          }`}
        >
          Tổng quan
        </p>

        <SidebarItem
          to="/"
          icon={<Home size={20} />}
          label="Dashboard"
          collapsed={!sidebarOpen}
        />

        {/* Nhóm Nghiệp vụ */}
        <p
          className={`mb-2 mt-6 px-3 text-xs font-semibold uppercase tracking-wider text-slate-500 ${
            !sidebarOpen && "hidden"
          }`}
        >
          Nghiệp vụ
        </p>

        <SidebarItem
          to="/documents"
          icon={<FileText size={20} />}
          label="Văn bản"
          collapsed={!sidebarOpen}
        />

        <SidebarItem
          to="/templates"
          icon={<Files size={20} />}
          label="Mẫu văn bản"
          collapsed={!sidebarOpen}
        />

        <SidebarItem
          to="/emails"
          icon={<Mail size={20} />}
          label="Email"
          collapsed={!sidebarOpen}
        />

        <SidebarItem
          to="/tasks"
          icon={<CalendarDays size={20} />}
          label="Công việc"
          collapsed={!sidebarOpen}
        />

        <SidebarItem
          to="/excel"
          icon={<BarChart3 size={20} />}
          label="Phân tích Excel"
          collapsed={!sidebarOpen}
        />

        {/* Nhóm Hệ thống */}
        <p
          className={`mb-2 mt-6 px-3 text-xs font-semibold uppercase tracking-wider text-slate-500 ${
            !sidebarOpen && "hidden"
          }`}
        >
          Hệ thống
        </p>

        <SidebarItem
          to="/notifications"
          icon={<Bell size={20} />}
          label="Thông báo"
          collapsed={!sidebarOpen}
        />

        <SidebarItem
          to="/users"
          icon={<Users size={20} />}
          label="Người dùng"
          collapsed={!sidebarOpen}
        />

        <SidebarItem
          to="/settings"
          icon={<Settings size={20} />}
          label="Cài đặt"
          collapsed={!sidebarOpen}
        />
      </nav>
    </aside>
  );
}

type SidebarItemProps = {
  to: string;
  icon: React.ReactNode;
  label: string;
  collapsed?: boolean;
};

function SidebarItem({ to, icon, label, collapsed = false }: SidebarItemProps) {
  return (
    <NavLink
      to={to}
      title={collapsed ? label : undefined}
      className={({ isActive }) =>
        `mb-1 flex w-full items-center rounded-xl px-3 py-2.5 text-sm transition ${
          isActive
            ? "bg-blue-600 text-white"
            : "text-slate-300 hover:bg-slate-800 hover:text-white"
        } ${collapsed ? "justify-center" : "gap-3"}`
      }
    >
      {icon}

      {!collapsed && <span>{label}</span>}
    </NavLink>
  );
}

export default Sidebar;
