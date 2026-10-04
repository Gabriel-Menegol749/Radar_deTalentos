import { getToken, goToLogin } from "./utils/auth.js";

// Em produção (Vercel) aponta para a API no Render; em dev, o proxy do Vite atende /api.
const BASE = (import.meta.env.VITE_API_URL || "").replace(/\/+$/, "");

export class ApiError extends Error {
  constructor(status, code, message) {
    super(message);
    this.status = status;
    this.code = code;
  }
}

function query(params) {
  const q = new URLSearchParams();
  Object.entries(params || {}).forEach(([k, v]) => {
    if (v !== undefined && v !== null && v !== "") q.set(k, v);
  });
  const s = q.toString();
  return s ? "?" + s : "";
}

async function request(method, path, { body, params, raw } = {}) {
  const headers = {};
  const token = getToken();
  if (token) headers.Authorization = "Bearer " + token;
  if (body !== undefined) headers["Content-Type"] = "application/json";

  let res;
  try {
    res = await fetch(BASE + path + query(params), { method, headers, body: body !== undefined ? JSON.stringify(body) : undefined });
  } catch {
    throw new ApiError(0, "NETWORK", "Não foi possível conectar à API.");
  }

  // Interceptor JWT: sessão expirada ou inválida volta para o login.
  if (res.status === 401 && !path.startsWith("/api/auth/login")) {
    goToLogin();
    throw new ApiError(401, "UNAUTHORIZED", "Sessão expirada.");
  }
  if (!res.ok) {
    let data = null;
    try { data = await res.json(); } catch { /* sem corpo */ }
    const fallback = res.status === 403 ? "Você não tem permissão para esta ação." : "Erro " + res.status + ".";
    throw new ApiError(res.status, data?.code || "HTTP_" + res.status, data?.message || fallback);
  }
  if (raw) return res;
  if (res.status === 204) return null;
  const text = await res.text();
  return text ? JSON.parse(text) : null;
}

export const api = {
  get: (path, params) => request("GET", path, { params }),
  post: (path, body) => request("POST", path, { body: body ?? {} }),
  put: (path, body) => request("PUT", path, { body }),
  del: (path) => request("DELETE", path),

  /** Baixa um arquivo gerado pelo backend (exportações XLSX). */
  async download(path, params, fallbackName) {
    const res = await request("GET", path, { params, raw: true });
    const blob = await res.blob();
    const disposition = res.headers.get("Content-Disposition") || "";
    const match = disposition.match(/filename\*?=(?:UTF-8'')?"?([^";]+)"?/i);
    const name = match ? decodeURIComponent(match[1]) : fallbackName;
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = name;
    document.body.appendChild(a);
    a.click();
    a.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  },
};
