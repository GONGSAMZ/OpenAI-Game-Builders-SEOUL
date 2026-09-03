(function attachGameBridge(global) {
  "use strict";

  class GameBridgeClient {
    constructor(options = {}) {
      this.baseUrl = (options.baseUrl || global.location.origin).replace(/\/$/, "");
      this.storageKey = options.storageKey || "game_session_token";
      this.serverOrigin = new URL(this.baseUrl).origin;
    }

    get sessionToken() {
      return global.sessionStorage.getItem(this.storageKey);
    }

    set sessionToken(value) {
      if (value) global.sessionStorage.setItem(this.storageKey, value);
      else global.sessionStorage.removeItem(this.storageKey);
    }

    async request(path, options = {}) {
      const headers = new Headers(options.headers || {});
      if (options.body && !headers.has("content-type")) headers.set("content-type", "application/json");
      if (this.sessionToken) headers.set("authorization", `Bearer ${this.sessionToken}`);

      const response = await fetch(`${this.baseUrl}${path}`, {
        ...options,
        headers,
        credentials: "include"
      });
      const isJson = response.headers.get("content-type")?.includes("application/json");
      const payload = isJson ? await response.json() : await response.text();

      if (!response.ok) {
        const message = payload?.error?.message || `HTTP ${response.status}`;
        const error = new Error(message);
        error.status = response.status;
        error.payload = payload;
        throw error;
      }

      return payload;
    }

    health() {
      return this.request("/api/v1/health");
    }

    getPublicConfig() {
      return this.request("/api/v1/config/public");
    }

    getSession() {
      return this.request("/api/v1/auth/session");
    }

    async loginWithHive() {
      const popup = global.open("about:blank", "hive-login", "popup,width=520,height=720");
      if (!popup) throw new Error("로그인 팝업이 차단되었습니다.");
      popup.document.title = "Hive 로그인 준비 중";

      try {
        const { loginUrl } = await this.request("/api/v1/auth/hive/login");
        popup.location.replace(loginUrl);
      } catch (error) {
        popup.close();
        throw error;
      }

      return new Promise((resolve, reject) => {
        let settled = false;
        let closeGrace = null;
        let sessionPoller = null;
        let sessionPollInFlight = false;

        const finish = (callback) => {
          if (settled) return;
          settled = true;
          global.removeEventListener("message", onMessage);
          global.clearInterval(closedWatcher);
          if (sessionPoller) global.clearInterval(sessionPoller);
          global.clearTimeout(timeout);
          if (closeGrace) global.clearTimeout(closeGrace);
          callback();
        };

        const onMessage = (event) => {
          if (event.origin !== this.serverOrigin) return;
          if (event.data?.type === "HIVE_AUTH_SUCCESS" && event.data.sessionToken) {
            finish(() => {
              this.sessionToken = event.data.sessionToken;
              this.notifyParentSession(true);
              try {
                event.source?.postMessage({ type: "HIVE_AUTH_ACK" }, event.origin);
              } catch (_error) {
                // The callback window may already be closing; the session is still valid.
              }
              resolve(event.data.sessionToken);
            });
          } else if (event.data?.type === "HIVE_AUTH_ERROR") {
            finish(() => {
              try {
                event.source?.postMessage({ type: "HIVE_AUTH_ACK" }, event.origin);
              } catch (_error) {
                // The callback window may already be closing.
              }
              reject(new Error(event.data.message || "Hive 로그인에 실패했습니다."));
            });
          }
        };

        const recoverSession = async () => {
          if (settled || sessionPollInFlight) return;
          sessionPollInFlight = true;
          try {
            await this.getSession();
            finish(() => {
              if (!popup.closed) popup.close();
              this.notifyParentSession(true);
              resolve(this.sessionToken);
            });
          } catch (_error) {
            // The callback may still be processing on the server.
          } finally {
            sessionPollInFlight = false;
          }
        };

        const closedWatcher = global.setInterval(() => {
          if (popup.closed && !closeGrace) {
            closeGrace = global.setTimeout(
              () => finish(() => reject(new Error("로그인 창이 닫혔습니다."))),
              4000
            );
          }
        }, 500);
        sessionPoller = global.setInterval(recoverSession, 500);
        recoverSession();
        const timeout = global.setTimeout(() => {
          popup.close();
          finish(() => reject(new Error("Hive 로그인 시간이 초과되었습니다.")));
        }, 2 * 60 * 1000);

        global.addEventListener("message", onMessage);
      });
    }

    async logout() {
      try {
        await this.request("/api/v1/auth/session", { method: "DELETE" });
      } finally {
        this.sessionToken = null;
        this.notifyParentSession(false);
      }
    }

    broadcastSession(authenticated, sessionToken = this.sessionToken) {
      const message = {
        type: "PLATFORM_SESSION",
        authenticated: Boolean(authenticated),
        sessionToken: authenticated && sessionToken ? sessionToken : null
      };
      const gameFrame = global.document.getElementById("game-frame");
      if (gameFrame?.contentWindow) {
        let deliveredDirectly = false;
        try {
          const frameOrigin = new URL(gameFrame.src || global.location.href, global.location.href).origin;
          if (
            frameOrigin === this.serverOrigin &&
            typeof gameFrame.contentWindow.GameBridge_ApplySession === "function"
          ) {
            gameFrame.contentWindow.GameBridge_ApplySession(message);
            deliveredDirectly = true;
          }
        } catch (_error) {
          // Cross-origin frames use the validated postMessage path below.
        }
        if (!deliveredDirectly)
          gameFrame.contentWindow.postMessage(message, this.serverOrigin);
      } else {
        deliverSessionToUnity(message);
      }
    }

    notifyParentSession(authenticated) {
      if (!global.parent || global.parent === global) return;
      global.parent.postMessage({
        type: "PLATFORM_SESSION",
        authenticated: Boolean(authenticated),
        sessionToken: authenticated && this.sessionToken ? this.sessionToken : null
      }, this.serverOrigin);
    }

    getStoreCatalog() {
      return this.request("/api/v1/store/catalog");
    }

    getInventory() {
      return this.request("/api/v1/store/me");
    }

    createMockPurchase(productId, idempotencyKey = global.crypto.randomUUID()) {
      return this.request("/api/v1/store/mock-purchases", {
        method: "POST",
        body: JSON.stringify({ productId, idempotencyKey })
      });
    }

    async openNicePayTestCheckout(productId) {
      const popup = global.open("about:blank", "nicepay-test", "popup,width=520,height=760");
      if (!popup) throw new Error("NICEPAY 테스트 결제 팝업이 차단되었습니다.");
      popup.document.title = "NICEPAY 테스트 결제 준비 중";

      try {
        const order = await this.request("/api/v1/store/nicepay/orders", {
          method: "POST",
          body: JSON.stringify({ productId })
        });
        popup.location.replace(new URL(order.checkoutUrl, this.baseUrl).toString());
      } catch (error) {
        popup.close();
        throw error;
      }

      await new Promise((resolve, reject) => {
        let settled = false;
        const finish = (callback) => {
          if (settled) return;
          settled = true;
          global.removeEventListener("message", onMessage);
          global.clearInterval(closedWatcher);
          global.clearTimeout(timeout);
          callback();
        };
        const onMessage = (event) => {
          if (event.origin !== this.serverOrigin) return;
          if (event.data?.type === "NICEPAY_PAYMENT_SUCCESS") {
            finish(resolve);
          } else if (event.data?.type === "NICEPAY_PAYMENT_ERROR") {
            finish(() => reject(new Error(event.data.message || "NICEPAY 테스트 결제에 실패했습니다.")));
          }
        };
        const closedWatcher = global.setInterval(() => {
          if (popup.closed) finish(() => reject(new Error("NICEPAY 테스트 결제창이 닫혔습니다.")));
        }, 500);
        const timeout = global.setTimeout(() => {
          popup.close();
          finish(() => reject(new Error("NICEPAY 테스트 결제 시간이 초과되었습니다.")));
        }, 10 * 60 * 1000);
        global.addEventListener("message", onMessage);
      });

      if (!popup.closed) popup.close();
      const state = await this.getInventory();
      this.broadcastInventory(state.inventory, state.equipment, state.wallet);
      return state;
    }

    createDevGrant(productId = "red-bean-100", idempotencyKey = global.crypto.randomUUID()) {
      return this.request("/api/v1/store/dev-grants", {
        method: "POST",
        body: JSON.stringify({ productId, idempotencyKey })
      });
    }

    creditDevTestPoints(amount = 10000, idempotencyKey = global.crypto.randomUUID()) {
      return this.request("/api/v1/store/dev-test-points", {
        method: "POST",
        body: JSON.stringify({ amount, idempotencyKey })
      });
    }

    async openHiveWebShop() {
      const popup = global.open("about:blank", "hive-web-shop", "popup,width=1180,height=820");
      if (!popup) throw new Error("상점 팝업이 차단되었습니다.");
      popup.document.title = "HIVE 웹 상점 준비 중";
      try {
        const config = await this.getPublicConfig();
        if (!config.hiveWebShopUrl) throw new Error("HIVE 웹 상점 URL이 설정되지 않았습니다.");
        popup.location.replace(config.hiveWebShopUrl);
      } catch (error) {
        popup.close();
        throw error;
      }

      await new Promise((resolve) => {
        const closedWatcher = global.setInterval(() => {
          if (!popup.closed) return;
          global.clearInterval(closedWatcher);
          resolve();
        }, 500);
      });
    }

    broadcastInventory(inventory, equipment = null, wallet = null) {
      const message = { type: "PLATFORM_INVENTORY", inventory, equipment, wallet };
      const gameFrame = global.document.getElementById("game-frame");
      if (gameFrame?.contentWindow) {
        gameFrame.contentWindow.postMessage(message, this.serverOrigin);
      } else {
        deliverInventoryToUnity(message);
      }
    }

    createNpcReaction(input) {
      return this.request("/api/v1/ai/npc-reaction", {
        method: "POST",
        body: JSON.stringify(input)
      });
    }
  }

  global.GameBridgeClient = GameBridgeClient;
  global.gameBridge = new GameBridgeClient();

  let pendingInventoryMessage = null;
  let pendingSessionMessage = null;

  function deliverInventoryToUnity(message) {
    if (!Array.isArray(message?.inventory)) return;
    if (!global.unityInstance?.SendMessage) {
      pendingInventoryMessage = message;
      return;
    }
    global.unityInstance.SendMessage(
      "@GamePlatformClient",
      "OnInventoryUpdated",
      JSON.stringify({
        inventory: message.inventory,
        equipment: message.equipment ?? null,
        wallet: message.wallet ?? null
      })
    );
    pendingInventoryMessage = null;
  }

  function deliverSessionToUnity(message) {
    if (message?.type !== "PLATFORM_SESSION") return;
    if (global.parent && global.parent !== global) {
      if (message.authenticated && message.sessionToken) {
        global.gameBridge.sessionToken = message.sessionToken;
      } else if (!message.authenticated) {
        // Each iframe has its own sessionStorage. Revoke and clear the game's
        // bearer session as well as the shell session so stale accounts cannot linger.
        global.gameBridge.logout().catch(() => {
          global.gameBridge.sessionToken = null;
        });
      }
    }
    const relayValue = !message.authenticated
      ? "logout"
      : message.sessionToken || "refresh";
    if (typeof global.GameBridge_SendSessionToUnity === "function") {
      global.GameBridge_SendSessionToUnity(relayValue);
      pendingSessionMessage = null;
      return;
    }
    global.GameBridge_PendingSessionValue = relayValue;
    if (!global.unityInstance?.SendMessage) {
      pendingSessionMessage = message;
      return;
    }
    const method = !message.authenticated
      ? "OnHiveLogoutSuccess"
      : message.sessionToken
        ? "OnHiveLoginSuccess"
        : "OnExternalSessionChanged";
    const payload = !message.authenticated
      ? ""
      : message.sessionToken || "refresh";
    global.unityInstance.SendMessage(
      "@GamePlatformClient",
      method,
      payload
    );
    global.GameBridge_PendingSessionValue = null;
    pendingSessionMessage = null;
  }

  // The shell and game iframe are normally same-origin. A direct entry point avoids
  // browser-specific postMessage loss while the WebGL canvas owns pointer focus;
  // cross-origin deployments continue to use the origin-checked message listener.
  global.GameBridge_ApplySession = deliverSessionToUnity;

  global.addEventListener("message", (event) => {
    if (event.origin !== global.gameBridge.serverOrigin) return;
    if (event.data?.type === "PLATFORM_INVENTORY") deliverInventoryToUnity(event.data);
    if (event.data?.type === "PLATFORM_SESSION") deliverSessionToUnity(event.data);
  });

  global.addEventListener("UNITY_INSTANCE_READY", () => {
    if (pendingInventoryMessage) deliverInventoryToUnity(pendingInventoryMessage);
    if (pendingSessionMessage) deliverSessionToUnity(pendingSessionMessage);
  });
})(window);
