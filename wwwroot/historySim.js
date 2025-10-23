(function () {
  const ns = window.historySim = window.historySim || {};

  ns.scrollToBottom = function (element) {
    if (!element) {
      return;
    }
    element.scrollTop = element.scrollHeight;
  };

  function bootBlazor() {
    if (!window.Blazor || ns._bootStarted) {
      return;
    }
    ns._bootStarted = true;

    const versionSuffix = 'v=' + Date.now();
    const withBust = function (uri) {
      return uri + (uri.indexOf('?') >= 0 ? '&' : '?') + versionSuffix;
    };

    Blazor.start({
      loadBootResource: function (type, name, defaultUri) {
        if (!ns._logOnce) {
          console.log('[HistorySim] loadBootResource override active');
          ns._logOnce = true;
        }

        switch (type) {
          case 'dotnetjs': {
            const uri = withBust(defaultUri);
            console.log('[HistorySim] dotnetjs ->', uri);
            return uri;
          }
          case 'wasmNative': {
            if (name === 'dotnet.native.wasm') {
              const uri = withBust(defaultUri);
              console.log('[HistorySim] wasmNative ->', uri);
              return fetch(uri, { cache: 'no-store' });
            }
            break;
          }
          default:
            break;
        }

        return fetch(defaultUri, { cache: 'no-store' });
      }
    }).catch(function (err) {
      console.error('Failed to start Blazor:', err);
    });
  }

  if (document.readyState === 'complete') {
    bootBlazor();
  } else {
    window.addEventListener('load', bootBlazor, { once: true });
  }
})();
