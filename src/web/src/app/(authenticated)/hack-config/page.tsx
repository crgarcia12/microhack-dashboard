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
  contentPath?: string;
  teams: TeamConfig[];
  coaches: string[];
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
  const [dataStoreInfo, setDataStoreInfo] = useState<DataStoreInfo | null>(null);

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
      const [configData, teamsData, storeData] = await Promise.all([
        api.get<HackConfig>('/api/hack/config'),
        api.get<string[]>('/api/admin/team-admin/teams'),
        api.get<DataStoreInfo>('/api/hack/datastore'),
      ]);
      setConfig(configData);
      setTeamCount(teamsData.length);
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

  const canLaunch = teamCount > 0 && (hackState?.status === 'waiting' || hackState?.status === 'configuration' || hackState?.status === 'not_started');

  return (
    <Box>
      <Box sx={{ mb: 3, display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <Box>
          <Typography variant="h4" sx={{ background: 'linear-gradient(135deg, #A78BFA 0%, #60A5FA 100%)', WebkitBackgroundClip: 'text', WebkitTextFillColor: 'transparent' }}>
            Hack Configuration
          </Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
            Set event content and lifecycle. Teams and coaches are managed in the Teams tab.
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
            • {teamCount} team(s) configured in Teams<br />
            • Content: {config.contentPath || 'default'}
          </Typography>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setLaunchDialogOpen(false)}>Cancel</Button>
          <Button onClick={handleLaunch} variant="contained" color="success" autoFocus>Launch Now</Button>
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
