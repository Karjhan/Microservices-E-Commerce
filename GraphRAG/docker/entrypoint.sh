#!/bin/bash
set -e

echo "Starting GraphRAG service..."

case "${SERVICE_MODE:-api}" in
    api)
        echo "Starting API server..."
        exec uvicorn app.api.main:app --host 0.0.0.0 --port 8100
        ;;
    worker)
        echo "Starting event consumer worker..."
        exec python -m app.workers.consumer
        ;;
    both)
        echo "Starting API + Worker..."
        python -m app.workers.consumer &
        exec uvicorn app.api.main:app --host 0.0.0.0 --port 8100
        ;;
    *)
        echo "Unknown SERVICE_MODE: ${SERVICE_MODE}"
        exit 1
        ;;
esac
