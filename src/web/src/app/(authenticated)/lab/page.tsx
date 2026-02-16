'use client';

import { useState, useEffect } from 'react';
import Box from '@mui/material/Box';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import CircularProgress from '@mui/material/CircularProgress';
import Alert from '@mui/material/Alert';
import OpenInNewIcon from '@mui/icons-material/OpenInNew';
import ScienceIcon from '@mui/icons-material/Science';
import { api, ApiError } from '@/lib/api';

interface LabEndpoint {
  name: string;
  url: string;
}

interface LabConfig {
  enabled: boolean;
  endpoints: LabEndpoint[];
}

export default function LabPage() {
  const [endpoints, setEndpoints] = useState<LabEndpoint[]>([]);
  const [enabled, setEnabled] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  useEffect(() => {
    async function fetchLab() {
      try {
        const data = await api.get<LabConfig>('/api/lab');
        setEnabled(data.enabled);
        setEndpoints(data.endpoints ?? []);
      } catch (err) {
        if (err instanceof ApiError && err.status === 404) {
          setEnabled(false);
        } else {
          setError('Failed to load lab configuration');
        }
      } finally {
        setLoading(false);
      }
    }
    fetchLab();
  }, []);

  if (loading) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', py: 8 }}>
        <CircularProgress />
      </Box>
    );
  }

  if (error) {
    return (
      <Box sx={{ maxWidth: 600, mx: 'auto', py: 4 }}>
        <Alert severity="error">{error}</Alert>
      </Box>
    );
  }

  if (!enabled || endpoints.length === 0) {
    return (
      <Box sx={{ maxWidth: 600, mx: 'auto', py: 4, textAlign: 'center' }}>
        <ScienceIcon sx={{ fontSize: 48, color: 'text.secondary', mb: 2 }} />
        <Typography variant="h6" color="text.secondary">
          Lab environment is not configured for this event
        </Typography>
      </Box>
    );
  }

  return (
    <Box sx={{ maxWidth: 800, mx: 'auto' }}>
      <Typography variant="h4" sx={{ fontWeight: 700, mb: 1 }}>
        Lab Environment
      </Typography>
      <Typography variant="body1" color="text.secondary" sx={{ mb: 3 }}>
        Access your lab environment through the gateways below.
      </Typography>

      <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
        {endpoints.map((ep) => (
          <Card
            key={ep.url}
            sx={{
              position: 'relative',
              overflow: 'visible',
              '&::before': {
                content: '""',
                position: 'absolute',
                inset: -1,
                borderRadius: '13px',
                background: 'linear-gradient(135deg, #7C3AED, #3B82F6)',
                zIndex: -1,
                opacity: 0.4,
              },
            }}
          >
            <CardContent
              sx={{
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
                py: 2.5,
                '&:last-child': { pb: 2.5 },
              }}
            >
              <Box>
                <Typography variant="h6" sx={{ fontWeight: 600 }}>
                  {ep.name || ep.url}
                </Typography>
                <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
                  {ep.url}
                </Typography>
              </Box>
              <Button
                variant="contained"
                endIcon={<OpenInNewIcon />}
                href={ep.url}
                target="_blank"
                rel="noopener noreferrer"
                sx={{ ml: 2, flexShrink: 0 }}
              >
                Launch
              </Button>
            </CardContent>
          </Card>
        ))}
      </Box>
    </Box>
  );
}
