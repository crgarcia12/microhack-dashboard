'use client';

import { useEffect, useState, useCallback } from 'react';
import { useRouter } from 'next/navigation';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import Paper from '@mui/material/Paper';
import TextField from '@mui/material/TextField';
import IconButton from '@mui/material/IconButton';
import Chip from '@mui/material/Chip';
import CircularProgress from '@mui/material/CircularProgress';
import Snackbar from '@mui/material/Snackbar';
import Alert from '@mui/material/Alert';
import Dialog from '@mui/material/Dialog';
import DialogTitle from '@mui/material/DialogTitle';
import DialogContent from '@mui/material/DialogContent';
import DialogActions from '@mui/material/DialogActions';
import AddIcon from '@mui/icons-material/Add';
import DeleteIcon from '@mui/icons-material/Delete';
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

  // New team/coach input states
  const [newTeamName, setNewTeamName] = useState('');
  const [newCoach, setNewCoach] = useState('');

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
      const result = await api.get<HackConfig>('/api/hack/config');
      setConfig(result);
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

  const handleAddTeam = () => {
    if (!newTeamName.trim()) return;
    setConfig((prev) => ({
      ...prev,
      teams: [...prev.teams, { name: newTeamName.trim(), members: [] }],
    }));
    setNewTeamName('');
  };

  const handleRemoveTeam = (index: number) => {
    setConfig((prev) => ({
      ...prev,
      teams: prev.teams.filter((_, i) => i !== index),
    }));
  };

  const handleAddMember = (teamIndex: number, member: string) => {
    if (!member.trim()) return;
    setConfig((prev) => {
      const teams = [...prev.teams];
      teams[teamIndex].members = [...teams[teamIndex].members, member.trim()];
      return { ...prev, teams };
    });
  };

  const handleRemoveMember = (teamIndex: number, memberIndex: number) => {
    setConfig((prev) => {
      const teams = [...prev.teams];
      teams[teamIndex].members = teams[teamIndex].members.filter((_, i) => i !== memberIndex);
      return { ...prev, teams };
    });
  };

  const handleAddCoach = () => {
    if (!newCoach.trim()) return;
    setConfig((prev) => ({
      ...prev,
      coaches: [...prev.coaches, newCoach.trim()],
    }));
    setNewCoach('');
  };

  const handleRemoveCoach = (index: number) => {
    setConfig((prev) => ({
      ...prev,
      coaches: prev.coaches.filter((_, i) => i !== index),
    }));
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

  const canLaunch = config.teams.length > 0 && (hackState?.status === 'waiting' || hackState?.status === 'configuration');

  return (
    <Box>
      <Box sx={{ mb: 3, display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <Box>
          <Typography variant="h4" sx={{ background: 'linear-gradient(135deg, #A78BFA 0%, #60A5FA 100%)', WebkitBackgroundClip: 'text', WebkitTextFillColor: 'transparent' }}>
            Hack Configuration
          </Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
            Set up teams, coaches, and content for the microhack event
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
        <Typography variant="h6" sx={{ mb: 2 }}>Teams ({config.teams.length})</Typography>
        
        <Box sx={{ display: 'flex', gap: 1, mb: 3 }}>
          <TextField fullWidth size="small" label="Team Name" value={newTeamName} onChange={(e) => setNewTeamName(e.target.value)} onKeyDown={(e) => e.key === 'Enter' && handleAddTeam()} placeholder="e.g., Team Alpha" />
          <Button variant="contained" startIcon={<AddIcon />} onClick={handleAddTeam}>Add Team</Button>
        </Box>

        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
          {config.teams.map((team, teamIndex) => (
            <Paper key={teamIndex} sx={{ p: 2, background: 'rgba(124, 58, 237, 0.05)', border: '1px solid rgba(124, 58, 237, 0.1)' }}>
              <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
                <Typography variant="subtitle1" fontWeight={600}>{team.name}</Typography>
                <IconButton size="small" color="error" onClick={() => handleRemoveTeam(teamIndex)}><DeleteIcon /></IconButton>
              </Box>
              
              <Box sx={{ display: 'flex', gap: 1, mb: 1 }}>
                <TextField fullWidth size="small" placeholder="Add member..." onKeyDown={(e) => { if (e.key === 'Enter') { handleAddMember(teamIndex, (e.target as HTMLInputElement).value); (e.target as HTMLInputElement).value = ''; } }} />
              </Box>
              
              <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 1 }}>
                {team.members.map((member, memberIndex) => (
                  <Chip key={memberIndex} label={member} onDelete={() => handleRemoveMember(teamIndex, memberIndex)} size="small" />
                ))}
              </Box>
            </Paper>
          ))}
        </Box>
      </Paper>

      <Paper sx={{ p: 3, background: 'linear-gradient(145deg, #1A1333 0%, #1E1045 100%)', border: '1px solid rgba(124, 58, 237, 0.15)' }}>
        <Typography variant="h6" sx={{ mb: 2 }}>Coaches ({config.coaches.length})</Typography>
        
        <Box sx={{ display: 'flex', gap: 1, mb: 2 }}>
          <TextField fullWidth size="small" label="Coach Name" value={newCoach} onChange={(e) => setNewCoach(e.target.value)} onKeyDown={(e) => e.key === 'Enter' && handleAddCoach()} placeholder="e.g., John Doe" />
          <Button variant="contained" startIcon={<AddIcon />} onClick={handleAddCoach}>Add Coach</Button>
        </Box>

        <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 1 }}>
          {config.coaches.map((coach, index) => (
            <Chip key={index} label={coach} onDelete={() => handleRemoveCoach(index)} color="primary" variant="outlined" />
          ))}
        </Box>
      </Paper>

      <Dialog open={launchDialogOpen} onClose={() => setLaunchDialogOpen(false)}>
        <DialogTitle>Launch Hack?</DialogTitle>
        <DialogContent>
          <Typography>Are you sure you want to launch the hack? This will start the event for all participants.</Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mt: 2 }}>
            • {config.teams.length} team(s) configured<br />
            • {config.coaches.length} coach(es) assigned<br />
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
