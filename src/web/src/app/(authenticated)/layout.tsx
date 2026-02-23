'use client';

import { useEffect, useMemo } from 'react';
import { useRouter, usePathname } from 'next/navigation';
import Box from '@mui/material/Box';
import AppBar from '@mui/material/AppBar';
import Toolbar from '@mui/material/Toolbar';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import CircularProgress from '@mui/material/CircularProgress';
import LogoutIcon from '@mui/icons-material/Logout';
import Link from 'next/link';
import { useAuth, type User } from '@/contexts/AuthContext';
import { HackStateProvider, useHackState } from '@/contexts/HackStateContext';
import WaitingScreen from '@/app/components/WaitingScreen';

interface NavItem {
  label: string;
  href: string;
}

function getNavItems(role: string, mode: 'team' | 'individual', participantSolutionsVisible: boolean): NavItem[] {
  const items: NavItem[] = [];
  if (role === 'techlead' || role === 'coach') {
    items.push({ label: 'Dashboard', href: '/dashboard' });
  }
  if (role === 'techlead' && mode !== 'individual') {
    items.push({ label: 'Teams', href: '/teams' });
  }
  if (role === 'techlead') {
    items.push({ label: 'Config', href: '/hack-config' });
  }
  items.push({ label: 'Challenges', href: '/challenges' });
  if (role === 'coach' || role === 'techlead' || (role === 'participant' && participantSolutionsVisible)) {
    items.push({ label: 'Solutions', href: '/solutions' });
  }
  items.push({ label: 'Credentials', href: '/credentials' });
  return items;
}

function getHomeRoute(user: User): string {
  return user.role === 'techlead' ? '/dashboard' : '/challenges';
}

function getAllowedPages(user: User, mode: 'team' | 'individual', participantSolutionsVisible: boolean): string[] {
  if (user.role === 'participant') {
    return participantSolutionsVisible
      ? ['/challenges', '/solutions', '/credentials']
      : ['/challenges', '/credentials'];
  }

  if (user.role === 'coach') {
    return ['/dashboard', '/challenges', '/solutions', '/credentials'];
  }

  return mode === 'individual'
    ? ['/dashboard', '/manage', '/challenges', '/solutions', '/credentials', '/hack-config']
    : ['/dashboard', '/teams', '/manage', '/challenges', '/solutions', '/credentials', '/hack-config'];
}

function AuthenticatedContent({ children }: { children: React.ReactNode }) {
  const { user, loading, logout } = useAuth();
  const { hackState, loading: hackStateLoading } = useHackState();
  const router = useRouter();
  const pathname = usePathname();

  // Redirect unauthenticated users to login
  useEffect(() => {
    if (!loading && !user) {
      router.replace('/login');
    }
  }, [loading, user, router]);

  // Redirect users who access pages not allowed for their role
  useEffect(() => {
    if (!loading && !hackStateLoading && user && pathname !== '/') {
      const mode = hackState?.mode || 'team';
      const participantSolutionsVisible = !!hackState?.participantSolutionsVisible;
      const allowedPages = getAllowedPages(user, mode, participantSolutionsVisible);
      const isAllowed = allowedPages.some((p) => pathname.startsWith(p));
      if (!isAllowed) {
        router.replace(getHomeRoute(user));
      }
    }
  }, [loading, hackStateLoading, user, pathname, router, hackState?.mode, hackState?.participantSolutionsVisible]);

  const navItems = useMemo(() => {
    if (!user) return [];
    return getNavItems(
      user.role,
      hackState?.mode || 'team',
      !!hackState?.participantSolutionsVisible,
    );
  }, [user, hackState?.mode, hackState?.participantSolutionsVisible]);

  const handleLogout = async () => {
    await logout();
    router.replace('/login');
  };

  // Participants should wait on a splash screen until the hack is active.
  const shouldShowWaiting = user?.role === 'participant' &&
    !!hackState &&
    hackState.status !== 'active' &&
    hackState.status !== 'completed';

  if (loading || !user) {
    return (
      <Box sx={{ minHeight: '100vh', display: 'flex', alignItems: 'center', justifyContent: 'center', bgcolor: 'background.default' }}>
        <CircularProgress />
      </Box>
    );
  }

  // Show waiting screen overlay if appropriate
  if (shouldShowWaiting) {
    return <WaitingScreen onLogout={handleLogout} />;
  }

  return (
    <Box sx={{ minHeight: '100vh', bgcolor: 'background.default' }}>
      <AppBar position="sticky" elevation={0}>
        <Toolbar>
          <Typography
            variant="h6"
            component={Link}
            href={getHomeRoute(user)}
            sx={{
              textDecoration: 'none',
              color: 'inherit',
              background: 'linear-gradient(135deg, #A78BFA 0%, #60A5FA 100%)',
              WebkitBackgroundClip: 'text',
              WebkitTextFillColor: 'transparent',
              mr: 4,
            }}
          >
            <Typography component="span" sx={{ fontFamily: 'monospace', mr: 0.5 }}>&gt;_</Typography>MicroHack
          </Typography>

          <Box sx={{ display: 'flex', gap: 1, flexGrow: 1 }}>
            {navItems.map((item) => (
              <Button
                key={item.href}
                component={Link}
                href={item.href}
                size="small"
                sx={{
                  color: pathname.startsWith(item.href) ? 'primary.light' : 'text.secondary',
                  fontWeight: pathname.startsWith(item.href) ? 600 : 400,
                  '&:hover': { color: 'primary.light' },
                }}
              >
                {item.label}
              </Button>
            ))}
          </Box>

          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5 }}>
            <Typography variant="body2" color="text.secondary">
              {user.username}
            </Typography>
            <Button
              onClick={handleLogout}
              size="small"
              startIcon={<LogoutIcon />}
              sx={{ color: 'text.secondary', '&:hover': { color: 'error.main' } }}
            >
              Logout
            </Button>
          </Box>
        </Toolbar>
      </AppBar>

      <Box component="main" sx={{ p: 3 }}>
        {children}
      </Box>
    </Box>
  );
}

export default function AuthenticatedLayout({ children }: { children: React.ReactNode }) {
  return (
    <HackStateProvider>
      <AuthenticatedContent>{children}</AuthenticatedContent>
    </HackStateProvider>
  );
}
