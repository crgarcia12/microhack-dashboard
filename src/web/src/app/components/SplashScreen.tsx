'use client';

import { useState, useEffect, useCallback } from 'react';
import Box from '@mui/material/Box';
import { keyframes } from '@mui/system';

const glowPulse = keyframes`
  0%, 100% { text-shadow: 0 0 20px rgba(124,58,237,0.8), 0 0 60px rgba(124,58,237,0.4), 0 0 100px rgba(59,130,246,0.3); }
  50% { text-shadow: 0 0 40px rgba(124,58,237,1), 0 0 80px rgba(124,58,237,0.6), 0 0 140px rgba(59,130,246,0.5); }
`;

const cursorBlink = keyframes`
  0%, 100% { opacity: 1; }
  50% { opacity: 0; }
`;

const scanLine = keyframes`
  0% { top: -2px; opacity: 0; }
  10% { opacity: 1; }
  90% { opacity: 1; }
  100% { top: 100%; opacity: 0; }
`;

const fadeOut = keyframes`
  0% { opacity: 1; transform: scale(1); }
  100% { opacity: 0; transform: scale(1.1); }
`;

const particleFloat = keyframes`
  0% { transform: translateY(0) translateX(0); opacity: 0; }
  20% { opacity: 1; }
  80% { opacity: 1; }
  100% { transform: translateY(-120vh) translateX(40px); opacity: 0; }
`;

const logoReveal = keyframes`
  0% { transform: scale(3) rotate(-10deg); opacity: 0; filter: blur(20px); }
  50% { transform: scale(1.1) rotate(2deg); opacity: 1; filter: blur(0); }
  70% { transform: scale(1) rotate(0deg); }
  100% { transform: scale(1) rotate(0deg); opacity: 1; }
`;

const textReveal = keyframes`
  0% { clip-path: inset(0 100% 0 0); }
  100% { clip-path: inset(0 0% 0 0); }
`;

const shimmer = keyframes`
  0% { background-position: -200% center; }
  100% { background-position: 200% center; }
`;

const TITLE = 'MicroHack';
const LOGO_DURATION = 900;
const TYPE_DELAY = 80;
const HOLD_DURATION = 1200;

interface SplashScreenProps {
  onComplete: () => void;
}

export default function SplashScreen({ onComplete }: SplashScreenProps) {
  const [phase, setPhase] = useState<'logo' | 'typing' | 'hold' | 'exit'>('logo');
  const [typedCount, setTypedCount] = useState(0);
  const [particles] = useState(() =>
    Array.from({ length: 20 }, (_, i) => ({
      id: i,
      left: Math.random() * 100,
      delay: Math.random() * 2,
      duration: 2 + Math.random() * 3,
      size: 2 + Math.random() * 4,
    }))
  );

  const advancePhase = useCallback(() => {
    setPhase((prev) => {
      if (prev === 'logo') return 'typing';
      if (prev === 'typing') return 'hold';
      if (prev === 'hold') return 'exit';
      return prev;
    });
  }, []);

  // Phase transitions
  useEffect(() => {
    if (phase === 'logo') {
      const t = setTimeout(advancePhase, LOGO_DURATION);
      return () => clearTimeout(t);
    }
    if (phase === 'hold') {
      const t = setTimeout(advancePhase, HOLD_DURATION);
      return () => clearTimeout(t);
    }
    if (phase === 'exit') {
      const t = setTimeout(onComplete, 600);
      return () => clearTimeout(t);
    }
  }, [phase, advancePhase, onComplete]);

  // Typing effect
  useEffect(() => {
    if (phase !== 'typing') return;
    if (typedCount >= TITLE.length) {
      advancePhase();
      return;
    }
    const t = setTimeout(() => setTypedCount((c) => c + 1), TYPE_DELAY);
    return () => clearTimeout(t);
  }, [phase, typedCount, advancePhase]);

  return (
    <Box
      sx={{
        position: 'fixed',
        inset: 0,
        zIndex: 9999,
        background: 'radial-gradient(ellipse at 50% 40%, rgba(124,58,237,0.12) 0%, transparent 60%), #0F0B1A',
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        overflow: 'hidden',
        animation: phase === 'exit' ? `${fadeOut} 0.6s ease-in forwards` : undefined,
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
          animation: `${logoReveal} ${LOGO_DURATION}ms cubic-bezier(0.16, 1, 0.3, 1) forwards, ${glowPulse} 2s ease-in-out infinite`,
          mb: 2,
          userSelect: 'none',
          lineHeight: 1,
        }}
      >
        &gt;_
      </Box>

      {/* Title "MicroHack" typed out */}
      <Box
        sx={{
          height: { xs: '60px', sm: '80px' },
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          opacity: phase === 'logo' ? 0 : 1,
          transition: 'opacity 0.3s ease',
        }}
      >
        <Box
          sx={{
            fontSize: { xs: '48px', sm: '72px' },
            fontWeight: 900,
            fontFamily: '"JetBrains Mono", "Fira Code", monospace',
            letterSpacing: '-0.02em',
            display: 'flex',
            userSelect: 'none',
          }}
        >
          {TITLE.split('').map((char, i) => (
            <Box
              component="span"
              key={i}
              sx={{
                display: 'inline-block',
                opacity: i < typedCount ? 1 : 0,
                transform: i < typedCount ? 'translateY(0) scale(1)' : 'translateY(20px) scale(0.5)',
                transition: 'all 0.15s cubic-bezier(0.16, 1, 0.3, 1)',
                background: 'linear-gradient(135deg, #A78BFA 0%, #7C3AED 30%, #3B82F6 70%, #60A5FA 100%)',
                backgroundSize: '200% auto',
                animation: i < typedCount ? `${shimmer} 3s linear infinite` : undefined,
                WebkitBackgroundClip: 'text',
                WebkitTextFillColor: 'transparent',
                backgroundClip: 'text',
                textShadow: 'none',
                filter: i < typedCount ? 'drop-shadow(0 0 12px rgba(124,58,237,0.5))' : 'none',
              }}
            >
              {char}
            </Box>
          ))}
          {/* Blinking cursor */}
          {phase === 'typing' && (
            <Box
              component="span"
              sx={{
                display: 'inline-block',
                width: { xs: '3px', sm: '4px' },
                height: { xs: '48px', sm: '68px' },
                background: 'linear-gradient(180deg, #7C3AED, #3B82F6)',
                ml: '2px',
                animation: `${cursorBlink} 0.6s step-end infinite`,
                borderRadius: '2px',
                boxShadow: '0 0 10px rgba(124,58,237,0.6)',
              }}
            />
          )}
        </Box>
      </Box>

      {/* Subtitle line reveal */}
      <Box
        sx={{
          mt: 3,
          fontSize: { xs: '14px', sm: '16px' },
          fontFamily: 'monospace',
          color: 'rgba(148,163,184,0.8)',
          letterSpacing: '0.3em',
          textTransform: 'uppercase',
          opacity: phase === 'hold' || phase === 'exit' ? 1 : 0,
          animation: phase === 'hold' ? `${textReveal} 0.4s ease-out forwards` : undefined,
          transition: 'opacity 0.3s ease',
        }}
      >
        hack · learn · build
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
