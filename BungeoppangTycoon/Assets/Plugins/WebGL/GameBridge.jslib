mergeInto(LibraryManager.library, {
  GameBridge_RegisterSessionReceiver: function (gameObjectPointer, methodPointer) {
    var gameObject = UTF8ToString(gameObjectPointer);
    var method = UTF8ToString(methodPointer);
    window.GameBridge_SendSessionToUnity = function (value) {
      if (value === "logout") {
        SendMessage(gameObject, "OnHiveLogoutSuccess", "");
      } else if (value === "refresh") {
        SendMessage(gameObject, method, value);
      } else {
        SendMessage(gameObject, "OnHiveLoginSuccess", value);
      }
    };
    if (typeof window.GameBridge_PendingSessionValue === "string") {
      var pendingValue = window.GameBridge_PendingSessionValue;
      window.GameBridge_PendingSessionValue = null;
      window.GameBridge_SendSessionToUnity(pendingValue);
    }
  },

  GameBridge_Login: function (gameObjectPointer, successMethodPointer, errorMethodPointer) {
    var gameObject = UTF8ToString(gameObjectPointer);
    var successMethod = UTF8ToString(successMethodPointer);
    var errorMethod = UTF8ToString(errorMethodPointer);

    if (!window.gameBridge) {
      SendMessage(gameObject, errorMethod, "window.gameBridge가 없습니다. WebGL 템플릿에 game-bridge.js를 추가하세요.");
      return;
    }

    window.gameBridge.loginWithHive()
      .then(function (token) { SendMessage(gameObject, successMethod, token); })
      .catch(function (error) { SendMessage(gameObject, errorMethod, error.message || String(error)); });
  },

  GameBridge_Logout: function (gameObjectPointer, successMethodPointer, errorMethodPointer) {
    var gameObject = UTF8ToString(gameObjectPointer);
    var successMethod = UTF8ToString(successMethodPointer);
    var errorMethod = UTF8ToString(errorMethodPointer);

    window.gameBridge.logout()
      .then(function () { SendMessage(gameObject, successMethod, "ok"); })
      .catch(function (error) { SendMessage(gameObject, errorMethod, error.message || String(error)); });
  },

  GameBridge_OpenShop: function (gameObjectPointer, successMethodPointer, errorMethodPointer) {
    var gameObject = UTF8ToString(gameObjectPointer);
    var successMethod = UTF8ToString(successMethodPointer);
    var errorMethod = UTF8ToString(errorMethodPointer);

    if (!window.gameBridge) {
      SendMessage(gameObject, errorMethod, "window.gameBridge가 없습니다.");
      return;
    }

    window.gameBridge.openHiveWebShop()
      .then(function () { SendMessage(gameObject, successMethod, "closed"); })
      .catch(function (error) { SendMessage(gameObject, errorMethod, error.message || String(error)); });
  },

  GameBridge_OpenNicePay: function (productIdPointer, gameObjectPointer, successMethodPointer, errorMethodPointer) {
    var productId = UTF8ToString(productIdPointer);
    var gameObject = UTF8ToString(gameObjectPointer);
    var successMethod = UTF8ToString(successMethodPointer);
    var errorMethod = UTF8ToString(errorMethodPointer);

    if (!window.gameBridge) {
      SendMessage(gameObject, errorMethod, "window.gameBridge가 없습니다.");
      return;
    }

    window.gameBridge.openNicePayTestCheckout(productId)
      .then(function () { SendMessage(gameObject, successMethod, "paid"); })
      .catch(function (error) { SendMessage(gameObject, errorMethod, error.message || String(error)); });
  }
});
