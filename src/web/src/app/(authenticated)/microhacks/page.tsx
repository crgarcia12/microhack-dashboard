'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Paper from '@mui/material/Paper';
import Table from '@mui/material/Table';
import TableBody from '@mui/material/TableBody';
import TableCell from '@mui/material/TableCell';
import TableContainer from '@mui/material/TableContainer';
import TableHead from '@mui/material/TableHead';
import TableRow from '@mui/material/TableRow';
import Button from '@mui/material/Button';
import IconButton from '@mui/material/IconButton';
import Tooltip from '@mui/material/Tooltip';
import Dialog from '@mui/material/Dialog';
import DialogTitle from '@mui/material/DialogTitle';
import DialogContent from '@mui/material/DialogContent';
import DialogActions from '@mui/material/DialogActions';
import TextField from '@mui/material/TextField';
import FormControlLabel from '@mui/material/FormControlLabel';
import Switch from '@mui/material/Switch';
import Checkbox from '@mui/material/Checkbox';
import Snackbar from '@mui/material/Snackbar';
import Alert from '@mui/material/Alert';
import Chip from '@mui/material/Chip';
import CircularProgress from '@mui/material/CircularProgress';
import AddIcon from '@mui/icons-material/Add';
import EditIcon from '@mui/icons-material/Edit';
import DashboardIcon from '@mui/icons-material/Dashboard';
import PowerSettingsNewIcon from '@mui/icons-material/PowerSettingsNew';
import { api, ApiError } from '@/lib/api';
import { useAuth } from '@/contexts/AuthContext';

interface Microhack {
  microhackId: string;
  enabled: boolean;
  startDate: string | null;
  endDate: string | null;
  scheduleStart: string | null;
  scheduleEnd: string | null;
  timeZone: string | null;
  contentPath: string | null;
  environmentReference: string | null;
  teams: string[];
  teamCount: number;
}

interface MicrohackFormState {
  microhackId: string;
  enabled: boolean;
  scheduleStart: string;
  scheduleEnd: string;
  timeZone: string;
  contentPath: string;
  environmentReference: string;
  teams: string[];
}

interface SnackState {
  open: boolean;
  message: string;
  severity: 'success' | 'error' | 'info' | 'warning';
}

type MicrohackLifecycleState = 'Not started' | 'Started' | 'Completed' | 'Disabled';

function toDateTimeInputValue(value: string | null): string {
  if (!value) return '';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '';
  const pad = (part: number) => String(part).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

function toDisplayDateTime(value: string | null): string {
  if (!value) return '—';
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString();
}

function getMicrohackLifecycleState(microhack: Microhack): MicrohackLifecycleState {
  if (!microhack.enabled) return 'Disabled';

  const now = Date.now();
  const startDate = microhack.startDate ?? microhack.scheduleStart;
  const endDate = microhack.endDate ?? microhack.scheduleEnd;
  const startMs = startDate ? new Date(startDate).getTime() : Number.NaN;
  const endMs = endDate ? new Date(endDate).getTime() : Number.NaN;

  if (!Number.isNaN(startMs) && now < startMs) return 'Not started';
  if (!Number.isNaN(endMs) && now >= endMs) return 'Completed';
  return 'Started';
}

function getMicrohackLifecycleColor(state: MicrohackLifecycleState): 'default' | 'success' | 'warning' | 'info' {
  if (state === 'Started') return 'success';
  if (state === 'Not started') return 'info';
  if (state === 'Completed') return 'warning';
  return 'default';
}

function createEmptyFormState(): MicrohackFormState {
  return {
    microhackId: '',
    enabled: true,
    scheduleStart: '',
    scheduleEnd: '',
    timeZone: '',
    contentPath: '',
    environmentReference: '',
    teams: [],
  };
}

export default function MicrohacksPage() {
  const { user, loading: authLoading } = useAuth();
  const router = useRouter();
  const [microhacks, setMicrohacks] = useState<Microhack[]>([]);
  const [availableTeams, setAvailableTeams] = useState<string[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [actionLoading, setActionLoading] = useState<string | null>(null);
  const [snack, setSnack] = useState<SnackState>({ open: false, message: '', severity: 'success' });
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingMicrohackId, setEditingMicrohackId] = useState<string | null>(null);
  const [form, setForm] = useState<MicrohackFormState>(createEmptyFormState);

  const showSnack = useCallback((message: string, severity: SnackState['severity'] = 'success') => {
    setSnack({ open: true, message, severity });
  }, []);

  const fetchMicrohacks = useCallback(async () => {
    if (!user || user.role !== 'techlead') {
      setLoading(false);
      return;
    }

    try {
      const [microhackData, teamData] = await Promise.all([
        api.get<Microhack[]>('/api/admin/microhacks'),
        api.get<string[]>('/api/admin/team-admin/teams'),
      ]);
      setMicrohacks(microhackData);
      setAvailableTeams(teamData);
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
      void fetchMicrohacks();
    }
  }, [authLoading, fetchMicrohacks]);

  const openCreateDialog = () => {
    setEditingMicrohackId(null);
    setForm(createEmptyFormState());
    setDialogOpen(true);
  };

  const openEditDialog = (microhack: Microhack) => {
    setEditingMicrohackId(microhack.microhackId);
    setForm({
      microhackId: microhack.microhackId,
      enabled: microhack.enabled,
      scheduleStart: toDateTimeInputValue(microhack.scheduleStart),
      scheduleEnd: toDateTimeInputValue(microhack.scheduleEnd),
      timeZone: microhack.timeZone ?? '',
      contentPath: microhack.contentPath ?? '',
      environmentReference: microhack.environmentReference ?? '',
      teams: microhack.teams ?? [],
    });
    setDialogOpen(true);
  };

  const buildPayload = () => ({
    enabled: form.enabled,
    scheduleStart: form.scheduleStart ? new Date(form.scheduleStart).toISOString() : null,
    scheduleEnd: form.scheduleEnd ? new Date(form.scheduleEnd).toISOString() : null,
    timeZone: form.timeZone.trim() || null,
    contentPath: form.contentPath.trim() || null,
    environmentReference: form.environmentReference.trim() || null,
    teams: form.teams,
  });

  const toggleTeamSelection = (teamName: string) => {
    setForm((prev) => {
      const exists = prev.teams.some((name) => name.toLowerCase() === teamName.toLowerCase());
      return {
        ...prev,
        teams: exists
          ? prev.teams.filter((name) => name.toLowerCase() !== teamName.toLowerCase())
          : [...prev.teams, teamName].sort((a, b) => a.localeCompare(b)),
      };
    });
  };

  const handleSave = async () => {
    if (!editingMicrohackId && !form.microhackId.trim()) {
      showSnack('Microhack ID is required', 'error');
      return;
    }

    setSaving(true);
    try {
      const payload = buildPayload();
      if (editingMicrohackId) {
        await api.put(`/api/admin/microhacks/${encodeURIComponent(editingMicrohackId)}`, payload);
        showSnack(`Updated "${editingMicrohackId}"`);
      } else {
        await api.post('/api/admin/microhacks', {
          microhackId: form.microhackId.trim(),
          ...payload,
        });
        showSnack(`Created "${form.microhackId.trim()}"`);
      }

      setDialogOpen(false);
      await fetchMicrohacks();
    } catch (err) {
      showSnack(err instanceof ApiError ? err.message : 'Failed to save microhack', 'error');
    } finally {
      setSaving(false);
    }
  };

  const handleSetEnabled = async (microhackId: string, enabled: boolean) => {
    const key = `${microhackId}-${enabled ? 'enable' : 'disable'}`;
    setActionLoading(key);
    try {
      await api.post(`/api/admin/microhacks/${encodeURIComponent(microhackId)}/${enabled ? 'enable' : 'disable'}`, {});
      showSnack(`"${microhackId}" ${enabled ? 'enabled' : 'disabled'}`);
      await fetchMicrohacks();
    } catch (err) {
      showSnack(err instanceof ApiError ? err.message : 'Failed to update microhack state', 'error');
    } finally {
      setActionLoading(null);
    }
  };

  const sortedMicrohacks = useMemo(
    () => [...microhacks].sort((a, b) => a.microhackId.localeCompare(b.microhackId)),
    [microhacks],
  );

  if (authLoading || loading) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', py: 8 }}>
        <CircularProgress />
      </Box>
    );
  }

  if (user?.role !== 'techlead') {
    return (
      <Box sx={{ textAlign: 'center', py: 8 }}>
        <Typography variant="h5" color="error">Access Denied</Typography>
        <Typography color="text.secondary" sx={{ mt: 1 }}>
          Microhack management is only accessible to Tech Leads.
        </Typography>
      </Box>
    );
  }

  return (
    <Box>
      <Box sx={{ mb: 3, display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 2, flexWrap: 'wrap' }}>
        <Box>
          <Typography
            variant="h4"
            sx={{
              background: 'linear-gradient(135deg, #A78BFA 0%, #60A5FA 100%)',
              WebkitBackgroundClip: 'text',
              WebkitTextFillColor: 'transparent',
            }}
          >
            Manage Microhacks
          </Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
            Configure and control each microhack independently.
          </Typography>
        </Box>
        <Button variant="contained" startIcon={<AddIcon />} onClick={openCreateDialog}>
          New Microhack
        </Button>
      </Box>

      {sortedMicrohacks.length === 0 ? (
        <Box sx={{ textAlign: 'center', py: 8 }}>
          <Typography variant="h5" color="text.secondary" sx={{ mb: 1 }}>
            No microhacks configured
          </Typography>
          <Typography color="text.secondary">
            Create your first microhack to start managing isolated event environments.
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
                <TableCell sx={{ fontWeight: 700 }}>Microhack</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>Lifecycle State</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>Schedule</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>Teams</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>Content Path</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>Environment</TableCell>
                <TableCell sx={{ fontWeight: 700 }} align="center">Actions</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {sortedMicrohacks.map((microhack) => (
                <TableRow key={microhack.microhackId} hover>
                  <TableCell>
                    <Typography variant="body2" fontWeight={600}>
                      {microhack.microhackId}
                    </Typography>
                    <Typography variant="caption" color="text.secondary">
                      {microhack.timeZone || 'UTC'}
                    </Typography>
                  </TableCell>
                  <TableCell>
                    {(() => {
                      const lifecycleState = getMicrohackLifecycleState(microhack);
                      return (
                        <Chip
                          size="small"
                          color={getMicrohackLifecycleColor(lifecycleState)}
                          label={lifecycleState}
                        />
                      );
                    })()}
                  </TableCell>
                  <TableCell>
                    <Typography variant="body2">{toDisplayDateTime(microhack.scheduleStart)}</Typography>
                    <Typography variant="body2" color="text.secondary">to {toDisplayDateTime(microhack.scheduleEnd)}</Typography>
                  </TableCell>
                  <TableCell>
                    <Typography variant="body2">
                      {microhack.teamCount} team{microhack.teamCount === 1 ? '' : 's'}
                    </Typography>
                    {microhack.teams.length > 0 && (
                      <Typography variant="caption" color="text.secondary">
                        {microhack.teams.join(', ')}
                      </Typography>
                    )}
                  </TableCell>
                  <TableCell>{microhack.contentPath || '—'}</TableCell>
                  <TableCell>{microhack.environmentReference || '—'}</TableCell>
                  <TableCell align="center">
                    <Box sx={{ display: 'flex', justifyContent: 'center', gap: 0.5 }}>
                      <Tooltip title="Open Dashboard">
                        <span>
                          <IconButton
                            size="small"
                            color="primary"
                            onClick={() => router.push(`/dashboard?microhackId=${encodeURIComponent(microhack.microhackId)}`)}
                          >
                            <DashboardIcon fontSize="small" />
                          </IconButton>
                        </span>
                      </Tooltip>
                      <Tooltip title="Edit">
                        <span>
                          <IconButton size="small" onClick={() => openEditDialog(microhack)}>
                            <EditIcon fontSize="small" />
                          </IconButton>
                        </span>
                      </Tooltip>
                      <Tooltip title={microhack.enabled ? 'Disable' : 'Enable'}>
                        <span>
                          <IconButton
                            size="small"
                            color={microhack.enabled ? 'warning' : 'success'}
                            disabled={actionLoading === `${microhack.microhackId}-${microhack.enabled ? 'disable' : 'enable'}`}
                            onClick={() => handleSetEnabled(microhack.microhackId, !microhack.enabled)}
                          >
                            <PowerSettingsNewIcon fontSize="small" />
                          </IconButton>
                        </span>
                      </Tooltip>
                    </Box>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      <Dialog open={dialogOpen} onClose={() => setDialogOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle>{editingMicrohackId ? `Edit ${editingMicrohackId}` : 'Create Microhack'}</DialogTitle>
        <DialogContent>
          {!editingMicrohackId && (
            <TextField
              margin="dense"
              fullWidth
              label="Microhack ID"
              value={form.microhackId}
              onChange={(event) => setForm((prev) => ({ ...prev, microhackId: event.target.value }))}
              autoFocus
            />
          )}
          <FormControlLabel
            sx={{ mt: 1 }}
            control={(
              <Switch
                checked={form.enabled}
                onChange={(event) => setForm((prev) => ({ ...prev, enabled: event.target.checked }))}
              />
            )}
            label="Enabled"
          />
          <TextField
            margin="dense"
            fullWidth
            label="Schedule Start"
            type="datetime-local"
            value={form.scheduleStart}
            onChange={(event) => setForm((prev) => ({ ...prev, scheduleStart: event.target.value }))}
            InputLabelProps={{ shrink: true }}
          />
          <TextField
            margin="dense"
            fullWidth
            label="Schedule End"
            type="datetime-local"
            value={form.scheduleEnd}
            onChange={(event) => setForm((prev) => ({ ...prev, scheduleEnd: event.target.value }))}
            InputLabelProps={{ shrink: true }}
          />
          <TextField
            margin="dense"
            fullWidth
            label="Time Zone"
            placeholder="UTC"
            value={form.timeZone}
            onChange={(event) => setForm((prev) => ({ ...prev, timeZone: event.target.value }))}
          />
          <TextField
            margin="dense"
            fullWidth
            label="Content Path"
            placeholder="hackcontent"
            value={form.contentPath}
            onChange={(event) => setForm((prev) => ({ ...prev, contentPath: event.target.value }))}
          />
          <TextField
            margin="dense"
            fullWidth
            label="Environment Reference"
            value={form.environmentReference}
            onChange={(event) => setForm((prev) => ({ ...prev, environmentReference: event.target.value }))}
          />
          <Box sx={{ mt: 2 }}>
            <Typography variant="subtitle2" sx={{ mb: 1 }}>
              Teams in this hackathon
            </Typography>
            {availableTeams.length === 0 ? (
              <Typography variant="body2" color="text.secondary">
                No teams available. Create teams first in Manage Users.
              </Typography>
            ) : (
              <Box sx={{ maxHeight: 180, overflowY: 'auto', border: '1px solid rgba(124, 58, 237, 0.2)', borderRadius: 1, p: 1 }}>
                {availableTeams.map((teamName) => (
                  <FormControlLabel
                    key={teamName}
                    control={(
                      <Checkbox
                        checked={form.teams.some((name) => name.toLowerCase() === teamName.toLowerCase())}
                        onChange={() => toggleTeamSelection(teamName)}
                      />
                    )}
                    label={teamName}
                  />
                ))}
              </Box>
            )}
          </Box>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDialogOpen(false)} disabled={saving}>Cancel</Button>
          <Button onClick={handleSave} variant="contained" disabled={saving}>
            {saving ? <CircularProgress size={20} /> : 'Save'}
          </Button>
        </DialogActions>
      </Dialog>

      <Snackbar open={snack.open} autoHideDuration={5000} onClose={() => setSnack((prev) => ({ ...prev, open: false }))}>
        <Alert onClose={() => setSnack((prev) => ({ ...prev, open: false }))} severity={snack.severity} variant="filled" sx={{ width: '100%' }}>
          {snack.message}
        </Alert>
      </Snackbar>
    </Box>
  );
}
