'use client';

import React from 'react';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import rehypeHighlight from 'rehype-highlight';
import rehypeRaw from 'rehype-raw';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Table from '@mui/material/Table';
import TableBody from '@mui/material/TableBody';
import TableCell from '@mui/material/TableCell';
import TableContainer from '@mui/material/TableContainer';
import TableHead from '@mui/material/TableHead';
import TableRow from '@mui/material/TableRow';
import Paper from '@mui/material/Paper';
import Link from '@mui/material/Link';
import type { Components } from 'react-markdown';

interface MarkdownRendererProps {
  content: string;
}

const components: Partial<Components> = {
  h1: ({ children }) => (
    <Typography variant="h4" gutterBottom sx={{ mt: 3, mb: 2, fontWeight: 700 }}>
      {children}
    </Typography>
  ),
  h2: ({ children }) => (
    <Typography variant="h5" gutterBottom sx={{ mt: 3, mb: 1.5, fontWeight: 600 }}>
      {children}
    </Typography>
  ),
  h3: ({ children }) => (
    <Typography variant="h6" gutterBottom sx={{ mt: 2.5, mb: 1 }}>
      {children}
    </Typography>
  ),
  h4: ({ children }) => (
    <Typography variant="subtitle1" gutterBottom sx={{ mt: 2, mb: 1, fontWeight: 600 }}>
      {children}
    </Typography>
  ),
  h5: ({ children }) => (
    <Typography variant="subtitle2" gutterBottom sx={{ mt: 1.5, mb: 0.5, fontWeight: 600 }}>
      {children}
    </Typography>
  ),
  h6: ({ children }) => (
    <Typography variant="subtitle2" gutterBottom sx={{ mt: 1, mb: 0.5, fontWeight: 500, color: 'text.secondary' }}>
      {children}
    </Typography>
  ),
  p: ({ children }) => (
    <Typography variant="body1" sx={{ mb: 1.5, lineHeight: 1.7 }}>
      {children}
    </Typography>
  ),
  a: ({ href, children }) => (
    <Link href={href} target="_blank" rel="noopener noreferrer" sx={{ color: 'secondary.light' }}>
      {children}
    </Link>
  ),
  img: ({ src, alt }) => (
    <Box
      component="img"
      src={src}
      alt={alt || ''}
      sx={{ maxWidth: '100%', height: 'auto', borderRadius: 1, my: 2 }}
    />
  ),
  table: ({ children }) => (
    <TableContainer component={Paper} sx={{ my: 2, bgcolor: 'rgba(26, 19, 51, 0.6)' }}>
      <Table size="small">{children}</Table>
    </TableContainer>
  ),
  thead: ({ children }) => <TableHead>{children}</TableHead>,
  tbody: ({ children }) => <TableBody>{children}</TableBody>,
  tr: ({ children }) => <TableRow>{children}</TableRow>,
  th: ({ children }) => (
    <TableCell sx={{ fontWeight: 600, borderColor: 'rgba(124, 58, 237, 0.2)' }}>
      {children}
    </TableCell>
  ),
  td: ({ children }) => (
    <TableCell sx={{ borderColor: 'rgba(124, 58, 237, 0.2)' }}>
      {children}
    </TableCell>
  ),
  code: ({ className, children, ...props }) => {
    const isBlock = className?.includes('language-') || className?.includes('hljs');
    if (isBlock) {
      return (
        <Box
          component="code"
          className={className}
          sx={{
            display: 'block',
            bgcolor: '#0D0A14',
            borderRadius: 1,
            p: 2,
            my: 2,
            overflow: 'auto',
            fontSize: '0.875rem',
            fontFamily: '"Fira Code", "Roboto Mono", monospace',
            border: '1px solid rgba(124, 58, 237, 0.15)',
            '& .hljs-keyword': { color: '#C084FC' },
            '& .hljs-string': { color: '#86EFAC' },
            '& .hljs-comment': { color: '#64748B' },
            '& .hljs-number': { color: '#FDA4AF' },
            '& .hljs-built_in': { color: '#93C5FD' },
            '& .hljs-title': { color: '#67E8F9' },
            '& .hljs-attr': { color: '#FDE68A' },
          }}
          {...props}
        >
          {children}
        </Box>
      );
    }
    return (
      <Box
        component="code"
        sx={{
          bgcolor: 'rgba(124, 58, 237, 0.15)',
          px: 0.75,
          py: 0.25,
          borderRadius: 0.5,
          fontSize: '0.875em',
          fontFamily: '"Fira Code", "Roboto Mono", monospace',
        }}
      >
        {children}
      </Box>
    );
  },
  pre: ({ children }) => <>{children}</>,
  ul: ({ children }) => (
    <Box component="ul" sx={{ pl: 3, mb: 1.5, '& li': { mb: 0.5 } }}>
      {children}
    </Box>
  ),
  ol: ({ children }) => (
    <Box component="ol" sx={{ pl: 3, mb: 1.5, '& li': { mb: 0.5 } }}>
      {children}
    </Box>
  ),
  blockquote: ({ children }) => (
    <Box
      sx={{
        borderLeft: '4px solid',
        borderColor: 'primary.main',
        pl: 2,
        py: 0.5,
        my: 2,
        bgcolor: 'rgba(124, 58, 237, 0.05)',
        borderRadius: '0 4px 4px 0',
      }}
    >
      {children}
    </Box>
  ),
  hr: () => (
    <Box
      component="hr"
      sx={{
        border: 'none',
        height: 1,
        bgcolor: 'rgba(124, 58, 237, 0.2)',
        my: 3,
      }}
    />
  ),
};

export default function MarkdownRenderer({ content }: MarkdownRendererProps) {
  return (
    <Box sx={{ color: 'text.primary', wordBreak: 'break-word' }}>
      <ReactMarkdown
        remarkPlugins={[remarkGfm]}
        rehypePlugins={[rehypeHighlight, rehypeRaw]}
        components={components}
      >
        {content}
      </ReactMarkdown>
    </Box>
  );
}
