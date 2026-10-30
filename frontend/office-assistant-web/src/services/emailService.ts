import axios from 'axios'
import api from './api'

// ============================================================
// HÀM HỖ TRỢ LỖI
// ============================================================

export function getApiMessage(error: unknown, fallback: string): string {
  if (axios.isAxiosError(error)) {
    const data = error.response?.data as { message?: string } | undefined
    return data?.message ?? fallback
  }

  return fallback
}

export function isReconnectRequired(error: unknown): boolean {
  if (!axios.isAxiosError(error)) return false

  const data = error.response?.data as { code?: string } | undefined
  return data?.code === 'gmail_reconnect_required'
}

// ============================================================
// TÓM TẮT / GỢI Ý TRẢ LỜI / GỬI THƯ ĐƠN
// ============================================================

export type ReplyTone = 'formal' | 'friendly' | 'short'

export interface SendEmailPayload {
  to: string
  cc?: string
  subject: string
  body: string
  replyToMessageId?: string
}

export async function summarizeEmail(
  messageId: string,
  refresh = false,
): Promise<{ summary: string; cached: boolean }> {
  const response = await api.post<{ summary: string; cached: boolean }>(
    `/Emails/messages/${encodeURIComponent(messageId)}/summarize`,
    null,
    { params: { refresh } },
  )

  return response.data
}

export async function suggestReply(
  messageId: string,
  tone: ReplyTone,
  note: string,
): Promise<string> {
  const response = await api.post<{ reply: string }>(
    `/Emails/messages/${encodeURIComponent(messageId)}/suggest-reply`,
    { tone, note: note.trim() || null },
  )

  return response.data.reply
}

export async function sendEmail(payload: SendEmailPayload): Promise<void> {
  await api.post('/Emails/send', payload)
}

// ============================================================
// GỬI HÀNG LOẠT
// ============================================================

export interface RecipientInput {
  email: string
  name: string | null
  fields: Record<string, string>
}

export interface ParsedRecipients {
  variables: string[]
  recipients: RecipientInput[]
  totalRows: number
  invalid: number
  duplicates: number
  truncated: boolean
}

export interface CampaignSummary {
  id: number
  name: string
  subject: string
  status: string
  note: string | null
  total: number
  sent: number
  failed: number
  pending: number
  createdAtUtc: string
  startedAtUtc: string | null
  finishedAtUtc: string | null
}

export interface CampaignRecipient {
  id: number
  email: string
  name: string | null
  status: string
  error: string | null
  sentAtUtc: string | null
}

export interface CampaignDetail extends CampaignSummary {
  recipients: CampaignRecipient[]
}

export async function parseRecipients(file: File): Promise<ParsedRecipients> {
  const formData = new FormData()
  formData.append('file', file)

  const response = await api.post<ParsedRecipients>(
    '/EmailCampaigns/parse-recipients',
    formData,
  )

  return response.data
}

export async function previewCampaign(
  subject: string,
  bodyTemplate: string,
  recipient: RecipientInput | null,
): Promise<{ subject: string; body: string }> {
  const response = await api.post<{ subject: string; body: string }>(
    '/EmailCampaigns/preview',
    { subject, bodyTemplate, recipient },
  )

  return response.data
}

export async function createCampaign(payload: {
  name: string
  subject: string
  bodyTemplate: string
  recipients: RecipientInput[]
}): Promise<number> {
  const response = await api.post<{ id: number }>('/EmailCampaigns', payload)
  return response.data.id
}

export async function startCampaign(id: number): Promise<void> {
  await api.post(`/EmailCampaigns/${id}/start`)
}

export async function cancelCampaign(id: number): Promise<void> {
  await api.post(`/EmailCampaigns/${id}/cancel`)
}

export async function deleteCampaign(id: number): Promise<void> {
  await api.delete(`/EmailCampaigns/${id}`)
}

export async function getCampaigns(): Promise<CampaignSummary[]> {
  const response = await api.get<CampaignSummary[]>('/EmailCampaigns')
  return response.data
}

export async function getCampaign(id: number): Promise<CampaignDetail> {
  const response = await api.get<CampaignDetail>(`/EmailCampaigns/${id}`)
  return response.data
}

// Backend trả UTC không có "Z" → thêm vào để JS hiểu đúng.
export function parseUtc(value: string): Date {
  const hasTimeZone = /([zZ]|[+-]\d{2}:\d{2})$/.test(value)
  return new Date(hasTimeZone ? value : value + 'Z')
}
