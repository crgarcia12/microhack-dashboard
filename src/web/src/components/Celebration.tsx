'use client';

import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import EmojiEventsIcon from '@mui/icons-material/EmojiEvents';
import CelebrationIcon from '@mui/icons-material/Celebration';

interface CelebrationProps {
  totalChallenges: number;
}

export default function Celebration({ totalChallenges }: CelebrationProps) {
  return (
    <Box
      sx={{
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        minHeight: '60vh',
      }}
    >
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
        </CardContent>
      </Card>
    </Box>
  );
}
