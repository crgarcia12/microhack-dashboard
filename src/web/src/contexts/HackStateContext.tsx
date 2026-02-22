'use client';

import { createContext, useContext, useState, useEffect, useCallback, ReactNode, useRef } from 'react';
import { api } from '@/lib/api';
import { HubConnectionBuilder, LogLevel, HubConnection } from '@microsoft/signalr';

export interface HackState {
  status: 'not_started' | 'configuration' | 'waiting' | 'active' | 'completed';
  startedAt: string | null;
  configuredBy: string | null;
  updatedAt: string;
}

interface HackStateContextType {
  hackState: HackState | null;
  loading: boolean;
  refetch: () => Promise<void>;
}

const HackStateContext = createContext<HackStateContextType>({
  hackState: null,
  loading: true,
  refetch: async () => {},
});

export function useHackState() {
  return useContext(HackStateContext);
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

export function HackStateProvider({ children }: { children: ReactNode }) {
  const [hackState, setHackState] = useState<HackState | null>(null);
  const [loading, setLoading] = useState(true);
  const connectionRef = useRef<HubConnection | null>(null);

  const fetchHackState = useCallback(async () => {
    try {
      const state = await api.get<HackState>('/api/hack/state');
      setHackState(state);
    } catch (error) {
      console.error('Failed to fetch hack state:', error);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchHackState();
  }, [fetchHackState]);

  // Setup SignalR connection for hack state updates
  useEffect(() => {
    let disposed = false;
    let connection: HubConnection | null = null;

    const setupConnection = async () => {
      if (disposed) return;

      const apiUrl = await getApiUrl();
      const hubUrl = apiUrl ? `${apiUrl}/hubs/progress` : `/hubs/progress`;

      connection = new HubConnectionBuilder()
        .withUrl(hubUrl, { withCredentials: true })
        .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
        .configureLogging(LogLevel.Warning)
        .build();

      connectionRef.current = connection;

      connection.on('hackStateChanged', (state: HackState) => {
        console.log('Hack state changed:', state);
        setHackState(state);
      });

      connection.on('hackLaunched', (state: HackState) => {
        console.log('Hack launched!', state);
        setHackState(state);
      });

      try {
        await connection.start();
        console.log('SignalR connected for hack state updates');
      } catch (err) {
        console.error('Failed to connect SignalR for hack state:', err);
      }
    };

    const timer = setTimeout(setupConnection, 1000);

    return () => {
      disposed = true;
      clearTimeout(timer);
      connection?.stop();
    };
  }, []);

  return (
    <HackStateContext.Provider value={{ hackState, loading, refetch: fetchHackState }}>
      {children}
    </HackStateContext.Provider>
  );
}
