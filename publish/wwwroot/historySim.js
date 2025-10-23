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

    Blazor.start({
      loadBootResource: function (_, __, defaultUri) {
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
