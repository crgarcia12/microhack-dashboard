'use client';

import { useEffect, useState, useCallback } from 'react';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import LinearProgress from '@mui/material/LinearProgress';
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
import { api } from '@/lib/api';
import { useAuth } from '@/contexts/AuthContext';
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

export default function ChallengesPage() {
  const { user } = useAuth();
  const [challenges, setChallenges] = useState<Challenge[]>([]);
  const [progress, setProgress] = useState<TeamProgress | null>(null);
  const [selectedChallenge, setSelectedChallenge] = useState<ChallengeDetail | null>(null);
  const [selectedNumber, setSelectedNumber] = useState<number | null>(null);
  const [loadingList, setLoadingList] = useState(true);
  const [loadingContent, setLoadingContent] = useState(false);

  const isCoach = user?.role === 'coach' || user?.role === 'techlead';

  const fetchData = useCallback(async () => {
    try {
      const [challengeList, progressData] = await Promise.all([
        api.get<Challenge[]>('/api/challenges'),
        api.get<TeamProgress>('/api/teams/progress'),
      ]);
      setChallenges(challengeList);
      setProgress(progressData);
    } catch {
      // Silently handle — auth errors redirect via layout
    } finally {
      setLoadingList(false);
    }
  }, []);

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
    },
    [],
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

  const allCompleted = progress?.completed ?? false;

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

      {/* Coach controls */}
      {isCoach && (
        <Box sx={{ mb: 3 }}>
          <CoachControls disabled={challenges.length === 0} onAction={handleCoachAction} />
        </Box>
      )}

      {/* Celebration state */}
      {allCompleted ? (
        <Celebration totalChallenges={progress?.totalChallenges ?? 0} />
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
                      primaryTypographyProps={{
                        variant: 'body2',
                        fontWeight: isSelected ? 600 : 400,
                        noWrap: true,
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
