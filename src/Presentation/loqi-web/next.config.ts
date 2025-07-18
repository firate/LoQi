/** @type {import('next').NextConfig} */
const nextConfig = {
    // Static export for Docker integration
    output: 'export',
    distDir: 'out',
    trailingSlash: true,

    // Optimize for production
    images: {
        unoptimized: true // Required for static export
    },

    // Asset prefix for production (optional)
    assetPrefix: process.env.NODE_ENV === 'production' ? '' : '',

    // Environment variables
    env: {
        API_URL: process.env.API_URL || '',
    },

    // No rewrites needed in production - same origin
    experimental: {
        optimizeCss: false,
    }
};

module.exports = nextConfig;