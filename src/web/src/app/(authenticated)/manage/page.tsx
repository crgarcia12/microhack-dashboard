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
import TextField from '@mui/material/TextField';
import Select from '@mui/material/Select';
import MenuItem from '@mui/material/MenuItem';
import FormControl from '@mui/material/FormControl';
import InputLabel from '@mui/material/InputLabel';
import Dialog from '@mui/material/Dialog';
import DialogTitle from '@mui/material/DialogTitle';
import DialogContent from '@mui/material/DialogContent';
import DialogActions from '@mui/material/DialogActions';
import Snackbar from '@mui/material/Snackbar';
import Alert from '@mui/material/Alert';
import Chip from '@mui/material/Chip';
import CircularProgress from '@mui/material/CircularProgress';
import Tabs from '@mui/material/Tabs';
import Tab from '@mui/material/Tab';
import AddIcon from '@mui/icons-material/Add';
import EditIcon from '@mui/icons-material/Edit';
import DeleteIcon from '@mui/icons-material/Delete';
import GroupIcon from '@mui/icons-material/Group';
import PersonIcon from '@mui/icons-material/Person';
import { api, ApiError } from '@/lib/api';
import { useAuth } from '@/contexts/AuthContext';

interface UserInfo {
  username: string;
  role: string;
  team: string | null;
}

interface SnackState {
  open: boolean;
  message: string;
  severity: 'success' | 'error' | 'info' | 'warning';
}

const ROLE_COLORS: Record<string, 'primary' | 'secondary' | 'warning'> = {
  participant: 'primary',
  coach: 'secondary',
  techlead: 'warning',
};

const ROLE_LABELS: Record<string, string> = {
  participant: 'Hacker',
  coach: 'Coach',
  techlead: 'Tech Lead',
};

export default function ManagePage() {
  const { user, loading: authLoading } = useAuth();
  const [tab, setTab] = useState(0);
  const [teams, setTeams] = useState<string[]>([]);
  const [users, setUsers] = useState<UserInfo[]>([]);
  const [loading, setLoading] = useState(true);
  const [snack, setSnack] = useState<SnackState>({ open: false, message: '', severity: 'success' });

  // Dialog state
  const [teamDialogOpen, setTeamDialogOpen] = useState(false);
  const [newTeamName, setNewTeamName] = useState('');

  const [userDialogOpen, setUserDialogOpen] = useState(false);
  const [editingUser, setEditingUser] = useState<UserInfo | null>(null);
  const [userForm, setUserForm] = useState({ username: '', password: '', role: 'participant', team: '' });

  const showSnack = useCallback((message: string, severity: SnackState['severity'] = 'success') => {
    setSnack({ open: true, message, severity });
  }, []);

  const fetchData = useCallback(async () => {
    if (!user || user.role !== 'techlead') {
      setLoading(false);
      return;
    }

    try {
      const [teamsData, usersData] = await Promise.all([
        api.get<string[]>('/api/admin/team-admin/teams'),
        api.get<UserInfo[]>('/api/admin/team-admin/users'),
      ]);
      setTeams(teamsData);
      setUsers(usersData);
    } catch (err) {
      if (err instanceof ApiError) showSnack(err.message, 'error');
    } finally {
      setLoading(false);
    }
  }, [showSnack, user]);

  useEffect(() => {
    if (!authLoading) {
      fetchData();
    }
  }, [authLoading, fetchData]);

  // ── Team operations ──
  const handleCreateTeam = async () => {
    if (!newTeamName.trim()) return;
    try {
      await api.post('/api/admin/team-admin/teams', { name: newTeamName.trim() });
      showSnack(`Team "${newTeamName.trim()}" created`);
      setNewTeamName('');
      setTeamDialogOpen(false);
      await fetchData();
    } catch (err) {
      showSnack(err instanceof ApiError ? err.message : 'Failed to create team', 'error');
    }
  };

  const handleDeleteTeam = async (teamName: string) => {
    try {
      await api.delete(`/api/admin/team-admin/teams/${encodeURIComponent(teamName)}`);
      showSnack(`Team "${teamName}" deleted`);
      await fetchData();
    } catch (err) {
      showSnack(err instanceof ApiError ? err.message : 'Failed to delete team', 'error');
    }
  };

  // ── User operations ──
  const openCreateUser = () => {
    setEditingUser(null);
    setUserForm({ username: '', password: '', role: 'participant', team: teams[0] || '' });
    setUserDialogOpen(true);
  };

  const openEditUser = (u: UserInfo) => {
    setEditingUser(u);
    setUserForm({ username: u.username, password: '', role: u.role, team: u.team || '' });
    setUserDialogOpen(true);
  };

  const handleSaveUser = async () => {
    try {
      if (editingUser) {
        // Update
        const body: Record<string, string | null> = {};
        if (userForm.password) body.password = userForm.password;
        if (userForm.role !== editingUser.role) body.role = userForm.role;
        if (userForm.role === 'techlead') {
          body.team = null;
        } else if (userForm.team !== (editingUser.team || '')) {
          body.team = userForm.team;
        }
        await api.put(`/api/admin/team-admin/users/${encodeURIComponent(editingUser.username)}`, body);
        showSnack(`User "${editingUser.username}" updated`);
      } else {
        // Create
        await api.post('/api/admin/team-admin/users', {
          username: userForm.username,
          password: userForm.password,
          role: userForm.role,
          team: userForm.role === 'techlead' ? null : userForm.team,
        });
        showSnack(`User "${userForm.username}" created`);
      }
      setUserDialogOpen(false);
      await fetchData();
    } catch (err) {
      showSnack(err instanceof ApiError ? err.message : 'Failed to save user', 'error');
    }
  };

  const handleDeleteUser = async (username: string) => {
    try {
      await api.delete(`/api/admin/team-admin/users/${encodeURIComponent(username)}`);
      showSnack(`User "${username}" deleted`);
      await fetchData();
    } catch (err) {
      showSnack(err instanceof ApiError ? err.message : 'Failed to delete user', 'error');
    }
  };

  if (user?.role !== 'techlead') {
    return (
      <Box sx={{ textAlign: 'center', py: 8 }}>
        <Typography variant="h5" color="error">Access Denied</Typography>
        <Typography color="text.secondary" sx={{ mt: 1 }}>
          Management is only accessible to Tech Leads.
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

  return (
    <Box>
      <Box sx={{ mb: 3 }}>
        <Typography
          variant="h4"
          sx={{
            background: 'linear-gradient(135deg, #A78BFA 0%, #60A5FA 100%)',
            WebkitBackgroundClip: 'text',
            WebkitTextFillColor: 'transparent',
          }}
        >
          Teams &amp; Users
        </Typography>
        <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
          Create teams, add hackers, assign coaches
        </Typography>
      </Box>

      <Tabs value={tab} onChange={(_, v) => setTab(v)} sx={{ mb: 3 }}>
        <Tab icon={<GroupIcon />} label={`Teams (${teams.length})`} iconPosition="start" />
        <Tab icon={<PersonIcon />} label={`Users (${users.length})`} iconPosition="start" />
      </Tabs>

      {/* ── Teams Tab ── */}
      {tab === 0 && (
        <>
          <Box sx={{ mb: 2 }}>
            <Button variant="contained" startIcon={<AddIcon />} onClick={() => setTeamDialogOpen(true)}>
              Create Team
            </Button>
          </Box>
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
                  <TableCell sx={{ fontWeight: 700 }}>Members</TableCell>
                  <TableCell sx={{ fontWeight: 700 }}>Coaches</TableCell>
                  <TableCell sx={{ fontWeight: 700 }} align="right">Actions</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {teams.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={4}>
                      <Typography color="text.secondary" sx={{ textAlign: 'center', py: 3 }}>
                        No teams yet. Create one to get started.
                      </Typography>
                    </TableCell>
                  </TableRow>
                ) : (
                  teams.map((teamName) => {
                    const members = users.filter((u) => u.team === teamName && u.role === 'participant');
                    const coaches = users.filter((u) => u.team === teamName && u.role === 'coach');
                    return (
                      <TableRow key={teamName} hover>
                        <TableCell>
                          <Typography variant="body2" fontWeight={600}>{teamName}</Typography>
                        </TableCell>
                        <TableCell>
                          {members.length > 0 ? (
                            <Box sx={{ display: 'flex', gap: 0.5, flexWrap: 'wrap' }}>
                              {members.map((m) => (
                                <Chip key={m.username} label={m.username} size="small" variant="outlined" />
                              ))}
                            </Box>
                          ) : (
                            <Typography variant="body2" color="text.secondary">No hackers</Typography>
                          )}
                        </TableCell>
                        <TableCell>
                          {coaches.length > 0 ? (
                            <Box sx={{ display: 'flex', gap: 0.5, flexWrap: 'wrap' }}>
                              {coaches.map((c) => (
                                <Chip key={c.username} label={c.username} size="small" color="secondary" variant="outlined" />
                              ))}
                            </Box>
                          ) : (
                            <Typography variant="body2" color="text.secondary">No coaches</Typography>
                          )}
                        </TableCell>
                        <TableCell align="right">
                          <Tooltip title="Delete team">
                            <span>
                              <IconButton
                                size="small"
                                color="error"
                                onClick={() => handleDeleteTeam(teamName)}
                                disabled={members.length > 0 || coaches.length > 0}
                              >
                                <DeleteIcon fontSize="small" />
                              </IconButton>
                            </span>
                          </Tooltip>
                        </TableCell>
                      </TableRow>
                    );
                  })
                )}
              </TableBody>
            </Table>
          </TableContainer>
        </>
      )}

      {/* ── Users Tab ── */}
      {tab === 1 && (
        <>
          <Box sx={{ mb: 2 }}>
            <Button variant="contained" startIcon={<AddIcon />} onClick={openCreateUser}>
              Create User
            </Button>
          </Box>
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
                  <TableCell sx={{ fontWeight: 700 }}>Username</TableCell>
                  <TableCell sx={{ fontWeight: 700 }}>Role</TableCell>
                  <TableCell sx={{ fontWeight: 700 }}>Team</TableCell>
                  <TableCell sx={{ fontWeight: 700 }} align="right">Actions</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {users.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={4}>
                      <Typography color="text.secondary" sx={{ textAlign: 'center', py: 3 }}>
                        No users yet. Create one to get started.
                      </Typography>
                    </TableCell>
                  </TableRow>
                ) : (
                  users.map((u) => (
                    <TableRow key={u.username} hover>
                      <TableCell>
                        <Typography variant="body2" fontWeight={600}>{u.username}</Typography>
                      </TableCell>
                      <TableCell>
                        <Chip
                          label={ROLE_LABELS[u.role] || u.role}
                          size="small"
                          color={ROLE_COLORS[u.role] || 'default'}
                          variant="outlined"
                        />
                      </TableCell>
                      <TableCell>
                        <Typography variant="body2" color={u.team ? 'text.primary' : 'text.secondary'}>
                          {u.team || '—'}
                        </Typography>
                      </TableCell>
                      <TableCell align="right">
                        <Tooltip title="Edit user">
                          <IconButton size="small" onClick={() => openEditUser(u)}>
                            <EditIcon fontSize="small" />
                          </IconButton>
                        </Tooltip>
                        <Tooltip title="Delete user">
                          <IconButton size="small" color="error" onClick={() => handleDeleteUser(u.username)}>
                            <DeleteIcon fontSize="small" />
                          </IconButton>
                        </Tooltip>
                      </TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </TableContainer>
        </>
      )}

      {/* ── Create Team Dialog ── */}
      <Dialog open={teamDialogOpen} onClose={() => setTeamDialogOpen(false)} maxWidth="xs" fullWidth>
        <DialogTitle>Create Team</DialogTitle>
        <DialogContent>
          <TextField
            autoFocus
            fullWidth
            label="Team Name"
            value={newTeamName}
            onChange={(e) => setNewTeamName(e.target.value)}
            sx={{ mt: 1 }}
            onKeyDown={(e) => { if (e.key === 'Enter') handleCreateTeam(); }}
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setTeamDialogOpen(false)}>Cancel</Button>
          <Button variant="contained" onClick={handleCreateTeam} disabled={!newTeamName.trim()}>
            Create
          </Button>
        </DialogActions>
      </Dialog>

      {/* ── Create / Edit User Dialog ── */}
      <Dialog open={userDialogOpen} onClose={() => setUserDialogOpen(false)} maxWidth="xs" fullWidth>
        <DialogTitle>{editingUser ? `Edit ${editingUser.username}` : 'Create User'}</DialogTitle>
        <DialogContent>
          <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2, mt: 1 }}>
            {!editingUser && (
              <TextField
                autoFocus
                fullWidth
                label="Username"
                value={userForm.username}
                onChange={(e) => setUserForm((f) => ({ ...f, username: e.target.value }))}
              />
            )}
            <TextField
              fullWidth
              label={editingUser ? 'New Password (leave blank to keep)' : 'Password'}
              type="password"
              value={userForm.password}
              onChange={(e) => setUserForm((f) => ({ ...f, password: e.target.value }))}
            />
            <FormControl fullWidth>
              <InputLabel>Role</InputLabel>
              <Select
                value={userForm.role}
                label="Role"
                onChange={(e) => setUserForm((f) => ({ ...f, role: e.target.value }))}
              >
                <MenuItem value="participant">Hacker</MenuItem>
                <MenuItem value="coach">Coach</MenuItem>
                <MenuItem value="techlead">Tech Lead</MenuItem>
              </Select>
            </FormControl>
            {userForm.role !== 'techlead' && (
              <FormControl fullWidth>
                <InputLabel>Team</InputLabel>
                <Select
                  value={userForm.team}
                  label="Team"
                  onChange={(e) => setUserForm((f) => ({ ...f, team: e.target.value }))}
                >
                  {teams.map((t) => (
                    <MenuItem key={t} value={t}>{t}</MenuItem>
                  ))}
                </Select>
              </FormControl>
            )}
          </Box>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setUserDialogOpen(false)}>Cancel</Button>
          <Button
            variant="contained"
            onClick={handleSaveUser}
            disabled={
              !editingUser && (!userForm.username.trim() || !userForm.password)
            }
          >
            {editingUser ? 'Save' : 'Create'}
          </Button>
        </DialogActions>
      </Dialog>

      {/* ── Snackbar ── */}
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
