import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  output: 'standalone',
  transpilePackages: ['@mui/material', '@mui/icons-material'],
};

export default nextConfig;
