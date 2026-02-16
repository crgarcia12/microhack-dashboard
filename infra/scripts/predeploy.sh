#!/bin/bash
set -e

# The Next.js frontend uses server-side proxy routes (/api/* and /hubs/*) to
# forward requests to the backend API via the API_URL env var set on the
# container at deploy time. No client-side NEXT_PUBLIC_API_URL is needed.
echo "Predeploy: frontend uses server-side proxy; no .env.production needed."
