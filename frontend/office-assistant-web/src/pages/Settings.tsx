import { useCallback, useEffect, useState } from "react";
import type { FormEvent } from "react";
import {
  Check,
  KeyRound,
  LoaderCircle,
  Mail,
  RefreshCw,
  Settings as SettingsIcon,
  Unplug,
  UserCircle,
} from "lucide-react";

import api from "../services/api";
import { getCurrentUser } from "../services/authService";
import { getApiMessage } from "../services/emailService";

type GmailStatus = {
  connected: boolean;
  gmailAddress?: string | null;
};

const inputClass =
  "w-full rounded-xl border border-slate-300 bg-white px-4 py-3 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100 disabled:bg-slate-100";

function Settings() {
  const currentUser = getCurrentUser();

  // ---------------- Đổi mật khẩu ----------------
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [passwordSaving, setPasswordSaving] = useState(false);
  const [passwordError, setPasswordError] = useState("");
  const [passwordSuccess, setPasswordSuccess] = useState("");

  // ---------------- Gmail ----------------
  const [gmail, setGmail] = useState<GmailStatus | null>(null);
  const [gmailLoading, setGmailLoading] = useState(true);
  const [gmailBusy, setGmailBusy] = useState(false);
  const [gmailError, setGmailError] = useState("");

  const loadGmailStatus = useCallback(async () => {
    try {
      const response = await api.get<GmailStatus>("/Emails/status");
      setGmail(response.data);
      setGmailError("");
    } catch (err) {
      console.error("Không thể kiểm tra Gmail:", err);
      setGmail(null);
      setGmailError(
        getApiMessage(err, "Không thể kiểm tra trạng thái kết nối Gmail."),
      );
    } finally {
      setGmailLoading(false);
    }
  }, []);

  useEffect(() => {
    async function fetchStatus() {
      await loadGmailStatus();
    }

    void fetchStatus();
  }, [loadGmailStatus]);

  // -------------------------------------------------------
  // ĐỔI MẬT KHẨU
  // -------------------------------------------------------
  const handleChangePassword = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    setPasswordError("");
    setPasswordSuccess("");

    if (!currentPassword) {
      setPasswordError("Vui lòng nhập mật khẩu hiện tại.");
      return;
    }

    if (newPassword.length < 8) {
      setPasswordError("Mật khẩu mới phải có ít nhất 8 ký tự.");
      return;
    }

    if (newPassword !== confirmPassword) {
      setPasswordError("Mật khẩu xác nhận không khớp.");
      return;
    }

    try {
      setPasswordSaving(true);

      await api.post("/auth/change-password", {
        currentPassword,
        newPassword,
      });

      setPasswordSuccess("Đổi mật khẩu thành công.");
      setCurrentPassword("");
      setNewPassword("");
      setConfirmPassword("");
    } catch (err) {
      console.error("Lỗi đổi mật khẩu:", err);
      setPasswordError(getApiMessage(err, "Không thể đổi mật khẩu."));
    } finally {
      setPasswordSaving(false);
    }
  };

  // -------------------------------------------------------
  // GMAIL: KẾT NỐI / ĐỔI TÀI KHOẢN / NGẮT KẾT NỐI
  // -------------------------------------------------------
  const handleConnectGmail = async () => {
    try {
      setGmailBusy(true);
      setGmailError("");

      const response = await api.get<{ authorizationUrl: string }>(
        "/Emails/connect",
      );

      window.location.assign(response.data.authorizationUrl);
    } catch (err) {
      setGmailError(getApiMessage(err, "Không thể bắt đầu kết nối Gmail."));
      setGmailBusy(false);
    }
  };

  const handleDisconnectGmail = async () => {
    const confirmed = window.confirm(
      "Ngắt kết nối Gmail?\n\nHệ thống sẽ ngừng đọc email và quét công việc từ tài khoản này.",
    );

    if (!confirmed) return;

    try {
      setGmailBusy(true);
      setGmailError("");

      await api.delete("/Emails/disconnect");

      await loadGmailStatus();
    } catch (err) {
      setGmailError(getApiMessage(err, "Không thể ngắt kết nối Gmail."));
    } finally {
      setGmailBusy(false);
    }
  };

  // =======================================================
  // GIAO DIỆN
  // =======================================================
  return (
    <main className="p-6">
      {/* Tiêu đề */}
      <div className="mb-6 flex items-center gap-3">
        <div className="rounded-xl bg-blue-100 p-3 text-blue-600">
          <SettingsIcon size={26} />
        </div>

        <div>
          <h1 className="text-2xl font-bold text-slate-800">Cài đặt</h1>
          <p className="text-sm text-slate-500">
            Cấu hình tài khoản và hệ thống trợ lý văn phòng.
          </p>
        </div>
      </div>

      <div className="grid gap-6 xl:grid-cols-2">
        {/* ================= THÔNG TIN TÀI KHOẢN ================= */}
        <section className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
          <div className="mb-5 flex items-center gap-2">
            <UserCircle size={20} className="text-blue-600" />
            <h2 className="font-semibold text-slate-800">
              Thông tin tài khoản
            </h2>
          </div>

          <div className="flex items-center gap-4">
            <div className="flex h-16 w-16 shrink-0 items-center justify-center rounded-full bg-blue-100 text-2xl font-bold text-blue-700">
              {currentUser?.fullName.trim().charAt(0).toUpperCase() || "U"}
            </div>

            <div className="min-w-0">
              <p className="text-lg font-semibold text-slate-800">
                {currentUser?.fullName || "Người dùng"}
              </p>
              <p className="truncate text-sm text-slate-500">
                {currentUser?.email}
              </p>
            </div>
          </div>

          <dl className="mt-5 space-y-3 border-t border-slate-100 pt-5 text-sm">
            <div className="flex justify-between">
              <dt className="text-slate-500">Vai trò</dt>
              <dd className="font-medium text-slate-700">
                {currentUser?.role === "Admin"
                  ? "Quản trị viên"
                  : "Nhân viên văn phòng"}
              </dd>
            </div>

            <div className="flex justify-between">
              <dt className="text-slate-500">Mã người dùng</dt>
              <dd className="font-medium text-slate-700">
                #{currentUser?.userId}
              </dd>
            </div>
          </dl>
        </section>

        {/* ================= ĐỔI MẬT KHẨU ================= */}
        <section className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
          <div className="mb-5 flex items-center gap-2">
            <KeyRound size={20} className="text-blue-600" />
            <h2 className="font-semibold text-slate-800">Đổi mật khẩu</h2>
          </div>

          <form onSubmit={handleChangePassword} className="space-y-4">
            {passwordError && (
              <div
                role="alert"
                className="rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700"
              >
                {passwordError}
              </div>
            )}

            {passwordSuccess && (
              <div className="flex items-center gap-2 rounded-xl border border-green-200 bg-green-50 px-4 py-3 text-sm text-green-700">
                <Check size={16} />
                {passwordSuccess}
              </div>
            )}

            <div>
              <label className="mb-2 block text-sm font-medium text-slate-700">
                Mật khẩu hiện tại
              </label>
              <input
                type="password"
                value={currentPassword}
                onChange={(event) => setCurrentPassword(event.target.value)}
                disabled={passwordSaving}
                autoComplete="current-password"
                className={inputClass}
              />
            </div>

            <div>
              <label className="mb-2 block text-sm font-medium text-slate-700">
                Mật khẩu mới
              </label>
              <input
                type="password"
                value={newPassword}
                onChange={(event) => setNewPassword(event.target.value)}
                disabled={passwordSaving}
                autoComplete="new-password"
                placeholder="Ít nhất 8 ký tự"
                className={inputClass}
              />
            </div>

            <div>
              <label className="mb-2 block text-sm font-medium text-slate-700">
                Nhập lại mật khẩu mới
              </label>
              <input
                type="password"
                value={confirmPassword}
                onChange={(event) => setConfirmPassword(event.target.value)}
                disabled={passwordSaving}
                autoComplete="new-password"
                className={inputClass}
              />
            </div>

            <div className="flex justify-end">
              <button
                type="submit"
                disabled={passwordSaving}
                className="inline-flex items-center gap-2 rounded-xl bg-blue-600 px-5 py-2.5 text-sm font-semibold text-white hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-60"
              >
                {passwordSaving && (
                  <LoaderCircle size={16} className="animate-spin" />
                )}
                {passwordSaving ? "Đang lưu..." : "Đổi mật khẩu"}
              </button>
            </div>
          </form>
        </section>

        {/* ================= KẾT NỐI GMAIL ================= */}
        <section className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm xl:col-span-2">
          <div className="mb-5 flex items-center gap-2">
            <Mail size={20} className="text-blue-600" />
            <h2 className="font-semibold text-slate-800">Kết nối Gmail</h2>
          </div>

          {gmailError && (
            <div
              role="alert"
              className="mb-4 rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700"
            >
              {gmailError}
            </div>
          )}

          <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
            <div>
              {gmailLoading ? (
                <p className="text-sm text-slate-500">
                  Đang kiểm tra kết nối...
                </p>
              ) : gmail?.connected ? (
                <div className="flex flex-wrap items-center gap-2 text-sm">
                  <span className="inline-flex items-center gap-1.5 rounded-full bg-emerald-50 px-2.5 py-1 font-medium text-emerald-700">
                    <Check size={14} />
                    Đã kết nối
                  </span>
                  <span className="break-all text-slate-600">
                    {gmail.gmailAddress}
                  </span>
                </div>
              ) : (
                <p className="text-sm text-slate-500">
                  Chưa kết nối. Kết nối Gmail để đọc email, tóm tắt, gửi thư và
                  tự động phát hiện công việc.
                </p>
              )}
            </div>

            {!gmailLoading && (
              <div className="flex flex-wrap gap-2">
                <button
                  type="button"
                  onClick={() => void handleConnectGmail()}
                  disabled={gmailBusy}
                  className="inline-flex items-center gap-2 rounded-xl bg-blue-600 px-4 py-2.5 text-sm font-semibold text-white hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-60"
                >
                  {gmailBusy ? (
                    <LoaderCircle size={16} className="animate-spin" />
                  ) : gmail?.connected ? (
                    <RefreshCw size={16} />
                  ) : (
                    <Mail size={16} />
                  )}
                  {gmail?.connected ? "Đổi tài khoản Gmail" : "Kết nối Gmail"}
                </button>

                {gmail?.connected && (
                  <button
                    type="button"
                    onClick={() => void handleDisconnectGmail()}
                    disabled={gmailBusy}
                    className="inline-flex items-center gap-2 rounded-xl border border-red-200 px-4 py-2.5 text-sm font-medium text-red-600 hover:bg-red-50 disabled:cursor-not-allowed disabled:opacity-60"
                  >
                    <Unplug size={16} />
                    Ngắt kết nối
                  </button>
                )}
              </div>
            )}
          </div>
        </section>
      </div>
    </main>
  );
}

export default Settings;
