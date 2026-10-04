const TOKEN_KEY = "radar.token";
const USER_KEY = "radar.user";

export function getToken() {
  try { return localStorage.getItem(TOKEN_KEY); } catch { return null; }
}

export function getUser() {
  try { return JSON.parse(localStorage.getItem(USER_KEY) || "null"); } catch { return null; }
}

export function saveSession({ token, user }) {
  localStorage.setItem(TOKEN_KEY, token);
  localStorage.setItem(USER_KEY, JSON.stringify(user));
}

export function clearSession() {
  try { localStorage.removeItem(TOKEN_KEY); localStorage.removeItem(USER_KEY); } catch { /* ignora */ }
}

export function isAdmin() {
  return getUser()?.role === "Admin";
}

export function goToLogin() {
  clearSession();
  window.location.replace("/login.html");
}
