'use client';

import { useEffect, useState, useCallback, useMemo } from 'react';
import { useRouter } from 'next/navigation';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import List from '@mui/material/List';
import ListItemButton from '@mui/material/ListItemButton';
import ListItemIcon from '@mui/material/ListItemIcon';
import ListItemText from '@mui/material/ListItemText';
import CircularProgress from '@mui/material/CircularProgress';
import Chip from '@mui/material/Chip';
import MenuBookIcon from '@mui/icons-material/MenuBook';
import FiberManualRecordIcon from '@mui/icons-material/FiberManualRecord';
import { api } from '@/lib/api';
import { useAuth } from '@/contexts/AuthContext';
import { useSignalR, type TeamProgress } from '@/hooks/useSignalR';
import MarkdownRenderer from '@/components/MarkdownRenderer';
import CoachControls from '@/components/CoachControls';

interface SolutionMeta {
  number: number;
  title: string;
  fileName: string;
}

interface SolutionsListResponse {
  solutions: SolutionMeta[];
  totalCount: number;
  currentStep: number;
}

interface SolutionDetail {
  number: number;
  title: string;
  fileName: string;
  content: string;
}

const API_BASE = '';

/** Transform relative media paths in markdown to API URLs */
function transformMediaPaths(markdown: string): string {
  return markdown.replace(
    /!\[([^\]]*)\]\(media\/([^)]+)\)/g,
    `![$1](${API_BASE}/api/solutions/media/$2)`,
  );
}

export default function SolutionsPage() {
  const { user, loading: authLoading } = useAuth();
  const router = useRouter();

  const [solutions, setSolutions] = useState<SolutionMeta[]>([]);
  const [currentStep, setCurrentStep] = useState<number>(1);
  const [selectedNumber, setSelectedNumber] = useState<number | null>(null);
  const [selectedSolution, setSelectedSolution] = useState<SolutionDetail | null>(null);
  const [loadingList, setLoadingList] = useState(true);
  const [loadingContent, setLoadingContent] = useState(false);

  const isCoach = user?.role === 'coach' || user?.role === 'techlead';

  // Redirect non-coach users to /challenges
  useEffect(() => {
    if (!authLoading && user && !isCoach) {
      router.replace('/challenges');
    }
  }, [authLoading, user, isCoach, router]);

  const fetchSolutions = useCallback(async () => {
    try {
      const data = await api.get<SolutionsListResponse>('/api/solutions');
      setSolutions(data.solutions);
      setCurrentStep(data.currentStep);
    } catch {
      // Auth errors handled by layout redirect
    } finally {
      setLoadingList(false);
    }
  }, []);

  useEffect(() => {
    if (isCoach) {
      fetchSolutions();
    }
  }, [isCoach, fetchSolutions]);

  // SignalR for real-time progress updates
  const handleProgressUpdated = useCallback(
    (newProgress: TeamProgress) => {
      setCurrentStep(newProgress.currentStep);
    },
    [],
  );

  const { connected } = useSignalR({ onProgressUpdated: handleProgressUpdated });

  // Auto-select first solution on load
  useEffect(() => {
    if (solutions.length > 0 && selectedNumber === null) {
      setSelectedNumber(solutions[0].number);
    }
  }, [solutions, selectedNumber]);

  // Fetch solution content when selection changes
  useEffect(() => {
    if (selectedNumber === null) {
      setSelectedSolution(null);
      return;
    }
    setLoadingContent(true);
    api
      .get<SolutionDetail>(`/api/solutions/${selectedNumber}`)
      .then(setSelectedSolution)
      .catch(() => setSelectedSolution(null))
      .finally(() => setLoadingContent(false));
  }, [selectedNumber]);

  const handleCoachAction = useCallback(() => {
    fetchSolutions();
  }, [fetchSolutions]);

  // Transform media paths in the selected solution content
  const renderedContent = useMemo(() => {
    if (!selectedSolution?.content) return '';
    return transformMediaPaths(selectedSolution.content);
  }, [selectedSolution]);

  if (!authLoading && !isCoach) return null;

  // Loading state
  if (authLoading || loadingList) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', py: 8 }}>
        <CircularProgress />
      </Box>
    );
  }

  // Empty state
  if (solutions.length === 0) {
    return (
      <Box sx={{ textAlign: 'center', py: 8 }}>
        <Typography variant="h5" color="text.secondary" sx={{ mb: 2 }}>
          No solutions loaded
        </Typography>
        <Typography color="text.secondary">
          Add Markdown files to <code>hackcontent/solutions/</code>
        </Typography>
      </Box>
    );
  }

  return (
    <Box>
      {/* Header */}
      <Box sx={{ mb: 3 }}>
        <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', mb: 1 }}>
          <Typography variant="h4">Solutions</Typography>
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5 }}>
            {!connected && (
              <Chip
                icon={<FiberManualRecordIcon sx={{ fontSize: 10 }} />}
                label="Reconnecting…"
                size="small"
                sx={{
                  bgcolor: 'rgba(234, 179, 8, 0.15)',
                  color: '#EAB308',
                  '& .MuiChip-icon': { color: '#EAB308' },
                }}
              />
            )}
            <Typography variant="body2" color="text.secondary">
              {solutions.length} solutions · Team on step {currentStep}
            </Typography>
          </Box>
        </Box>
      </Box>

      {/* Coach controls */}
      <Box sx={{ mb: 3 }}>
        <CoachControls disabled={solutions.length === 0} onAction={handleCoachAction} />
      </Box>

      {/* Main content: sidebar + solution content */}
      <Box sx={{ display: 'flex', gap: 3, flexDirection: { xs: 'column', md: 'row' } }}>
        {/* Sidebar */}
        <Card
          sx={{
            width: { xs: '100%', md: 280 },
            flexShrink: 0,
            alignSelf: 'flex-start',
            background: 'linear-gradient(135deg, rgba(26,19,51,0.9), rgba(15,12,35,0.95))',
            border: '1px solid rgba(124, 58, 237, 0.2)',
          }}
        >
          <List disablePadding>
            {solutions.map((s) => {
              const isSelected = s.number === selectedNumber;
              const isCurrentStep = s.number === currentStep;
              return (
                <ListItemButton
                  key={s.number}
                  selected={isSelected}
                  onClick={() => setSelectedNumber(s.number)}
                  sx={{
                    '&.Mui-selected': {
                      bgcolor: 'rgba(124, 58, 237, 0.15)',
                      borderLeft: '3px solid',
                      borderColor: 'primary.main',
                    },
                    transition: 'all 0.2s ease',
                  }}
                >
                  <ListItemIcon sx={{ minWidth: 36 }}>
                    <MenuBookIcon sx={{ color: isSelected ? 'secondary.light' : 'text.secondary', fontSize: 20 }} />
                  </ListItemIcon>
                  <ListItemText
                    primary={s.title || `Solution ${s.number}`}
                    primaryTypographyProps={{
                      variant: 'body2',
                      fontWeight: isSelected ? 600 : 400,
                      noWrap: true,
                    }}
                  />
                  {isCurrentStep && (
                    <Chip
                      label="Current"
                      size="small"
                      sx={{
                        height: 20,
                        fontSize: '0.65rem',
                        fontWeight: 600,
                        background: 'linear-gradient(135deg, rgba(124,58,237,0.4), rgba(59,130,246,0.4))',
                        color: 'primary.light',
                      }}
                    />
                  )}
                </ListItemButton>
              );
            })}
          </List>
        </Card>

        {/* Content area */}
        <Card
          sx={{
            flex: 1,
            minWidth: 0,
            background: 'linear-gradient(135deg, rgba(26,19,51,0.9), rgba(15,12,35,0.95))',
            border: '1px solid rgba(124, 58, 237, 0.2)',
          }}
        >
          <CardContent sx={{ p: { xs: 2, md: 3 } }}>
            {loadingContent ? (
              <Box sx={{ display: 'flex', justifyContent: 'center', py: 6 }}>
                <CircularProgress />
              </Box>
            ) : selectedSolution ? (
              <Box sx={{ transition: 'opacity 0.3s ease', opacity: 1 }}>
                <MarkdownRenderer content={renderedContent} />
              </Box>
            ) : (
              <Box sx={{ textAlign: 'center', py: 6 }}>
                <Typography color="text.secondary">
                  Select a solution from the list
                </Typography>
              </Box>
            )}
          </CardContent>
        </Card>
      </Box>
    </Box>
  );
}
