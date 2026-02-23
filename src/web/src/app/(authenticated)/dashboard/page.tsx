'use client';

import { useEffect, useState, useCallback } from 'react';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Table from '@mui/material/Table';
import TableBody from '@mui/material/TableBody';
import TableCell from '@mui/material/TableCell';
import TableContainer from '@mui/material/TableContainer';
import TableHead from '@mui/material/TableHead';
import TableRow from '@mui/material/TableRow';
import Paper from '@mui/material/Paper';
import Button from '@mui/material/Button';
import IconButton from '@mui/material/IconButton';
import Tooltip from '@mui/material/Tooltip';
import LinearProgress from '@mui/material/LinearProgress';
import CircularProgress from '@mui/material/CircularProgress';
import Snackbar from '@mui/material/Snackbar';
import Alert from '@mui/material/Alert';
import Divider from '@mui/material/Divider';
import Dialog from '@mui/material/Dialog';
import DialogTitle from '@mui/material/DialogTitle';
import DialogContent from '@mui/material/DialogContent';
import DialogContentText from '@mui/material/DialogContentText';
import DialogActions from '@mui/material/DialogActions';
import RefreshIcon from '@mui/icons-material/Refresh';
import ArrowForwardIcon from '@mui/icons-material/ArrowForward';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import RestartAltIcon from '@mui/icons-material/RestartAlt';
import PlayArrowIcon from '@mui/icons-material/PlayArrow';
import StopIcon from '@mui/icons-material/Stop';
import TimerIcon from '@mui/icons-material/Timer';
import { api, ApiError } from '@/lib/api';
import { useAuth } from '@/contexts/AuthContext';
import { useHackState } from '@/contexts/HackStateContext';

interface TeamStatus {
  teamName: string;
  currentStep: number;
  totalChallenges: number;
}

interface TeamsResponse {
  totalChallenges: number;
  teams: TeamStatus[];
}

interface SnackState {
  open: boolean;
  message: string;
  severity: 'success' | 'error' | 'info' | 'warning';
}

type ConfirmAction =
  | { kind: 'hack'; action: 'launch' | 'pause' }
  | { kind: 'team'; teamName: string; action: 'approve' | 'revert' | 'reset' }
  | { kind: 'bulk'; action: 'approve-all' | 'revert-all' | 'reset-all' };

interface ConfirmDetails {
  title: string;
  message: string;
  confirmLabel: string;
  confirmColor: 'primary' | 'success' | 'warning' | 'error';
}

function formatElapsed(seconds: number): string {
  const h = Math.floor(seconds / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  const s = seconds % 60;
  return `${String(h).padStart(2, '0')}:${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`;
}

function getConfirmDetails(action: ConfirmAction | null): ConfirmDetails {
  if (!action) {
    return {
      title: 'Confirm action',
      message: 'Are you sure?',
      confirmLabel: 'Confirm',
      confirmColor: 'primary',
    };
  }

  if (action.kind === 'hack' && action.action === 'launch') {
    return {
      title: 'Start hack?',
      message: 'This will start the event for all participants.',
      confirmLabel: 'Start Hack',
      confirmColor: 'success',
    };
  }

  if (action.kind === 'hack' && action.action === 'pause') {
    return {
      title: 'Pause hack?',
      message: 'This will pause the event for all participants.',
      confirmLabel: 'Pause Hack',
      confirmColor: 'warning',
    };
  }

  if (action.kind === 'team' && action.action === 'approve') {
    return {
      title: `Advance ${action.teamName}?`,
      message: 'This will move the team to the next challenge.',
      confirmLabel: 'Advance',
      confirmColor: 'primary',
    };
  }

  if (action.kind === 'team' && action.action === 'revert') {
    return {
      title: `Revert ${action.teamName}?`,
      message: 'This will move the team back one challenge.',
      confirmLabel: 'Revert',
      confirmColor: 'warning',
    };
  }

  if (action.kind === 'team' && action.action === 'reset') {
    return {
      title: `Reset ${action.teamName}?`,
      message: 'This will reset team progress back to Challenge 1.',
      confirmLabel: 'Reset',
      confirmColor: 'error',
    };
  }

  if (action.kind === 'bulk' && action.action === 'approve-all') {
    return {
      title: 'Advance all teams?',
      message: 'This will move every team to the next challenge.',
      confirmLabel: 'Advance All',
      confirmColor: 'primary',
    };
  }

  if (action.kind === 'bulk' && action.action === 'revert-all') {
    return {
      title: 'Revert all teams?',
      message: 'This will move every team back one challenge.',
      confirmLabel: 'Revert All',
      confirmColor: 'warning',
    };
  }

  return {
    title: 'Reset all teams?',
    message: 'This will reset every team back to Challenge 1.',
    confirmLabel: 'Reset All',
    confirmColor: 'error',
  };
}

export default function DashboardPage() {
  const { user, loading: authLoading } = useAuth();
  const { hackState, refetch: refetchHackState } = useHackState();
  const [data, setData] = useState<TeamsResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [actionLoading, setActionLoading] = useState<string | null>(null);
  const [snack, setSnack] = useState<SnackState>({ open: false, message: '', severity: 'success' });
  const [confirmAction, setConfirmAction] = useState<ConfirmAction | null>(null);
  const [currentTime, setCurrentTime] = useState(Date.now());

  // Update current time every second for real-time timer calculation
  useEffect(() => {
    const interval = setInterval(() => {
      setCurrentTime(Date.now());
    }, 1000);
    return () => clearInterval(interval);
  }, []);

  // Calculate elapsed seconds from hack start time
  const calculateElapsedSeconds = useCallback(() => {
    if (!hackState?.startedAt) return 0;
    const startTime = new Date(hackState.startedAt).getTime();
    const elapsed = Math.floor((currentTime - startTime) / 1000);
    return Math.max(0, elapsed);
  }, [hackState?.startedAt, currentTime]);

  const showSnack = useCallback((message: string, severity: SnackState['severity'] = 'success') => {
    setSnack({ open: true, message, severity });
  }, []);

  const fetchTeams = useCallback(async () => {
    if (!user || user.role !== 'techlead') {
      setLoading(false);
      return;
    }

    try {
      const result = await api.get<TeamsResponse>('/api/admin/teams');
      setData(result);
    } catch (err) {
      if (err instanceof ApiError) {
        showSnack(err.message, 'error');
      }
    } finally {
      setLoading(false);
    }
  }, [showSnack, user]);

  useEffect(() => {
    if (!authLoading) {
      fetchTeams();
    }
  }, [authLoading, fetchTeams]);

  const handleRefresh = async () => {
    setLoading(true);
    await fetchTeams();
  };

  const handleHackAction = async (action: 'launch' | 'pause') => {
    const key = `hack-${action}`;
    setActionLoading(key);
    try {
      if (action === 'launch') {
        await api.post('/api/hack/launch', {});
        showSnack('Hack started');
      } else {
        await api.post('/api/hack/pause', {});
        showSnack('Hack paused');
      }
      await Promise.all([fetchTeams(), refetchHackState()]);
    } catch (err) {
      showSnack(err instanceof ApiError ? err.message : 'Failed to update hack state', 'error');
    } finally {
      setActionLoading(null);
    }
  };

  // Per-team challenge actions
  const handleChallengeAction = async (teamName: string, action: 'approve' | 'revert' | 'reset') => {
    const key = `${teamName}-challenge-${action}`;
    setActionLoading(key);
    try {
      await api.post(`/api/admin/teams/${encodeURIComponent(teamName)}/challenges/${action}`);
      showSnack(`${action === 'approve' ? 'Advanced' : action === 'revert' ? 'Reverted' : 'Reset'} ${teamName}`);
      await fetchTeams();
    } catch (err) {
      showSnack(err instanceof ApiError ? err.message : 'Operation failed', 'error');
    } finally {
      setActionLoading(null);
    }
  };

  // Bulk challenge actions
  const handleBulkChallenge = async (action: 'approve-all' | 'revert-all' | 'reset-all') => {
    const key = `bulk-challenge-${action}`;
    setActionLoading(key);
    try {
      const result = await api.post<{ results?: Array<{ teamName: string; success: boolean; error?: string }> }>(
        `/api/admin/challenges/${action}`
      );
      const results = result.results;
      if (results) {
        const successes = results.filter((r) => r.success).length;
        const failures = results.filter((r) => !r.success);
        if (failures.length === 0) {
          showSnack(`All ${successes} teams updated`);
        } else {
          showSnack(`${successes} succeeded, ${failures.length} failed: ${failures.map((f) => `${f.teamName}: ${f.error}`).join('; ')}`, 'warning');
        }
      } else {
        showSnack('Bulk operation completed');
      }
      await fetchTeams();
    } catch (err) {
      showSnack(err instanceof ApiError ? err.message : 'Bulk operation failed', 'error');
    } finally {
      setActionLoading(null);
    }
  };

  if (user?.role !== 'techlead') {
    return (
      <Box sx={{ textAlign: 'center', py: 8 }}>
        <Typography variant="h5" color="error">Access Denied</Typography>
        <Typography color="text.secondary" sx={{ mt: 1 }}>
          The dashboard is only accessible to Tech Leads.
        </Typography>
      </Box>
    );
  }

  if (loading) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', py: 8 }}>
        <CircularProgress />
      </Box>
    );
  }

  const teams = data?.teams ?? [];
  const totalChallenges = data?.totalChallenges ?? 0;
  const canStartHack = !!hackState && (hackState.status === 'waiting' || hackState.status === 'configuration' || hackState.status === 'not_started');
  const canPauseHack = hackState?.status === 'active';
  const confirmDetails = getConfirmDetails(confirmAction);

  const handleConfirmAction = async () => {
    const action = confirmAction;
    if (!action) return;

    setConfirmAction(null);
    if (action.kind === 'hack') {
      await handleHackAction(action.action);
      return;
    }
    if (action.kind === 'team') {
      await handleChallengeAction(action.teamName, action.action);
      return;
    }
    await handleBulkChallenge(action.action);
  };

  return (
    <Box>
      {/* Header */}
      <Box sx={{ mb: 3 }}>
        <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', mb: 1 }}>
          <Box>
            <Typography
              variant="h4"
              sx={{
                background: 'linear-gradient(135deg, #A78BFA 0%, #60A5FA 100%)',
                WebkitBackgroundClip: 'text',
                WebkitTextFillColor: 'transparent',
              }}
            >
              Event Organizer Dashboard
            </Typography>
            <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
              {teams.length} team{teams.length !== 1 ? 's' : ''} • {totalChallenges} challenge{totalChallenges !== 1 ? 's' : ''} available
            </Typography>
          </Box>
          <Button
            variant="outlined"
            startIcon={<RefreshIcon />}
            onClick={handleRefresh}
            disabled={!!actionLoading}
          >
            Refresh
          </Button>
        </Box>
      </Box>

      {/* Global Hack Timer (shown when hack is active) */}
      {hackState?.status === 'active' && hackState.startedAt && (
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
          <Box>
            <Typography variant="subtitle2" color="text.secondary" sx={{ mb: 0.5 }}>
              Hack Started At
            </Typography>
            <Typography variant="body2" fontFamily="monospace">
              {new Date(hackState.startedAt).toLocaleString()}
            </Typography>
          </Box>
          <Box sx={{ textAlign: 'right' }}>
            <Typography variant="subtitle2" color="text.secondary" sx={{ mb: 0.5 }}>
              Elapsed Time
            </Typography>
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, justifyContent: 'flex-end' }}>
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
                {formatElapsed(calculateElapsedSeconds())}
              </Typography>
            </Box>
          </Box>
        </Paper>
      )}

      {/* Bulk Operations Toolbar */}
      <Paper
        sx={{
          p: 2,
          mb: 3,
          background: 'linear-gradient(145deg, #1A1333 0%, #1E1045 100%)',
          border: '1px solid rgba(124, 58, 237, 0.15)',
        }}
      >
        <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 2, alignItems: 'center' }}>
          <Typography variant="subtitle2" color="text.secondary" sx={{ mr: 1 }}>
            Hack:
          </Typography>
          <Button
            size="small"
            variant="contained"
            color="success"
            startIcon={<PlayArrowIcon />}
            onClick={() => setConfirmAction({ kind: 'hack', action: 'launch' })}
            disabled={!!actionLoading || teams.length === 0 || !canStartHack}
          >
            Start Hack
          </Button>
          <Button
            size="small"
            variant="outlined"
            startIcon={<StopIcon />}
            onClick={() => setConfirmAction({ kind: 'hack', action: 'pause' })}
            disabled={!!actionLoading || !canPauseHack}
          >
            Pause Hack
          </Button>

          <Divider orientation="vertical" flexItem sx={{ mx: 1 }} />

          {/* Challenge bulk ops */}
          <Typography variant="subtitle2" color="text.secondary" sx={{ mr: 1 }}>
            Challenges:
          </Typography>
          <Button
            size="small"
            variant="contained"
            startIcon={<ArrowForwardIcon />}
            onClick={() => setConfirmAction({ kind: 'bulk', action: 'approve-all' })}
            disabled={!!actionLoading || teams.length === 0}
          >
            Advance All
          </Button>
          <Button
            size="small"
            variant="outlined"
            startIcon={<ArrowBackIcon />}
            onClick={() => setConfirmAction({ kind: 'bulk', action: 'revert-all' })}
            disabled={!!actionLoading || teams.length === 0}
          >
            Revert All
          </Button>
          <Button
            size="small"
            variant="outlined"
            color="error"
            startIcon={<RestartAltIcon />}
            onClick={() => setConfirmAction({ kind: 'bulk', action: 'reset-all' })}
            disabled={!!actionLoading || teams.length === 0}
          >
            Reset All
          </Button>

        </Box>
      </Paper>

      {/* Teams Table or Empty State */}
      {teams.length === 0 ? (
        <Box sx={{ textAlign: 'center', py: 8 }}>
          <Typography variant="h5" color="text.secondary" sx={{ mb: 2 }}>
            No teams configured
          </Typography>
          <Typography color="text.secondary">
            Teams will appear here once they are configured in the system.
          </Typography>
        </Box>
      ) : (
        <TableContainer
          component={Paper}
          sx={{
            background: 'linear-gradient(145deg, #1A1333 0%, #1E1045 100%)',
            border: '1px solid rgba(124, 58, 237, 0.15)',
          }}
        >
          <Table>
            <TableHead>
              <TableRow>
                <TableCell sx={{ fontWeight: 700 }}>Team Name</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>Challenge Progress</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>Completion</TableCell>
                <TableCell sx={{ fontWeight: 700 }} align="center">Challenge Actions</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {teams.map((team) => {
                const progressPercent = totalChallenges > 0 ? ((team.currentStep - 1) / totalChallenges) * 100 : 0;
                return (
                  <TableRow key={team.teamName} hover>
                    <TableCell>
                      <Typography variant="body2" fontWeight={600}>
                        {team.teamName}
                      </Typography>
                    </TableCell>
                    <TableCell>
                      <Typography variant="body2">
                        {team.currentStep} / {totalChallenges}
                      </Typography>
                    </TableCell>
                    <TableCell sx={{ minWidth: 150 }}>
                      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                        <LinearProgress
                          variant="determinate"
                          value={progressPercent}
                          sx={{
                            flex: 1,
                            height: 6,
                            borderRadius: 3,
                            bgcolor: 'rgba(124, 58, 237, 0.1)',
                            '& .MuiLinearProgress-bar': {
                              borderRadius: 3,
                              background: progressPercent >= 100
                                ? 'linear-gradient(90deg, #34D399, #10B981)'
                                : 'linear-gradient(90deg, #7C3AED, #3B82F6)',
                            },
                          }}
                        />
                        <Typography variant="caption" color="text.secondary" sx={{ minWidth: 35 }}>
                          {Math.round(progressPercent)}%
                        </Typography>
                      </Box>
                    </TableCell>
                    <TableCell align="center">
                      <Box sx={{ display: 'flex', gap: 0.5, justifyContent: 'center' }}>
                        <Tooltip title="Advance">
                          <span>
                            <IconButton
                              size="small"
                              color="primary"
                              onClick={() => setConfirmAction({ kind: 'team', teamName: team.teamName, action: 'approve' })}
                              disabled={!!actionLoading}
                            >
                              <ArrowForwardIcon fontSize="small" />
                            </IconButton>
                          </span>
                        </Tooltip>
                        <Tooltip title="Revert">
                          <span>
                            <IconButton
                              size="small"
                              onClick={() => setConfirmAction({ kind: 'team', teamName: team.teamName, action: 'revert' })}
                              disabled={!!actionLoading}
                            >
                              <ArrowBackIcon fontSize="small" />
                            </IconButton>
                          </span>
                        </Tooltip>
                        <Tooltip title="Reset">
                          <span>
                            <IconButton
                              size="small"
                              color="error"
                              onClick={() => setConfirmAction({ kind: 'team', teamName: team.teamName, action: 'reset' })}
                              disabled={!!actionLoading}
                            >
                              <RestartAltIcon fontSize="small" />
                            </IconButton>
                          </span>
                        </Tooltip>
                      </Box>
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      <Dialog open={!!confirmAction} onClose={() => setConfirmAction(null)}>
        <DialogTitle>{confirmDetails.title}</DialogTitle>
        <DialogContent>
          <DialogContentText>{confirmDetails.message}</DialogContentText>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setConfirmAction(null)}>Cancel</Button>
          <Button
            onClick={handleConfirmAction}
            variant="contained"
            color={confirmDetails.confirmColor}
            autoFocus
          >
            {confirmDetails.confirmLabel}
          </Button>
        </DialogActions>
      </Dialog>

      {/* Snackbar for operation results */}
      <Snackbar
        open={snack.open}
        autoHideDuration={5000}
        onClose={() => setSnack((s) => ({ ...s, open: false }))}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
      >
        <Alert
          onClose={() => setSnack((s) => ({ ...s, open: false }))}
          severity={snack.severity}
          variant="filled"
          sx={{ width: '100%' }}
        >
          {snack.message}
        </Alert>
      </Snackbar>
    </Box>
  );
}
