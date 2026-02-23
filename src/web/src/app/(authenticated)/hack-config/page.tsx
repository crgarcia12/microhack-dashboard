'use client';

import { useEffect, useState, useCallback } from 'react';
import { useRouter } from 'next/navigation';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import Paper from '@mui/material/Paper';
import TextField from '@mui/material/TextField';
import Chip from '@mui/material/Chip';
import CircularProgress from '@mui/material/CircularProgress';
import Snackbar from '@mui/material/Snackbar';
import Alert from '@mui/material/Alert';
import FormControl from '@mui/material/FormControl';
import InputLabel from '@mui/material/InputLabel';
import Select from '@mui/material/Select';
import MenuItem from '@mui/material/MenuItem';
import FormControlLabel from '@mui/material/FormControlLabel';
import Switch from '@mui/material/Switch';
import Dialog from '@mui/material/Dialog';
import DialogTitle from '@mui/material/DialogTitle';
import DialogContent from '@mui/material/DialogContent';
import DialogActions from '@mui/material/DialogActions';
import RocketLaunchIcon from '@mui/icons-material/RocketLaunch';
import { api, ApiError } from '@/lib/api';
import { useAuth } from '@/contexts/AuthContext';
import { useHackState } from '@/contexts/HackStateContext';

interface TeamConfig {
  name: string;
  members: string[];
}

interface HackConfig {
  mode: 'team' | 'individual';
  participantSolutionsVisible: boolean;
  contentPath?: string;
  teams: TeamConfig[];
  coaches: string[];
}

interface UserInfo {
  username: string;
  role: string;
  team: string | null;
}

interface DataStoreInfo {
  provider: string;
  target: string;
}

interface SnackState {
  open: boolean;
  message: string;
  severity: 'success' | 'error' | 'info' | 'warning';
}

export default function ConfigPage() {
  const { user, loading: authLoading } = useAuth();
  const { hackState, refetch: refetchHackState } = useHackState();
  const router = useRouter();

  const [config, setConfig] = useState<HackConfig>({
    mode: 'team',
    participantSolutionsVisible: false,
    contentPath: 'hackcontent',
    teams: [],
    coaches: [],
  });
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [launching, setLaunching] = useState(false);
  const [snack, setSnack] = useState<SnackState>({ open: false, message: '', severity: 'success' });
  const [launchDialogOpen, setLaunchDialogOpen] = useState(false);
  const [teamCount, setTeamCount] = useState(0);
  const [participantCount, setParticipantCount] = useState(0);
  const [dataStoreInfo, setDataStoreInfo] = useState<DataStoreInfo | null>(null);
  const [pendingMode, setPendingMode] = useState<'team' | 'individual' | null>(null);
  const [modeDialogOpen, setModeDialogOpen] = useState(false);

  const showSnack = useCallback((message: string, severity: SnackState['severity'] = 'success') => {
    setSnack({ open: true, message, severity });
  }, []);

  // Fetch existing config
  const fetchConfig = useCallback(async () => {
    if (!user || user.role !== 'techlead') {
      setLoading(false);
      return;
    }

    try {
      const [configData, teamsData, usersData, storeData] = await Promise.all([
        api.get<HackConfig>('/api/hack/config'),
        api.get<string[]>('/api/admin/team-admin/teams'),
        api.get<UserInfo[]>('/api/admin/team-admin/users'),
        api.get<DataStoreInfo>('/api/hack/datastore'),
      ]);
      const normalizedMode = configData.mode === 'individual' ? 'individual' : 'team';
      const participantSolutionsVisible = configData.participantSolutionsVisible ?? (normalizedMode === 'individual');
      setConfig({
        ...configData,
        mode: normalizedMode,
        participantSolutionsVisible,
      });
      setTeamCount(teamsData.length);
      setParticipantCount(usersData.filter((u) => u.role === 'participant').length);
      setDataStoreInfo(storeData);
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
      fetchConfig();
    }
  }, [authLoading, fetchConfig]);

  const handleSaveConfig = async () => {
    setSaving(true);
    try {
      await api.post('/api/hack/config', config);
      showSnack('Configuration saved successfully');
      await refetchHackState();
    } catch (err) {
      showSnack(err instanceof ApiError ? err.message : 'Failed to save configuration', 'error');
    } finally {
      setSaving(false);
    }
  };

  const requestModeChange = (nextMode: 'team' | 'individual') => {
    if (nextMode === config.mode) return;
    setPendingMode(nextMode);
    setModeDialogOpen(true);
  };

  const handleConfirmModeChange = () => {
    if (!pendingMode) return;
    setConfig((prev) => ({
      ...prev,
      mode: pendingMode,
      participantSolutionsVisible: pendingMode === 'individual',
    }));
    setModeDialogOpen(false);
    setPendingMode(null);
  };

  const handleCancelModeChange = () => {
    setModeDialogOpen(false);
    setPendingMode(null);
  };

  const handleLaunch = async () => {
    setLaunching(true);
    setLaunchDialogOpen(false);
    try {
      await api.post('/api/hack/launch', {});
      showSnack('🚀 Hack launched successfully!', 'success');
      await refetchHackState();
      // Redirect to dashboard after launch
      setTimeout(() => router.push('/dashboard'), 2000);
    } catch (err) {
      showSnack(err instanceof ApiError ? err.message : 'Failed to launch hack', 'error');
    } finally {
      setLaunching(false);
    }
  };

  if (user?.role !== 'techlead') {
    return (
      <Box sx={{ textAlign: 'center', py: 8 }}>
        <Typography variant="h5" color="error">Access Denied</Typography>
        <Typography color="text.secondary" sx={{ mt: 1 }}>
          Configuration is only accessible to Tech Leads.
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

  const hasLaunchTargets = config.mode === 'individual' ? participantCount > 0 : teamCount > 0;
  const canLaunch = hasLaunchTargets && (hackState?.status === 'waiting' || hackState?.status === 'configuration' || hackState?.status === 'not_started');

  return (
    <Box>
      <Box sx={{ mb: 3, display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <Box>
          <Typography variant="h4" sx={{ background: 'linear-gradient(135deg, #A78BFA 0%, #60A5FA 100%)', WebkitBackgroundClip: 'text', WebkitTextFillColor: 'transparent' }}>
            Hack Configuration
          </Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
            Configure event lifecycle, operating mode, and participant content visibility.
          </Typography>
        </Box>
        <Box sx={{ display: 'flex', gap: 2 }}>
          <Button variant="contained" onClick={handleSaveConfig} disabled={saving || launching}>
            {saving ? <CircularProgress size={20} /> : 'Save Configuration'}
          </Button>
          <Button variant="contained" color="success" startIcon={<RocketLaunchIcon />} onClick={() => setLaunchDialogOpen(true)} disabled={!canLaunch || launching}>
            {launching ? <CircularProgress size={20} /> : 'Launch Hack'}
          </Button>
        </Box>
      </Box>

      {hackState && (
        <Paper sx={{ p: 2, mb: 3, background: 'linear-gradient(145deg, #1A1333 0%, #1E1045 100%)', border: '1px solid rgba(124, 58, 237, 0.15)' }}>
          <Typography variant="subtitle2" color="text.secondary">
            Current Status: <Chip label={hackState.status} size="small" color={hackState.status === 'active' ? 'success' : 'default'} />
          </Typography>
        </Paper>
      )}

      <Paper sx={{ p: 3, mb: 3, background: 'linear-gradient(145deg, #1A1333 0%, #1E1045 100%)', border: '1px solid rgba(124, 58, 237, 0.15)' }}>
        <Typography variant="h6" sx={{ mb: 2 }}>Operating Mode</Typography>
        <FormControl fullWidth sx={{ mb: 2 }}>
          <InputLabel id="mode-select-label">Mode</InputLabel>
          <Select
            labelId="mode-select-label"
            value={config.mode}
            label="Mode"
            onChange={(e) => requestModeChange(e.target.value as 'team' | 'individual')}
          >
            <MenuItem value="team">Team Mode</MenuItem>
            <MenuItem value="individual">Individual Mode</MenuItem>
          </Select>
        </FormControl>
        <FormControlLabel
          control={(
            <Switch
              checked={config.participantSolutionsVisible}
              onChange={(e) => setConfig((prev) => ({ ...prev, participantSolutionsVisible: e.target.checked }))}
            />
          )}
          label="Participants can view Solutions tab"
        />
        <Typography variant="body2" color="text.secondary">
          Default behavior: Team Mode = hidden, Individual Mode = visible. You can override this toggle.
        </Typography>
      </Paper>

      <Paper sx={{ p: 3, mb: 3, background: 'linear-gradient(145deg, #1A1333 0%, #1E1045 100%)', border: '1px solid rgba(124, 58, 237, 0.15)' }}>
        <Typography variant="h6" sx={{ mb: 2 }}>Content Path</Typography>
        <TextField fullWidth label="Content Directory" value={config.contentPath || ''} onChange={(e) => setConfig((prev) => ({ ...prev, contentPath: e.target.value }))} placeholder="hackcontent" helperText="Path to challenges and solutions (default: hackcontent)" />
      </Paper>

      <Paper sx={{ p: 3, mb: 3, background: 'linear-gradient(145deg, #1A1333 0%, #1E1045 100%)', border: '1px solid rgba(124, 58, 237, 0.15)' }}>
        <Typography variant="h6" sx={{ mb: 1 }}>Database Provider</Typography>
        <Typography variant="body2" color="text.secondary">
          Provider: <strong>{dataStoreInfo?.provider ?? 'Unknown'}</strong>
        </Typography>
        <Typography variant="body2" color="text.secondary">
          Target: <strong>{dataStoreInfo?.target ?? 'Unknown'}</strong>
        </Typography>
      </Paper>

      <Dialog open={launchDialogOpen} onClose={() => setLaunchDialogOpen(false)}>
        <DialogTitle>Launch Hack?</DialogTitle>
        <DialogContent>
          <Typography>Are you sure you want to launch the hack? This will start the event for all participants.</Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mt: 2 }}>
            • Mode: {config.mode === 'individual' ? 'Individual Mode' : 'Team Mode'}<br />
            • {config.mode === 'individual'
              ? `${participantCount} participant(s) available`
              : `${teamCount} team(s) configured in Teams`}<br />
            • Content: {config.contentPath || 'default'}
          </Typography>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setLaunchDialogOpen(false)}>Cancel</Button>
          <Button onClick={handleLaunch} variant="contained" color="success" autoFocus>Launch Now</Button>
        </DialogActions>
      </Dialog>

      <Dialog open={modeDialogOpen} onClose={handleCancelModeChange}>
        <DialogTitle>Change mode to {pendingMode === 'individual' ? 'Individual Mode' : 'Team Mode'}?</DialogTitle>
        <DialogContent>
          <Typography>
            Changing mode is disruptive and changes how challenge progress is scoped.
          </Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mt: 2 }}>
            Progress and timers will be preserved, but participant/coach behavior and visibility rules will change immediately.
          </Typography>
        </DialogContent>
        <DialogActions>
          <Button onClick={handleCancelModeChange}>Cancel</Button>
          <Button variant="contained" color="warning" onClick={handleConfirmModeChange}>
            Confirm Mode Change
          </Button>
        </DialogActions>
      </Dialog>

      <Snackbar open={snack.open} autoHideDuration={5000} onClose={() => setSnack((s) => ({ ...s, open: false }))} anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}>
        <Alert onClose={() => setSnack((s) => ({ ...s, open: false }))} severity={snack.severity} variant="filled" sx={{ width: '100%' }}>
          {snack.message}
        </Alert>
      </Snackbar>
    </Box>
  );
}
