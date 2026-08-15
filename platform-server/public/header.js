(function initializeHeader() {
  "use strict";

  const loginButton = document.getElementById("login-button");
  const loginLabel = document.getElementById("login-label");
  const accountAvatar = document.getElementById("account-avatar");
  const shopLink = document.getElementById("shop-link");
  let session = null;

  function renderSession(nextSession) {
    session = nextSession;
    const playerName = nextSession?.playerId || nextSession?.subject;

    loginButton.classList.toggle("signed-in", Boolean(playerName));
    loginButton.title = playerName ? `${playerName} · 로그아웃` : "HIVE 가입·로그인";
    loginLabel.textContent = playerName || "HIVE 로그인";
    accountAvatar.textContent = playerName ? playerName.slice(0, 1).toUpperCase() : "H";
  }

  async function restoreSession() {
    if (!window.gameBridge.sessionToken) {
      renderSession(null);
      return;
    }

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
    loginButton.disabled = true;
    try {
      if (session) {
        await window.gameBridge.logout();
        renderSession(null);
      } else {
        await window.gameBridge.loginWithHive();
        const result = await window.gameBridge.getSession();
        renderSession(result.session);
      }
    } catch (error) {
      loginButton.title = error instanceof Error ? error.message : "HIVE 로그인에 실패했습니다.";
      if (!session) loginLabel.textContent = "로그인 재시도";
    } finally {
      loginButton.disabled = false;
    }
  });

  configureHeader().catch(() => renderSession(null));
})();
