import { readFileSync } from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

describe("game bridge", () => {
  it("즉시 동기화 메시지에 인벤토리와 장착 상태를 함께 전달한다", () => {
    const listeners = new Map<string, Array<(event?: unknown) => void>>();
    const sentMessages: Array<{ gameObject: string; method: string; payload: string }> = [];
    const storage = new Map<string, string>();
    const fakeWindow = {
      location: { origin: "http://localhost:3000" },
      sessionStorage: {
        getItem: (key: string) => storage.get(key) ?? null,
        setItem: (key: string, value: string) => storage.set(key, value),
        removeItem: (key: string) => storage.delete(key)
      },
      document: { getElementById: () => null },
      addEventListener: (type: string, listener: (event?: unknown) => void) => {
        listeners.set(type, [...(listeners.get(type) ?? []), listener]);
      },
      setInterval,
      clearInterval,
      setTimeout,
      clearTimeout,
      crypto: { randomUUID: () => "00000000-0000-4000-8000-000000000000" },
      unityInstance: {
        SendMessage: (gameObject: string, method: string, payload: string) => {
          sentMessages.push({ gameObject, method, payload });
        }
      }
    };

    const source = readFileSync(path.resolve(process.cwd(), "public/game-bridge.js"), "utf8");
    new Function("window", source)(fakeWindow);

    const inventory = [
      { itemId: "red-bean-coin", quantity: 650 },
      { itemId: "golden-pan", quantity: 1 }
    ];
    const equipment = { moldSkin: "golden-pan" };
    for (const listener of listeners.get("message") ?? []) {
      listener({
        origin: "http://localhost:3000",
        data: { type: "PLATFORM_INVENTORY", inventory, equipment }
      });
    }

    expect(sentMessages).toHaveLength(1);
    expect(sentMessages[0]).toEqual({
      gameObject: "@GamePlatformClient",
      method: "OnInventoryUpdated",
      payload: JSON.stringify({ inventory, equipment })
    });
  });
});
