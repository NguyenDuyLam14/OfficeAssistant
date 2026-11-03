import { useCallback, useEffect, useMemo, useState } from "react";
import type { FormEvent } from "react";
import {
  KeyRound,
  LoaderCircle,
  Pencil,
  Plus,
  Search,
  ShieldCheck,
  UserCheck,
  UserX,
  Users as UsersIcon,
  X,
} from "lucide-react";

import api from "../services/api";
import { getCurrentUser } from "../services/authService";
import { getApiMessage } from "../services/emailService";

// =========================================================
// KIỂU DỮ LIỆU
// =========================================================

type UserItem = {
  userId: number;
  fullName: string;
  email: string;
  role: "Admin" | "Staff" | string;
  isActive: boolean;
  createdAt: string;
};

// Modal đang mở: tạo mới, sửa hoặc đặt lại mật khẩu.
type ModalState =
  | { type: "create" }
  | { type: "edit"; user: UserItem }
  | { type: "password"; user: UserItem }
  | null;

const inputClass =
  "w-full rounded-xl border border-slate-300 bg-white px-4 py-3 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100 disabled:bg-slate-100";

const roleLabel = (role: string) =>
  role === "Admin" ? "Quản trị viên" : "Nhân viên";

const formatDate = (value: string) =>
  new Date(value).toLocaleDateString("vi-VN", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
  });

// =========================================================
// TRANG QUẢN LÝ NGƯỜI DÙNG
// =========================================================

function Users() {
  const currentUser = getCurrentUser();
  const isAdmin = currentUser?.role === "Admin";

  const [users, setUsers] = useState<UserItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");

  const [searchText, setSearchText] = useState("");
  const [roleFilter, setRoleFilter] = useState("");

  const [modal, setModal] = useState<ModalState>(null);
  const [saving, setSaving] = useState(false);
  const [modalError, setModalError] = useState("");

  // Form dùng chung cho các modal.
  const [fullName, setFullName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [role, setRole] = useState("Staff");
  const [isActive, setIsActive] = useState(true);

  // -------------------------------------------------------
  // TẢI DANH SÁCH
  // -------------------------------------------------------
  const loadUsers = useCallback(async () => {
    try {
      const response = await api.get<UserItem[]>("/Users");
      setUsers(response.data);
      setError("");
    } catch (err) {
      console.error("Không thể tải danh sách người dùng:", err);
      setError(getApiMessage(err, "Không thể tải danh sách người dùng."));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    async function fetchUsers() {
      if (!isAdmin) {
        setLoading(false);
        return;
      }

      await loadUsers();
    }

    void fetchUsers();
  }, [isAdmin, loadUsers]);

  // -------------------------------------------------------
  // LỌC + THỐNG KÊ
  // -------------------------------------------------------
  const filteredUsers = useMemo(() => {
    const keyword = searchText.trim().toLowerCase();

    return users.filter((user) => {
      const matchesSearch =
        !keyword ||
        user.fullName.toLowerCase().includes(keyword) ||
        user.email.toLowerCase().includes(keyword);

      const matchesRole = !roleFilter || user.role === roleFilter;

      return matchesSearch && matchesRole;
    });
  }, [users, searchText, roleFilter]);

  const stats = useMemo(
    () => ({
      total: users.length,
      active: users.filter((u) => u.isActive).length,
      admins: users.filter((u) => u.role === "Admin").length,
    }),
    [users],
  );

  // -------------------------------------------------------
  // MỞ / ĐÓNG MODAL
  // -------------------------------------------------------
  const openCreate = () => {
    setFullName("");
    setEmail("");
    setPassword("");
    setRole("Staff");
    setModalError("");
    setModal({ type: "create" });
  };

  const openEdit = (user: UserItem) => {
    setFullName(user.fullName);
    setRole(user.role);
    setIsActive(user.isActive);
    setModalError("");
    setModal({ type: "edit", user });
  };

  const openPassword = (user: UserItem) => {
    setPassword("");
    setModalError("");
    setModal({ type: "password", user });
  };

  const closeModal = () => {
    if (saving) return;
    setModal(null);
  };

  // -------------------------------------------------------
  // LƯU (tạo / sửa / đặt lại mật khẩu)
  // -------------------------------------------------------
  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!modal) return;

    setModalError("");

    try {
      if (modal.type === "create") {
        if (!fullName.trim() || !email.trim()) {
          setModalError("Vui lòng nhập họ tên và email.");
          return;
        }

        if (password.length < 8) {
          setModalError("Mật khẩu phải có ít nhất 8 ký tự.");
          return;
        }

        setSaving(true);

        await api.post("/Users", {
          fullName: fullName.trim(),
          email: email.trim(),
          password,
          role,
        });

        setSuccess("Đã tạo người dùng mới.");
      }

      if (modal.type === "edit") {
        if (!fullName.trim()) {
          setModalError("Họ và tên không được để trống.");
          return;
        }

        setSaving(true);

        await api.put(`/Users/${modal.user.userId}`, {
          fullName: fullName.trim(),
          role,
          isActive,
        });

        setSuccess("Đã cập nhật người dùng.");
      }

      if (modal.type === "password") {
        if (password.length < 8) {
          setModalError("Mật khẩu phải có ít nhất 8 ký tự.");
          return;
        }

        setSaving(true);

        await api.post(`/Users/${modal.user.userId}/reset-password`, {
          newPassword: password,
        });

        setSuccess(`Đã đặt lại mật khẩu cho ${modal.user.fullName}.`);
      }

      setModal(null);
      await loadUsers();
    } catch (err) {
      console.error("Lỗi lưu người dùng:", err);
      setModalError(getApiMessage(err, "Không thể lưu thay đổi."));
    } finally {
      setSaving(false);
    }
  };

  // -------------------------------------------------------
  // KHÓA / MỞ KHÓA NHANH
  // -------------------------------------------------------
  const handleToggleActive = async (user: UserItem) => {
    const action = user.isActive ? "khóa" : "mở khóa";

    if (
      !window.confirm(
        `Bạn có chắc muốn ${action} tài khoản "${user.fullName}"?`,
      )
    ) {
      return;
    }

    try {
      setError("");

      await api.put(`/Users/${user.userId}`, {
        fullName: user.fullName,
        role: user.role,
        isActive: !user.isActive,
      });

      setSuccess(`Đã ${action} tài khoản ${user.fullName}.`);
      await loadUsers();
    } catch (err) {
      console.error("Lỗi đổi trạng thái:", err);
      setError(getApiMessage(err, "Không thể thay đổi trạng thái."));
    }
  };

  // =======================================================
  // KHÔNG PHẢI ADMIN
  // =======================================================
  if (!isAdmin) {
    return (
      <main className="p-6">
        <div className="mx-auto max-w-lg rounded-2xl border border-slate-200 bg-white p-10 text-center shadow-sm">
          <div className="mx-auto mb-4 flex h-14 w-14 items-center justify-center rounded-full bg-amber-50 text-amber-600">
            <ShieldCheck size={28} />
          </div>

          <h1 className="text-xl font-bold text-slate-800">
            Không có quyền truy cập
          </h1>

          <p className="mt-2 text-sm text-slate-500">
            Chỉ quản trị viên mới được quản lý người dùng.
          </p>
        </div>
      </main>
    );
  }

  // =======================================================
  // GIAO DIỆN
  // =======================================================
  return (
    <main className="p-6">
      {/* Tiêu đề */}
      <div className="mb-6 flex flex-col gap-4 md:flex-row md:items-center md:justify-between">
        <div className="flex items-center gap-3">
          <div className="rounded-xl bg-blue-100 p-3 text-blue-600">
            <UsersIcon size={26} />
          </div>

          <div>
            <h1 className="text-2xl font-bold text-slate-800">Người dùng</h1>
            <p className="text-sm text-slate-500">
              Quản lý người dùng và quyền truy cập hệ thống.
            </p>
          </div>
        </div>

        <button
          type="button"
          onClick={openCreate}
          className="flex items-center justify-center gap-2 rounded-xl bg-blue-600 px-5 py-3 text-sm font-semibold text-white shadow-sm hover:bg-blue-700"
        >
          <Plus size={19} />
          Thêm người dùng
        </button>
      </div>

      {/* Thông báo */}
      {error && (
        <div className="mb-5 flex items-center justify-between rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
          <span>{error}</span>
          <button type="button" onClick={() => setError("")}>
            <X size={17} />
          </button>
        </div>
      )}

      {success && (
        <div className="mb-5 flex items-center justify-between rounded-xl border border-green-200 bg-green-50 px-4 py-3 text-sm text-green-700">
          <span>{success}</span>
          <button type="button" onClick={() => setSuccess("")}>
            <X size={17} />
          </button>
        </div>
      )}

      {/* Thống kê */}
      <section className="mb-6 grid gap-4 sm:grid-cols-3">
        <StatBox label="Tổng người dùng" value={stats.total} />
        <StatBox label="Đang hoạt động" value={stats.active} />
        <StatBox label="Quản trị viên" value={stats.admins} />
      </section>

      {/* Tìm kiếm + lọc */}
      <section className="mb-6 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
        <div className="grid gap-4 md:grid-cols-[1fr_220px]">
          <div className="relative">
            <Search
              size={18}
              className="absolute left-3 top-1/2 -translate-y-1/2 text-slate-400"
            />

            <input
              type="text"
              value={searchText}
              onChange={(event) => setSearchText(event.target.value)}
              placeholder="Tìm theo họ tên hoặc email..."
              className="w-full rounded-xl border border-slate-300 bg-white py-3 pl-10 pr-4 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100"
            />
          </div>

          <select
            value={roleFilter}
            onChange={(event) => setRoleFilter(event.target.value)}
            className="rounded-xl border border-slate-300 bg-white px-4 py-3 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100"
          >
            <option value="">Tất cả vai trò</option>
            <option value="Admin">Quản trị viên</option>
            <option value="Staff">Nhân viên</option>
          </select>
        </div>
      </section>

      {/* Bảng người dùng */}
      <section className="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
        <div className="border-b border-slate-200 px-5 py-4">
          <h2 className="font-semibold text-slate-800">Danh sách người dùng</h2>
          <p className="mt-1 text-sm text-slate-500">
            {filteredUsers.length} người dùng
          </p>
        </div>

        {loading ? (
          <div className="flex items-center justify-center py-16 text-sm text-slate-500">
            <LoaderCircle
              size={22}
              className="mr-2 animate-spin text-blue-600"
            />
            Đang tải danh sách...
          </div>
        ) : filteredUsers.length === 0 ? (
          <div className="py-16 text-center text-sm text-slate-500">
            Không tìm thấy người dùng phù hợp.
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[800px] text-left text-sm">
              <thead>
                <tr className="border-b border-slate-200 bg-slate-50 text-slate-500">
                  <th className="px-5 py-3 font-medium">Người dùng</th>
                  <th className="px-5 py-3 font-medium">Vai trò</th>
                  <th className="px-5 py-3 font-medium">Trạng thái</th>
                  <th className="px-5 py-3 font-medium">Ngày tạo</th>
                  <th className="px-5 py-3 text-right font-medium">Thao tác</th>
                </tr>
              </thead>

              <tbody>
                {filteredUsers.map((user) => {
                  const isSelf = user.userId === currentUser?.userId;

                  return (
                    <tr
                      key={user.userId}
                      className="border-b border-slate-100 last:border-0 hover:bg-slate-50"
                    >
                      <td className="px-5 py-4">
                        <div className="flex items-center gap-3">
                          <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-blue-100 text-sm font-semibold text-blue-700">
                            {user.fullName.trim().charAt(0).toUpperCase() ||
                              "U"}
                          </div>

                          <div className="min-w-0">
                            <p className="font-medium text-slate-700">
                              {user.fullName}
                              {isSelf && (
                                <span className="ml-2 text-xs font-normal text-slate-400">
                                  (bạn)
                                </span>
                              )}
                            </p>
                            <p className="truncate text-xs text-slate-500">
                              {user.email}
                            </p>
                          </div>
                        </div>
                      </td>

                      <td className="px-5 py-4">
                        <span
                          className={`rounded-full px-3 py-1 text-xs font-medium ${
                            user.role === "Admin"
                              ? "bg-purple-50 text-purple-700"
                              : "bg-blue-50 text-blue-700"
                          }`}
                        >
                          {roleLabel(user.role)}
                        </span>
                      </td>

                      <td className="px-5 py-4">
                        <span
                          className={`rounded-full px-3 py-1 text-xs font-medium ${
                            user.isActive
                              ? "bg-green-100 text-green-700"
                              : "bg-slate-100 text-slate-500"
                          }`}
                        >
                          {user.isActive ? "Hoạt động" : "Đã khóa"}
                        </span>
                      </td>

                      <td className="px-5 py-4 text-slate-500">
                        {formatDate(user.createdAt)}
                      </td>

                      <td className="px-5 py-4">
                        <div className="flex justify-end gap-1">
                          <button
                            type="button"
                            onClick={() => openEdit(user)}
                            className="rounded-lg p-2 text-slate-500 hover:bg-blue-50 hover:text-blue-600"
                            title="Chỉnh sửa"
                          >
                            <Pencil size={17} />
                          </button>

                          <button
                            type="button"
                            onClick={() => openPassword(user)}
                            className="rounded-lg p-2 text-slate-500 hover:bg-amber-50 hover:text-amber-600"
                            title="Đặt lại mật khẩu"
                          >
                            <KeyRound size={17} />
                          </button>

                          <button
                            type="button"
                            onClick={() => void handleToggleActive(user)}
                            disabled={isSelf}
                            className="rounded-lg p-2 text-slate-500 hover:bg-red-50 hover:text-red-600 disabled:cursor-not-allowed disabled:opacity-30"
                            title={
                              isSelf
                                ? "Không thể khóa chính mình"
                                : user.isActive
                                  ? "Khóa tài khoản"
                                  : "Mở khóa tài khoản"
                            }
                          >
                            {user.isActive ? (
                              <UserX size={17} />
                            ) : (
                              <UserCheck size={17} />
                            )}
                          </button>
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </section>

      {/* ================= MODAL ================= */}
      {modal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/40 px-4">
          <div className="max-h-[90vh] w-full max-w-lg overflow-y-auto rounded-2xl bg-white shadow-2xl">
            <div className="flex items-center justify-between border-b border-slate-200 px-6 py-4">
              <div>
                <h2 className="text-lg font-semibold text-slate-800">
                  {modal.type === "create" && "Thêm người dùng"}
                  {modal.type === "edit" && "Chỉnh sửa người dùng"}
                  {modal.type === "password" && "Đặt lại mật khẩu"}
                </h2>

                {modal.type !== "create" && (
                  <p className="mt-1 text-sm text-slate-500">
                    {modal.user.email}
                  </p>
                )}
              </div>

              <button
                type="button"
                onClick={closeModal}
                disabled={saving}
                className="rounded-lg p-2 text-slate-500 hover:bg-slate-100 disabled:opacity-50"
              >
                <X size={20} />
              </button>
            </div>

            <form onSubmit={handleSubmit} className="space-y-5 p-6">
              {modalError && (
                <div
                  role="alert"
                  className="rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700"
                >
                  {modalError}
                </div>
              )}

              {modal.type !== "password" && (
                <div>
                  <label className="mb-2 block text-sm font-medium text-slate-700">
                    Họ và tên
                  </label>
                  <input
                    type="text"
                    value={fullName}
                    onChange={(event) => setFullName(event.target.value)}
                    disabled={saving}
                    maxLength={150}
                    className={inputClass}
                  />
                </div>
              )}

              {modal.type === "create" && (
                <div>
                  <label className="mb-2 block text-sm font-medium text-slate-700">
                    Email đăng nhập
                  </label>
                  <input
                    type="email"
                    value={email}
                    onChange={(event) => setEmail(event.target.value)}
                    disabled={saving}
                    maxLength={255}
                    className={inputClass}
                  />
                </div>
              )}

              {modal.type !== "edit" && (
                <div>
                  <label className="mb-2 block text-sm font-medium text-slate-700">
                    {modal.type === "create"
                      ? "Mật khẩu ban đầu"
                      : "Mật khẩu mới"}
                  </label>
                  <input
                    type="password"
                    value={password}
                    onChange={(event) => setPassword(event.target.value)}
                    disabled={saving}
                    autoComplete="new-password"
                    placeholder="Ít nhất 8 ký tự"
                    className={inputClass}
                  />
                </div>
              )}

              {modal.type !== "password" && (
                <div>
                  <label className="mb-2 block text-sm font-medium text-slate-700">
                    Vai trò
                  </label>
                  <select
                    value={role}
                    onChange={(event) => setRole(event.target.value)}
                    disabled={saving}
                    className={inputClass}
                  >
                    <option value="Staff">Nhân viên</option>
                    <option value="Admin">Quản trị viên</option>
                  </select>
                </div>
              )}

              {modal.type === "edit" && (
                <label className="flex cursor-pointer items-center gap-3">
                  <input
                    type="checkbox"
                    checked={isActive}
                    onChange={(event) => setIsActive(event.target.checked)}
                    disabled={saving}
                    className="h-4 w-4 rounded border-slate-300"
                  />
                  <span className="text-sm font-medium text-slate-700">
                    Cho phép tài khoản đăng nhập
                  </span>
                </label>
              )}

              <div className="flex justify-end gap-3 border-t border-slate-200 pt-5">
                <button
                  type="button"
                  onClick={closeModal}
                  disabled={saving}
                  className="rounded-xl border border-slate-300 px-5 py-2.5 text-sm font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-50"
                >
                  Hủy
                </button>

                <button
                  type="submit"
                  disabled={saving}
                  className="inline-flex items-center gap-2 rounded-xl bg-blue-600 px-5 py-2.5 text-sm font-semibold text-white hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-60"
                >
                  {saving && (
                    <LoaderCircle size={16} className="animate-spin" />
                  )}
                  {saving ? "Đang lưu..." : "Lưu"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </main>
  );
}

// =========================================================
// COMPONENT PHỤ
// =========================================================

function StatBox({ label, value }: { label: string; value: number }) {
  return (
    <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
      <p className="text-sm text-slate-500">{label}</p>
      <p className="mt-2 text-3xl font-bold text-slate-800">{value}</p>
    </div>
  );
}

export default Users;
