# Project Log

## 2026-06-26 — YOLOv8n threshold sweep after 15-epoch shared-backbone run

### Current status

- The YOLOv8n shared-backbone multi-task checkpoint is deployable from Unity through the Python FastAPI server.
- Active YOLOv8n checkpoint used for local Unity deployment:
  - `artifacts/best_model/best2.pt`
- The model uses the same single-pass architecture:
  - YOLOv8n provides the shared full-image backbone/neck;
  - the detection head predicts `Road-defect-general`, `Person`, and `Car`;
  - the ROIAlign subtype head classifies road-defect detections as `Crocodile Crack`, `Single Crack`, or `Pothole`;
  - inference executes one full-image YOLOv8n forward per image.

### 15-epoch training result

- Training budget: 15 epochs.
- Best validation checkpoint selected at:
  - epoch: `8`
  - validation `final_5class_mAP50`: `0.6997`
- Final test evaluation:

| Metric | Value |
| --- | ---: |
| final 5-class mAP50 | 0.6901 |
| final 5-class mAP50-95 | 0.5503 |
| detection mAP50 | 0.8504 |
| detection mAP50-95 | 0.7496 |
| subtype predicted-ROI macro F1 | 0.9200 |

### Threshold sweep

Threshold sweep output:

```text
/content/drive/MyDrive/Research 🔬/RDMO 🛣/UAV Simulator 🛣️/Experiments/threshold_sweep_best2
```

Road-defect threshold sweep with `Person` and `Car` fixed at `0.25`:

| Road threshold | Road-defect recall | Detection macro F1 | Final macro F1 | Final mAP50 | Subtype predicted-ROI macro F1 |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 0.050 | 0.6730 | 0.8151 | 0.6750 | 0.6904 | 0.9252 |
| 0.075 | 0.6458 | 0.8298 | 0.6949 | 0.6904 | 0.9265 |
| 0.100 | 0.6221 | 0.8387 | 0.7060 | 0.6904 | 0.9263 |
| 0.125 | 0.5985 | 0.8435 | 0.7113 | 0.6904 | 0.9256 |
| 0.150 | 0.5803 | 0.8471 | 0.7149 | 0.6904 | 0.9248 |
| 0.200 | 0.5453 | 0.8504 | 0.7171 | 0.6904 | 0.9247 |
| 0.250 | 0.5165 | 0.8517 | 0.7169 | 0.6904 | 0.9249 |

### Interpretation notes

- Threshold tuning did not change final mAP50 because mAP is computed from a precision-recall curve using low-confidence predictions.
- Threshold tuning does affect deployment behavior, confusion matrices, recall, and macro F1.
- The best final macro F1 was observed at road threshold `0.20`.
- The highest road-defect recall was observed at road threshold `0.05`, but with lower macro F1.
- Recommended Unity deployment default:

```bash
RDMO_CONFIDENCE=0.20
```

- If Unity visually misses too many cracks, test:

```bash
RDMO_CONFIDENCE=0.15
```

- The main remaining bottleneck is road-defect localization/recall, especially `Crocodile Crack` and `Single Crack`; the subtype head remains strong once a road-defect ROI is available.

## 2026-06-25 — Multi-task YOLO model integrated with Unity simulator

### Current status

- The Python inference server is running correctly with the current shared-backbone multi-task YOLO model.
- The Unity-based simulator launches correctly and communicates with the Python inference server.
- Unity is using the server-side model endpoint for detections rather than running the model directly inside Unity.
- The active model artifact is the trained checkpoint available at:
  - `artifacts/best_model/best.pt`
- The implemented architecture is the single-pass multi-task design:
  - one shared YOLO backbone/neck encodes the image once;
  - the detection head predicts `Road-defect-general`, `Person`, and `Car`;
  - the subtype head uses ROI-aligned features from the shared feature maps to classify road-defect detections as `Crocodile Crack`, `Single Crack`, or `Pothole`.

### Unity integration notes

- The Unity project launches successfully.
- The Python inference server starts successfully and exposes the model endpoint used by Unity.
- The server confirms that the single-pass assertion is enabled, meaning each inference should execute the shared backbone once per image.
- The simulator can now use the model server during runtime.

### Training run

- Training configuration: 30 epochs.
- Best validation checkpoint loaded during evaluation:
  - epoch: `11`
  - validation `final_5class_mAP50`: `0.7098`
- Final test evaluation completed successfully.

### Final test metrics

Primary metrics:

| Metric | Value |
| --- | ---: |
| final 5-class mAP50 | 0.6980 |
| final 5-class mAP50-95 | 0.5548 |

### Detection head metrics

Detection classes:

- `Road-defect-general`
- `Person`
- `Car`

| Class | Precision | Recall | F1 |
| --- | ---: | ---: | ---: |
| Road-defect-general | 0.6418 | 0.5605 | 0.5984 |
| Person | 0.9858 | 0.9761 | 0.9809 |
| Car | 0.9986 | 0.9972 | 0.9979 |
| Macro | 0.8754 | 0.8446 | 0.8591 |

Detection mAP:

| Metric | Value |
| --- | ---: |
| detection mAP50 | 0.8525 |
| detection mAP50-95 | 0.7498 |

Detection confusion matrix:

Rows are ground truth classes, columns are predicted classes. Last row/column is background.

```text
[[1265    0    0  992]
 [   0  693    0   17]
 [   0    1  708    1]
 [ 706    9    1    0]]
```

### Final 5-class metrics

Final classes:

- `Crocodile Crack`
- `Single Crack`
- `Pothole`
- `Person`
- `Car`

| Class | Precision | Recall | F1 |
| --- | ---: | ---: | ---: |
| Crocodile Crack | 0.5589 | 0.4187 | 0.4787 |
| Single Crack | 0.4075 | 0.3099 | 0.3521 |
| Pothole | 0.8375 | 0.7927 | 0.8145 |
| Person | 0.9858 | 0.9761 | 0.9809 |
| Car | 0.9986 | 0.9972 | 0.9979 |
| Macro | 0.7577 | 0.6989 | 0.7248 |

Final 5-class mAP:

| Metric | Value |
| --- | ---: |
| final 5-class mAP50 | 0.6980 |
| final 5-class mAP50-95 | 0.5548 |

Final 5-class confusion matrix:

Rows are ground truth classes, columns are predicted classes. Last row/column is background.

```text
[[332  53   0   0   0 408]
 [ 14 216   1   0   0 466]
 [  0   1 608   0   0 158]
 [  0   0   0 693   0  17]
 [  0   0   0   1 708   1]
 [248 260 117   9   1   0]]
```

### Subtype head metrics using ground-truth ROIs

| Metric | Value |
| --- | ---: |
| Accuracy | 0.9070 |
| Macro F1 | 0.9063 |
| Samples | 2257 |

| Class | Precision | Recall | F1 |
| --- | ---: | ---: | ---: |
| Crocodile Crack | 0.9281 | 0.8298 | 0.8762 |
| Single Crack | 0.8119 | 0.9168 | 0.8612 |
| Pothole | 0.9855 | 0.9778 | 0.9817 |

Subtype confusion matrix using ground-truth ROIs:

```text
[[658 132   3]
 [ 50 639   8]
 [  1  16 750]]
```

### Subtype head metrics using predicted ROIs

| Metric | Value |
| --- | ---: |
| Accuracy | 0.9360 |
| Macro F1 | 0.9151 |
| Samples | 1265 |

| Class | Precision | Recall | F1 |
| --- | ---: | ---: | ---: |
| Crocodile Crack | 0.9560 | 0.8488 | 0.8992 |
| Single Crack | 0.7801 | 0.9303 | 0.8486 |
| Pothole | 0.9984 | 0.9967 | 0.9975 |

Subtype confusion matrix using predicted ROIs:

```text
[[348  62   0]
 [ 16 227   1]
 [  0   2 609]]
```

### Interpretation notes

- The detection head performs very strongly on `Person` and `Car`.
- The main performance bottleneck remains fine-grained road-defect localization/classification, especially `Crocodile Crack` and `Single Crack`.
- The subtype head performs well when a valid road-defect ROI is available, which suggests that many final 5-class errors are caused by missed or unmatched road-defect detections rather than only subtype classification mistakes.
- `Pothole` is substantially stronger than the two crack categories in the final 5-class evaluation.
