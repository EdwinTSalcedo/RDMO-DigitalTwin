# 🤖 AI Agent Master Directive (`AGENTS.md`) - RDMO Release v2.0

This file defines the system architecture, execution directives, and technical context for the **RDMO (Digital Twin & Perception Framework)** deployment package for AI assistants and automated agents on **Windows**, **Linux**, and **Docker Compose**.

---

## 📌 Project General Context

**RDMO** is a Digital Twin and real-time perception platform focused on 3D simulation and automated road defect detection (potholes, cracks) using AI and YOLOv8 models.

The project orchestrates 2 core services using Docker Compose:
1. **Frontend WebGL (Nginx)**: Web server delivering the 3D interactive simulation in browser (Port `80`).
2. **Backend Inference (FastAPI + PyTorch / YOLOv8)**: REST API for real-time pothole and defect detection (Port `5000`).

---

## 📂 Deployment Package Structure

- **`docker-compose.yml`**: Multi-container orchestrator.
- **`start_windows.bat` & `stop_windows.bat`**: Windows batch execution scripts (located in parent folder and `.zip` archive).
- **`README_WINDOWS.txt` & `README.md`**: User setup documentation.
- **`docker/`**:
  - `docker/frontend/`: `Dockerfile` and `nginx.conf` (Nginx serving `web/simulator-web`).
  - `docker/backend/`: `Dockerfile` and `requirements.txt` (FastAPI + PyTorch).
- **`web/simulator-web/`**: Exported Unity WebGL package (`Build/`, `TemplateData/`, `index.html`).
- **`models/`**: AI model weights (`model_finetuned.pt` and `model_base.pt`).
- **`unity/Assets/Scripts/IA/api_model_pt.py`**: FastAPI inference server code.

---

## ⚠️ Key Directives for AI Agents

### 1. Docker Orchestration Commands (Windows & Linux)
- Start services: `docker compose up -d` (or execute `start_windows.bat` on Windows).
- Stop services: `docker compose down` (or execute `stop_windows.bat`).
- Check container health & status: `docker compose ps`
- Stream live backend logs: `docker compose logs -f yolo-backend`

### 2. Volume Persistence & Detection Outputs
- The AI capture volume is mapped to `./detections_output:/app/detections`.
- Annotated photos with YOLO bounding boxes are automatically saved on the host machine in:  
  📁 `./detections_output/`
- The static HTTP reading endpoint is:  
  👉 `http://localhost/api/detections/<filename>.jpg`

### 3. GPU Acceleration
- On Windows with Docker Desktop + WSL2, NVIDIA GPU is shared automatically if `deploy.resources.reservations.devices` is active in `docker-compose.yml`.
- On Linux, host requires `nvidia-container-toolkit` installed.

---

## 📊 Backend API Endpoints (Port 5000 / Nginx Proxy `/api/`)

- **Server Health**: `GET http://localhost:5000/health` (Returns `{"status": "ok"}`)
- **Image Prediction**: `POST http://localhost:5000/predict` (Multipart `file: <image>`)
- **Detection Static Files**: `GET http://localhost:5000/detections/<filename>`
