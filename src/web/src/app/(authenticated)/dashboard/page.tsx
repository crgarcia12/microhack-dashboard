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
import Chip from '@mui/material/Chip';
import Divider from '@mui/material/Divider';
import RefreshIcon from '@mui/icons-material/Refresh';
import ArrowForwardIcon from '@mui/icons-material/ArrowForward';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import RestartAltIcon from '@mui/icons-material/RestartAlt';
import PlayArrowIcon from '@mui/icons-material/PlayArrow';
import StopIcon from '@mui/icons-material/Stop';
import TimerIcon from '@mui/icons-material/Timer';
import { api, ApiError } from '@/lib/api';
import { useAuth } from '@/contexts/AuthContext';

interface TeamStatus {
  teamName: string;
  currentStep: number;
  totalChallenges: number;
  timerStatus: 'running' | 'stopped' | 'not_started';
  elapsedSeconds: number;
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

function formatElapsed(seconds: number): string {
  const h = Math.floor(seconds / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  const s = seconds % 60;
  return `${String(h).padStart(2, '0')}:${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`;
}

function timerLabel(status: string): string {
  if (status === 'running') return 'Running';
  if (status === 'stopped') return 'Stopped';
  return 'Not started';
}

function timerColor(status: string): 'success' | 'default' | 'warning' {
  if (status === 'running') return 'success';
  if (status === 'stopped') return 'default';
  return 'warning';
}

export default function DashboardPage() {
  const { user, loading: authLoading } = useAuth();
  const [data, setData] = useState<TeamsResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [actionLoading, setActionLoading] = useState<string | null>(null);
  const [snack, setSnack] = useState<SnackState>({ open: false, message: '', severity: 'success' });

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

  // Per-team timer actions
  const handleTimerAction = async (teamName: string, action: 'start' | 'stop' | 'reset') => {
    const key = `${teamName}-timer-${action}`;
    setActionLoading(key);
    try {
      await api.post(`/api/admin/teams/${encodeURIComponent(teamName)}/timer/${action}`);
      showSnack(`Timer ${action === 'start' ? 'started' : action === 'stop' ? 'stopped' : 'reset'} for ${teamName}`);
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

  // Bulk timer actions
  const handleBulkTimer = async (action: 'start-all' | 'stop-all' | 'reset-all') => {
    const key = `bulk-timer-${action}`;
    setActionLoading(key);
    try {
      const result = await api.post<{ results?: Array<{ teamName: string; success: boolean; error?: string }> }>(
        `/api/admin/timer/${action}`
      );
      const results = result.results;
      if (results) {
        const successes = results.filter((r) => r.success).length;
        const failures = results.filter((r) => !r.success);
        if (failures.length === 0) {
          showSnack(`All ${successes} team timers updated`);
        } else {
          showSnack(`${successes} succeeded, ${failures.length} failed: ${failures.map((f) => `${f.teamName}: ${f.error}`).join('; ')}`, 'warning');
        }
      } else {
        showSnack('Bulk timer operation completed');
      }
      await fetchTeams();
    } catch (err) {
      showSnack(err instanceof ApiError ? err.message : 'Bulk timer operation failed', 'error');
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
          {/* Challenge bulk ops */}
          <Typography variant="subtitle2" color="text.secondary" sx={{ mr: 1 }}>
            Challenges:
          </Typography>
          <Button
            size="small"
            variant="contained"
            startIcon={<ArrowForwardIcon />}
            onClick={() => handleBulkChallenge('approve-all')}
            disabled={!!actionLoading || teams.length === 0}
          >
            Advance All
          </Button>
          <Button
            size="small"
            variant="outlined"
            startIcon={<ArrowBackIcon />}
            onClick={() => handleBulkChallenge('revert-all')}
            disabled={!!actionLoading || teams.length === 0}
          >
            Revert All
          </Button>
          <Button
            size="small"
            variant="outlined"
            color="error"
            startIcon={<RestartAltIcon />}
            onClick={() => handleBulkChallenge('reset-all')}
            disabled={!!actionLoading || teams.length === 0}
          >
            Reset All
          </Button>

          <Divider orientation="vertical" flexItem sx={{ mx: 1 }} />

          {/* Timer bulk ops */}
          <Typography variant="subtitle2" color="text.secondary" sx={{ mr: 1 }}>
            Timers:
          </Typography>
          <Button
            size="small"
            variant="contained"
            color="success"
            startIcon={<PlayArrowIcon />}
            onClick={() => handleBulkTimer('start-all')}
            disabled={!!actionLoading || teams.length === 0}
          >
            Start All
          </Button>
          <Button
            size="small"
            variant="outlined"
            startIcon={<StopIcon />}
            onClick={() => handleBulkTimer('stop-all')}
            disabled={!!actionLoading || teams.length === 0}
          >
            Stop All
          </Button>
          <Button
            size="small"
            variant="outlined"
            color="error"
            startIcon={<RestartAltIcon />}
            onClick={() => handleBulkTimer('reset-all')}
            disabled={!!actionLoading || teams.length === 0}
          >
            Reset All Timers
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
                <TableCell sx={{ fontWeight: 700 }}>Timer</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>Elapsed</TableCell>
                <TableCell sx={{ fontWeight: 700 }} align="center">Challenge Actions</TableCell>
                <TableCell sx={{ fontWeight: 700 }} align="center">Timer Controls</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {teams.map((team) => {
                const progressPercent = totalChallenges > 0 ? ((team.currentStep - 1) / totalChallenges) * 100 : 0;
                const isRunning = team.timerStatus === 'running';
                const isStopped = team.timerStatus === 'stopped' || team.timerStatus === 'not_started';
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
                    <TableCell>
                      <Chip
                        size="small"
                        label={timerLabel(team.timerStatus)}
                        color={timerColor(team.timerStatus)}
                        variant={isRunning ? 'filled' : 'outlined'}
                      />
                    </TableCell>
                    <TableCell>
                      <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
                        <TimerIcon sx={{ fontSize: 16, color: 'text.secondary' }} />
                        <Typography variant="body2" fontFamily="monospace">
                          {formatElapsed(team.elapsedSeconds)}
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
                              onClick={() => handleChallengeAction(team.teamName, 'approve')}
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
                              onClick={() => handleChallengeAction(team.teamName, 'revert')}
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
                              onClick={() => handleChallengeAction(team.teamName, 'reset')}
                              disabled={!!actionLoading}
                            >
                              <RestartAltIcon fontSize="small" />
                            </IconButton>
                          </span>
                        </Tooltip>
                      </Box>
                    </TableCell>
                    <TableCell align="center">
                      <Box sx={{ display: 'flex', gap: 0.5, justifyContent: 'center' }}>
                        <Tooltip title="Start Timer">
                          <span>
                            <IconButton
                              size="small"
                              color="success"
                              onClick={() => handleTimerAction(team.teamName, 'start')}
                              disabled={!!actionLoading || isRunning}
                            >
                              <PlayArrowIcon fontSize="small" />
                            </IconButton>
                          </span>
                        </Tooltip>
                        <Tooltip title="Stop Timer">
                          <span>
                            <IconButton
                              size="small"
                              onClick={() => handleTimerAction(team.teamName, 'stop')}
                              disabled={!!actionLoading || isStopped}
                            >
                              <StopIcon fontSize="small" />
                            </IconButton>
                          </span>
                        </Tooltip>
                        <Tooltip title="Reset Timer">
                          <span>
                            <IconButton
                              size="small"
                              color="error"
                              onClick={() => handleTimerAction(team.teamName, 'reset')}
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
