'use client';

import { useEffect, useRef, useCallback, useState } from 'react';
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';

const POLL_INTERVAL = 5000;

export interface TeamProgress {
  teamId: string;
  currentStep: number;
  totalChallenges: number;
  completedChallenges: number;
  completed: boolean;
}

interface UseSignalROptions {
  onProgressUpdated: (progress: TeamProgress) => void;
  enabled?: boolean;
}

async function getApiUrl(): Promise<string> {
  try {
    const res = await fetch('/config');
    if (res.ok) {
      const data = await res.json();
      return data.apiUrl || '';
    }
  } catch {
    // fall through
  }
  return '';
}

export function useSignalR({ onProgressUpdated, enabled = true }: UseSignalROptions) {
  const [connected, setConnected] = useState(false);
  const connectionRef = useRef<ReturnType<typeof HubConnectionBuilder.prototype.build> | null>(null);
  const pollRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const callbackRef = useRef(onProgressUpdated);
  callbackRef.current = onProgressUpdated;

  const startPolling = useCallback(() => {
    if (pollRef.current) return;
    pollRef.current = setInterval(async () => {
      try {
        const res = await fetch(`/api/teams/progress`, { credentials: 'include' });
        if (res.ok) {
          const data: TeamProgress = await res.json();
          callbackRef.current(data);
        }
      } catch {
        // Silently continue polling
      }
    }, POLL_INTERVAL);
  }, []);

  const stopPolling = useCallback(() => {
    if (pollRef.current) {
      clearInterval(pollRef.current);
      pollRef.current = null;
    }
  }, []);

  useEffect(() => {
    if (!enabled) return;

    let disposed = false;
    let connection: ReturnType<typeof HubConnectionBuilder.prototype.build> | null = null;

    // Delay connection to survive React StrictMode's mount/unmount/remount cycle
    const timer = setTimeout(async () => {
      if (disposed) return;

      // Connect SignalR directly to the API for full WebSocket support
      const apiUrl = await getApiUrl();
      const hubUrl = apiUrl ? `${apiUrl}/hubs/progress` : `/hubs/progress`;

      connection = new HubConnectionBuilder()
        .withUrl(hubUrl, { withCredentials: true })
        .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
        .configureLogging(LogLevel.Warning)
        .build();

      connectionRef.current = connection;

      connection.on('progressUpdated', (progress: TeamProgress) => {
        callbackRef.current(progress);
      });

      connection.onclose(() => {
        if (disposed) return;
        setConnected(false);
        startPolling();
      });

      connection.onreconnecting(() => {
        setConnected(false);
        startPolling();
      });

      connection.onreconnected(async () => {
        setConnected(true);
        stopPolling();
        try {
          const res = await fetch(`/api/teams/progress`, { credentials: 'include' });
          if (res.ok) {
            const data: TeamProgress = await res.json();
            callbackRef.current(data);
          }
        } catch {
          // Ignore
        }
      });

      connection
        .start()
        .then(async () => {
          if (disposed) { connection?.stop(); return; }
          setConnected(true);
          stopPolling();
          // Catch-up: fetch latest progress in case events were missed during connection
          try {
            const res = await fetch(`/api/teams/progress`, { credentials: 'include' });
            if (res.ok) {
              const data: TeamProgress = await res.json();
              callbackRef.current(data);
            }
          } catch {
            // Ignore
          }
        })
        .catch(() => {
          if (disposed) return;
          setConnected(false);
          startPolling();
        });
    }, 1000);

    return () => {
      disposed = true;
      clearTimeout(timer);
      stopPolling();
      connection?.stop();
    };
  }, [enabled, startPolling, stopPolling]);

  return { connected };
}
