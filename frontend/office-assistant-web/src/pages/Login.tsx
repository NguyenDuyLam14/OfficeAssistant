import { useState } from "react";
import type { FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import { LogIn, Mail, Lock } from "lucide-react";

import { login } from "../services/authService";

// ============================================================
// TRANG ĐĂNG NHẬP
// ============================================================

function Login() {
  const navigate = useNavigate();

  // ==========================================================
  // STATE
  // ==========================================================

  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  // ==========================================================
  // XỬ LÝ ĐĂNG NHẬP
  // ==========================================================

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    setError("");

    // Kiểm tra dữ liệu nhập
    if (!email.trim()) {
      setError("Vui lòng nhập email.");
      return;
    }

    if (!password.trim()) {
      setError("Vui lòng nhập mật khẩu.");
      return;
    }

    try {
      setLoading(true);

      // Gọi API đăng nhập
      await login(email.trim(), password);

      // Đăng nhập thành công
      // Chuyển đến Dashboard
      navigate("/", {
        replace: true,
      });
    } catch (error: unknown) {
      // Nếu Backend trả về message
      // thì hiển thị message đó.
      let message = "Đăng nhập thất bại. Vui lòng kiểm tra lại thông tin.";

      if (typeof error === "object" && error !== null && "response" in error) {
        const axiosError = error as {
          response?: {
            data?: {
              message?: string;
            };
          };
        };

        message = axiosError.response?.data?.message || message;
      }

      setError(message);
    } finally {
      setLoading(false);
    }
  };

  // ==========================================================
  // GIAO DIỆN
  // ==========================================================

  return (
    <div className="min-h-screen bg-slate-100 flex items-center justify-center px-4">
      {/* Khung đăng nhập */}
      <div className="w-full max-w-md">
        {/* Logo / tiêu đề */}
        <div className="text-center mb-8">
          <div className="mx-auto mb-4 flex h-16 w-16 items-center justify-center rounded-2xl bg-slate-900 text-white shadow-lg">
            <LogIn size={30} />
          </div>

          <h1 className="text-2xl font-bold text-slate-900">
            Office Assistant
          </h1>

          <p className="mt-2 text-sm text-slate-500">
            Trợ lý thông minh hỗ trợ nghiệp vụ văn phòng bằng AI
          </p>
        </div>

        {/* Card Login */}
        <div className="rounded-2xl bg-white p-8 shadow-xl border border-slate-200">
          <div className="mb-6">
            <h2 className="text-xl font-semibold text-slate-900">Đăng nhập</h2>

            <p className="mt-1 text-sm text-slate-500">
              Nhập thông tin tài khoản để tiếp tục
            </p>
          </div>

          {/* Thông báo lỗi */}
          {error && (
            <div className="mb-5 rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
              {error}
            </div>
          )}

          {/* Form */}
          <form onSubmit={handleSubmit} className="space-y-5">
            {/* Email */}
            <div>
              <label
                htmlFor="email"
                className="mb-2 block text-sm font-medium text-slate-700"
              >
                Email
              </label>

              <div className="relative">
                <Mail
                  size={18}
                  className="absolute left-3 top-1/2 -translate-y-1/2 text-slate-400"
                />

                <input
                  id="email"
                  type="email"
                  value={email}
                  onChange={(event) => setEmail(event.target.value)}
                  placeholder="Nhập email"
                  autoComplete="email"
                  disabled={loading}
                  className="w-full rounded-lg border border-slate-300 bg-white py-3 pl-10 pr-4 text-sm outline-none transition focus:border-slate-500 focus:ring-2 focus:ring-slate-200 disabled:bg-slate-100"
                />
              </div>
            </div>

            {/* Mật khẩu */}
            <div>
              <label
                htmlFor="password"
                className="mb-2 block text-sm font-medium text-slate-700"
              >
                Mật khẩu
              </label>

              <div className="relative">
                <Lock
                  size={18}
                  className="absolute left-3 top-1/2 -translate-y-1/2 text-slate-400"
                />

                <input
                  id="password"
                  type="password"
                  value={password}
                  onChange={(event) => setPassword(event.target.value)}
                  placeholder="Nhập mật khẩu"
                  autoComplete="current-password"
                  disabled={loading}
                  className="w-full rounded-lg border border-slate-300 bg-white py-3 pl-10 pr-4 text-sm outline-none transition focus:border-slate-500 focus:ring-2 focus:ring-slate-200 disabled:bg-slate-100"
                />
              </div>
            </div>

            {/* Nút đăng nhập */}
            <button
              type="submit"
              disabled={loading}
              className="flex w-full items-center justify-center gap-2 rounded-lg bg-slate-900 px-4 py-3 text-sm font-semibold text-white transition hover:bg-slate-800 disabled:cursor-not-allowed disabled:opacity-60"
            >
              <LogIn size={18} />

              {loading ? "Đang đăng nhập..." : "Đăng nhập"}
            </button>
          </form>
        </div>

        {/* Footer */}
        <p className="mt-6 text-center text-xs text-slate-400">
          Office Assistant — Graduation Project
        </p>
      </div>
    </div>
  );
}

export default Login;
