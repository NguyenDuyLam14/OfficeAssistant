import axios from 'axios'

const api = axios.create({
  baseURL: 'http://localhost:5221/api',
})

// =========================================================
// REQUEST INTERCEPTOR
// =========================================================
api.interceptors.request.use(
  (config) => {
    // Lấy JWT token đã lưu sau khi đăng nhập.
    const token = localStorage.getItem('token')

    // Nếu có token thì gửi token lên Backend.
    if (token) {
      config.headers.Authorization = `Bearer ${token}`
    }

    // =====================================================
    // XỬ LÝ UPLOAD FILE
    // =====================================================
    //
    // Khi gửi FormData:
    //
    // Browser/Axios sẽ tự tạo:
    //
    // multipart/form-data; boundary=...
    //
    // Vì vậy không được ép request thành:
    //
    // application/json
    //
    if (config.data instanceof FormData) {
      delete config.headers['Content-Type']
    } else {
      // Những request thông thường như:
      // POST tạo mẫu
      // PUT sửa mẫu
      // POST đăng nhập
      // ...
      // vẫn gửi JSON.
      config.headers['Content-Type'] = 'application/json'
    }

    return config
  },
  (error) => {
    return Promise.reject(error)
  },
)

// =========================================================
// RESPONSE INTERCEPTOR
// =========================================================
api.interceptors.response.use(
  (response) => {
    return response
  },
  (error) => {
    // Nếu JWT hết hạn hoặc không hợp lệ.
    if (error.response?.status === 401) {
      localStorage.removeItem('token')
      localStorage.removeItem('currentUser')
    }

    return Promise.reject(error)
  },
)

export default api