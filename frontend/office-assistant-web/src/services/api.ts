
import axios from 'axios'

// =========================================================
// TẠO AXIOS INSTANCE DÙNG CHUNG CHO BACKEND
// =========================================================

const api = axios.create({
  baseURL: 'http://localhost:5221/api',
})

// =========================================================
// REQUEST INTERCEPTOR
// Tự động thêm JWT và xử lý request upload file.
// =========================================================

api.interceptors.request.use(
  (config) => {
    // Lấy JWT token sau khi người dùng đăng nhập.
    const token = localStorage.getItem('token')

    // Nếu có token thì gửi lên Backend để xác thực.
    if (token) {
      config.headers.Authorization = `Bearer ${token}`
    }

    // FormData dùng để upload file.
    // Để trình duyệt/Axios tự thiết lập Content-Type
    // cùng boundary phù hợp cho multipart/form-data.
    if (config.data instanceof FormData) {
      config.headers.delete('Content-Type')
    }

    // Với request JSON thông thường, Axios tự xử lý
    // việc tuần tự hóa dữ liệu và Content-Type.

    return config
  },
  (error) => Promise.reject(error),
)

// =========================================================
// RESPONSE INTERCEPTOR
// =========================================================

api.interceptors.response.use(
  (response) => response,
  (error) => {
    // JWT hết hạn hoặc không hợp lệ.
    if (error.response?.status === 401) {
      localStorage.removeItem('token')
      localStorage.removeItem('currentUser')
    }

    return Promise.reject(error)
  },
)

export default api
