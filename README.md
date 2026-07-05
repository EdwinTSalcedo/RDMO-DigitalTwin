<p>
  <h1>
    A Digital Twin Framework for Traffic-Aware UAV Pavement Monitoring in Open-Traffic Conditions 🛣️ 
  </h1>
</p>

[Yamil Uchani](https://www.linkedin.com/in/yamiluchani/), [Grace Luna](https://www.linkedin.com/in/grace-luna-verdueta/), [Edwin Salcedo](https://www.linkedin.com/in/edwinsalcedo/), and [Mauricio Figueroa](https://www.linkedin.com/in/mau-figue/)

[![arXiv](https://img.shields.io/badge/arXiv-2606.20742-grey?labelColor=B31B1B&logo=arxiv&logoColor=white)](https://arxiv.org/abs/2606.20742)
[![Paper PDF](https://img.shields.io/badge/Paper-PDF-grey?labelColor=B31B1B&logo=adobeacrobatreader&logoColor=white)](https://arxiv.org/pdf/2606.20742)
[![Unity](https://img.shields.io/badge/Unity-6000.2.14f1-grey?labelColor=000000&logo=unity&logoColor=white)](unity/)
[![Model](https://img.shields.io/badge/Model-model__finetuned.pt-grey?labelColor=3776AB&logo=pytorch&logoColor=white)](models/model_finetuned.pt)
[![Datasets](https://img.shields.io/badge/Datasets-Google%20Drive-grey?labelColor=34A853&logo=googledrive&logoColor=white)](https://drive.google.com/drive/folders/1bfLm6uia9jM-xPxxl2PxLrq3OVG0Z8TZ?usp=sharing)
[![Repository](https://img.shields.io/badge/GitHub-RDMO--DigitalTwin-grey?labelColor=181717&logo=github&logoColor=white)](https://github.com/EdwinTSalcedo/RDMO-DigitalTwin)

<p align="center">
  <img src="assets/images/droneview.png" width="92%" alt="Unity digital twin simulator map view with traffic agents and an inspection segment" />
</p>

## Menu

1. [Introduction](#1-introduction)
2. [Quick Start](#2-quick-start)
3. [Data](#3-data)
4. [Perception Model](#4-perception-model)
5. [Recovery Strategy Experiments](#5-recovery-strategy-experiments)
6. [Test Automator](#6-test-automator)
7. [System Hardware Requirements](#7-system-hardware-requirements)
8. [Citation](#8-citation)

## 1. Introduction

This repository contains the Unity digital twin simulator, trained perception models, and Python inference server for traffic-aware UAV pavement monitoring in open-traffic conditions. It provides a controlled environment for studying how autonomous UAV inspection behaves when dynamic vehicles, pedestrians, and temporary occlusions affect road-surface visibility.

The framework integrates:

- a Unity urban-road environment with dynamic vehicles and pedestrians;
- procedurally generated road defects, including Single Crack, Crocodile Crack, and Pothole;
- autonomous UAV navigation over road segments using Unity NavMesh;
- adaptive recovery strategies for occluded inspection regions;
- a shared-backbone multitask YOLOv8n perception model for road defects, pedestrians, and vehicles;
- a batch experiment automator for repeatable UAV recovery-strategy evaluation.

At a high level, the runtime workflow is:

```text
Unity digital twin
  -> UAV route planning and segment inspection
  -> traffic-aware visibility and recovery logic
  -> optional Python perception server
  -> model-assisted detections and experiment logs
```

```text
RDMO-DigitalTwin/
|-- README.md
|-- assets/
|   `-- images/                # Paper figures used in this README
|-- docs/
|   |-- paper.pdf              # Local paper PDF/reference copy
|   |-- MODEL_CARD.md          # Historical model notes
|   |-- DEPLOYMENT.md          # Historical deployment notes
|   `-- LOG.md                 # Historical development notes
|-- models/
|   |-- model_base.pt          # Baseline checkpoint
|   `-- model_finetuned.pt     # Deployed checkpoint for the simulator server
|-- results/                   # UAV recovery-strategy results and CSV exports
`-- unity/
    |-- Assets/
    |-- Packages/
    `-- ProjectSettings/
```

## 2. Quick Start

### 1. Open the Unity project

The Unity project root is: `unity/`. Open that folder in Unity Hub. The project was last configured with: `Unity 6000.2.14f1`.

The main scenes are registered in `unity/ProjectSettings/EditorBuildSettings.asset`.

| Scene | Purpose |
| --- | --- |
| `Assets/Scenes/Mode_Menu.unity` | Entry scene for launching simulator modes. |
| `Assets/Scenes/Mode_Load.unity` | Loading and initialisation scene. |
| `Assets/Scenes/Mode_Model.unity` | Interactive visual simulation. |
| `Assets/Scenes/Mode_Data.unity` | Batch experiment and data mode. |
| `Assets/Scenes/Mode_Capture.unity` | Dataset capture mode. |

### 2. Create the Python environment

From the repository root:

```bash
python3 -m venv .venv
source .venv/bin/activate
python -m pip install --upgrade pip
python -m pip install torch torchvision ultralytics fastapi "uvicorn[standard]" python-multipart opencv-python numpy
```

### 3. Start the model server

The deployed checkpoint is: `models/model_finetuned.pt` Start the server from the repository root:

```bash
source .venv/bin/activate
python unity/Assets/Scripts/AI/api_model_pt.py
```

Then, the server should allow inference at: `http://127.0.0.1:5000`. For a health check, use:
```bash
curl http://127.0.0.1:5000/health
```

Prediction test:
```bash
curl -X POST \
  -F "file=@/absolute/path/to/test_image.png" \
  http://127.0.0.1:5000/predict
```

Example response:

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

Annotated server outputs are written to:

```text
unity/Assets/Scripts/AI/Detecciones_model_pt/
```

### 4. Run the simulator

1. Start the Python server.
2. Open `unity/` in Unity Hub.
3. Open `Assets/Scenes/Mode_Menu.unity`.
4. Press Play.
5. Select the desired simulator mode from the menu.

## 3. Data

The datasets described in the paper are available from [Google Drive](https://drive.google.com/drive/folders/1bfLm6uia9jM-xPxxl2PxLrq3OVG0Z8TZ?usp=sharing). Download the datasets and extract them under the repository root so the expected layout is:

```text
data/
|-- merged_dataset/
|   |-- data.yaml
|   |-- train/
|   |-- val/
|   `-- test/
|-- augmented_dataset/
|   |-- dataset.yaml
|   |-- train/
|   |-- valid/
|   `-- test/
`-- synthetic_dataset/
    |-- dataset.yaml
    |-- train/
    |-- valid/
    `-- test/
```

### Dataset Files

| Folder | Paper name | Purpose | Paper statistics |
| --- | --- | --- | --- |
| `merged_dataset` | Merged Dataset | Normalised five-class dataset assembled from the source road-damage and UAV traffic datasets before balancing. | 18,741 images, including 17,938 annotated images and 803 backgrounds; 71,034 boxes. |
| `augmented_dataset` | Balanced Dataset | Class-balanced and augmented real-image dataset used for the first model-development stage. | 46,175 images, including 42,755 annotated images and 3,420 backgrounds; 120,769 boxes. |
| `synthetic_dataset` | Synthetic Dataset | Unity-captured target-domain dataset used for simulator-domain fine-tuning and evaluation. | 2,235 images; 25,943 boxes. |

### Simulator data

The Unity digital twin generates UAV-view road scenes with:

<p align="center">
  <img src="assets/images/carview.png" width="31%" alt="Ground vehicle view in the Unity simulator" />
  <img src="assets/images/droneview.png" width="31%" alt="UAV inspection view in the Unity simulator" />
  <img src="assets/images/droneview-topview.png" width="31%" alt="Top-down UAV camera view" />
</p>

- road-surface defects: `Single Crack`, `Crocodile Crack`, and `Pothole`;
- traffic agents: `Person` and `Car`;
- configurable traffic densities and UAV altitudes;
- road-segment inspection targets selected manually or by the experiment automator.

<p align="center">
  <img src="assets/images/crack-sample.png" width="31%" alt="Procedural single-crack sample" />
  <img src="assets/images/crocodile-sample.png" width="31%" alt="Procedural crocodile-crack sample" />
  <img src="assets/images/pothole-sample.png" width="31%" alt="Procedural pothole sample" />
</p>

## 4. Perception Model

The deployed model is a shared-backbone multitask YOLOv8n model. It performs coarse detection first and then classifies road-defect subtypes from ROI-aligned features.

<p align="center">
  <img src="assets/images/multitask-model.png" width="100%" alt="Shared-backbone multitask YOLOv8n perception model" />
</p>

The model maps classes as follows:

| ID | Class |
| ---: | --- |
| 0 | `Crocodile Crack` |
| 1 | `Single Crack` |
| 2 | `Pothole` |
| 3 | `Person` |
| 4 | `Car` |

## 5. Recovery Strategy Experiments

The digital twin evaluates UAV inspection behaviour under occlusion. At each time step, it tracks the road segments, dynamic traffic agents, UAV state, inspection memory, and available recovery policies. When the target road segment becomes insufficiently visible, the simulator can trigger a recovery strategy.

| ID | Strategy | Action |
| ---: | --- | --- |
| 1 | `Baseline` | Follow the planned inspection route without an explicit recovery action. |
| 2 | `Hover` | Wait briefly over the target segment so temporary occlusions can clear. |
| 3 | `Micro` | Apply a small local repositioning movement to improve visibility. |
| 4 | `Skip` | Skip the occluded segment and revisit it later in the mission. |

The experiment protocol combines:

| Factor | Levels |
| --- | --- |
| Traffic density | `Low`, `Medium`, `High` |
| UAV altitude | `Low = 6 m`, `Medium = 10 m`, `High = 15 m` |
| Recovery strategy | `Baseline`, `Hover`, `Micro`, `Skip` |

The full batch covers:

```text
3 traffic levels x 3 altitude levels x 4 strategies = 36 episodes
```

The final paper reports that flight altitude strongly affects inspection coverage, but no single recovery strategy dominates across all traffic and altitude settings. Baseline remains strongest in several medium- and high-altitude configurations, Hover improves coverage in some medium- and high-traffic cases but can increase mission time under congestion, Micro is useful in selected low-altitude cases, and Skip introduces revisit behaviour at the cost of longer missions and higher energy use.

The processed recovery-strategy results are available in the tracked `results/` folder:

| File | Purpose |
| --- | --- |
| `results/results.xlsx` | Original workbook used to organise the UAV recovery-strategy experiment results. |
| `results/uav_segment_results.csv` | Detailed per-segment export with 720 rows: 36 experiment configurations x 20 segment-level entries. Use this for fine-grained analysis. |
| `results/uav_summary_results.csv` | Compact summary table with one row per traffic-altitude-strategy configuration. Use this to reproduce paper-style coverage, recovery, energy, and mission-time tables. |
| `results/uav_results.csv` | Full-sheet CSV export that preserves the workbook's side-by-side layout for traceability. |

## 6. Test Automator

The batch experiment automator is attached to the `DigitalTwinManager` object in:

```text
unity/Assets/Scenes/Mode_Data.unity
```

Recommended workflow:

1. Open `Assets/Scenes/Mode_Data.unity`.
2. Select the inactive `DigitalTwinManager` object in the Hierarchy.
3. Find the `DigitalTwin.ExperimentAutomator` component in the Inspector.
4. Configure the experiment fields.
5. Press Play.
6. From the component context menu, run `Start Experiment Batch`.

Useful automator fields:

| Field | Recommended value |
| --- | --- |
| `segmentsPerEpisode` | Use `1` for a smoke test or `20` for the full protocol. |
| `startFromEpisode` | Use `0` to start from the beginning. |
| `initialTrafficLevel` | `0=Low`, `1=Medium`, `2=High`. |
| `initialAltitudeLevel` | `0=Low`, `1=Medium`, `2=High`. |
| `initialNavigationMode` | `1=Baseline`, `2=Hover`, `3=Micro`, `4=Skip`. |
| `recordingMode` | Disabled for the full 36-episode sweep; enabled for selected recording runs. |

The automator disables `PythonInferenceClient` during controlled batch execution so recovery-strategy comparisons remain deterministic and memory-efficient. Use the Python server for model-assisted interactive runs and dataset capture workflows.

Batch outputs are written under:

```text
unity/Assets/DigitalTwin_Logs/
```

Important files:

| File | Contents |
| --- | --- |
| `Progress_Results.csv` | Progress rows written while the batch runs. |
| `All_Streets_Results.csv` | Full experiment summary. |
| `Paper_Table_Results.csv` | Compact coverage, time, energy, and recovery table. |
| `Ep*_Seg*_*.csv` | Per-segment details. |
| `Episode_*_*.csv` | Per-episode segment details. |

## 7. System Hardware Requirements

### Simulator

- Unity `6000.2.14f1`.
- A desktop or laptop capable of running a Unity 3D project with dynamic agents and NavMesh navigation.
- A discrete GPU is recommended for smoother interactive runs.

### Python inference server

- Python 3.10 or newer.
- PyTorch, TorchVision, Ultralytics, FastAPI, Uvicorn, OpenCV, and NumPy.
- CUDA is recommended for real-time inference, but CPU execution can be used for debugging.

The experiments reported in the paper were run on a desktop with an AMD Ryzen 5 5600G CPU, NVIDIA GeForce GTX 1050 Ti GPU with 4 GB VRAM, and 16 GB RAM. Detector FPS comparisons were measured on an NVIDIA T4 GPU.

## 8. Citation

If you use this simulator, model, or experiment automator in your research, please cite the associated paper:

```bibtex
@misc{uchani2026digitaltwinopentraffic,
  title         = {A Digital Twin Framework for Traffic-Aware UAV Pavement Monitoring in Open-Traffic Conditions},
  author        = {Uchani, Yamil and Luna, Grace and Salcedo, Edwin and Figueroa, Mauricio},
  year          = {2026},
  eprint        = {2606.20742},
  archivePrefix = {arXiv},
  primaryClass  = {cs.RO},
  doi           = {10.48550/arXiv.2606.20742},
  url           = {https://arxiv.org/abs/2606.20742}
}
```
