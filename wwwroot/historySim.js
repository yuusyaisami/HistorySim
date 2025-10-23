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

    // Start manually so we can ignore stale integrity hashes on GitHub Pages.
    Blazor.start({
      loadBootResource: function (type, name, defaultUri, integrity) {
        if (!ns._logOnce) {
          console.log('[HistorySim] loadBootResource override active');
          ns._logOnce = true;
        }
        if (type === 'wasmNative' && name === 'dotnet.native.wasm') {
          console.log('[HistorySim] forcing cache bust for dotnet.native.wasm', integrity);
          return fetch(defaultUri + '?v=' + Date.now(), { cache: 'no-store' });
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
