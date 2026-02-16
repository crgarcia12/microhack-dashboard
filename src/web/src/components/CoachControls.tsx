'use client';

import { useEffect, useCallback, useState } from 'react';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Snackbar from '@mui/material/Snackbar';
import Alert from '@mui/material/Alert';
import Dialog from '@mui/material/Dialog';
import DialogTitle from '@mui/material/DialogTitle';
import DialogContent from '@mui/material/DialogContent';
import DialogContentText from '@mui/material/DialogContentText';
import DialogActions from '@mui/material/DialogActions';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import UndoIcon from '@mui/icons-material/Undo';
import RestartAltIcon from '@mui/icons-material/RestartAlt';
import Tooltip from '@mui/material/Tooltip';
import { api, ApiError } from '@/lib/api';

interface CoachControlsProps {
  disabled?: boolean;
  onAction: () => void;
}

export default function CoachControls({ disabled = false, onAction }: CoachControlsProps) {
  const [snackbar, setSnackbar] = useState<{ open: boolean; message: string; severity: 'error' | 'info' }>({
    open: false,
    message: '',
    severity: 'error',
  });
  const [resetDialogOpen, setResetDialogOpen] = useState(false);
  const [loading, setLoading] = useState<string | null>(null);

  const handleAction = useCallback(
    async (action: 'approve' | 'revert' | 'reset') => {
      setLoading(action);
      try {
        await api.post(`/api/challenges/${action}`);
        onAction();
      } catch (err) {
        const message =
          err instanceof ApiError
            ? err.message
            : 'Network error. Check your connection.';
        setSnackbar({ open: true, message, severity: 'error' });
      } finally {
        setLoading(null);
      }
    },
    [onAction],
  );

  const handleApprove = useCallback(() => handleAction('approve'), [handleAction]);
  const handleRevert = useCallback(() => handleAction('revert'), [handleAction]);
  const handleResetConfirm = useCallback(() => {
    setResetDialogOpen(false);
    handleAction('reset');
  }, [handleAction]);

  // Ctrl+Enter keyboard shortcut for approve
  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      if (e.ctrlKey && e.key === 'Enter' && !disabled && !loading) {
        e.preventDefault();
        handleApprove();
      }
    };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, [disabled, loading, handleApprove]);

  return (
    <>
      <Box sx={{ display: 'flex', gap: 1.5, flexWrap: 'wrap' }}>
        <Tooltip title="Ctrl+Enter">
          <span>
            <Button
              variant="contained"
              color="primary"
              startIcon={<CheckCircleIcon />}
              onClick={handleApprove}
              disabled={disabled || loading === 'approve'}
              sx={{ minWidth: 120 }}
            >
              Approve
            </Button>
          </span>
        </Tooltip>
        <Button
          variant="outlined"
          color="secondary"
          startIcon={<UndoIcon />}
          onClick={handleRevert}
          disabled={disabled || loading === 'revert'}
          sx={{ minWidth: 100 }}
        >
          Revert
        </Button>
        <Button
          variant="outlined"
          color="error"
          startIcon={<RestartAltIcon />}
          onClick={() => setResetDialogOpen(true)}
          disabled={disabled || loading === 'reset'}
          sx={{ minWidth: 100 }}
        >
          Reset
        </Button>
      </Box>

      <Dialog open={resetDialogOpen} onClose={() => setResetDialogOpen(false)}>
        <DialogTitle>Reset Progress</DialogTitle>
        <DialogContent>
          <DialogContentText>
            This will reset the team back to Challenge 1. All progress will be lost. Are you sure?
          </DialogContentText>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setResetDialogOpen(false)}>Cancel</Button>
          <Button onClick={handleResetConfirm} color="error" variant="contained">
            Reset
          </Button>
        </DialogActions>
      </Dialog>

      <Snackbar
        open={snackbar.open}
        autoHideDuration={4000}
        onClose={() => setSnackbar((s) => ({ ...s, open: false }))}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
      >
        <Alert
          severity={snackbar.severity}
          onClose={() => setSnackbar((s) => ({ ...s, open: false }))}
          variant="filled"
        >
          {snackbar.message}
        </Alert>
      </Snackbar>
    </>
  );
}
