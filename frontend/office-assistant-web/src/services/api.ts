import axios from 'axios'

// ============================================================
// CẤU HÌNH ĐỊA CHỈ BACKEND API
// ============================================================

const api = axios.create({
  baseURL: 'http://localhost:5221/api',
  headers: {
    'Content-Type': 'application/json',
  },
})

// ============================================================
// AXIOS REQUEST INTERCEPTOR
// ============================================================
//
// Mỗi khi Frontend gửi request đến Backend,
// interceptor sẽ kiểm tra JWT trong localStorage.
//
// Nếu có token:
//     Authorization: Bearer <token>
//
// Backend sẽ dùng token này để xác thực người dùng.
// ============================================================

api.interceptors.request.use(
  (config) => {
    const token = localStorage.getItem('token')

    if (token) {
      config.headers.Authorization = `Bearer ${token}`
    }

    return config
  },
  (error) => {
    return Promise.reject(error)
  }
)

// ============================================================
// AXIOS RESPONSE INTERCEPTOR
// ============================================================
//
// Nếu Backend trả về 401 Unauthorized,
// có nghĩa token không hợp lệ hoặc đã hết hạn.
//
// Khi đó xóa token khỏi localStorage.
// ============================================================

api.interceptors.response.use(
  (response) => {
    return response
  },
  (error) => {
    if (error.response?.status === 401) {
      localStorage.removeItem('token')
      localStorage.removeItem('currentUser')
    }

    return Promise.reject(error)
  }
)

export default api