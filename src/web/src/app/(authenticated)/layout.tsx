'use client';

import { useEffect, useMemo, useState } from 'react';
import { useRouter, usePathname } from 'next/navigation';
import Box from '@mui/material/Box';
import AppBar from '@mui/material/AppBar';
import Toolbar from '@mui/material/Toolbar';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import Chip from '@mui/material/Chip';
import CircularProgress from '@mui/material/CircularProgress';
import LogoutIcon from '@mui/icons-material/Logout';
import Link from 'next/link';
import { useAuth, type User } from '@/contexts/AuthContext';
import { api, ApiError } from '@/lib/api';

const ROLE_LABELS: Record<string, string> = {
  participant: 'Participant',
  coach: 'Coach',
  techlead: 'Tech Lead',
};

interface NavItem {
  label: string;
  href: string;
}

function getNavItems(role: string, labEnabled: boolean): NavItem[] {
  const items: NavItem[] = [];
  if (role === 'techlead') {
    items.push({ label: 'Dashboard', href: '/dashboard' });
    items.push({ label: 'Manage', href: '/manage' });
  }
  items.push({ label: 'Challenges', href: '/challenges' });
  if (role === 'coach' || role === 'techlead') {
    items.push({ label: 'Solutions', href: '/solutions' });
  }
  items.push({ label: 'Credentials', href: '/credentials' });
  items.push({ label: 'Timer', href: '/timer' });
  if (labEnabled) {
    items.push({ label: 'Lab', href: '/lab' });
  }
  return items;
}

function getHomeRoute(user: User): string {
  return user.role === 'techlead' ? '/dashboard' : '/challenges';
}

// Pages each role can access
const ROLE_PAGES: Record<string, string[]> = {
  participant: ['/challenges', '/credentials', '/timer', '/lab'],
  coach: ['/challenges', '/solutions', '/credentials', '/timer', '/lab'],
  techlead: ['/dashboard', '/manage', '/challenges', '/solutions', '/credentials', '/timer', '/lab'],
};

export default function AuthenticatedLayout({ children }: { children: React.ReactNode }) {
  const { user, loading, logout } = useAuth();
  const router = useRouter();
  const pathname = usePathname();
  const [labEnabled, setLabEnabled] = useState(false);

  // Redirect unauthenticated users to login
  useEffect(() => {
    if (!loading && !user) {
      router.replace('/login');
    }
  }, [loading, user, router]);

  // Redirect users who access pages not allowed for their role
  useEffect(() => {
    if (!loading && user && pathname !== '/') {
      const allowedPages = ROLE_PAGES[user.role] || [];
      const isAllowed = allowedPages.some((p) => pathname.startsWith(p));
      if (!isAllowed) {
        router.replace(getHomeRoute(user));
      }
    }
  }, [loading, user, pathname, router]);

  // Fetch lab config to conditionally show Lab nav item
  useEffect(() => {
    if (!loading && user) {
      api.get<{ enabled: boolean }>('/api/lab')
        .then((data) => setLabEnabled(data.enabled))
        .catch(() => setLabEnabled(false));
    }
  }, [loading, user]);

  const navItems = useMemo(() => (user ? getNavItems(user.role, labEnabled) : []), [user, labEnabled]);

  const handleLogout = async () => {
    await logout();
    router.replace('/login');
  };

  if (loading || !user) {
    return (
      <Box sx={{ minHeight: '100vh', display: 'flex', alignItems: 'center', justifyContent: 'center', bgcolor: 'background.default' }}>
        <CircularProgress />
      </Box>
    );
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
            <Chip
              label={ROLE_LABELS[user.role] || user.role}
              size="small"
              sx={{
                background: 'linear-gradient(135deg, rgba(124,58,237,0.3), rgba(59,130,246,0.3))',
                color: 'primary.light',
                fontWeight: 600,
                fontSize: '0.75rem',
              }}
            />
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
