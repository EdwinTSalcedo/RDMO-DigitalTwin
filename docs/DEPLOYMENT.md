# Deployment Guide: Unity Simulator + Python Inference Server

This document describes the current working deployment path for the RDMO digital twin simulator and the shared-backbone multi-task YOLO model.

## Current Deployment Architecture

The current deployment uses Unity as the simulator frontend and a local Python FastAPI server as the inference backend.

```text
Unity simulator
  -> captures/sends image frame
  -> HTTP POST /predict
  -> FastAPI inference server
  -> YOLOv8n shared-backbone checkpoint
  -> JSON detections
  -> Unity logs / downstream simulator behavior
```

The model runs in Python with PyTorch. Unity does not currently execute this model natively through ONNX or Sentis.

## Model Artifact

The deployed checkpoint is stored in:

```text
models/model_finetuned.pt
```

The Python server loads its runtime copy from:

```text
unity/Assets/Scripts/IA/model_finetuned.pt
```

These files should be kept identical. The root `models/` folder is the public checkpoint location referenced by the README; the Unity-side copy is colocated with the server script so local simulator runs work without extra path configuration.

## Server Implementation

The inference server is implemented in:

```text
unity/Assets/Scripts/IA/api_model_pt.py
```

The server exposes:

| Endpoint | Method | Purpose |
| --- | --- | --- |
| `/health` | GET | Check server/model readiness. |
| `/predict` | POST | Upload one image and receive detections. |
| `/` | GET | Inspect loaded model metadata. |

The current server uses constants in `api_model_pt.py` for model filename, thresholds, image size, and output folder. It does not read the older `RDMO_*` environment variables.

## Local Setup

Recommended local Python environment from the repository root:

```bash
python3 -m venv .venv
source .venv/bin/activate
python -m pip install --upgrade pip
python -m pip install -r requirements.txt
```

The `torch` and `torchvision` versions must be compatible because the model uses `torchvision.ops.roi_align`.

## Starting The Server

From the repository root:

```bash
source .venv/bin/activate
python unity/Assets/Scripts/IA/api_model_pt.py
```

The server listens at:

```text
http://127.0.0.1:5000
```

## Health Check

```bash
curl http://127.0.0.1:5000/health
```

Expected response shape:

```json
{
  "ready": true,
  "modelo": "model_finetuned.pt",
  "modo_carga": "checkpoint_state_dict",
  "device": "cpu"
}
```

The `device` value may be `cuda` when CUDA is available.

## Testing `/predict` From The Terminal

Use any local image:

```bash
curl -X POST \
  -F "file=@/absolute/path/to/test_image.png" \
  http://127.0.0.1:5000/predict
```

Response format:

```json
[
  {
    "clase": "Pothole",
    "det_conf": 0.91,
    "cls_conf": 0.88,
    "caja": [120, 95, 260, 180]
  }
]
```

Field meanings:

| Field | Meaning |
| --- | --- |
| `clase` | Final class label. |
| `det_conf` | Detection confidence from the YOLO head. |
| `cls_conf` | Road-defect subtype confidence, or `null` for `Person` and `Car`. |
| `caja` | Bounding box `[x1, y1, x2, y2]` in image pixels. |

Annotated server outputs are written to:

```text
unity/Assets/Scripts/IA/Detecciones_model_pt/
```

## Unity Setup

Open the Unity project:

```text
unity/
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

## How To Verify Unity Is Using The Server

Use these checks:

1. The Python terminal should show POST requests to `/predict`.
2. Unity Console should show detections from the API client.
3. Annotated images should be saved to `unity/Assets/Scripts/IA/Detecciones_model_pt/`.

## Current Deployment Behavior

The server returns final five-class detections:

- Crocodile Crack
- Single Crack
- Pothole
- Person
- Car

Internally, the model predicts:

- detection classes: Road-defect-general, Person, Car
- subtype classes for road-defect ROIs: Pothole, Crocodile Crack, Single Crack

Road-defect subtypes are classified from ROI-aligned shared feature maps. The server does not crop the RGB image and pass the crop through a second model.

## Troubleshooting

### `torchvision` Import Or ROIAlign Failure

Install dependencies from the project requirements file:

```bash
python -m pip install -r requirements.txt
```

If the issue persists, install a compatible `torch` / `torchvision` pair for your platform and then reinstall the remaining requirements.

### Server Starts, But Unity Shows No Detections

Check:

- server terminal for `/predict` requests
- Unity Console for API logs
- whether the Python server is running before entering the simulator mode
- whether annotated images are being written to `Detecciones_model_pt`
- whether the model thresholds in `api_model_pt.py` are too strict for the test image

### Model File Not Found

Confirm that this file exists:

```text
unity/Assets/Scripts/IA/model_finetuned.pt
```

If only the root checkpoint exists, copy/sync `models/model_finetuned.pt` into `unity/Assets/Scripts/IA/model_finetuned.pt`.

## Web Deployment Notes

The browser-based simulator build lives in:

```text
web/simulator-web/
```

The WebGL simulator runs locally from a static file server. It does not require the Python inference server unless you extend it to call the API.

ONNX is useful for future optimization, especially if the goal is browser-native or Unity-native inference. However, ONNX is not required for the current local Unity + FastAPI workflow.
