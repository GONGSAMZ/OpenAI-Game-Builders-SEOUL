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
        throw new Error(message);
      }

      return payload;
    }

    health() {
      return this.request("/api/v1/health");
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
          if (event.data?.type === "HIVE_AUTH_SUCCESS" && event.data.sessionToken) {
            finish(() => {
              this.sessionToken = event.data.sessionToken;
              resolve(event.data.sessionToken);
            });
          } else if (event.data?.type === "HIVE_AUTH_ERROR") {
            finish(() => reject(new Error(event.data.message || "Hive 로그인에 실패했습니다.")));
          }
        };

        const closedWatcher = global.setInterval(() => {
          if (popup.closed) finish(() => reject(new Error("로그인 창이 닫혔습니다.")));
        }, 500);
        const timeout = global.setTimeout(() => {
          popup.close();
          finish(() => reject(new Error("Hive 로그인 시간이 초과되었습니다.")));
        }, 2 * 60 * 1000);

        global.addEventListener("message", onMessage);
      });
    }

    async logout() {
      if (this.sessionToken) {
        await this.request("/api/v1/auth/session", { method: "DELETE" });
      }
      this.sessionToken = null;
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
})(window);
