(function initializeStoreDevTools() {
  "use strict";

  const panel = document.getElementById("store-dev-tools");
  const playerId = document.getElementById("dev-player-id");
  const balance = document.getElementById("dev-currency-balance");
  const grantButton = document.getElementById("dev-grant-button");
  const status = document.getElementById("dev-grant-status");
  let session = null;
  let syncing = false;

  function currencyFromInventory(inventory) {
    return inventory.find((entry) => entry.itemId === "red-bean-coin")?.quantity ?? 0;
  }

  function renderSession(nextSession) {
    session = nextSession;
    const subject = nextSession?.playerId || nextSession?.subject;
    playerId.textContent = subject || "로그인 필요";
    grantButton.disabled = !subject;
    if (!subject) {
      balance.textContent = "0";
      status.textContent = "HIVE 로그인 후 사용할 수 있습니다.";
    }
  }

  function renderInventory(inventory, message, equipment = null) {
    balance.textContent = currencyFromInventory(inventory).toLocaleString("ko-KR");
    status.textContent = message;
    window.gameBridge.broadcastInventory(inventory, equipment);
  }

  async function syncInventory(message = "게임과 자동 동기화됩니다.") {
    if (!session || syncing) return;
    syncing = true;
    try {
      const result = await window.gameBridge.getInventory();
      renderInventory(result.inventory, message, result.equipment);
    } catch (error) {
      status.textContent = error instanceof Error ? error.message : "재화 조회에 실패했습니다.";
    } finally {
      syncing = false;
    }
  }

  grantButton.addEventListener("click", async () => {
    grantButton.disabled = true;
    status.textContent = "테스트 재화를 지급하고 있습니다…";
    try {
      const result = await window.gameBridge.createDevGrant();
      renderInventory(
        result.inventory,
        result.duplicate ? "이미 처리된 요청입니다." : "+100 팥 코인을 게임에 반영했습니다.",
        result.equipment
      );
    } catch (error) {
      status.textContent = error instanceof Error ? error.message : "테스트 지급에 실패했습니다.";
    } finally {
      grantButton.disabled = !session;
    }
  });

  window.addEventListener("GAME_SESSION_CHANGED", (event) => {
    renderSession(event.detail?.session ?? null);
    if (session) syncInventory();
  });

  async function configure() {
    const config = await window.gameBridge.getPublicConfig();
    if (!config.storeDevTools) return;
    panel.hidden = false;

    try {
      const result = await window.gameBridge.getSession();
      renderSession(result.session);
      await syncInventory();
    } catch (_error) {
      renderSession(null);
    }

    window.setInterval(() => syncInventory(), 5000);
  }

  configure().catch((error) => {
    panel.hidden = false;
    status.textContent = error instanceof Error ? error.message : "개발 도구를 불러오지 못했습니다.";
  });
})();
