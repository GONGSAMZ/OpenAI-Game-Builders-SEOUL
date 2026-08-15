(function initializePortal() {
  "use strict";

  const byId = (id) => document.getElementById(id);
  const output = byId("output");
  const state = { config: null, catalog: null, session: null };

  const print = (label, value) => {
    const rendered = typeof value === "string" ? value : JSON.stringify(value, null, 2);
    output.textContent = `[${new Date().toLocaleTimeString()}] ${label}\n${rendered}\n\n${output.textContent}`;
  };

  const run = async (label, operation) => {
    try {
      const result = await operation();
      print(label, result);
      return result;
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      print(`${label} 실패`, message);
      throw error;
    }
  };

  function setServerStatus(ok, text) {
    const node = byId("server-status");
    node.className = `status ${ok ? "" : "error"}`.trim();
    node.textContent = text;
  }

  function renderSession(session) {
    state.session = session;
    const loggedIn = Boolean(session);
    byId("account-message").textContent = loggedIn
      ? "HIVE 연동 게임 세션으로 접속 중입니다."
      : "HIVE 계정으로 가입하거나 로그인하면 재화와 구매 내역을 안전하게 연결할 수 있습니다.";
    byId("account-details").hidden = !loggedIn;
    byId("account-login-button").hidden = loggedIn;
    byId("login-button").hidden = loggedIn;
    byId("logout-button").hidden = !loggedIn;

    if (session) {
      byId("account-id").textContent = session.playerId || session.subject;
      byId("account-expiry").textContent = new Date(session.expiresAt).toLocaleTimeString();
    }
  }

  function renderInventory(entries) {
    const inventory = byId("inventory");
    if (!state.session) {
      inventory.textContent = "로그인하면 보유 아이템이 표시됩니다.";
      return;
    }
    if (!entries.length) {
      inventory.textContent = "보유 아이템이 없습니다. 데모 상품을 선택해 지급 흐름을 확인하세요.";
      return;
    }
    inventory.textContent = `보유 아이템 · ${entries.map((item) => `${item.itemId} × ${item.quantity}`).join(" · ")}`;
  }

  async function refreshInventory() {
    if (!state.session) return renderInventory([]);
    const { inventory } = await window.gameBridge.getInventory();
    renderInventory(inventory);
  }

  async function purchase(productId, button) {
    if (!state.session) await login();
    button.disabled = true;
    try {
      const result = await run("Mock 구매", () => window.gameBridge.createMockPurchase(productId));
      renderInventory(result.inventory);
    } finally {
      button.disabled = false;
    }
  }

  function renderCatalog(catalog) {
    state.catalog = catalog;
    const grid = byId("product-grid");
    grid.replaceChildren();

    for (const [index, product] of catalog.products.entries()) {
      const article = document.createElement("article");
      article.className = "product-card";
      const icon = document.createElement("div");
      icon.className = "product-icon";
      icon.textContent = ["豆", "福", "金"][index] || "魚";
      const title = document.createElement("h3");
      title.textContent = product.name;
      const description = document.createElement("p");
      description.textContent = product.description;
      const footer = document.createElement("footer");
      const price = document.createElement("strong");
      price.textContent = product.priceLabel;
      const button = document.createElement("button");
      button.type = "button";
      button.textContent = catalog.mode === "mock" ? "데모 구매" : "웹 상점에서 보기";
      button.addEventListener("click", () => {
        if (catalog.mode === "mock") {
          purchase(product.id, button).catch(() => undefined);
        } else if (state.config?.hiveWebShopUrl) {
          window.open(state.config.hiveWebShopUrl, "_blank", "noopener");
        }
      });
      footer.append(price, button);
      article.append(icon, title, description, footer);
      grid.append(article);
    }
  }

  async function login() {
    await run("HIVE 가입·로그인", () => window.gameBridge.loginWithHive());
    const { session } = await window.gameBridge.getSession();
    renderSession(session);
    await refreshInventory();
    return session;
  }

  async function restoreSession() {
    if (!window.gameBridge.sessionToken) return renderSession(null);
    try {
      const { session } = await window.gameBridge.getSession();
      renderSession(session);
      await refreshInventory();
    } catch (_error) {
      window.gameBridge.sessionToken = null;
      renderSession(null);
    }
  }

  async function bootstrap() {
    try {
      await window.gameBridge.health();
      setServerStatus(true, "서버 연결됨");
    } catch (error) {
      setServerStatus(false, "서버 연결 실패");
      print("Health 실패", error instanceof Error ? error.message : String(error));
    }

    const [config, catalog] = await Promise.all([
      window.gameBridge.getPublicConfig(),
      window.gameBridge.getStoreCatalog()
    ]);
    state.config = config;
    byId("integration-mode").textContent = `HIVE ${config.hiveMode} · 상점 ${config.storeMode}`;
    if (config.hiveWebShopUrl) {
      const link = byId("web-shop-link");
      link.href = config.hiveWebShopUrl;
      link.hidden = false;
      byId("market-notice").textContent = "실제 결제는 HIVE 웹 상점에서 처리되며 게임 서버는 사용자·아이템 정보를 연결합니다.";
    }
    renderCatalog(catalog);
    await restoreSession();
    print("포털", config);
  }

  byId("health-button").addEventListener("click", () => run("Health", () => window.gameBridge.health()).catch(() => undefined));
  byId("session-button").addEventListener("click", () => run("세션", () => window.gameBridge.getSession()).catch(() => undefined));
  byId("login-button").addEventListener("click", () => login().catch(() => undefined));
  byId("account-login-button").addEventListener("click", () => login().catch(() => undefined));
  byId("logout-button").addEventListener("click", () => run("로그아웃", async () => {
    await window.gameBridge.logout();
    renderSession(null);
    renderInventory([]);
    return "완료";
  }).catch(() => undefined));
  byId("clear-button").addEventListener("click", () => { output.textContent = ""; });
  byId("ai-form").addEventListener("submit", (event) => {
    event.preventDefault();
    run("NPC 반응", () => window.gameBridge.createNpcReaction({
      situation: byId("situation").value,
      playerAction: byId("player-action").value,
      locale: "ko"
    })).then((result) => { byId("ai-output").textContent = result.text; }).catch((error) => {
      byId("ai-output").textContent = error instanceof Error ? error.message : String(error);
    });
  });

  bootstrap().catch((error) => {
    setServerStatus(false, "초기화 실패");
    print("초기화 실패", error instanceof Error ? error.message : String(error));
  });
})();
