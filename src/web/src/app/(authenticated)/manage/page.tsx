'use client';

import { Fragment, useEffect, useState, useCallback, useMemo, type ChangeEvent } from 'react';
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
import Collapse from '@mui/material/Collapse';
import AddIcon from '@mui/icons-material/Add';
import EditIcon from '@mui/icons-material/Edit';
import DeleteIcon from '@mui/icons-material/Delete';
import KeyboardArrowDownIcon from '@mui/icons-material/KeyboardArrowDown';
import KeyboardArrowRightIcon from '@mui/icons-material/KeyboardArrowRight';
import { api, ApiError } from '@/lib/api';
import { useAuth } from '@/contexts/AuthContext';
import { useHackState } from '@/contexts/HackStateContext';

interface UserInfo {
  username: string;
  role: string;
  team: string | null;
}

interface MicrohackSummary {
  microhackId: string;
  teams: string[];
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

const TEAM_IMPORT_CSV_PATH = '/api/admin/team-admin/teams/import-csv';
const TEAM_EXPORT_CSV_PATH = '/api/admin/team-admin/teams/export-csv';
const USER_IMPORT_CSV_PATH = '/api/admin/team-admin/users/import-csv';
const USER_EXPORT_CSV_PATH = '/api/admin/team-admin/users/export-csv';

export default function ManagePage() {
  const { user, loading: authLoading } = useAuth();
  const { hackState } = useHackState();
  const [teams, setTeams] = useState<string[]>([]);
  const [microhacks, setMicrohacks] = useState<MicrohackSummary[]>([]);
  const [users, setUsers] = useState<UserInfo[]>([]);
  const [loading, setLoading] = useState(true);
  const [snack, setSnack] = useState<SnackState>({ open: false, message: '', severity: 'success' });

  // Dialog state
  const [teamDialogOpen, setTeamDialogOpen] = useState(false);
  const [newTeamName, setNewTeamName] = useState('');
  const [newTeamMicrohackId, setNewTeamMicrohackId] = useState('');

  const [userDialogOpen, setUserDialogOpen] = useState(false);
  const [editingUser, setEditingUser] = useState<UserInfo | null>(null);
  const [userForm, setUserForm] = useState({ username: '', password: '', role: 'participant', team: '', microhackId: '' });
  const [expandedTeamRows, setExpandedTeamRows] = useState<Record<string, boolean>>({});
  const isIndividualMode = hackState?.mode === 'individual';

  const teamUserGroups = useMemo(() => {
    if (isIndividualMode) return [];

    const normalizedTeams = new Set(teams.map((team) => team.toLowerCase()));
    const grouped = teams.map((team) => ({
      team,
      users: users.filter((u) => u.team?.toLowerCase() === team.toLowerCase()),
    }));

    const unassignedUsers = users.filter((u) => !u.team || !normalizedTeams.has(u.team.toLowerCase()));
    if (unassignedUsers.length > 0) {
      grouped.push({ team: 'Unassigned', users: unassignedUsers });
    }

    return grouped;
  }, [isIndividualMode, teams, users]);

  const microhackOptions = useMemo(
    () => [...microhacks].map((microhack) => microhack.microhackId).sort((a, b) => a.localeCompare(b)),
    [microhacks],
  );

  const teamsByMicrohack = useMemo(() => {
    return new Map(
      microhacks.map((microhack) => [
        microhack.microhackId,
        [...(microhack.teams ?? [])].sort((a, b) => a.localeCompare(b)),
      ]),
    );
  }, [microhacks]);

  const teamsForSelectedMicrohack = useMemo(() => {
    if (!userForm.microhackId) return teams;
    return teamsByMicrohack.get(userForm.microhackId) ?? [];
  }, [teams, teamsByMicrohack, userForm.microhackId]);

  const showSnack = useCallback((message: string, severity: SnackState['severity'] = 'success') => {
    setSnack({ open: true, message, severity });
  }, []);

  const fetchData = useCallback(async () => {
    if (!user || user.role !== 'techlead') {
      setLoading(false);
      return;
    }

    try {
      const [teamsData, usersData, microhacksData] = await Promise.all([
        api.get<string[]>('/api/admin/team-admin/teams'),
        api.get<UserInfo[]>('/api/admin/team-admin/users'),
        api.get<MicrohackSummary[]>('/api/admin/microhacks'),
      ]);
      setTeams(teamsData);
      setUsers(usersData);
      setMicrohacks(microhacksData);
      const defaultMicrohackId = [...microhacksData]
        .map((microhack) => microhack.microhackId)
        .sort((a, b) => a.localeCompare(b))[0] ?? '';
      setNewTeamMicrohackId((previous) => previous || defaultMicrohackId);
      setUserForm((previous) => ({ ...previous, microhackId: previous.microhackId || defaultMicrohackId }));
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
    if (!newTeamMicrohackId) {
      showSnack('Select a microhack for the team', 'error');
      return;
    }
    try {
      await api.post('/api/admin/team-admin/teams', { name: newTeamName.trim(), microhackId: newTeamMicrohackId });
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
    const defaultMicrohackId = userForm.microhackId || microhackOptions[0] || '';
    const defaultTeams = teamsByMicrohack.get(defaultMicrohackId) ?? [];
    setEditingUser(null);
    setUserForm({
      username: '',
      password: '',
      role: 'participant',
      team: isIndividualMode ? '' : defaultTeams[0] || '',
      microhackId: defaultMicrohackId,
    });
    setUserDialogOpen(true);
  };

  const openEditUser = (u: UserInfo) => {
    const matchedMicrohackId = microhacks.find((microhack) =>
      microhack.teams.some((teamName) => teamName.toLowerCase() === (u.team ?? '').toLowerCase()),
    )?.microhackId ?? (userForm.microhackId || microhackOptions[0] || '');
    setEditingUser(u);
    setUserForm({
      username: u.username,
      password: '',
      role: u.role,
      team: u.team || '',
      microhackId: matchedMicrohackId,
    });
    setUserDialogOpen(true);
  };

  const handleSaveUser = async () => {
    try {
      if (editingUser) {
        // Update
        const body: Record<string, string | null> = {};
        if (userForm.password) body.password = userForm.password;
        if (userForm.role !== editingUser.role) body.role = userForm.role;
        if (userForm.role !== 'techlead') body.microhackId = userForm.microhackId;
        if (userForm.role === 'techlead') {
          body.team = null;
        } else if (!isIndividualMode && userForm.team !== (editingUser.team || '')) {
          body.team = userForm.team;
        }
        await api.put(`/api/admin/team-admin/users/${encodeURIComponent(editingUser.username)}`, body);
        showSnack(`User "${editingUser.username}" updated`);
      } else {
        // Create
        const payload: { username: string; password: string; role: string; team?: string | null; microhackId?: string } = {
          username: userForm.username,
          password: userForm.password,
          role: userForm.role,
        };
        if (userForm.role !== 'techlead') {
          payload.microhackId = userForm.microhackId;
        }
        if (userForm.role === 'techlead') {
          payload.team = null;
        } else if (!isIndividualMode) {
          payload.team = userForm.team;
        }
        await api.post('/api/admin/team-admin/users', payload);
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

  const toggleTeamRow = (teamName: string) => {
    setExpandedTeamRows((prev) => ({ ...prev, [teamName]: !prev[teamName] }));
  };

  const triggerCsvDownload = async (path: string, filename: string, label: string) => {
    try {
      const blob = await api.downloadCsv(path);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = filename;
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(url);
      showSnack(`${label} CSV downloaded`);
    } catch (err) {
      showSnack(err instanceof ApiError ? err.message : `Failed to download ${label.toLowerCase()} CSV`, 'error');
    }
  };

  const handleCsvUpload = async (path: string, file: File | null, label: string) => {
    if (!file) return;
    try {
      await api.uploadCsv(path, file);
      showSnack(`${label} CSV imported`);
      await fetchData();
    } catch (err) {
      showSnack(err instanceof ApiError ? err.message : `Failed to import ${label.toLowerCase()} CSV`, 'error');
    }
  };

  const onTeamsCsvSelected = (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0] ?? null;
    event.target.value = '';
    void handleCsvUpload(TEAM_IMPORT_CSV_PATH, file, 'Teams');
  };

  const onUsersCsvSelected = (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0] ?? null;
    event.target.value = '';
    void handleCsvUpload(USER_IMPORT_CSV_PATH, file, 'Users');
  };

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
          Management is only accessible to Tech Leads.
        </Typography>
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
          Manage Users
        </Typography>
        <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
          {isIndividualMode
            ? 'Create users and choose the microhack. Each user automatically gets a team with the same name.'
            : 'Manage teams and user assignments by microhack'}
        </Typography>
      </Box>

      {!isIndividualMode && (
        <>
          {/* ── Team management ── */}
          <Box sx={{ mb: 2, display: 'flex', gap: 1, flexWrap: 'wrap' }}>
            <Button variant="contained" startIcon={<AddIcon />} onClick={() => setTeamDialogOpen(true)}>
              Create Team
            </Button>
            <Button variant="outlined" component="label">
              Import Teams CSV
              <input hidden type="file" accept=".csv,text/csv" onChange={onTeamsCsvSelected} />
            </Button>
            <Button variant="outlined" onClick={() => void triggerCsvDownload(TEAM_EXPORT_CSV_PATH, 'teams.csv', 'Teams')}>
              Export Teams CSV
            </Button>
          </Box>
          <TableContainer
            component={Paper}
            sx={{
              background: 'linear-gradient(145deg, #1A1333 0%, #1E1045 100%)',
              border: '1px solid rgba(124, 58, 237, 0.15)',
              mb: 4,
            }}
          >
            <Table>
              <TableHead>
                <TableRow>
                  <TableCell sx={{ fontWeight: 700 }}>Team Name</TableCell>
                  <TableCell sx={{ fontWeight: 700 }}>Participants</TableCell>
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
                            <Typography variant="body2" color="text.secondary">No participants</Typography>
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

      {/* ── Users management ── */}
      <Box sx={{ mb: 2, display: 'flex', gap: 1, flexWrap: 'wrap' }}>
        <Button variant="contained" startIcon={<AddIcon />} onClick={openCreateUser}>
          Create User
        </Button>
        <Button variant="outlined" component="label">
          Import Users CSV
          <input hidden type="file" accept=".csv,text/csv" onChange={onUsersCsvSelected} />
        </Button>
        <Button variant="outlined" onClick={() => void triggerCsvDownload(USER_EXPORT_CSV_PATH, 'users.csv', 'Users')}>
          Export Users CSV
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
              <TableCell sx={{ fontWeight: 700 }}>{isIndividualMode ? 'Username' : 'Team / User'}</TableCell>
              <TableCell sx={{ fontWeight: 700 }}>Role</TableCell>
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
              isIndividualMode ? (
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
              ) : (
                teamUserGroups.map((group) => {
                  const isExpanded = !!expandedTeamRows[group.team];
                  return (
                    <Fragment key={group.team}>
                      <TableRow hover>
                        <TableCell>
                          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                            <IconButton size="small" onClick={() => toggleTeamRow(group.team)} aria-label={`Toggle ${group.team}`}>
                              {isExpanded ? <KeyboardArrowDownIcon fontSize="small" /> : <KeyboardArrowRightIcon fontSize="small" />}
                            </IconButton>
                            <Typography variant="body2" fontWeight={700}>{group.team}</Typography>
                            <Chip size="small" label={`${group.users.length} user${group.users.length === 1 ? '' : 's'}`} />
                          </Box>
                        </TableCell>
                        <TableCell>
                          <Typography variant="body2" color="text.secondary">Group</Typography>
                        </TableCell>
                        <TableCell align="right" />
                      </TableRow>
                      <TableRow>
                        <TableCell colSpan={3} sx={{ py: 0, borderBottom: 0 }}>
                          <Collapse in={isExpanded} timeout="auto" unmountOnExit>
                            <Table size="small">
                              <TableBody>
                                {group.users.length === 0 ? (
                                  <TableRow>
                                    <TableCell colSpan={3}>
                                      <Typography variant="body2" color="text.secondary" sx={{ pl: 6 }}>
                                        No users in this team
                                      </Typography>
                                    </TableCell>
                                  </TableRow>
                                ) : (
                                  group.users.map((u) => (
                                    <TableRow key={u.username} hover>
                                      <TableCell sx={{ pl: 7 }}>
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
                          </Collapse>
                        </TableCell>
                      </TableRow>
                    </Fragment>
                  );
                })
              )
            )}
          </TableBody>
        </Table>
      </TableContainer>

      {/* ── Create Team Dialog ── */}
      {!isIndividualMode && (
        <Dialog open={teamDialogOpen} onClose={() => setTeamDialogOpen(false)} maxWidth="xs" fullWidth>
          <DialogTitle>Create Team</DialogTitle>
          <DialogContent>
            <FormControl fullWidth sx={{ mt: 1 }}>
              <InputLabel>Microhack</InputLabel>
              <Select
                value={newTeamMicrohackId}
                label="Microhack"
                onChange={(e) => setNewTeamMicrohackId(e.target.value)}
              >
                {microhackOptions.length === 0 && (
                  <MenuItem value="" disabled>No microhacks available</MenuItem>
                )}
                {microhackOptions.map((microhackId) => (
                  <MenuItem key={microhackId} value={microhackId}>{microhackId}</MenuItem>
                ))}
              </Select>
            </FormControl>
            <TextField
              fullWidth
              label="Team Name"
              value={newTeamName}
              onChange={(e) => setNewTeamName(e.target.value)}
              sx={{ mt: 2 }}
              onKeyDown={(e) => { if (e.key === 'Enter') handleCreateTeam(); }}
            />
          </DialogContent>
          <DialogActions>
            <Button onClick={() => setTeamDialogOpen(false)}>Cancel</Button>
            <Button variant="contained" onClick={handleCreateTeam} disabled={!newTeamName.trim() || !newTeamMicrohackId}>
              Create
            </Button>
          </DialogActions>
        </Dialog>
      )}

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
                <InputLabel>Microhack</InputLabel>
                <Select
                  value={userForm.microhackId}
                  label="Microhack"
                  onChange={(e) => {
                    const microhackId = e.target.value;
                    const availableTeams = teamsByMicrohack.get(microhackId) ?? [];
                    setUserForm((f) => ({
                      ...f,
                      microhackId,
                      team: isIndividualMode ? f.team : (availableTeams.includes(f.team) ? f.team : (availableTeams[0] || '')),
                    }));
                  }}
                >
                  {microhackOptions.length === 0 && (
                    <MenuItem value="" disabled>No microhacks available</MenuItem>
                  )}
                  {microhackOptions.map((microhackId) => (
                    <MenuItem key={microhackId} value={microhackId}>{microhackId}</MenuItem>
                  ))}
                </Select>
              </FormControl>
            )}
            {userForm.role !== 'techlead' && !isIndividualMode && (
              <FormControl fullWidth>
                <InputLabel>Team</InputLabel>
                <Select
                  value={userForm.team}
                  label="Team"
                  onChange={(e) => setUserForm((f) => ({ ...f, team: e.target.value }))}
                >
                  {teamsForSelectedMicrohack.map((t) => (
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
              (!editingUser && (!userForm.username.trim() || !userForm.password))
              || (userForm.role !== 'techlead' && !userForm.microhackId)
              || (userForm.role !== 'techlead' && !isIndividualMode && !userForm.team)
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
