'use client';

import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import LogoutIcon from '@mui/icons-material/Logout';
import { keyframes } from '@mui/system';

const glowPulse = keyframes`
  0%, 100% { text-shadow: 0 0 20px rgba(124,58,237,0.8), 0 0 60px rgba(124,58,237,0.4), 0 0 100px rgba(59,130,246,0.3); }
  50% { text-shadow: 0 0 40px rgba(124,58,237,1), 0 0 80px rgba(124,58,237,0.6), 0 0 140px rgba(59,130,246,0.5); }
`;

const scanLine = keyframes`
  0% { top: -2px; opacity: 0; }
  10% { opacity: 1; }
  90% { opacity: 1; }
  100% { top: 100%; opacity: 0; }
`;

const particleFloat = keyframes`
  0% { transform: translateY(0) translateX(0); opacity: 0; }
  20% { opacity: 1; }
  80% { opacity: 1; }
  100% { transform: translateY(-120vh) translateX(40px); opacity: 0; }
`;

const fadeIn = keyframes`
  0% { opacity: 0; transform: translateY(20px); }
  100% { opacity: 1; transform: translateY(0); }
`;

const PARTICLES = Array.from({ length: 20 }, (_, i) => ({
  id: i,
  left: ((i * 37) % 100) + 0.5,
  delay: ((i * 17) % 20) / 10,
  duration: 2 + ((i * 13) % 30) / 10,
  size: 2 + ((i * 11) % 40) / 10,
}));

interface WaitingScreenProps {
  onLogout: () => void;
}

export default function WaitingScreen({ onLogout }: WaitingScreenProps) {
  const particles = PARTICLES;

  return (
    <Box
      sx={{
        position: 'fixed',
        inset: 0,
        zIndex: 9998,
        background: 'radial-gradient(ellipse at 50% 40%, rgba(124,58,237,0.12) 0%, transparent 60%), #0F0B1A',
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        overflow: 'hidden',
      }}
    >
      {/* Floating particles */}
      {particles.map((p) => (
        <Box
          key={p.id}
          sx={{
            position: 'absolute',
            bottom: -10,
            left: `${p.left}%`,
            width: p.size,
            height: p.size,
            borderRadius: '50%',
            background: p.id % 2 === 0
              ? 'rgba(124,58,237,0.7)'
              : 'rgba(59,130,246,0.7)',
            boxShadow: p.id % 2 === 0
              ? '0 0 8px rgba(124,58,237,0.5)'
              : '0 0 8px rgba(59,130,246,0.5)',
            animation: `${particleFloat} ${p.duration}s ${p.delay}s ease-out infinite`,
            pointerEvents: 'none',
          }}
        />
      ))}

      {/* Scan line effect */}
      <Box
        sx={{
          position: 'absolute',
          left: 0,
          right: 0,
          height: '2px',
          background: 'linear-gradient(90deg, transparent, rgba(124,58,237,0.6), rgba(59,130,246,0.6), transparent)',
          animation: `${scanLine} 2.5s ease-in-out infinite`,
          pointerEvents: 'none',
        }}
      />

      {/* Logo >_ */}
      <Box
        sx={{
          fontSize: { xs: '80px', sm: '120px' },
          fontFamily: '"JetBrains Mono", "Fira Code", monospace',
          fontWeight: 900,
          color: '#fff',
          animation: `${glowPulse} 2s ease-in-out infinite`,
          mb: 3,
          userSelect: 'none',
          lineHeight: 1,
        }}
      >
        &gt;_
      </Box>

      {/* Main message */}
      <Box
        sx={{
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
          gap: 2,
          animation: `${fadeIn} 1s ease-out`,
        }}
      >
        <Typography
          variant="h4"
          sx={{
            fontFamily: '"JetBrains Mono", "Fira Code", monospace',
            fontWeight: 700,
            background: 'linear-gradient(135deg, #A78BFA 0%, #60A5FA 100%)',
            WebkitBackgroundClip: 'text',
            WebkitTextFillColor: 'transparent',
            textAlign: 'center',
            px: 2,
          }}
        >
          MicroHack will start soon
        </Typography>

        <Button
          variant="outlined"
          startIcon={<LogoutIcon />}
          onClick={onLogout}
          sx={{
            mt: 2,
            borderColor: 'rgba(124,58,237,0.45)',
            color: 'text.secondary',
            '&:hover': {
              borderColor: 'primary.light',
              color: 'primary.light',
            },
          }}
        >
          Logout
        </Button>

      </Box>

      {/* Bottom gradient bar */}
      <Box
        sx={{
          position: 'absolute',
          bottom: 0,
          left: 0,
          right: 0,
          height: '3px',
          background: 'linear-gradient(90deg, transparent, #7C3AED, #3B82F6, transparent)',
          opacity: 0.7,
        }}
      />
    </Box>
  );
}
