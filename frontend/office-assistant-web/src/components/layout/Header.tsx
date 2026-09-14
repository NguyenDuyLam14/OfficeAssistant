import { useState } from "react";

import { Bell, Menu, X, LogOut, User } from "lucide-react";

import { useNavigate } from "react-router-dom";

import { getCurrentUser, logout } from "../../services/authService";

type HeaderProps = {
  sidebarOpen: boolean;
  onToggleSidebar: () => void;
};

function Header({ sidebarOpen, onToggleSidebar }: HeaderProps) {
  // ==========================================================
  // STATE MENU TÀI KHOẢN
  // ==========================================================

  const [userMenuOpen, setUserMenuOpen] = useState(false);

  // ==========================================================
  // ĐIỀU HƯỚNG
  // ==========================================================

  const navigate = useNavigate();

  // ==========================================================
  // LẤY THÔNG TIN NGƯỜI DÙNG ĐANG ĐĂNG NHẬP
  // ==========================================================

  const currentUser = getCurrentUser();

  // ==========================================================
  // TẠO CHỮ VIẾT TẮT CHO AVATAR
  // ==========================================================

  const getInitials = (fullName: string) => {
    const words = fullName.trim().split(/\s+/).filter(Boolean);

    if (words.length === 0) {
      return "U";
    }

    if (words.length === 1) {
      return words[0].substring(0, 2).toUpperCase();
    }

    return (words[0][0] + words[words.length - 1][0]).toUpperCase();
  };

  const initials = currentUser ? getInitials(currentUser.fullName) : "U";

  // ==========================================================
  // HIỂN THỊ VAI TRÒ
  // ==========================================================

  const roleName =
    currentUser?.role === "Admin" ? "Quản trị viên" : "Nhân viên văn phòng";

  // ==========================================================
  // ĐĂNG XUẤT
  // ==========================================================

  const handleLogout = () => {
    // Xóa JWT và thông tin user
    logout();

    // Đóng menu
    setUserMenuOpen(false);

    // Chuyển về Login
    navigate("/login", {
      replace: true,
    });
  };

  return (
    <header className="sticky top-0 z-30 flex h-16 items-center justify-between border-b border-slate-200 bg-white px-6 shadow-sm">
      {/* =====================================================
          LEFT
      ===================================================== */}

      <div className="flex items-center gap-4">
        {/* Nút thu gọn / mở rộng Sidebar */}
        <button
          type="button"
          onClick={onToggleSidebar}
          className="rounded-lg p-2 text-slate-600 hover:bg-slate-100"
          title={sidebarOpen ? "Thu gọn menu" : "Mở rộng menu"}
        >
          {sidebarOpen ? <X size={21} /> : <Menu size={21} />}
        </button>

        {/* Tiêu đề */}
        <div>
          <h2 className="font-semibold text-slate-800">Dashboard</h2>

          <p className="text-xs text-slate-500">
            Tổng quan hoạt động văn phòng
          </p>
        </div>
      </div>

      {/* =====================================================
          RIGHT
      ===================================================== */}

      <div className="flex items-center gap-4">
        {/* ===================================================
            THÔNG BÁO
        =================================================== */}

        <button
          type="button"
          className="relative rounded-lg p-2 text-slate-600 hover:bg-slate-100"
          title="Thông báo"
        >
          <Bell size={21} />

          <span className="absolute right-1 top-1 h-2 w-2 rounded-full bg-red-500" />
        </button>

        {/* ===================================================
            KHU VỰC TÀI KHOẢN
        =================================================== */}

        <div className="relative border-l border-slate-200 pl-4">
          {/* Nút mở menu tài khoản */}
          <button
            type="button"
            onClick={() => setUserMenuOpen(!userMenuOpen)}
            className="flex items-center gap-3 rounded-lg px-2 py-1.5 hover:bg-slate-50"
          >
            {/* Avatar */}
            <div className="flex h-9 w-9 items-center justify-center rounded-full bg-blue-100 font-semibold text-blue-700">
              {initials}
            </div>

            {/* Thông tin người dùng */}
            <div className="hidden text-left md:block">
              <p className="text-sm font-semibold">
                {currentUser?.fullName || "Người dùng"}
              </p>

              <p className="text-xs text-slate-500">{roleName}</p>
            </div>
          </button>

          {/* =================================================
              MENU TÀI KHOẢN
          ================================================= */}

          {userMenuOpen && (
            <div className="absolute right-0 top-12 z-50 w-56 overflow-hidden rounded-xl border border-slate-200 bg-white shadow-lg">
              {/* Thông tin tài khoản */}
              <div className="border-b border-slate-100 px-4 py-3">
                <p className="text-sm font-semibold text-slate-800">
                  {currentUser?.fullName || "Người dùng"}
                </p>

                <p className="mt-1 truncate text-xs text-slate-500">
                  {currentUser?.email || ""}
                </p>
              </div>

              {/* Thông tin tài khoản */}
              <button
                type="button"
                onClick={() => {
                  setUserMenuOpen(false);
                  navigate("/settings");
                }}
                className="flex w-full items-center gap-3 px-4 py-3 text-sm text-slate-700 hover:bg-slate-50"
              >
                <User size={17} />

                <span>Thông tin tài khoản</span>
              </button>

              {/* Đăng xuất */}
              <button
                type="button"
                onClick={handleLogout}
                className="flex w-full items-center gap-3 border-t border-slate-100 px-4 py-3 text-sm text-red-600 hover:bg-red-50"
              >
                <LogOut size={17} />

                <span>Đăng xuất</span>
              </button>
            </div>
          )}
        </div>
      </div>
    </header>
  );
}

export default Header;
