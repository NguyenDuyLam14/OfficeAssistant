import api from './api'

export type FileCategory =
  | ''
  | 'word'
  | 'excel'
  | 'pdf'
  | 'powerpoint'
  | 'text'

export interface RootInfo {
  index: number
  name: string
}

export interface LocalFileItem {
  rootIndex: number
  relativePath: string
  name: string
  extension: string
  category: string
  size: number
  modifiedAtUtc: string
  folder: string
}

export interface LocalFileSearchResponse {
  items: LocalFileItem[]
  totalMatched: number
  scanned: number
  truncated: boolean
}

export interface SearchParams {
  keyword: string
  category: FileCategory
  from: string | null // ISO
  to: string | null // ISO
  searchContent: boolean
  rootIndex: number | null
}

export interface SummaryResult {
  summary: string
  truncated: boolean
}

// Định dạng backend đọc được nội dung để tóm tắt.
export const SUMMARIZABLE_EXTENSIONS = ['.docx', '.txt', '.md', '.csv']

export async function getRoots(): Promise<RootInfo[]> {
  const response = await api.get<RootInfo[]>('/LocalFiles/roots')
  return response.data
}

export async function searchFiles(
  params: SearchParams,
): Promise<LocalFileSearchResponse> {
  const response = await api.get<LocalFileSearchResponse>(
    '/LocalFiles/search',
    {
      params: {
        keyword: params.keyword.trim() || undefined,
        category: params.category || undefined,
        from: params.from ?? undefined,
        to: params.to ?? undefined,
        searchContent: params.searchContent,
        rootIndex: params.rootIndex ?? undefined,
      },
    },
  )

  return response.data
}

export async function downloadLocalFile(item: LocalFileItem): Promise<void> {
  const response = await api.get('/LocalFiles/download', {
    params: { rootIndex: item.rootIndex, path: item.relativePath },
    responseType: 'blob',
  })

  const url = URL.createObjectURL(response.data as Blob)
  const link = document.createElement('a')

  link.href = url
  link.download = item.name
  document.body.appendChild(link)
  link.click()
  link.remove()

  URL.revokeObjectURL(url)
}

// ---------------- Tóm tắt ----------------

export async function summarizeLocalFile(
  item: LocalFileItem,
): Promise<SummaryResult> {
  const response = await api.post<SummaryResult>('/Summary/file', {
    rootIndex: item.rootIndex,
    relativePath: item.relativePath,
  })

  return response.data
}

export async function summarizeDocument(id: number): Promise<SummaryResult> {
  const response = await api.post<SummaryResult>(`/Summary/document/${id}`)
  return response.data
}

export async function summarizeText(
  title: string,
  content: string,
): Promise<SummaryResult> {
  const response = await api.post<SummaryResult>('/Summary/text', {
    title,
    content,
  })

  return response.data
}

// ---------------- Hỏi đáp ----------------

export interface AnswerResult {
  answer: string
  truncated: boolean
}

export async function askText(
  title: string,
  content: string,
  question: string,
): Promise<AnswerResult> {
  const response = await api.post<AnswerResult>('/Summary/ask-text', {
    title,
    content,
    question,
  })

  return response.data
}

export async function askDocument(
  id: number,
  question: string,
): Promise<AnswerResult> {
  const response = await api.post<AnswerResult>(
    `/Summary/ask-document/${id}`,
    { question },
  )

  return response.data
}

export async function askFile(
  item: LocalFileItem,
  question: string,
): Promise<AnswerResult> {
  const response = await api.post<AnswerResult>('/Summary/ask-file', {
    rootIndex: item.rootIndex,
    relativePath: item.relativePath,
    question,
  })

  return response.data
}