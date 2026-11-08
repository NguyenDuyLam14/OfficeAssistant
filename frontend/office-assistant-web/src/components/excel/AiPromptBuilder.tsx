import { useState } from "react";

const GOALS = [
  "Tổng quan dữ liệu",
  "Tìm giá trị bất thường",
  "Chất lượng dữ liệu",
  "Xu hướng / so sánh",
  "Đề xuất hành động",
];
const LENGTHS = [
  "Ngắn (dưới 100 từ)",
  "Vừa (khoảng 200 từ)",
  "Chi tiết (khoảng 400 từ)",
];

type Fields = {
  goals: string[];
  focus: string[];
  audience: string;
  length: string;
  extra: string;
};

function build(f: Fields): string {
  const parts: string[] = [];
  if (f.goals.length) parts.push(`Mục tiêu: ${f.goals.join(", ")}`);
  if (f.focus.length)
    parts.push(`Tập trung vào các cột: ${f.focus.join(", ")}`);
  if (f.audience.trim()) parts.push(`Đối tượng đọc: ${f.audience.trim()}`);
  parts.push(`Độ dài: ${f.length}`);
  if (f.extra.trim()) parts.push(`Yêu cầu thêm: ${f.extra.trim()}`);
  return parts.join("\n");
}

type Props = {
  columns: string[];
  value: string;
  onChange: (prompt: string) => void;
};

export default function AiPromptBuilder({ columns, value, onChange }: Props) {
  const [f, setF] = useState<Fields>({
    goals: [],
    focus: [],
    audience: "Trưởng phòng / ban giám đốc",
    length: LENGTHS[1],
    extra: "",
  });

  const update = (patch: Partial<Fields>) => {
    const next = { ...f, ...patch };
    setF(next);
    onChange(build(next));
  };

  const toggle = (list: string[], item: string) =>
    list.includes(item) ? list.filter((x) => x !== item) : [...list, item];

  const chip = (active: boolean) =>
    `rounded-full px-3 py-1 text-xs font-medium transition ${
      active
        ? "bg-blue-600 text-white"
        : "bg-blue-50 text-blue-700 hover:bg-blue-100"
    }`;

  return (
    <div className="mt-4 space-y-4 rounded-xl border border-slate-200 bg-slate-50 p-4">
      <div>
        <p className="mb-2 text-xs font-medium text-slate-600">
          Mục tiêu phân tích
        </p>
        <div className="flex flex-wrap gap-2">
          {GOALS.map((g) => (
            <button
              key={g}
              type="button"
              className={chip(f.goals.includes(g))}
              onClick={() => update({ goals: toggle(f.goals, g) })}
            >
              {g}
            </button>
          ))}
        </div>
      </div>

      <div>
        <p className="mb-2 text-xs font-medium text-slate-600">
          Cột cần tập trung (không bắt buộc)
        </p>
        <div className="flex flex-wrap gap-2">
          {columns.map((c) => (
            <button
              key={c}
              type="button"
              className={chip(f.focus.includes(c))}
              onClick={() => update({ focus: toggle(f.focus, c) })}
            >
              {c}
            </button>
          ))}
        </div>
      </div>

      <div className="grid gap-3 sm:grid-cols-2">
        <label className="text-xs font-medium text-slate-600">
          Đối tượng đọc
          <input
            value={f.audience}
            onChange={(e) => update({ audience: e.target.value })}
            className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 text-sm font-normal"
          />
        </label>
        <label className="text-xs font-medium text-slate-600">
          Độ dài
          <select
            value={f.length}
            onChange={(e) => update({ length: e.target.value })}
            className="mt-1 w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm font-normal"
          >
            {LENGTHS.map((l) => (
              <option key={l}>{l}</option>
            ))}
          </select>
        </label>
      </div>

      <label className="block text-xs font-medium text-slate-600">
        Yêu cầu thêm
        <input
          value={f.extra}
          onChange={(e) => update({ extra: e.target.value })}
          placeholder="Ví dụ: so sánh doanh thu giữa các khu vực"
          className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 text-sm font-normal"
        />
      </label>

      <label className="block text-xs font-medium text-slate-600">
        Prompt gửi cho AI (có thể sửa trực tiếp)
        <textarea
          rows={5}
          maxLength={1000}
          value={value}
          onChange={(e) => onChange(e.target.value)}
          className="mt-1 w-full resize-y rounded-lg border border-slate-300 px-3 py-2 text-sm font-normal"
        />
      </label>
    </div>
  );
}
