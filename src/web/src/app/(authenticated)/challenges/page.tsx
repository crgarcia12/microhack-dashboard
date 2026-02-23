'use client';

import { useEffect, useState, useCallback } from 'react';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import LinearProgress from '@mui/material/LinearProgress';
import Paper from '@mui/material/Paper';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import List from '@mui/material/List';
import ListItemButton from '@mui/material/ListItemButton';
import ListItemIcon from '@mui/material/ListItemIcon';
import ListItemText from '@mui/material/ListItemText';
import CircularProgress from '@mui/material/CircularProgress';
import Chip from '@mui/material/Chip';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import RadioButtonCheckedIcon from '@mui/icons-material/RadioButtonChecked';
import LockIcon from '@mui/icons-material/Lock';
import FiberManualRecordIcon from '@mui/icons-material/FiberManualRecord';
import TimerIcon from '@mui/icons-material/Timer';
import { api } from '@/lib/api';
import { useAuth } from '@/contexts/AuthContext';
import { useHackState } from '@/contexts/HackStateContext';
import { useSignalR, type TeamProgress } from '@/hooks/useSignalR';
import MarkdownRenderer from '@/components/MarkdownRenderer';
import CoachControls from '@/components/CoachControls';
import Celebration from '@/components/Celebration';

interface Challenge {
  challengeNumber: number;
  title: string | null;
  status: 'completed' | 'current' | 'locked';
}

interface ChallengeDetail {
  challengeNumber: number;
  title: string;
  contentHtml: string;
}

interface TimerSnapshot {
  automatic: {
    timerStartedAt: string | null;
    challengeTimes: Record<string, number>;
  };
}

function formatElapsed(seconds: number): string {
  const h = Math.floor(seconds / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  const s = seconds % 60;
  return `${String(h).padStart(2, '0')}:${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`;
}

export default function ChallengesPage() {
  const { user } = useAuth();
  const { hackState } = useHackState();
  const [challenges, setChallenges] = useState<Challenge[]>([]);
  const [progress, setProgress] = useState<TeamProgress | null>(null);
  const [timerSnapshot, setTimerSnapshot] = useState<TimerSnapshot | null>(null);
  const [selectedChallenge, setSelectedChallenge] = useState<ChallengeDetail | null>(null);
  const [selectedNumber, setSelectedNumber] = useState<number | null>(null);
  const [loadingList, setLoadingList] = useState(true);
  const [loadingContent, setLoadingContent] = useState(false);
  const [currentTime, setCurrentTime] = useState(Date.now());
  const [completedElapsedSeconds, setCompletedElapsedSeconds] = useState<number | null>(null);

  const isCoach = user?.role === 'coach' || user?.role === 'techlead';
  const isParticipant = user?.role === 'participant';
  const shouldLoadTimerData = user?.role === 'participant' || user?.role === 'coach';

  useEffect(() => {
    const interval = setInterval(() => setCurrentTime(Date.now()), 1000);
    return () => clearInterval(interval);
  }, []);

  const fetchData = useCallback(async () => {
    try {
      const [challengeList, progressData, timerData] = await Promise.all([
        api.get<Challenge[]>('/api/challenges'),
        api.get<TeamProgress>('/api/teams/progress'),
        shouldLoadTimerData ? api.get<TimerSnapshot>('/api/timer') : Promise.resolve(null),
      ]);
      setChallenges(challengeList);
      setProgress(progressData);
      setTimerSnapshot(timerData);
    } catch {
      // Silently handle — auth errors redirect via layout
    } finally {
      setLoadingList(false);
    }
  }, [shouldLoadTimerData]);

  // Initial fetch
  useEffect(() => {
    fetchData();
  }, [fetchData]);

  // SignalR for real-time updates
  const handleProgressUpdated = useCallback(
    (newProgress: TeamProgress) => {
      setProgress(newProgress);
      // Re-fetch challenge list to get updated statuses
      api.get<Challenge[]>('/api/challenges').then(setChallenges).catch(() => {});
      if (shouldLoadTimerData) {
        api.get<TimerSnapshot>('/api/timer').then(setTimerSnapshot).catch(() => {});
      }
    },
    [shouldLoadTimerData],
  );

  const { connected } = useSignalR({ onProgressUpdated: handleProgressUpdated });

  // Auto-select current challenge on load or progress change
  useEffect(() => {
    if (!progress || challenges.length === 0) return;
    if (progress.completed) {
      setSelectedNumber(null);
      setSelectedChallenge(null);
      return;
    }
    const current = challenges.find((c) => c.status === 'current');
    if (current && selectedNumber === null) {
      setSelectedNumber(current.challengeNumber);
    }
  }, [progress, challenges, selectedNumber]);

  // Fetch challenge content when selection changes
  useEffect(() => {
    if (selectedNumber === null) {
      setSelectedChallenge(null);
      return;
    }
    const challenge = challenges.find((c) => c.challengeNumber === selectedNumber);
    if (!challenge || challenge.status === 'locked') return;

    setLoadingContent(true);
    api
      .get<ChallengeDetail>(`/api/challenges/${selectedNumber}`)
      .then(setSelectedChallenge)
      .catch(() => setSelectedChallenge(null))
      .finally(() => setLoadingContent(false));
  }, [selectedNumber, challenges]);

  const handleChallengeClick = (challenge: Challenge) => {
    if (challenge.status === 'locked') return;
    setSelectedNumber(challenge.challengeNumber);
  };

  const handleCoachAction = () => {
    fetchData();
    setSelectedNumber(null);
  };

  const progressPercent =
    progress && progress.totalChallenges > 0
      ? (progress.completedChallenges / progress.totalChallenges) * 100
      : 0;
  const allCompleted = progress?.completed ?? false;
  const challengeTimes = timerSnapshot?.automatic.challengeTimes ?? {};
  const timerStartedAt = timerSnapshot?.automatic.timerStartedAt;
  const challengeStartFallback = progress?.currentStep === 1 ? hackState?.startedAt : null;
  const hackStartMs = hackState?.startedAt ? new Date(hackState.startedAt).getTime() : Number.NaN;
  const eventElapsedSeconds = Number.isNaN(hackStartMs)
    ? 0
    : Math.max(0, Math.floor((currentTime - hackStartMs) / 1000));
  const completedChallengeTimes = Object.values(challengeTimes);
  const completionElapsedFromChallenges =
    allCompleted && progress?.totalChallenges && completedChallengeTimes.length >= progress.totalChallenges
      ? completedChallengeTimes.reduce((sum, seconds) => sum + seconds, 0)
      : null;

  useEffect(() => {
    if (!allCompleted) {
      setCompletedElapsedSeconds((prev) => (prev === null ? prev : null));
      return;
    }

    const baselineElapsed = completionElapsedFromChallenges ?? eventElapsedSeconds;
    setCompletedElapsedSeconds((prev) => (prev === null ? baselineElapsed : prev));
  }, [allCompleted, completionElapsedFromChallenges, eventElapsedSeconds]);

  const displayElapsedSeconds = allCompleted
    ? completedElapsedSeconds ?? completionElapsedFromChallenges ?? eventElapsedSeconds
    : eventElapsedSeconds;

  // Empty state
  if (!loadingList && challenges.length === 0) {
    return (
      <Box sx={{ textAlign: 'center', py: 8 }}>
        <Typography variant="h5" color="text.secondary" sx={{ mb: 2 }}>
          No challenges loaded
        </Typography>
        <Typography color="text.secondary">
          Add Markdown files to <code>hackcontent/challenges/</code>
        </Typography>
      </Box>
    );
  }

  if (loadingList) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', py: 8 }}>
        <CircularProgress />
      </Box>
    );
  }

  const getChallengeElapsed = (challenge: Challenge): number => {
    const stored = challengeTimes[String(challenge.challengeNumber)] ?? 0;
    if (challenge.status !== 'current' || hackState?.status !== 'active') {
      return stored;
    }

    const currentStart = timerStartedAt ?? challengeStartFallback;
    if (!currentStart) {
      return stored;
    }

    const startMs = new Date(currentStart).getTime();
    if (Number.isNaN(startMs)) {
      return stored;
    }

    const running = Math.max(0, Math.floor((currentTime - startMs) / 1000));
    return stored + running;
  };

  return (
    <Box>
      {/* Header with progress */}
      <Box sx={{ mb: 3 }}>
        <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', mb: 1 }}>
          <Typography variant="h4">Challenges</Typography>
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5 }}>
            {!connected && (
              <Chip
                icon={<FiberManualRecordIcon sx={{ fontSize: 10 }} />}
                label="Reconnecting…"
                size="small"
                sx={{
                  bgcolor: 'rgba(234, 179, 8, 0.15)',
                  color: '#EAB308',
                  '& .MuiChip-icon': { color: '#EAB308' },
                }}
              />
            )}
            {progress && (
              <Typography variant="body2" color="text.secondary">
                {progress.completedChallenges} / {progress.totalChallenges} completed
              </Typography>
            )}
          </Box>
        </Box>
        {progress && progress.totalChallenges > 0 && (
          <LinearProgress
            variant="determinate"
            value={progressPercent}
            sx={{
              height: 8,
              borderRadius: 4,
              bgcolor: 'rgba(124, 58, 237, 0.1)',
              '& .MuiLinearProgress-bar': {
                borderRadius: 4,
                background: allCompleted
                  ? 'linear-gradient(90deg, #34D399, #10B981)'
                  : 'linear-gradient(90deg, #7C3AED, #3B82F6)',
              },
            }}
          />
        )}
      </Box>

      {hackState?.startedAt && (
        <Paper
          sx={{
            p: 2,
            mb: 3,
            background: 'linear-gradient(145deg, #1A1333 0%, #1E1045 100%)',
            border: '1px solid rgba(124, 58, 237, 0.15)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
          }}
        >
          <Typography variant="subtitle2" color="text.secondary">
            Elapsed Time
          </Typography>
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
            <TimerIcon sx={{ fontSize: 20, color: 'primary.main' }} />
            <Typography
              variant="h5"
              fontFamily="monospace"
              sx={{
                background: 'linear-gradient(135deg, #A78BFA 0%, #60A5FA 100%)',
                WebkitBackgroundClip: 'text',
                WebkitTextFillColor: 'transparent',
                fontWeight: 700,
              }}
            >
              {formatElapsed(displayElapsedSeconds)}
            </Typography>
          </Box>
        </Paper>
      )}

      {/* Coach controls */}
      {isCoach && (
        <Box sx={{ mb: 3 }}>
          <CoachControls disabled={challenges.length === 0} onAction={handleCoachAction} />
        </Box>
      )}

      {/* Celebration state */}
      {allCompleted ? (
        <Celebration
          totalChallenges={progress?.totalChallenges ?? 0}
          totalElapsedSeconds={displayElapsedSeconds}
        />
      ) : (
        /* Main content: sidebar + challenge content */
        <Box sx={{ display: 'flex', gap: 3, flexDirection: { xs: 'column', md: 'row' } }}>
          {/* Sidebar */}
          <Card
            sx={{
              width: { xs: '100%', md: 280 },
              flexShrink: 0,
              alignSelf: 'flex-start',
            }}
          >
            <List disablePadding>
              {challenges.map((c) => {
                const isSelected = c.challengeNumber === selectedNumber;
                const isLocked = c.status === 'locked';
                return (
                  <ListItemButton
                    key={c.challengeNumber}
                    selected={isSelected}
                    disabled={isLocked}
                    onClick={() => handleChallengeClick(c)}
                    sx={{
                      opacity: isLocked ? 0.5 : 1,
                      '&.Mui-selected': {
                        bgcolor: 'rgba(124, 58, 237, 0.15)',
                        borderLeft: '3px solid',
                        borderColor: 'primary.main',
                      },
                      transition: 'all 0.2s ease',
                    }}
                  >
                    <ListItemIcon sx={{ minWidth: 36 }}>
                      {c.status === 'completed' && (
                        <CheckCircleIcon sx={{ color: '#34D399', fontSize: 20 }} />
                      )}
                      {c.status === 'current' && (
                        <RadioButtonCheckedIcon sx={{ color: 'secondary.light', fontSize: 20 }} />
                      )}
                      {c.status === 'locked' && (
                        <LockIcon sx={{ color: 'text.secondary', fontSize: 20 }} />
                      )}
                    </ListItemIcon>
                    <ListItemText
                      primary={isLocked ? `Challenge ${c.challengeNumber}` : (c.title ?? `Challenge ${c.challengeNumber}`)}
                      secondary={isParticipant ? `Elapsed ${formatElapsed(getChallengeElapsed(c))}` : undefined}
                      primaryTypographyProps={{
                        variant: 'body2',
                        fontWeight: isSelected ? 600 : 400,
                        noWrap: true,
                      }}
                      secondaryTypographyProps={{
                        variant: 'caption',
                        fontFamily: 'monospace',
                        color: 'text.secondary',
                      }}
                    />
                  </ListItemButton>
                );
              })}
            </List>
          </Card>

          {/* Content area */}
          <Card sx={{ flex: 1, minWidth: 0 }}>
            <CardContent sx={{ p: { xs: 2, md: 3 } }}>
              {loadingContent ? (
                <Box sx={{ display: 'flex', justifyContent: 'center', py: 6 }}>
                  <CircularProgress />
                </Box>
              ) : selectedChallenge ? (
                <Box sx={{ transition: 'opacity 0.3s ease', opacity: 1 }}>
                  <MarkdownRenderer content={selectedChallenge.contentHtml} />
                </Box>
              ) : (
                <Box sx={{ textAlign: 'center', py: 6 }}>
                  <Typography color="text.secondary">
                    Select a challenge from the list
                  </Typography>
                </Box>
              )}
            </CardContent>
          </Card>
        </Box>
      )}
    </Box>
  );
}
