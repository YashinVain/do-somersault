// Bridge between the game and the Yandex Games SDK (https://yandex.ru/games/sdk/v2).
// Results come back through SendMessage to the object named in YG_Init; every call is safe when the SDK is missing
// (a local build, or the game opened outside the platform), it just reports "offline".
mergeInto(LibraryManager.library, {

  YG_Init: function (objectName) {
    var target = UTF8ToString(objectName);
    window.ygTarget = target;
    window.ygSend = function (method, text) {
      if (window.unityGame) window.unityGame.SendMessage(target, method, text || '');
    };

    if (typeof YaGames === 'undefined') { window.ygSend('OnYandexInit', 'offline|ru'); return; }

    YaGames.init().then(function (ysdk) {
      window.ysdk = ysdk;
      var lang = (ysdk.environment && ysdk.environment.i18n && ysdk.environment.i18n.lang) || 'ru';

      // The pause events fire when the platform shows its own overlay or an ad.
      ysdk.on('game_api_pause', function () { window.ygSend('OnYandexPause', '1'); });
      ysdk.on('game_api_resume', function () { window.ygSend('OnYandexPause', '0'); });

      ysdk.getPlayer({ scopes: false }).then(function (player) {
        window.ygPlayer = player;
        window.ygSend('OnYandexInit', 'ok|' + lang);
      }).catch(function () {
        window.ygSend('OnYandexInit', 'ok|' + lang);
      });
    }).catch(function () {
      window.ygSend('OnYandexInit', 'offline|ru');
    });
  },

  YG_Ready: function () {
    if (window.ysdk && window.ysdk.features && window.ysdk.features.LoadingAPI) window.ysdk.features.LoadingAPI.ready();
  },

  YG_GameplayStart: function () {
    if (window.ysdk && window.ysdk.features && window.ysdk.features.GameplayAPI) window.ysdk.features.GameplayAPI.start();
  },

  YG_GameplayStop: function () {
    if (window.ysdk && window.ysdk.features && window.ysdk.features.GameplayAPI) window.ysdk.features.GameplayAPI.stop();
  },

  YG_LoadData: function () {
    if (!window.ygPlayer) { window.ygSend('OnYandexData', ''); return; }
    window.ygPlayer.getData(['save']).then(function (data) {
      window.ygSend('OnYandexData', (data && data.save) ? String(data.save) : '');
    }).catch(function () { window.ygSend('OnYandexData', ''); });
  },

  YG_SaveData: function (json) {
    if (!window.ygPlayer) return;
    window.ygPlayer.setData({ save: UTF8ToString(json) }, false).catch(function () {});
  },

  YG_ShowInterstitial: function () {
    if (!window.ysdk) { window.ygSend('OnAdClosed', '0'); return; }
    window.ysdk.adv.showFullscreenAdv({
      callbacks: {
        onOpen: function () { window.ygSend('OnAdOpened', ''); },
        onClose: function (shown) { window.ygSend('OnAdClosed', shown ? '1' : '0'); },
        onError: function () { window.ygSend('OnAdClosed', '0'); }
      }
    });
  },

  YG_ShowRewarded: function () {
    if (!window.ysdk) { window.ygSend('OnAdClosed', '0'); return; }
    window.ysdk.adv.showRewardedVideo({
      callbacks: {
        onOpen: function () { window.ygSend('OnAdOpened', ''); },
        onRewarded: function () { window.ygSend('OnAdRewarded', ''); },
        onClose: function () { window.ygSend('OnAdClosed', '1'); },
        onError: function () { window.ygSend('OnAdClosed', '0'); }
      }
    });
  },

  YG_SetScore: function (board, score) {
    if (!window.ysdk) return;
    var name = UTF8ToString(board);
    window.ysdk.getLeaderboards().then(function (lb) {
      return lb.setLeaderboardScore(name, score);
    }).catch(function () {});
  },

  YG_LoadLeaderboard: function (board) {
    var name = UTF8ToString(board);
    if (!window.ysdk) { window.ygSend('OnLeaderboard', '{"items":[]}'); return; }
    window.ysdk.getLeaderboards().then(function (lb) {
      return lb.getLeaderboardEntries(name, { quantityTop: 10, includeUser: true, quantityAround: 2 });
    }).then(function (res) {
      var items = (res.entries || []).map(function (e) {
        return { rank: e.rank, name: (e.player && e.player.publicName) || 'Игрок', score: e.score };
      });
      window.ygSend('OnLeaderboard', JSON.stringify({ items: items }));
    }).catch(function () {
      window.ygSend('OnLeaderboard', '{"items":[]}');
    });
  }
});
