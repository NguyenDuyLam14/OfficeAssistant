import type { ReactNode } from 'react'

type QuickActionProps = {
  icon: ReactNode
  title: string
  description: string
}

function QuickAction({
  icon,
  title,
  description,
}: QuickActionProps) {
  return (
    <button
      type="button"
      className="group flex items-start gap-4 rounded-xl border border-slate-200 p-4 text-left transition hover:border-blue-300 hover:bg-blue-50"
    >
      {/* Icon */}
      <div className="rounded-xl bg-blue-50 p-3 text-blue-600 group-hover:bg-blue-100">
        {icon}
      </div>

      {/* Nội dung */}
      <div>
        <h4 className="font-semibold text-slate-800">
          {title}
        </h4>

        <p className="mt-1 text-xs leading-5 text-slate-500">
          {description}
        </p>
      </div>
    </button>
  )
}

export default QuickAction