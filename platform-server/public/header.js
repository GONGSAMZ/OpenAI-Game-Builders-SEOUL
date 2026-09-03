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
  let sessionResolved = false;

  function setMenuOpen(open) {
    const nextOpen = Boolean(open && session);
    accountMenu.hidden = !nextOpen;
    loginButton.setAttribute("aria-expanded", String(nextOpen));
  }

  function renderSession(nextSession, notifyGame = true) {
    sessionResolved = true;
    session = nextSession;
    const stableId = nextSession?.playerId || nextSession?.subject;
    const accountLabel = nextSession?.accountLabel || (
      nextSession?.provider === "hive" && stableId
        ? `HIVE 계정 · ${stableId.slice(-6)}`
        : stableId
    );
    const signedIn = Boolean(accountLabel);

    loginButton.classList.toggle("signed-in", signedIn);
    loginButton.title = signedIn ? "내 정보 열기" : "HIVE 가입·로그인";
    loginLabel.textContent = signedIn ? "내 정보" : "HIVE 로그인";
    accountAvatar.textContent = "H";
    accountPlayerId.textContent = accountLabel || "-";
    accountProvider.textContent = nextSession?.provider === "hive" ? "HIVE 계정" : "연결된 계정";
    setMenuOpen(false);
    window.dispatchEvent(
      new CustomEvent("GAME_SESSION_CHANGED", { detail: { session: nextSession } })
    );
    if (notifyGame) window.gameBridge.broadcastSession(signedIn);
  }

  async function restoreSession(notifyGame = false) {
    try {
      const result = await window.gameBridge.getSession();
      renderSession(result.session, notifyGame);
    } catch (error) {
      if (error?.status === 401) {
        window.gameBridge.sessionToken = null;
        renderSession(null, notifyGame);
        return;
      }
      loginButton.title = "서버 연결이 불안정합니다. 로그인 상태를 유지하며 다시 확인합니다.";
    }
  }

  async function configureHeader() {
    const gameFrame = document.getElementById("game-frame");
    gameFrame?.addEventListener("load", () => {
      // null means "not resolved" until GET /auth/session returns 200 or 401.
      // Never turn a slow/bootstrap failure into an authoritative logout.
      if (sessionResolved) window.gameBridge.broadcastSession(Boolean(session));
    });
    const [config] = await Promise.all([
      window.gameBridge.getPublicConfig().catch(() => null),
      restoreSession(true)
    ]);
    if (config?.hiveWebShopUrl) {
      shopLink.href = config.hiveWebShopUrl;
      shopLink.hidden = false;
    }
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

  window.addEventListener("message", (event) => {
    const gameFrame = document.getElementById("game-frame");
    if (
      event.origin === window.gameBridge.serverOrigin &&
      event.source === gameFrame?.contentWindow &&
      event.data?.type === "PLATFORM_SESSION"
    ) {
      restoreSession(false);
    }
  });

  configureHeader().catch(() => {
    loginButton.title = "서버 연결이 불안정합니다. 로그인 상태를 유지하며 다시 확인합니다.";
  });
})();
