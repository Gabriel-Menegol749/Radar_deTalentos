// Formatadores e normalizações portados do protótipo (o backend aplica as mesmas regras).

export function todayISO() {
  const d = new Date();
  const local = new Date(d.getTime() - d.getTimezoneOffset() * 60000);
  return local.toISOString().slice(0, 10);
}

export function fmtDateBR(iso) {
  if (!iso) return "—";
  const [y, m, d] = String(iso).slice(0, 10).split("-");
  if (!y || !m || !d) return "—";
  return `${d}/${m}/${y}`;
}

export function fmtDateTimeBR(iso) {
  if (!iso) return "—";
  const d = new Date(iso);
  return d.toLocaleDateString("pt-BR") + " " + d.toLocaleTimeString("pt-BR", { hour: "2-digit", minute: "2-digit" });
}

export function fmtMoneyBR(n) {
  if (n === null || n === undefined || n === "" || isNaN(n)) return "—";
  return "R$ " + Number(n).toLocaleString("pt-BR");
}

export function normalizeLinkedin(raw) {
  if (!raw) return "";
  let s = String(raw).trim();
  if (!s) return "";
  try { s = decodeURIComponent(s); } catch { /* mantém */ }
  const m = s.match(/linkedin\.com\/in\/([^\/?&#\s]+)/i);
  if (m) return "linkedin.com/in/" + m[1].replace(/\/+$/, "").toLowerCase();
  s = s.replace(/^@/, "");
  if (/^[\p{L}\p{N}\-._]+$/u.test(s)) return "linkedin.com/in/" + s.toLowerCase();
  return s;
}

export function normalizePhone(raw) {
  if (!raw) return { value: "", label: "" };
  const s = String(raw).trim();
  if (!s) return { value: "", label: "" };
  if (/inmail/i.test(s)) return { value: "inmail", label: "Inmail (sem telefone)" };
  const digits = s.replace(/\D/g, "");
  if (!digits) return { value: s, label: "Não reconhecido como telefone" };
  let ddd = "", rest = "";
  if (digits.length === 11 || digits.length === 10) { ddd = digits.slice(0, 2); rest = digits.slice(2); }
  else if (digits.length === 9 || digits.length === 8) { rest = digits; }
  else return { value: digits, label: digits.length + " dígitos — confira" };
  const formatted = rest.length === 9 ? rest.slice(0, 5) + "-" + rest.slice(5) : rest.slice(0, 4) + "-" + rest.slice(4);
  return { value: (ddd ? "(" + ddd + ") " : "") + formatted, label: "Padronizado" };
}

/** Converte valores de planilha ("12.500", "R$ 9000", 18000) em número; null se não for valor monetário. */
export function parseMoney(v) {
  if (v === null || v === undefined || v === "") return null;
  if (typeof v === "number") return isFinite(v) ? v : null;
  const s = String(v).trim();
  if (!/^\s*(R\$)?\s*[\d.,]+\s*$/i.test(s)) return null;
  let n = s.replace(/[^\d.,]/g, "");
  if (n.includes(",")) n = n.replace(/\./g, "").replace(",", ".");
  else if (/^\d{1,3}(\.\d{3})+$/.test(n)) n = n.replace(/\./g, "");
  const num = Number(n);
  return isFinite(num) ? num : null;
}

/** Formato básico de e-mail (o backend aplica a mesma regra). */
export function isValidEmail(email) {
  return /^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/.test(String(email || "").trim());
}

export const STATUS_LABELS = {
  Active: "Em andamento",
  Hired: "Contratado",
  Rejected: "Reprovado / sem sucesso",
  Declined: "Declínio",
};
