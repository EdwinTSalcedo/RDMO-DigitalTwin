FROM python:3.10-slim

# Install uv binary from official image
COPY --from=ghcr.io/astral-sh/uv:latest /uv /uvx /bin/

ENV PYTHONUNBUFFERED=1 \
    DEBIAN_FRONTEND=noninteractive \
    MODEL_PATH=/app/models/model_finetuned.pt \
    DETECTIONS_DIR=/app/detecciones \
    UV_HTTP_TIMEOUT=600

WORKDIR /app

# Install system dependencies for OpenCV and PyTorch
RUN apt-get update && apt-get install -y --no-install-recommends \
    libgl1 \
    libglib2.0-0 \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Install python dependencies using uv for fast installation
COPY docker/backend/requirements.txt /app/requirements.txt
RUN uv pip install --system --no-cache -r /app/requirements.txt

# Copy backend script and model
COPY unity/Assets/Scripts/IA/api_model_pt.py /app/api_model_pt.py
COPY models/ /app/models/

RUN mkdir -p /app/detecciones

EXPOSE 5000

HEALTHCHECK --interval=10s --timeout=5s --start-period=15s --retries=3 \
    CMD curl -f http://localhost:5000/health || exit 1

CMD ["python", "api_model_pt.py"]
