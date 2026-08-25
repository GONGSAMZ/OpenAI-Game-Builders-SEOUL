(function initializeStoreDevTools() {
  "use strict";

  const panel = document.getElementById("store-dev-tools");
  const playerId = document.getElementById("dev-player-id");
  const balance = document.getElementById("dev-currency-balance");
  const pointBalance = document.getElementById("dev-test-point-balance");
  const pointButton = document.getElementById("dev-test-point-button");
  const purchaseButtons = [...document.querySelectorAll("[data-dev-product-id]")];
  const actionButtons = [pointButton, ...purchaseButtons];
  const status = document.getElementById("dev-grant-status");
  let session = null;
  let syncing = false;
  let storeMode = "mock";

  function currencyFromInventory(inventory) {
    return inventory.find((entry) => entry.itemId === "red-bean-coin")?.quantity ?? 0;
  }

  function renderSession(nextSession) {
    session = nextSession;
    const subject = nextSession?.playerId || nextSession?.subject;
    const accountLabel = nextSession?.accountLabel || (
      nextSession?.provider === "hive" && subject
        ? `HIVE 계정 · ${subject.slice(-6)}`
        : subject
    );
    playerId.textContent = accountLabel || "로그인 필요";
    actionButtons.forEach((button) => { button.disabled = !subject; });
    if (!subject) {
      balance.textContent = "0";
      pointBalance.textContent = "0 P";
      status.textContent = "HIVE 로그인 후 사용할 수 있습니다.";
    }
  }

  function renderState(inventory, wallet, message, equipment = null) {
    balance.textContent = currencyFromInventory(inventory).toLocaleString("ko-KR");
    pointBalance.textContent = `${(wallet?.testPoints ?? 0).toLocaleString("ko-KR")} P`;
    status.textContent = message;
    window.gameBridge.broadcastInventory(inventory, equipment, wallet);
  }

  async function syncInventory(message = "게임과 자동 동기화됩니다.") {
    if (!session || syncing) return;
    syncing = true;
    try {
      const result = await window.gameBridge.getInventory();
      renderState(result.inventory, result.wallet, message, result.equipment);
    } catch (error) {
      status.textContent = error instanceof Error ? error.message : "재화 조회에 실패했습니다.";
    } finally {
      syncing = false;
    }
  }

  pointButton.addEventListener("click", async () => {
    actionButtons.forEach((button) => { button.disabled = true; });
    status.textContent = "테스트 포인트를 충전하고 있습니다…";
    try {
      const result = await window.gameBridge.creditDevTestPoints();
      renderState(
        result.inventory,
        result.wallet,
        result.duplicate ? "이미 처리된 충전 요청입니다." : "+10,000 테스트 포인트를 충전했습니다.",
        result.equipment
      );
    } catch (error) {
      status.textContent = error instanceof Error ? error.message : "테스트 포인트 충전에 실패했습니다.";
    } finally {
      actionButtons.forEach((button) => { button.disabled = !session; });
    }
  });

  purchaseButtons.forEach((button) => {
    button.addEventListener("click", async () => {
      actionButtons.forEach((action) => { action.disabled = true; });
      status.textContent = `${button.dataset.productName || "상품"} 테스트 결제를 처리하고 있습니다…`;
      try {
        const result = storeMode === "nicepay-test"
          ? await window.gameBridge.openNicePayTestCheckout(button.dataset.devProductId)
          : await window.gameBridge.createMockPurchase(button.dataset.devProductId);
        renderState(
          result.inventory,
          result.wallet,
          result.duplicate ? "이미 처리된 결제 요청입니다." : "테스트 결제가 게임 계정에 반영됐습니다.",
          result.equipment
        );
      } catch (error) {
        status.textContent = error instanceof Error ? error.message : "테스트 결제에 실패했습니다.";
      } finally {
        actionButtons.forEach((action) => { action.disabled = !session; });
      }
    });
  });

  window.addEventListener("GAME_SESSION_CHANGED", (event) => {
    renderSession(event.detail?.session ?? null);
    if (session) syncInventory();
  });

  async function configure() {
    const config = await window.gameBridge.getPublicConfig();
    if (!config.storeDevTools) return;
    storeMode = config.storeMode;
    if (storeMode === "nicepay-test") {
      purchaseButtons.forEach((button) => {
        const price = button.querySelector("strong");
        if (price) price.textContent = price.textContent.replace(" P", "원");
      });
    }
    panel.hidden = false;

    try {
      const result = await window.gameBridge.getSession();
      renderSession(result.session);
      await syncInventory();
    } catch (_error) {
      renderSession(null);
    }

    window.setInterval(() => syncInventory(), 5000);
    window.addEventListener("focus", () => syncInventory("최신 재화가 게임에 반영됐습니다."));
    document.addEventListener("visibilitychange", () => {
      if (document.visibilityState === "visible")
        syncInventory("최신 재화가 게임에 반영됐습니다.");
    });
  }

  configure().catch((error) => {
    panel.hidden = false;
    status.textContent = error instanceof Error ? error.message : "개발 도구를 불러오지 못했습니다.";
  });
})();
