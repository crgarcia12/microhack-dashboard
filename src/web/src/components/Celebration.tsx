'use client';

import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import EmojiEventsIcon from '@mui/icons-material/EmojiEvents';
import CelebrationIcon from '@mui/icons-material/Celebration';

interface CelebrationProps {
  totalChallenges: number;
  totalElapsedSeconds: number;
}

const FIREWORKS = [
  { left: '10%', top: '26%', color: '#A78BFA', delay: '0s' },
  { left: '26%', top: '12%', color: '#60A5FA', delay: '0.35s' },
  { left: '42%', top: '22%', color: '#34D399', delay: '0.65s' },
  { left: '58%', top: '10%', color: '#F472B6', delay: '0.9s' },
  { left: '74%', top: '24%', color: '#F59E0B', delay: '1.15s' },
  { left: '90%', top: '14%', color: '#22D3EE', delay: '1.45s' },
];

const SPARK_OFFSETS = [
  [-30, 0], [30, 0], [0, -30], [0, 30], [-22, -22], [22, -22], [-22, 22], [22, 22],
] as const;

function formatElapsed(seconds: number): string {
  const h = Math.floor(seconds / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  const s = seconds % 60;
  return `${String(h).padStart(2, '0')}:${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`;
}

export default function Celebration({ totalChallenges, totalElapsedSeconds }: CelebrationProps) {
  return (
    <Box
      sx={{
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        minHeight: '60vh',
        position: 'relative',
        overflow: 'hidden',
      }}
    >
      <Box
        sx={{
          position: 'absolute',
          inset: 0,
          pointerEvents: 'none',
          '& .firework-burst': {
            position: 'absolute',
            width: 0,
            height: 0,
            transform: 'translate(-50%, -50%) scale(0.2)',
            opacity: 0,
            animation: 'fireworkBurst 2.8s ease-out infinite',
          },
          '& .firework-core': {
            position: 'absolute',
            width: 10,
            height: 10,
            borderRadius: '50%',
            backgroundColor: 'currentColor',
            boxShadow: '0 0 16px currentColor',
            transform: 'translate(-50%, -50%)',
          },
          '& .firework-ring': {
            position: 'absolute',
            width: 24,
            height: 24,
            borderRadius: '50%',
            border: '2px solid currentColor',
            transform: 'translate(-50%, -50%)',
            opacity: 0.75,
          },
          '& .firework-spark': {
            position: 'absolute',
            width: 6,
            height: 6,
            borderRadius: '50%',
            backgroundColor: 'currentColor',
            boxShadow: '0 0 8px currentColor',
            animation: 'sparkPulse 0.9s ease-in-out infinite',
          },
          '@keyframes fireworkBurst': {
            '0%': { opacity: 0, transform: 'translate(-50%, -50%) scale(0.2)' },
            '10%': { opacity: 1 },
            '55%': { opacity: 0.95 },
            '100%': { opacity: 0, transform: 'translate(-50%, -50%) scale(1.8)' },
          },
          '@keyframes sparkPulse': {
            '0%, 100%': { opacity: 0.4, transform: 'scale(0.6)' },
            '50%': { opacity: 1, transform: 'scale(1)' },
          },
        }}
      >
        {FIREWORKS.map((firework) => (
          <Box
            key={`${firework.left}-${firework.top}`}
            className="firework-burst"
            sx={{
              left: firework.left,
              top: firework.top,
              color: firework.color,
              animationDelay: firework.delay,
            }}
          >
            <Box className="firework-core" />
            <Box className="firework-ring" />
            {SPARK_OFFSETS.map(([x, y], idx) => (
              <Box
                key={`${x}-${y}`}
                className="firework-spark"
                sx={{
                  left: x,
                  top: y,
                  animationDelay: `calc(${firework.delay} + ${idx * 0.06}s)`,
                }}
              />
            ))}
          </Box>
        ))}
      </Box>

      <Card
        sx={{
          maxWidth: 520,
          width: '100%',
          textAlign: 'center',
          background: 'linear-gradient(145deg, rgba(124,58,237,0.15) 0%, rgba(59,130,246,0.15) 100%)',
          border: '1px solid rgba(124, 58, 237, 0.3)',
          position: 'relative',
          overflow: 'visible',
        }}
      >
        <CardContent sx={{ py: 6, px: 4 }}>
          <Box
            sx={{
              display: 'flex',
              justifyContent: 'center',
              alignItems: 'center',
              gap: 1,
              mb: 3,
            }}
          >
            <CelebrationIcon sx={{ fontSize: 40, color: 'secondary.light', opacity: 0.7 }} />
            <EmojiEventsIcon
              sx={{
                fontSize: 72,
                background: 'linear-gradient(135deg, #FDE68A, #F59E0B)',
                WebkitBackgroundClip: 'text',
                WebkitTextFillColor: 'transparent',
                filter: 'drop-shadow(0 0 12px rgba(245,158,11,0.3))',
              }}
            />
            <CelebrationIcon sx={{ fontSize: 40, color: 'primary.light', opacity: 0.7 }} />
          </Box>

          <Typography
            variant="h3"
            sx={{
              fontWeight: 800,
              mb: 2,
              background: 'linear-gradient(135deg, #A78BFA 0%, #60A5FA 50%, #34D399 100%)',
              WebkitBackgroundClip: 'text',
              WebkitTextFillColor: 'transparent',
            }}
          >
            Congratulations!
          </Typography>

          <Typography variant="h6" color="text.secondary" sx={{ mb: 3 }}>
            You&apos;ve completed all challenges!
          </Typography>

          <Box
            sx={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: 1,
              px: 3,
              py: 1.5,
              borderRadius: 2,
              bgcolor: 'rgba(124, 58, 237, 0.1)',
              border: '1px solid rgba(124, 58, 237, 0.2)',
            }}
          >
            <Typography variant="h5" sx={{ fontWeight: 700, color: 'primary.light' }}>
              {totalChallenges}
            </Typography>
            <Typography variant="body1" color="text.secondary">
              {totalChallenges === 1 ? 'challenge' : 'challenges'} completed
            </Typography>
          </Box>

          <Box
            sx={{
              mt: 2,
              px: 3,
              py: 1.5,
              borderRadius: 2,
              bgcolor: 'rgba(16, 185, 129, 0.12)',
              border: '1px solid rgba(16, 185, 129, 0.25)',
            }}
          >
            <Typography variant="body2" color="text.secondary">
              Total elapsed time
            </Typography>
            <Typography variant="h5" fontFamily="monospace" sx={{ mt: 0.5, fontWeight: 700, color: '#34D399' }}>
              {formatElapsed(totalElapsedSeconds)}
            </Typography>
          </Box>
        </CardContent>
      </Card>
    </Box>
  );
}
