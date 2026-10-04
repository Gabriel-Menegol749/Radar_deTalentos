import { api } from "./api.js";
import { $ } from "./utils/dom.js";
import { getToken, saveSession } from "./utils/auth.js";
import { isValidEmail } from "./utils/formatters.js";

if (getToken()) window.location.replace("/");

$("loginForm").addEventListener("submit", async (e) => {
  e.preventDefault();
  const btn = $("loginBtn");
  const err = $("loginError");
  const showError = (msg) => { err.textContent = msg; err.hidden = false; };
  err.hidden = true;

  const email = $("email").value.trim();
  if (!isValidEmail(email)) { showError("Informe um e-mail válido (ex.: nome@empresa.com)."); $("email").focus(); return; }
  if (!$("password").value) { showError("Informe a senha."); $("password").focus(); return; }

  btn.disabled = true;
  try {
    const res = await api.post("/api/auth/login", { email, password: $("password").value });
    saveSession(res);
    window.location.replace("/");
  } catch (ex) {
    showError(ex.message);
  } finally {
    btn.disabled = false;
  }
});
