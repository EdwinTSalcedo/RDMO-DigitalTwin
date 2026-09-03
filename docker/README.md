# Docker Deployment Guide with GPU Acceleration

This guide describes how to deploy and run the RDMO Digital Twin & Real-Time Perception Platform using Docker Compose, with browser GPU rendering support and optional NVIDIA CUDA acceleration for backend inference.

---

## Service Architecture

| Service | Container | Port | Description |
| :--- | :--- | :--- | :--- |
| **Frontend WebGL** | `rdmo-web-frontend` | `http://localhost/` | Nginx web server delivering the 3D interactive simulation compiled in WebGL 2.0. |
| **Backend YOLO API** | `rdmo-yolo-backend` | `http://localhost:5000/` | REST API served with FastAPI + PyTorch for real-time pothole and road defect inference. |

---

## 1. Standard Docker Compose Startup

Start all services with a single command:

```bash
docker compose up -d
```

Once the containers are running, open the web browser at: `http://localhost/`

---

## 2. GPU Acceleration Setup

To achieve peak performance for 3D rendering and AI inference speed, enable GPU acceleration:

### A. AI Inference Backend (Docker Container + NVIDIA CUDA)
Enables YOLO model execution on GPU instead of CPU (reducing latency from ~200ms to **10–15ms per frame**).

1. **Install NVIDIA Container Toolkit (Linux):**
   ```bash
   curl -fsSL https://nvidia.github.io/libnvidia-container/gpgkey | sudo gpg --dearmor --yes -o /etc/apt/trusted.gpg.d/nvidia-container-toolkit.gpg
   echo "deb https://nvidia.github.io/libnvidia-container/stable/ubuntu18.04/amd64 /" | sudo tee /etc/apt/sources.list.d/nvidia-container-toolkit.list
   sudo apt-get update && sudo apt-get install -y nvidia-container-toolkit
   sudo nvidia-ctk runtime configure --runtime=docker
   sudo systemctl restart docker
   ```

2. **Verify GPU configuration in `docker-compose.yml`:**
   ```yaml
   services:
     yolo-backend:
       deploy:
         resources:
           reservations:
             devices:
               - driver: nvidia
                 count: 1
                 capabilities: [gpu]
   ```

3. **Start or restart containers:**
   ```bash
   docker compose up -d
   ```

---

### B. Frontend WebGL (3D Rendering at 60+ FPS)
On laptops with hybrid graphics (Intel Integrated + NVIDIA Dedicated GPU), force dedicated GPU rendering for optimal frame rates:

#### Option 1: Firefox (Linux)
- Right-click Firefox icon -> Select **"Run with Dedicated Graphics Card"**.
- Or via terminal:
  ```bash
  MOZ_ENABLE_WAYLAND=0 __NV_PRIME_RENDER_OFFLOAD=1 __GLX_VENDOR_LIBRARY_NAME=nvidia firefox http://localhost &
  ```

#### Option 2: Chrome / Microsoft Edge / Brave
1. Go to `Settings -> System` and turn on: **"Use hardware acceleration when available"**.
2. Open `chrome://flags` (or `edge://flags`) and set:
   - **Override software rendering list:** `Enabled`
   - **GPU rasterization:** `Enabled`

---

## 3. Local Storage for AI Detection Captures

Volume storage is configured in `docker-compose.yml`:

```yaml
volumes:
  - ./detections_output:/app/detections
```

### Output Behaviour:
1. Docker automatically creates the `detections_output/` folder in your repository directory upon container launch.
2. When the simulator captures frames or detects potholes, annotated images with bounding boxes are saved to: `./detections_output/`
3. **Web / HTTP Access:** Access processed captures via browser at: `http://localhost/api/detections/<image_name>.jpg`

---

## Management Commands

- Check container status and health: `docker compose ps`
- Stream live backend logs: `docker compose logs -f yolo-backend`
- Stop services: `docker compose down`
