'use client';

import { useState, useEffect, useCallback } from 'react';
import Box from '@mui/material/Box';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Typography from '@mui/material/Typography';
import CircularProgress from '@mui/material/CircularProgress';
import Alert from '@mui/material/Alert';
import IconButton from '@mui/material/IconButton';
import Tooltip from '@mui/material/Tooltip';
import ContentCopyIcon from '@mui/icons-material/ContentCopy';
import { api, ApiError } from '@/lib/api';

interface CredentialItem {
  label: string;
  value: string;
}

interface CredentialCategory {
  name: string;
  credentials: CredentialItem[];
}

interface CredentialsResponse {
  teamName: string;
  categories: CredentialCategory[];
}

function CopyButton({ value }: { value: string }) {
  const [copied, setCopied] = useState(false);

  const handleCopy = useCallback(async () => {
    await navigator.clipboard.writeText(value);
    setCopied(true);
    setTimeout(() => setCopied(false), 1500);
  }, [value]);

  return (
    <Tooltip title={copied ? 'Copied!' : 'Copy'}>
      <IconButton
        size="small"
        onClick={handleCopy}
        sx={{ color: copied ? 'success.main' : 'text.secondary', ml: 1 }}
      >
        <ContentCopyIcon fontSize="small" />
      </IconButton>
    </Tooltip>
  );
}

export default function CredentialsPage() {
  const [data, setData] = useState<CredentialsResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  useEffect(() => {
    let cancelled = false;

    async function fetchCredentials() {
      try {
        const result = await api.get<CredentialsResponse>('/api/credentials');
        if (!cancelled) setData(result);
      } catch (err) {
        if (!cancelled) {
          if (err instanceof ApiError && err.status === 403) {
            setError('Credentials are not available for organizer accounts.');
          } else {
            setError('Unable to load credentials. Please try again later.');
          }
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    }

    fetchCredentials();
    return () => { cancelled = true; };
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
      <Box sx={{ maxWidth: 600, mx: 'auto', mt: 4 }}>
        <Alert severity="error">{error}</Alert>
      </Box>
    );
  }

  const categories = data?.categories ?? [];

  return (
    <Box sx={{ maxWidth: 900, mx: 'auto' }}>
      <Typography variant="h4" sx={{ fontWeight: 700, mb: 3 }}>
        Credentials
      </Typography>

      {categories.length === 0 ? (
        <Typography color="text.secondary" sx={{ mt: 4 }}>
          No credentials have been provisioned for your team yet.
        </Typography>
      ) : (
        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 3 }}>
          {categories.map((category) => (
            <Card
              key={category.name}
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
                  opacity: 0.5,
                },
              }}
            >
              <CardContent sx={{ p: 3 }}>
                <Typography
                  variant="h6"
                  sx={{
                    fontWeight: 600,
                    mb: 2,
                    background: 'linear-gradient(135deg, #A78BFA 0%, #60A5FA 100%)',
                    WebkitBackgroundClip: 'text',
                    WebkitTextFillColor: 'transparent',
                  }}
                >
                  {category.name}
                </Typography>

                <Box component="ul" sx={{ listStyle: 'none', m: 0, p: 0 }}>
                  {category.credentials.map((item) => (
                    <Box
                      component="li"
                      key={item.label}
                      sx={{
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'space-between',
                        py: 1.5,
                        borderBottom: '1px solid',
                        borderColor: 'divider',
                        '&:last-child': { borderBottom: 'none' },
                      }}
                    >
                      <Box sx={{ minWidth: 0, flex: 1 }}>
                        <Typography variant="body2" color="text.secondary">
                          {item.label}
                        </Typography>
                        <Typography
                          variant="body1"
                          sx={{
                            fontFamily: 'monospace',
                            wordBreak: 'break-all',
                            mt: 0.25,
                          }}
                        >
                          {item.value}
                        </Typography>
                      </Box>
                      <CopyButton value={item.value} />
                    </Box>
                  ))}
                </Box>
              </CardContent>
            </Card>
          ))}
        </Box>
      )}
    </Box>
  );
}
