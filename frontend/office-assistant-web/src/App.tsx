import { useState } from "react";
import { BrowserRouter, Routes, Route, Outlet } from "react-router-dom";

import Sidebar from "./components/layout/Sidebar";
import Header from "./components/layout/Header";
import ProtectedRoute from "./components/auth/ProtectedRoute";

import Login from "./pages/Login";

import Dashboard from "./pages/Dashboard";
import Documents from "./pages/Documents";
import Templates from "./pages/Templates";
import Emails from "./pages/Emails";
import Tasks from "./pages/Tasks";
import ExcelAnalysis from "./pages/ExcelAnalysis";
import Notifications from "./pages/Notifications";
import Users from "./pages/Users";
import Settings from "./pages/Settings";

// ============================================================
// LAYOUT CỦA ỨNG DỤNG
// ============================================================
//
// Sidebar + Header chỉ xuất hiện sau khi người dùng đăng nhập.
// ============================================================

function MainLayout() {
  const [sidebarOpen, setSidebarOpen] = useState(true);

  return (
    <div className="min-h-screen bg-slate-100 text-slate-800">
      {/* Sidebar */}
      <Sidebar sidebarOpen={sidebarOpen} />

      {/* Nội dung chính */}
      <div
        className={`transition-all duration-300 ${
          sidebarOpen ? "ml-64" : "ml-20"
        }`}
      >
        {/* Header */}
        <Header
          sidebarOpen={sidebarOpen}
          onToggleSidebar={() => setSidebarOpen(!sidebarOpen)}
        />

        {/* Nội dung từng trang */}
        <Outlet />
      </div>
    </div>
  );
}

// ============================================================
// APP
// ============================================================

function App() {
  return (
    <BrowserRouter>
      <Routes>
        {/* ==================================================
            TRANG LOGIN
            Không cần JWT
        ================================================== */}

        <Route path="/login" element={<Login />} />

        {/* ==================================================
            CÁC TRANG YÊU CẦU ĐĂNG NHẬP
        ================================================== */}

        <Route element={<ProtectedRoute />}>
          <Route element={<MainLayout />}>
            <Route path="/" element={<Dashboard />} />

            <Route path="/documents" element={<Documents />} />

            <Route path="/templates" element={<Templates />} />

            <Route path="/emails" element={<Emails />} />

            <Route path="/tasks" element={<Tasks />} />

            <Route path="/excel" element={<ExcelAnalysis />} />

            <Route path="/notifications" element={<Notifications />} />

            <Route path="/users" element={<Users />} />

            <Route path="/settings" element={<Settings />} />
          </Route>
        </Route>
      </Routes>
    </BrowserRouter>
  );
}

export default App;
