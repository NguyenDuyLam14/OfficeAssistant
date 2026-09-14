import api from './api'

// ============================================================
// KIỂU DỮ LIỆU NGƯỜI DÙNG
// ============================================================

export interface CurrentUser {
  userId: number
  fullName: string
  email: string
  role: string
}

// ============================================================
// KIỂU DỮ LIỆU RESPONSE KHI LOGIN
// ============================================================

interface LoginResponse {
  token: string
  userId: number
  fullName: string
  email: string
  role: string
}

// ============================================================
// ĐĂNG NHẬP
// ============================================================
//
// Gửi email + password đến Backend.
//
// Backend:
//     POST /api/auth/login
//
// Nếu đăng nhập thành công:
//     1. Nhận JWT token
//     2. Lưu token vào localStorage
//     3. Lưu thông tin user vào localStorage
// ============================================================

export async function login(
  email: string,
  password: string
): Promise<CurrentUser> {
  const response = await api.post<LoginResponse>(
    '/auth/login',
    {
      email,
      password,
    }
  )

  const data = response.data

  const currentUser: CurrentUser = {
    userId: data.userId,
    fullName: data.fullName,
    email: data.email,
    role: data.role,
  }

  // Lưu JWT
  localStorage.setItem('token', data.token)

  // Lưu thông tin người dùng
  localStorage.setItem(
    'currentUser',
    JSON.stringify(currentUser)
  )

  return currentUser
}

// ============================================================
// ĐĂNG XUẤT
// ============================================================
//
// Xóa JWT và thông tin người dùng khỏi trình duyệt.
// ============================================================

export function logout(): void {
  localStorage.removeItem('token')
  localStorage.removeItem('currentUser')
}

// ============================================================
// LẤY THÔNG TIN USER HIỆN TẠI
// ============================================================

export function getCurrentUser(): CurrentUser | null {
  const user = localStorage.getItem('currentUser')

  if (!user) {
    return null
  }

  try {
    return JSON.parse(user) as CurrentUser
  } catch {
    localStorage.removeItem('currentUser')
    return null
  }
}

// ============================================================
// KIỂM TRA ĐÃ ĐĂNG NHẬP HAY CHƯA
// ============================================================

export function isAuthenticated(): boolean {
  const token = localStorage.getItem('token')

  return Boolean(token)
}