window.historySim = window.historySim || {
  scrollToBottom: element => {
    if (!element) {
      return;
    }
    element.scrollTop = element.scrollHeight;
  }
};
