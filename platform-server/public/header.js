(function initializeHeader() {
  "use strict";

  const loginButton = document.getElementById("login-button");
  const loginLabel = document.getElementById("login-label");
  const accountAvatar = document.getElementById("account-avatar");
  const accountShell = document.getElementById("account-shell");
  const accountMenu = document.getElementById("account-menu");
  const accountPlayerId = document.getElementById("account-player-id");
  const accountProvider = document.getElementById("account-provider");
  const logoutButton = document.getElementById("logout-button");
  const shopLink = document.getElementById("shop-link");
  let session = null;

  function setMenuOpen(open) {
    const nextOpen = Boolean(open && session);
    accountMenu.hidden = !nextOpen;
    loginButton.setAttribute("aria-expanded", String(nextOpen));
  }

  function renderSession(nextSession) {
    session = nextSession;
    const playerName = nextSession?.playerId || nextSession?.subject;
    const signedIn = Boolean(playerName);

    loginButton.classList.toggle("signed-in", signedIn);
    loginButton.title = signedIn ? "내 정보 열기" : "HIVE 가입·로그인";
    loginLabel.textContent = signedIn ? "내 정보" : "HIVE 로그인";
    accountAvatar.textContent = playerName ? playerName.slice(0, 1).toUpperCase() : "H";
    accountPlayerId.textContent = playerName || "-";
    accountProvider.textContent = nextSession?.provider === "hive" ? "HIVE 계정" : "연결된 계정";
    setMenuOpen(false);
    window.dispatchEvent(
      new CustomEvent("GAME_SESSION_CHANGED", { detail: { session: nextSession } })
    );
  }

  async function restoreSession() {
    try {
      const result = await window.gameBridge.getSession();
      renderSession(result.session);
    } catch (_error) {
      window.gameBridge.sessionToken = null;
      renderSession(null);
    }
  }

  async function configureHeader() {
    const config = await window.gameBridge.getPublicConfig();
    if (config.hiveWebShopUrl) {
      shopLink.href = config.hiveWebShopUrl;
      shopLink.hidden = false;
    }
    await restoreSession();
  }

  loginButton.addEventListener("click", async () => {
    if (session) {
      setMenuOpen(accountMenu.hidden);
      return;
    }

    loginButton.disabled = true;
    try {
      await window.gameBridge.loginWithHive();
      const result = await window.gameBridge.getSession();
      renderSession(result.session);
    } catch (error) {
      loginButton.title = error instanceof Error ? error.message : "HIVE 로그인에 실패했습니다.";
      if (!session) loginLabel.textContent = "로그인 재시도";
    } finally {
      loginButton.disabled = false;
    }
  });

  logoutButton.addEventListener("click", async () => {
    logoutButton.disabled = true;
    try {
      await window.gameBridge.logout();
      renderSession(null);
    } finally {
      logoutButton.disabled = false;
    }
  });

  document.addEventListener("click", (event) => {
    if (!accountMenu.hidden && !accountShell.contains(event.target)) setMenuOpen(false);
  });

  document.addEventListener("keydown", (event) => {
    if (event.key === "Escape") setMenuOpen(false);
  });

  configureHeader().catch(() => renderSession(null));
})();
