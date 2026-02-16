'use client';

import { useEffect, useState, useCallback, useRef } from 'react';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Button from '@mui/material/Button';
import CircularProgress from '@mui/material/CircularProgress';
import Table from '@mui/material/Table';
import TableBody from '@mui/material/TableBody';
import TableCell from '@mui/material/TableCell';
import TableContainer from '@mui/material/TableContainer';
import TableHead from '@mui/material/TableHead';
import TableRow from '@mui/material/TableRow';
import PlayArrowIcon from '@mui/icons-material/PlayArrow';
import StopIcon from '@mui/icons-material/Stop';
import RestartAltIcon from '@mui/icons-material/RestartAlt';
import { api } from '@/lib/api';

interface ManualTimer {
  status: 'running' | 'stopped';
  startedAt: string | null;
  elapsed: number;
}

interface TimerState {
  automatic: {
    timerStartedAt: string | null;
    challengeTimes: Record<string, number>;
  };
  manual: ManualTimer;
}

function formatTime(totalSeconds: number): string {
  const h = Math.floor(totalSeconds / 3600);
  const m = Math.floor((totalSeconds % 3600) / 60);
  const s = Math.floor(totalSeconds % 60);
  return [h, m, s].map((v) => String(v).padStart(2, '0')).join(':');
}

function computeElapsed(manual: ManualTimer): number {
  if (manual.status === 'running' && manual.startedAt) {
    const diff = (Date.now() - new Date(manual.startedAt).getTime()) / 1000;
    return manual.elapsed + Math.max(0, Math.floor(diff));
  }
  return manual.elapsed;
}

export default function TimerPage() {
  const [timerState, setTimerState] = useState<TimerState | null>(null);
  const [loading, setLoading] = useState(true);
  const [displaySeconds, setDisplaySeconds] = useState(0);
  const [actionLoading, setActionLoading] = useState(false);
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);

  const fetchTimer = useCallback(async () => {
    try {
      const data = await api.get<TimerState>('/api/timer');
      setTimerState(data);
      setDisplaySeconds(computeElapsed(data.manual));
    } catch {
      // Auth errors handled by layout redirect
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchTimer();
  }, [fetchTimer]);

  // Tick every second while running
  useEffect(() => {
    if (intervalRef.current) {
      clearInterval(intervalRef.current);
      intervalRef.current = null;
    }

    if (timerState?.manual.status === 'running') {
      intervalRef.current = setInterval(() => {
        setDisplaySeconds(computeElapsed(timerState.manual));
      }, 1000);
    }

    return () => {
      if (intervalRef.current) clearInterval(intervalRef.current);
    };
  }, [timerState]);

  const handleStart = async () => {
    setActionLoading(true);
    try {
      await api.post('/api/timer/manual/start');
      await fetchTimer();
    } catch {
      // 409 or other — refresh state
      await fetchTimer();
    } finally {
      setActionLoading(false);
    }
  };

  const handleStop = async () => {
    setActionLoading(true);
    try {
      await api.post('/api/timer/manual/stop');
      await fetchTimer();
    } catch {
      await fetchTimer();
    } finally {
      setActionLoading(false);
    }
  };

  const handleReset = async () => {
    setActionLoading(true);
    try {
      await api.post('/api/timer/manual/reset');
      await fetchTimer();
    } catch {
      await fetchTimer();
    } finally {
      setActionLoading(false);
    }
  };

  if (loading) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', py: 8 }}>
        <CircularProgress />
      </Box>
    );
  }

  const isRunning = timerState?.manual.status === 'running';
  const challengeTimes = timerState?.automatic.challengeTimes ?? {};
  const challengeEntries = Object.entries(challengeTimes)
    .map(([key, secs]) => ({ challenge: Number(key), seconds: secs }))
    .sort((a, b) => a.challenge - b.challenge);

  return (
    <Box>
      <Typography variant="h4" sx={{ mb: 3 }}>
        Timer
      </Typography>

      {/* Stopwatch card */}
      <Card
        sx={{
          maxWidth: 520,
          mx: 'auto',
          mb: 4,
          background: 'linear-gradient(135deg, rgba(124,58,237,0.15) 0%, rgba(59,130,246,0.10) 100%)',
          border: '1px solid rgba(124,58,237,0.25)',
        }}
      >
        <CardContent sx={{ textAlign: 'center', py: 5, px: 3 }}>
          <Typography
            sx={{
              fontFamily: '"Roboto Mono", monospace',
              fontSize: { xs: '3rem', sm: '4.5rem' },
              fontWeight: 700,
              color: '#A78BFA',
              textShadow: '0 0 20px rgba(167,139,250,0.5), 0 0 40px rgba(167,139,250,0.25)',
              letterSpacing: '0.05em',
              lineHeight: 1.2,
              mb: 3,
            }}
          >
            {formatTime(displaySeconds)}
          </Typography>

          <Box sx={{ display: 'flex', gap: 2, justifyContent: 'center' }}>
            <Button
              variant="contained"
              startIcon={<PlayArrowIcon />}
              disabled={isRunning || actionLoading}
              onClick={handleStart}
              sx={{
                background: 'linear-gradient(135deg, #7C3AED, #3B82F6)',
                '&:hover': { background: 'linear-gradient(135deg, #6D28D9, #2563EB)' },
                '&.Mui-disabled': { opacity: 0.4 },
                px: 3,
              }}
            >
              Start
            </Button>
            <Button
              variant="contained"
              startIcon={<StopIcon />}
              disabled={!isRunning || actionLoading}
              onClick={handleStop}
              sx={{
                background: 'linear-gradient(135deg, #DC2626, #F97316)',
                '&:hover': { background: 'linear-gradient(135deg, #B91C1C, #EA580C)' },
                '&.Mui-disabled': { opacity: 0.4 },
                px: 3,
              }}
            >
              Stop
            </Button>
            <Button
              variant="outlined"
              startIcon={<RestartAltIcon />}
              disabled={actionLoading}
              onClick={handleReset}
              sx={{
                borderColor: 'rgba(167,139,250,0.4)',
                color: '#A78BFA',
                '&:hover': {
                  borderColor: '#A78BFA',
                  bgcolor: 'rgba(167,139,250,0.08)',
                },
                '&.Mui-disabled': { opacity: 0.4 },
                px: 3,
              }}
            >
              Reset
            </Button>
          </Box>
        </CardContent>
      </Card>

      {/* Challenge times summary */}
      {challengeEntries.length > 0 && (
        <Card sx={{ maxWidth: 520, mx: 'auto' }}>
          <CardContent>
            <Typography variant="h6" sx={{ mb: 2 }}>
              Challenge Times
            </Typography>
            <TableContainer>
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>Challenge</TableCell>
                    <TableCell align="right">Duration</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {challengeEntries.map((entry) => (
                    <TableRow key={entry.challenge}>
                      <TableCell>Challenge {entry.challenge}</TableCell>
                      <TableCell
                        align="right"
                        sx={{ fontFamily: '"Roboto Mono", monospace' }}
                      >
                        {formatTime(entry.seconds)}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </TableContainer>
          </CardContent>
        </Card>
      )}
    </Box>
  );
}
