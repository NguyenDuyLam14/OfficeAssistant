import { Navigate, Outlet } from "react-router-dom";

import { isAuthenticated } from "../../services/authService";

// ============================================================
// PROTECTED ROUTE
// ============================================================
//
// Component này bảo vệ các trang yêu cầu đăng nhập.
//
// Nếu có JWT:
//     Cho phép truy cập trang.
//
// Nếu chưa có JWT:
//     Chuyển người dùng về /login.
// ============================================================

function ProtectedRoute() {
  if (!isAuthenticated()) {
    return <Navigate to="/login" replace />;
  }

  return <Outlet />;
}

export default ProtectedRoute;
