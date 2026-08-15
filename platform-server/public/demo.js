(function initializeDemo() {
  "use strict";

  const output = document.querySelector("#output");
  const print = (label, value) => {
    const rendered = typeof value === "string" ? value : JSON.stringify(value, null, 2);
    output.textContent = `[${new Date().toLocaleTimeString()}] ${label}\n${rendered}\n\n${output.textContent}`;
  };
  const run = async (label, operation) => {
    try {
      print(label, await operation());
    } catch (error) {
      print(`${label} 실패`, error instanceof Error ? error.message : String(error));
    }
  };

  document.querySelector("#health-button").addEventListener("click", () =>
    run("Health", () => window.gameBridge.health())
  );
  document.querySelector("#login-button").addEventListener("click", () =>
    run("Hive 로그인", async () => {
      await window.gameBridge.loginWithHive();
      return window.gameBridge.getSession();
    })
  );
  document.querySelector("#session-button").addEventListener("click", () =>
    run("세션", () => window.gameBridge.getSession())
  );
  document.querySelector("#logout-button").addEventListener("click", () =>
    run("로그아웃", async () => {
      await window.gameBridge.logout();
      return "완료";
    })
  );
  document.querySelector("#clear-button").addEventListener("click", () => {
    output.textContent = "";
  });
  document.querySelector("#ai-form").addEventListener("submit", (event) => {
    event.preventDefault();
    run("NPC 반응", () =>
      window.gameBridge.createNpcReaction({
        situation: document.querySelector("#situation").value,
        playerAction: document.querySelector("#player-action").value,
        locale: "ko"
      })
    );
  });
})();
