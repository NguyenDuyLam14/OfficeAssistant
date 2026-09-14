import type { ReactNode } from 'react'

type StatCardProps = {
  title: string
  value: string
  description: string
  icon: ReactNode
}

function StatCard({
  title,
  value,
  description,
  icon,
}: StatCardProps) {
  return (
    <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
      {/* Nội dung chính */}
      <div className="flex items-start justify-between">

        {/* Thông tin thống kê */}
        <div>
          <p className="text-sm text-slate-500">
            {title}
          </p>

          <p className="mt-2 text-3xl font-bold text-slate-800">
            {value}
          </p>
        </div>

        {/* Icon */}
        <div className="rounded-xl bg-blue-50 p-3 text-blue-600">
          {icon}
        </div>
      </div>

      {/* Mô tả */}
      <p className="mt-4 text-xs text-slate-500">
        {description}
      </p>
    </div>
  )
}

export default StatCard