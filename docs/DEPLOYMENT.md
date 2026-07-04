# Deployment Guide: Unity Simulator + Python Inference Server

This document describes the current working deployment path for the RDMO simulator and the shared-backbone multi-task YOLO model.

## Current deployment architecture

The current deployment uses Unity as the simulator frontend and a local Python server as the inference backend.

```text
Unity simulator
  → captures/sends image frame
  → HTTP POST /predict
  → FastAPI inference server
  → SharedBackboneMultiTaskYOLO
  → JSON detections
  → Unity logs / downstream simulator behavior
```

The model runs in Python with PyTorch. Unity does not currently execute this model natively through ONNX or Sentis.

## Model artifact

The YOLOv8n shared-backbone server expects a checkpoint trained with the same YOLOv8n base:

```text
experiments/runs/shared_backbone_multitask_640/checkpoints/best.pt
```

Set `RDMO_MODEL_PATH` to that checkpoint after training. The earlier `artifacts/best_model/best.pt` artifact was produced before the YOLOv8n switch and should not be loaded into the YOLOv8n architecture.

## Server implementation

The inference server is implemented in:

```text
unity/Detector de baches/Assets/Scripts/IA/api_baches.py
```

The server exposes:

| Endpoint | Method | Purpose |
| --- | --- | --- |
| `/health` | GET | Check server/model readiness. |
| `/predict` | POST | Upload one image and receive detections. |

## Environment variables

The server can be configured with environment variables:

| Variable | Default | Purpose |
| --- | --- | --- |
| `RDMO_REPO_ROOT` | auto-detected | Path to the outer `rdmo-simulator` repository. |
| `RDMO_MODEL_PATH` | `experiments/runs/shared_backbone_multitask_640/checkpoints/best.pt` | Path to the YOLOv8n multi-task checkpoint. |
| `RDMO_DETECTOR_DEFINITION` | `yolov8n.pt` | YOLO base checkpoint used to instantiate the shared-backbone architecture before loading the custom checkpoint. |
| `RDMO_DEVICE` | `cuda` if available, else `cpu` | Inference device. |
| `RDMO_CONFIDENCE` | `0.25` | Detection confidence threshold. |
| `RDMO_IOU_THRESHOLD` | `0.45` | NMS IoU threshold. |
| `RDMO_SAVE_ANNOTATED_IMAGES` | `1` | Whether to save annotated images. |
| `RDMO_DETECTIONS_DIR` | `Assets/Scripts/IA/Detecciones_IA` | Output folder for annotated images. |

Example:

```bash
RDMO_DEVICE=cpu \
RDMO_CONFIDENCE=0.20 \
python "unity/Detector de baches/Assets/Scripts/IA/api_baches.py"
```

## Local setup

Recommended local Python environment:

```bash
pyenv virtualenv 3.10.20 rdmo-simulator-310
pyenv local rdmo-simulator-310
python -m pip install --upgrade pip
python -m pip install torch torchvision
python -m pip install -r experiments/requirements-unity-api.txt
```

The `torch` and `torchvision` versions must be compatible because the model uses `torchvision.ops.roi_align`.

## Starting the server

From the repository root:

```bash
python "unity/Detector de baches/Assets/Scripts/IA/api_baches.py"
```

Expected startup output:

```text
Loaded shared-backbone model: .../experiments/runs/shared_backbone_multitask_640/checkpoints/best.pt
Device: cpu or cuda
Single-pass assertion: enabled
Uvicorn running on http://127.0.0.1:5000
```

The server listens at:

```text
http://127.0.0.1:5000
```

## Health check

```bash
curl http://127.0.0.1:5000/health
```

Expected response:

```json
{
  "ready": true,
  "model": ".../experiments/runs/shared_backbone_multitask_640/checkpoints/best.pt",
  "device": "cpu",
  "architecture": "shared-yolo-backbone-roialign-subtype-head"
}
```

## Testing `/predict` from the terminal

Use any local image:

```bash
curl -X POST \
  -F "file=@/path/to/image.png" \
  http://127.0.0.1:5000/predict
```

Response format:

```json
[
  {
    "clase": "Pothole",
    "confianza": 0.8731,
    "caja": [120, 95, 260, 180]
  }
]
```

Field meanings:

| Field | Meaning |
| --- | --- |
| `clase` | Final class label. |
| `confianza` | Final confidence score. |
| `caja` | Bounding box `[x1, y1, x2, y2]` in image pixels. |

## Unity setup

Open the Unity project:

```text
unity/Detector de baches
```

Recommended Unity version:

```text
6000.2.14f1
```

Open:

```text
Assets/Scenes/Mode_Menu.unity
```

Then press Play.

The Unity client sends frames to:

```text
http://127.0.0.1:5000/predict
```

The relevant Unity-side script is:

```text
Assets/Scripts/IA/PythonInferenceClient.cs
```

## How to verify Unity is using the server

Use these checks:

1. The Python terminal should show POST requests to `/predict`.
2. Unity Console should show detections from the API client.
3. Annotated images should be saved, unless disabled, to:

```text
unity/Detector de baches/Assets/Scripts/IA/Detecciones_IA
```

The server also checks that every `/predict` call executes exactly one backbone forward pass. If this assertion fails, the request raises an error.

## Current deployment behavior

The server returns final five-class detections:

- Crocodile Crack;
- Single Crack;
- Pothole;
- Person;
- Car.

Internally, the model predicts:

- detection classes: Road-defect-general, Person, Car;
- subtype classes for road-defect ROIs: Crocodile Crack, Single Crack, Pothole.

Road-defect subtypes are classified from ROI-aligned shared feature maps. The server does not crop the RGB image and does not pass the crop through YOLO again.

## Troubleshooting

### `ModuleNotFoundError: No module named '_lzma'`

Use the Python 3.10 pyenv environment:

```bash
pyenv local rdmo-simulator-310
```

Then reinstall dependencies.

### `torchvision` import or ROIAlign failure

Install a compatible `torch` / `torchvision` pair:

```bash
python -m pip install --upgrade torch torchvision
```

Then reinstall the project requirements:

```bash
python -m pip install -r experiments/requirements-unity-api.txt
```

### Server starts, but Unity shows no detections

Check:

- server terminal for `/predict` requests;
- Unity Console for API logs;
- whether capture/recording mode is enabled in Unity;
- whether the confidence threshold is too high;
- whether annotated images are being written to `Detecciones_IA`.

For a lower road-defect threshold test:

```bash
RDMO_CONFIDENCE=0.15 \
python "unity/Detector de baches/Assets/Scripts/IA/api_baches.py"
```

### Model file not found

Set the model path explicitly:

```bash
RDMO_MODEL_PATH=/absolute/path/to/experiments/runs/shared_backbone_multitask_640/checkpoints/best.pt \
python "unity/Detector de baches/Assets/Scripts/IA/api_baches.py"
```

### Repository root not found

Set:

```bash
RDMO_REPO_ROOT=/absolute/path/to/RDMO-Simulator
```

## Web deployment notes

The current deployment can be exposed to a website by hosting the FastAPI server and making the web frontend or Unity WebGL build call the hosted HTTPS endpoint.

Recommended first web architecture:

```text
Website or Unity WebGL
  → HTTPS API
  → FastAPI server
  → PyTorch best.pt model
  → JSON detections
```

ONNX is useful for future optimization, especially if the goal is browser-native or Unity-native inference. However, ONNX is not required for a web demo that calls a hosted Python API.

The model is not a plain YOLO checkpoint, so ONNX export is more complex than a standard Ultralytics export. The custom ROIAlign subtype branch and postprocessing must be handled explicitly.
