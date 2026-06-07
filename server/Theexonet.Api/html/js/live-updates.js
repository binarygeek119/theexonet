const MAX_RECONNECT_DELAY_MS = 60000;

export function initLiveUpdates({
  apiBaseUrl,
  getToken,
  onEvent,
  onConnectionChange,
}) {
  let source = null;
  let reconnectTimer = null;
  let reconnectDelayMs = 1000;
  let stopped = true;

  function clearReconnectTimer() {
    if (reconnectTimer) {
      clearTimeout(reconnectTimer);
      reconnectTimer = null;
    }
  }

  function scheduleReconnect() {
    clearReconnectTimer();
    reconnectTimer = setTimeout(() => {
      reconnectTimer = null;
      reconnectDelayMs = Math.min(reconnectDelayMs * 2, MAX_RECONNECT_DELAY_MS);
      connect();
    }, reconnectDelayMs);
  }

  function connect() {
    if (stopped) {
      return;
    }

    const token = getToken();
    if (!token) {
      return;
    }

    const base = String(apiBaseUrl ?? "").replace(/\/$/, "");
    const url = `${base}/api/live/events?token=${encodeURIComponent(token)}`;
    source = new EventSource(url);

    source.onopen = () => {
      reconnectDelayMs = 1000;
      onConnectionChange?.(true);
    };

    source.onmessage = (event) => {
      try {
        const payload = JSON.parse(event.data);
        onEvent?.(payload);
      } catch (error) {
        console.warn("[theexonet] live update parse failed", error);
      }
    };

    source.onerror = () => {
      onConnectionChange?.(false);
      source?.close();
      source = null;
      if (!stopped) {
        scheduleReconnect();
      }
    };
  }

  function start() {
    stopped = false;
    reconnectDelayMs = 1000;
    clearReconnectTimer();
    connect();
  }

  function stop() {
    stopped = true;
    clearReconnectTimer();
    source?.close();
    source = null;
    onConnectionChange?.(false);
  }

  return { start, stop };
}
