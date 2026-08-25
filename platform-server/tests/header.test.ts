import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { describe, expect, it, vi } from "vitest";

class FakeElement {
  public hidden = true;
  public disabled = false;
  public title = "";
  public textContent = "";
  public href = "";
  public contentWindow = {};
  public readonly classList = { toggle: vi.fn() };
  private readonly listeners = new Map<string, Array<(event?: any) => void>>();

  public setAttribute() {}
  public contains() { return false; }
  public addEventListener(name: string, listener: (event?: any) => void) {
    this.listeners.set(name, [...(this.listeners.get(name) ?? []), listener]);
  }
  public dispatch(name: string, event?: any) {
    for (const listener of this.listeners.get(name) ?? []) listener(event);
  }
}

function deferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (reason?: unknown) => void;
  const promise = new Promise<T>((resolvePromise, rejectPromise) => {
    resolve = resolvePromise;
    reject = rejectPromise;
  });
  return { promise, resolve, reject };
}

async function flush() {
  await Promise.resolve();
  await Promise.resolve();
}

describe("header bootstrap session boundary", () => {
  it("세션 조회 전 iframe load를 로그아웃으로 방송하지 않는다", async () => {
    const elementIds = [
      "login-button", "login-label", "account-avatar", "account-shell", "account-menu",
      "account-player-id", "account-provider", "logout-button", "shop-link", "game-frame"
    ];
    const elements = new Map(elementIds.map((id) => [id, new FakeElement()]));
    const sessionRequest = deferred<{ session: { subject: string; playerId: string; provider: string } }>();
    const broadcasts: boolean[] = [];
    const document = {
      getElementById: (id: string) => elements.get(id) ?? null,
      addEventListener: vi.fn()
    };
    const window = {
      gameBridge: {
        serverOrigin: "https://example.test",
        getPublicConfig: () => Promise.reject(new Error("temporary config failure")),
        getSession: () => sessionRequest.promise,
        broadcastSession: (signedIn: boolean) => broadcasts.push(signedIn),
        loginWithHive: vi.fn(),
        logout: vi.fn()
      },
      dispatchEvent: vi.fn(),
      addEventListener: vi.fn()
    };
    class FakeCustomEvent {
      public constructor(public readonly name: string, public readonly init: unknown) {}
    }
    const source = readFileSync(
      fileURLToPath(new URL("../public/header.js", import.meta.url)),
      "utf8"
    );
    new Function("document", "window", "CustomEvent", source)(document, window, FakeCustomEvent);

    elements.get("game-frame")!.dispatch("load");
    await flush();
    expect(broadcasts).toEqual([]);

    sessionRequest.resolve({
      session: { subject: "subject-a", playerId: "player-a", provider: "hive" }
    });
    await flush();
    expect(broadcasts).toEqual([true]);
  });
});
