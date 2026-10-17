import { useCallback, useEffect, useRef, useState } from "react";
import type { FormEvent } from "react";
import {
  ArrowLeft,
  Check,
  ChevronRight,
  Inbox,
  LoaderCircle,
  Mail,
  MailOpen,
  RefreshCw,
  Search,
  Send,
  Unplug,
} from "lucide-react";
import { AxiosError } from "axios";

import api from "../services/api";

// ============================================================
// KIỂU DỮ LIỆU TRẢ VỀ TỪ BACKEND
// ============================================================

interface GmailStatus {
  connected: boolean;
  gmailAddress?: string | null;
}

interface GmailMessage {
  id: string;
  threadId?: string | null;
  snippet?: string | null;
  from?: string | null;
  subject?: string | null;
  date?: string | null;
  internalDate?: string | null;
  labelIds?: string[];
}

interface GmailMessageDetail extends GmailMessage {
  to?: string | null;
  cc?: string | null;
  plainTextBody?: string | null;
  htmlBody?: string | null;
}

interface GmailMessagesResponse {
  messages: GmailMessage[];
  resultCount: number;
  nextPageToken?: string | null;
}

interface ApiError {
  message?: string;
}

// ============================================================
// HÀM HỖ TRỢ
// ============================================================

function getErrorMessage(error: unknown, fallback: string): string {
  const axiosError = error as AxiosError<ApiError>;

  return axiosError.response?.data?.message ?? fallback;
}

function getSenderName(from?: string | null): string {
  if (!from) return "Không rõ người gửi";

  // Ví dụ: Nguyen Van A <example@gmail.com>
  const match = from.match(/^(.+?)\s*<[^>]+>$/);

  return match ? match[1].replace(/^["']|["']$/g, "") : from;
}

function getInitials(value?: string | null): string {
  const name = getSenderName(value);

  return name
    .split(/\s+/)
    .filter(Boolean)
    .slice(-2)
    .map((part) => part[0].toUpperCase())
    .join("");
}

function formatEmailDate(
  date?: string | null,
  internalDate?: string | null,
): string {
  const value = internalDate
    ? new Date(Number(internalDate))
    : date
      ? new Date(date)
      : null;

  if (!value || Number.isNaN(value.getTime())) {
    return "";
  }

  const now = new Date();
  const sameDay = value.toDateString() === now.toDateString();

  if (sameDay) {
    return value.toLocaleTimeString("vi-VN", {
      hour: "2-digit",
      minute: "2-digit",
    });
  }

  const sameYear = value.getFullYear() === now.getFullYear();

  return value.toLocaleDateString(
    "vi-VN",
    sameYear
      ? { day: "2-digit", month: "2-digit" }
      : { day: "2-digit", month: "2-digit", year: "numeric" },
  );
}

// ============================================================
// COMPONENT EMAILS
// ============================================================

function Emails() {
  const [gmailStatus, setGmailStatus] = useState<GmailStatus | null>(null);

  const [messages, setMessages] = useState<GmailMessage[]>([]);

  // Token dùng để yêu cầu trang email tiếp theo từ Gmail API.
  const [nextPageToken, setNextPageToken] = useState<string | null>(null);

  // Trạng thái tải thêm email khi người dùng cuộn xuống.
  const [loadingMore, setLoadingMore] = useState(false);

  // Ref khóa để ngăn gửi nhiều yêu cầu tải tiếp cùng lúc.
  const loadingMoreRef = useRef(false);

  const [selectedMessage, setSelectedMessage] =
    useState<GmailMessageDetail | null>(null);

  const [searchInput, setSearchInput] = useState("");
  const [searchQuery, setSearchQuery] = useState("");

  const [loadingStatus, setLoadingStatus] = useState(true);
  const [loadingMessages, setLoadingMessages] = useState(false);
  const [loadingDetail, setLoadingDetail] = useState(false);
  const [connecting, setConnecting] = useState(false);

  const [error, setError] = useState("");
  const [detailError, setDetailError] = useState("");

  // ----------------------------------------------------------
  // KIỂM TRA TRẠNG THÁI KẾT NỐI GMAIL
  // ----------------------------------------------------------

  const loadGmailStatus = useCallback(async () => {
    setLoadingStatus(true);
    setError("");

    try {
      const response = await api.get<GmailStatus>("/Emails/status");

      setGmailStatus(response.data);
    } catch (err) {
      setGmailStatus(null);
      setError(
        getErrorMessage(err, "Không thể kiểm tra trạng thái kết nối Gmail."),
      );
    } finally {
      setLoadingStatus(false);
    }
  }, []);

  // ----------------------------------------------------------
  // TẢI DANH SÁCH EMAIL
  // ----------------------------------------------------------

  const loadMessages = useCallback(async () => {
    if (!gmailStatus?.connected) return;

    setLoadingMessages(true);
    setError("");

    try {
      const response = await api.get<GmailMessagesResponse>(
        "/Emails/messages",
        {
          params: {
            maxResults: 20,
            ...(searchQuery.trim() ? { q: searchQuery.trim() } : {}),
          },
        },
      );

      setMessages(response.data.messages ?? []);

      // Lưu token để lần sau lấy trang tiếp theo.
      setNextPageToken(response.data.nextPageToken ?? null);

      // Nếu email đang mở không còn trong danh sách kết quả,
      // đóng phần chi tiết để tránh hiển thị nhầm kết quả tìm kiếm.
      setSelectedMessage((current) => {
        if (
          current &&
          !(response.data.messages ?? []).some(
            (message) => message.id === current.id,
          )
        ) {
          return null;
        }

        return current;
      });
    } catch (err) {
      setMessages([]);
      setNextPageToken(null);
      setError(getErrorMessage(err, "Không thể tải danh sách email."));
    } finally {
      setLoadingMessages(false);
    }
  }, [gmailStatus?.connected, searchQuery]);

  // ============================================================
  // TẢI THÊM EMAIL KHI CUỘN XUỐNG CUỐI DANH SÁCH
  // ============================================================

  const loadMoreMessages = useCallback(async () => {
    // Chỉ tải tiếp khi Gmail đã kết nối và còn trang tiếp theo.
    if (
      !gmailStatus?.connected ||
      !nextPageToken ||
      loadingMessages ||
      loadingMoreRef.current
    ) {
      return;
    }

    // Khóa ngay để tránh nhiều sự kiện cuộn gọi API cùng lúc.
    loadingMoreRef.current = true;
    setLoadingMore(true);

    try {
      const response = await api.get<GmailMessagesResponse>(
        "/Emails/messages",
        {
          params: {
            maxResults: 20,
            pageToken: nextPageToken,
            ...(searchQuery.trim() ? { q: searchQuery.trim() } : {}),
          },
        },
      );

      const newMessages = response.data.messages ?? [];

      // Nối email mới vào danh sách cũ, tránh thêm ID trùng.
      setMessages((currentMessages) => {
        const existingIds = new Set(
          currentMessages.map((message) => message.id),
        );

        const uniqueNewMessages = newMessages.filter(
          (message) => !existingIds.has(message.id),
        );

        return [...currentMessages, ...uniqueNewMessages];
      });

      // Lưu token của trang kế tiếp.
      setNextPageToken(response.data.nextPageToken ?? null);
      setError("");
    } catch (err) {
      setError(
        getErrorMessage(err, "Không thể tải thêm email. Vui lòng thử lại."),
      );
    } finally {
      loadingMoreRef.current = false;
      setLoadingMore(false);
    }
  }, [gmailStatus?.connected, nextPageToken, loadingMessages, searchQuery]);

  // ----------------------------------------------------------
  // KHỞI TẠO TRANG VÀ TẢI EMAIL
  // ----------------------------------------------------------

  useEffect(() => {
    let cancelled = false;

    async function initializePage() {
      setLoadingStatus(true);
      setError("");

      try {
        // Nếu Google vừa chuyển hướng về sau khi kết nối Gmail,
        // xóa tham số khỏi URL để không xử lý lại khi tải trang.
        const params = new URLSearchParams(window.location.search);

        if (params.get("gmailConnected") === "true") {
          window.history.replaceState(
            {},
            document.title,
            window.location.pathname,
          );
        }

        // Bước 1: kiểm tra trạng thái kết nối Gmail.
        const statusResponse = await api.get<GmailStatus>("/Emails/status");

        if (cancelled) return;

        const status = statusResponse.data;
        setGmailStatus(status);

        // Bước 2: nếu đã kết nối, tải danh sách email.
        if (status.connected) {
          setLoadingMessages(true);

          try {
            const response = await api.get<GmailMessagesResponse>(
              "/Emails/messages",
              {
                params: {
                  maxResults: 20,
                  ...(searchQuery.trim() ? { q: searchQuery.trim() } : {}),
                },
              },
            );

            if (cancelled) return;

            setMessages(response.data.messages ?? []);
            setNextPageToken(response.data.nextPageToken ?? null);
          } catch (err) {
            if (cancelled) return;

            setMessages([]);
            setNextPageToken(null);
            setError(getErrorMessage(err, "Không thể tải danh sách email."));
          } finally {
            if (!cancelled) {
              setLoadingMessages(false);
            }
          }
        } else {
          setMessages([]);
          setNextPageToken(null);
        }
      } catch (err) {
        if (cancelled) return;

        setGmailStatus(null);
        setError(
          getErrorMessage(err, "Không thể kiểm tra trạng thái kết nối Gmail."),
        );
      } finally {
        if (!cancelled) {
          setLoadingStatus(false);
        }
      }
    }

    void initializePage();

    return () => {
      cancelled = true;
    };
  }, [searchQuery]);

  // ----------------------------------------------------------
  // KẾT NỐI GMAIL
  // ----------------------------------------------------------

  const handleConnectGmail = async () => {
    setConnecting(true);
    setError("");

    try {
      const response = await api.get<{ authorizationUrl: string }>(
        "/Emails/connect",
      );

      if (!response.data.authorizationUrl) {
        throw new Error("Backend không trả về đường dẫn kết nối Gmail.");
      }

      // Chuyển trình duyệt đến trang xác thực chính thức của Google.
      window.location.assign(response.data.authorizationUrl);
    } catch (err) {
      setError(
        getErrorMessage(
          err,
          err instanceof Error
            ? err.message
            : "Không thể bắt đầu kết nối Gmail.",
        ),
      );

      setConnecting(false);
    }
  };

  // ----------------------------------------------------------
  // XEM CHI TIẾT EMAIL
  // ----------------------------------------------------------

  const handleOpenMessage = async (message: GmailMessage) => {
    setSelectedMessage(null);
    setLoadingDetail(true);
    setDetailError("");

    try {
      const response = await api.get<GmailMessageDetail>(
        `/Emails/messages/${encodeURIComponent(message.id)}`,
      );

      setSelectedMessage(response.data);
    } catch (err) {
      setDetailError(getErrorMessage(err, "Không thể tải nội dung email."));
    } finally {
      setLoadingDetail(false);
    }
  };

  // ----------------------------------------------------------
  // TÌM KIẾM
  // ----------------------------------------------------------

  const handleSearch = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    setSelectedMessage(null);
    setSearchQuery(searchInput.trim());
  };

  const handleClearSearch = () => {
    setSearchInput("");
    setSearchQuery("");
    setSelectedMessage(null);
  };

  // ============================================================
  // GIAO DIỆN
  // ============================================================

  return (
    <main className="min-h-[calc(100vh-72px)] p-4 sm:p-6 lg:p-8">
      {/* Tiêu đề trang */}
      <div className="mb-6 flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <div className="mb-2 flex items-center gap-2 text-sm text-slate-500">
            <Mail size={16} />
            <span>Trợ lý văn phòng</span>
            <ChevronRight size={14} />
            <span className="text-slate-700">Email</span>
          </div>

          <h1 className="text-2xl font-bold tracking-tight text-slate-900 sm:text-3xl">
            Quản lý email
          </h1>

          <p className="mt-2 text-sm text-slate-500 sm:text-base">
            Quản lý hộp thư Gmail và chuẩn bị tích hợp trợ lý AI.
          </p>
        </div>

        {gmailStatus?.connected && (
          <button
            type="button"
            onClick={() => void loadMessages()}
            disabled={loadingMessages}
            className="inline-flex items-center justify-center gap-2 rounded-xl border border-slate-200 bg-white px-4 py-2.5 text-sm font-medium text-slate-700 shadow-sm transition hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-60"
          >
            <RefreshCw
              size={16}
              className={loadingMessages ? "animate-spin" : ""}
            />
            Làm mới
          </button>
        )}
      </div>

      {/* Trạng thái kết nối */}
      <section className="mb-6 rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-5">
        <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
          <div className="flex items-center gap-3">
            <div className="flex h-12 w-12 shrink-0 items-center justify-center rounded-xl bg-red-50 text-red-600">
              <Mail size={24} />
            </div>

            <div>
              <h2 className="font-semibold text-slate-800">Kết nối Gmail</h2>

              {loadingStatus ? (
                <p className="mt-1 text-sm text-slate-500">
                  Đang kiểm tra kết nối...
                </p>
              ) : gmailStatus?.connected ? (
                <div className="mt-1 flex flex-wrap items-center gap-2 text-sm">
                  <span className="inline-flex items-center gap-1.5 rounded-full bg-emerald-50 px-2.5 py-1 font-medium text-emerald-700">
                    <Check size={14} />
                    Đã kết nối
                  </span>

                  <span className="break-all text-slate-500">
                    {gmailStatus.gmailAddress}
                  </span>
                </div>
              ) : (
                <p className="mt-1 text-sm text-slate-500">
                  Kết nối tài khoản Google để xem email của bạn.
                </p>
              )}
            </div>
          </div>

          {!loadingStatus && !gmailStatus?.connected && (
            <button
              type="button"
              onClick={() => void handleConnectGmail()}
              disabled={connecting}
              className="inline-flex items-center justify-center gap-2 rounded-xl bg-blue-600 px-4 py-2.5 text-sm font-semibold text-white shadow-sm transition hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-60"
            >
              {connecting ? (
                <LoaderCircle size={17} className="animate-spin" />
              ) : (
                <Mail size={17} />
              )}
              {connecting ? "Đang chuyển đến Google..." : "Kết nối Gmail"}
            </button>
          )}
          {/* Nút đổi tài khoản Gmail khi đã kết nối */}
          {!loadingStatus && gmailStatus?.connected && (
            <button
              type="button"
              onClick={() => void handleConnectGmail()}
              disabled={connecting}
              className="inline-flex items-center justify-center gap-2 rounded-xl border border-slate-200 bg-white px-4 py-2.5 text-sm font-semibold text-slate-700 shadow-sm transition hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-60"
            >
              {connecting ? (
                <LoaderCircle size={17} className="animate-spin" />
              ) : (
                <RefreshCw size={17} />
              )}

              {connecting ? "Đang chuyển đến Google..." : "Đổi tài khoản Gmail"}
            </button>
          )}
        </div>
      </section>

      {/* Thông báo lỗi */}
      {error && (
        <div
          role="alert"
          className="mb-5 flex items-start justify-between gap-3 rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-700"
        >
          <p>{error}</p>

          <button
            type="button"
            onClick={() => void loadGmailStatus()}
            className="shrink-0 font-semibold underline"
          >
            Thử lại
          </button>
        </div>
      )}

      {/* Nội dung hộp thư */}
      {gmailStatus?.connected && (
        <section className="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
          {/* Thanh tìm kiếm */}
          <div className="border-b border-slate-200 p-4 sm:p-5">
            <form
              onSubmit={handleSearch}
              className="flex flex-col gap-3 sm:flex-row"
            >
              <div className="relative flex-1">
                <Search
                  size={18}
                  className="absolute left-3.5 top-1/2 -translate-y-1/2 text-slate-400"
                />

                <input
                  type="text"
                  value={searchInput}
                  onChange={(event) => setSearchInput(event.target.value)}
                  placeholder="Tìm email, ví dụ: subject:meeting"
                  className="w-full rounded-xl border border-slate-200 bg-slate-50 py-3 pl-11 pr-4 text-sm text-slate-800 outline-none transition placeholder:text-slate-400 focus:border-blue-400 focus:bg-white focus:ring-4 focus:ring-blue-50"
                />
              </div>

              <div className="flex gap-2">
                <button
                  type="submit"
                  disabled={loadingMessages}
                  className="inline-flex flex-1 items-center justify-center gap-2 rounded-xl bg-blue-600 px-5 py-3 text-sm font-semibold text-white transition hover:bg-blue-700 disabled:opacity-60 sm:flex-none"
                >
                  <Search size={16} />
                  Tìm kiếm
                </button>

                {searchQuery && (
                  <button
                    type="button"
                    onClick={handleClearSearch}
                    className="rounded-xl border border-slate-200 px-4 py-3 text-sm font-medium text-slate-600 hover:bg-slate-50"
                  >
                    Xóa lọc
                  </button>
                )}
              </div>
            </form>

            <div className="mt-4 flex flex-wrap items-center justify-between gap-2">
              <div className="flex items-center gap-2 text-sm font-semibold text-slate-700">
                <Inbox size={18} className="text-blue-600" />
                Hộp thư đến
              </div>

              <span className="text-xs text-slate-500">
                {loadingMessages
                  ? "Đang tải email..."
                  : `${messages.length} email trong kết quả`}
              </span>
            </div>
          </div>

          {/* Danh sách và chi tiết email */}
          <div className="grid min-h-[480px] grid-cols-1 lg:grid-cols-[minmax(280px,0.9fr)_minmax(0,1.4fr)]">
            {/* Danh sách email có thể cuộn độc lập */}
            <div
              className="max-h-[calc(100vh-280px)] min-h-[480px] overflow-y-auto border-b border-slate-200 lg:border-b-0 lg:border-r"
              onScroll={(event) => {
                const element = event.currentTarget;

                // Khi còn cách cuối danh sách khoảng 120px, tải trang tiếp theo.
                const nearBottom =
                  element.scrollTop + element.clientHeight >=
                  element.scrollHeight - 120;

                if (nearBottom) {
                  void loadMoreMessages();
                }
              }}
            >
              {loadingMessages && messages.length === 0 ? (
                <div className="flex min-h-64 flex-col items-center justify-center gap-3 text-slate-500">
                  <LoaderCircle
                    size={28}
                    className="animate-spin text-blue-600"
                  />
                  <p className="text-sm">Đang tải danh sách email...</p>
                </div>
              ) : messages.length === 0 ? (
                <div className="flex min-h-64 flex-col items-center justify-center px-6 text-center">
                  <div className="mb-3 flex h-14 w-14 items-center justify-center rounded-full bg-slate-100 text-slate-400">
                    <Inbox size={26} />
                  </div>

                  <h3 className="font-semibold text-slate-700">
                    Không có email
                  </h3>

                  <p className="mt-1 max-w-xs text-sm text-slate-500">
                    {error
                      ? "Không thể tải dữ liệu. Hãy kiểm tra thông báo lỗi phía trên."
                      : searchQuery
                        ? "Không tìm thấy email phù hợp với điều kiện tìm kiếm."
                        : "Hộp thư hiện chưa có email để hiển thị."}
                  </p>
                </div>
              ) : (
                <div className="divide-y divide-slate-100">
                  {messages.map((message) => {
                    const active = selectedMessage?.id === message.id;
                    const unread =
                      message.labelIds?.includes("UNREAD") ?? false;

                    return (
                      <button
                        type="button"
                        key={message.id}
                        onClick={() => void handleOpenMessage(message)}
                        className={`w-full p-4 text-left transition hover:bg-blue-50/60 ${
                          active ? "bg-blue-50" : "bg-white"
                        }`}
                      >
                        <div className="flex items-start gap-3">
                          <div className="relative flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-blue-100 text-sm font-bold text-blue-700">
                            {getInitials(message.from)}

                            {unread && (
                              <span className="absolute -right-0.5 -top-0.5 h-2.5 w-2.5 rounded-full border-2 border-white bg-blue-600" />
                            )}
                          </div>

                          <div className="min-w-0 flex-1">
                            <div className="flex items-start justify-between gap-2">
                              <p
                                className={`truncate text-sm ${
                                  unread
                                    ? "font-bold text-slate-900"
                                    : "font-medium text-slate-700"
                                }`}
                              >
                                {getSenderName(message.from)}
                              </p>

                              <span className="shrink-0 text-[11px] text-slate-400">
                                {formatEmailDate(
                                  message.date,
                                  message.internalDate,
                                )}
                              </span>
                            </div>

                            <p
                              className={`mt-1 truncate text-sm ${
                                unread
                                  ? "font-semibold text-slate-800"
                                  : "text-slate-600"
                              }`}
                            >
                              {message.subject || "(Không có tiêu đề)"}
                            </p>

                            <p className="mt-1 line-clamp-2 text-xs leading-5 text-slate-500">
                              {message.snippet ||
                                "Không có nội dung xem trước."}
                            </p>

                            {unread && (
                              <span className="mt-2 inline-block rounded-full bg-blue-50 px-2 py-0.5 text-[10px] font-semibold text-blue-700">
                                Chưa đọc
                              </span>
                            )}
                          </div>
                        </div>
                      </button>
                    );
                  })}

                  {/* Trạng thái khi đang tải thêm email */}
                  {loadingMore && (
                    <div className="flex items-center justify-center gap-2 p-4 text-sm text-slate-500">
                      <LoaderCircle
                        size={18}
                        className="animate-spin text-blue-600"
                      />
                      Đang tải thêm email...
                    </div>
                  )}

                  {/* Thông báo khi đã hết trang */}
                  {!loadingMore && !nextPageToken && messages.length > 0 && (
                    <p className="p-4 text-center text-xs text-slate-400">
                      Đã hiển thị hết email phù hợp.
                    </p>
                  )}
                </div>
              )}
            </div>

            {/* Chi tiết email */}
            <div className="min-w-0">
              {loadingDetail ? (
                <div className="flex min-h-80 flex-col items-center justify-center gap-3 text-slate-500">
                  <LoaderCircle
                    size={28}
                    className="animate-spin text-blue-600"
                  />
                  <p className="text-sm">Đang tải nội dung email...</p>
                </div>
              ) : detailError ? (
                <div className="flex min-h-80 flex-col items-center justify-center gap-3 p-6 text-center">
                  <p className="text-sm text-red-600">{detailError}</p>
                  <button
                    type="button"
                    onClick={() => setDetailError("")}
                    className="text-sm font-semibold text-blue-600 hover:underline"
                  >
                    Đóng thông báo
                  </button>
                </div>
              ) : selectedMessage ? (
                <article className="p-5 sm:p-7">
                  <button
                    type="button"
                    onClick={() => setSelectedMessage(null)}
                    className="mb-5 inline-flex items-center gap-2 text-sm font-medium text-slate-500 hover:text-blue-600 lg:hidden"
                  >
                    <ArrowLeft size={16} />
                    Quay lại danh sách
                  </button>

                  <div className="flex items-start gap-3">
                    <div className="flex h-11 w-11 shrink-0 items-center justify-center rounded-full bg-blue-100 font-bold text-blue-700">
                      {getInitials(selectedMessage.from)}
                    </div>

                    <div className="min-w-0 flex-1">
                      <h2 className="break-words text-lg font-bold text-slate-900">
                        {selectedMessage.subject || "(Không có tiêu đề)"}
                      </h2>

                      <p className="mt-2 break-words text-sm font-medium text-slate-700">
                        {getSenderName(selectedMessage.from)}
                      </p>

                      <p className="mt-1 break-all text-xs text-slate-500">
                        {selectedMessage.from}
                      </p>

                      {selectedMessage.to && (
                        <p className="mt-2 break-words text-xs text-slate-500">
                          Đến: {selectedMessage.to}
                        </p>
                      )}

                      {selectedMessage.cc && (
                        <p className="mt-1 break-words text-xs text-slate-500">
                          CC: {selectedMessage.cc}
                        </p>
                      )}

                      <p className="mt-2 text-xs text-slate-400">
                        {formatEmailDate(
                          selectedMessage.date,
                          selectedMessage.internalDate,
                        )}
                      </p>
                    </div>

                    <MailOpen
                      size={19}
                      className="hidden shrink-0 text-slate-400 sm:block"
                    />
                  </div>

                  <div className="my-6 border-t border-slate-100" />

                  <div className="max-w-none overflow-hidden whitespace-pre-wrap break-words text-sm leading-7 text-slate-700">
                    {selectedMessage.plainTextBody?.trim() ||
                      selectedMessage.snippet ||
                      "Email không có nội dung văn bản để hiển thị."}
                  </div>

                  {!selectedMessage.plainTextBody &&
                    selectedMessage.htmlBody && (
                      <p className="mt-4 rounded-lg bg-amber-50 p-3 text-xs leading-5 text-amber-800">
                        Email này có nội dung HTML nhưng không có phần văn bản
                        thuần. Để bảo đảm an toàn, giao diện hiện chưa hiển thị
                        HTML trực tiếp.
                      </p>
                    )}

                  <div className="mt-8 rounded-xl border border-blue-100 bg-blue-50/70 p-4">
                    <div className="flex items-start gap-3">
                      <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-white text-blue-600 shadow-sm">
                        <Send size={17} />
                      </div>

                      <div>
                        <h3 className="text-sm font-semibold text-slate-800">
                          Trợ lý email AI
                        </h3>
                        <p className="mt-1 text-xs leading-5 text-slate-500">
                          Khu vực này có thể tích hợp chức năng tóm tắt email và
                          đề xuất nội dung trả lời ở bước tiếp theo.
                        </p>
                      </div>
                    </div>
                  </div>
                </article>
              ) : (
                <div className="flex min-h-80 flex-col items-center justify-center px-6 py-12 text-center">
                  <div className="mb-4 flex h-16 w-16 items-center justify-center rounded-2xl bg-slate-100 text-slate-400">
                    <MailOpen size={30} />
                  </div>

                  <h3 className="font-semibold text-slate-700">
                    Chọn một email để đọc
                  </h3>

                  <p className="mt-2 max-w-xs text-sm leading-6 text-slate-500">
                    Nội dung thư, người gửi, người nhận và ngày gửi sẽ hiển thị
                    tại đây.
                  </p>
                </div>
              )}
            </div>
          </div>

          {/* Chú thích cuối hộp thư */}
          <div className="flex flex-wrap items-center justify-between gap-2 border-t border-slate-200 bg-slate-50 px-4 py-3 text-xs text-slate-500 sm:px-5">
            <span className="inline-flex items-center gap-1.5">
              <Unplug size={14} className="text-slate-400" />
              Được đồng bộ từ Gmail
            </span>

            <span>Mỗi lần tải tối đa 20 email. Cuộn xuống để xem thêm.</span>
          </div>
        </section>
      )}

      {/* Trạng thái chưa kết nối */}
      {!loadingStatus && !gmailStatus?.connected && (
        <section className="rounded-2xl border border-dashed border-slate-300 bg-white px-6 py-12 text-center">
          <div className="mx-auto mb-4 flex h-16 w-16 items-center justify-center rounded-2xl bg-slate-100 text-slate-400">
            <Inbox size={30} />
          </div>

          <h2 className="text-lg font-semibold text-slate-800">
            Hộp thư chưa được kết nối
          </h2>

          <p className="mx-auto mt-2 max-w-md text-sm leading-6 text-slate-500">
            Kết nối Gmail để xem danh sách thư, tìm kiếm email và đọc nội dung
            ngay trong OfficeAssistant.
          </p>
        </section>
      )}
    </main>
  );
}

export default Emails;
