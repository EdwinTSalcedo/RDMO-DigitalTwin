# Model Card: Shared-Backbone Multi-Task YOLO for Road-Damage Monitoring

## Model summary

This model is a shared-backbone multi-task detector for UAV-based road-surface monitoring in the RDMO simulator.

It is not a standard five-class YOLO detector. The model performs coarse object detection and fine-grained road-defect subtype classification using one shared image encoding.

```text
Input image
  → shared YOLO backbone/neck
  → shared feature maps
      ├── detection head
      │     Road-defect-general / Person / Car
      └── ROIAlign + subtype head
            Crocodile Crack / Single Crack / Pothole
```

The checkpoint currently used by the Unity deployment is:

```text
models/model_finetuned.pt
```

The Python inference server loads the runtime copy at:

```text
unity/Assets/Scripts/IA/model_finetuned.pt
```

## Intended use

The model is intended for:

- road-defect detection inside the Unity-based RDMO digital twin;
- UAV-view pavement-monitoring experiments;
- simulated traffic-aware inspection workflows;
- research evaluation of multi-task perception for road monitoring.

It is not intended as a safety-critical real-world road-inspection system without further validation on real UAV imagery and site-specific calibration.

## Architecture

The model is implemented in:

```text
notebooks/multimodal_uav_detector.ipynb
```

The deployment loader and ROIAlign subtype inference path are implemented in:

```text
unity/Assets/Scripts/IA/api_model_pt.py
```

The architecture contains:

| Component | Role |
| --- | --- |
| YOLO backbone/neck | Encodes the full image once and produces shared feature maps. |
| YOLO detection head | Predicts bounding boxes and three coarse detection classes. |
| ROIAlign | Pools fixed-size road-defect features from the shared P3 feature map. |
| Subtype head | Classifies road-defect ROIs into three fine-grained defect subtypes. |

The model uses a hook to capture the feature pyramid tensors entering the YOLO detection head. The subtype classifier consumes ROI-aligned features from those cached shared tensors. The RGB crop is not reprocessed by YOLO or by a second backbone.

The deployed server reconstructs the YOLO detector from the checkpoint state dict, collects compatible feature maps from the same image forward path, and applies ROIAlign for road-defect subtype classification.

## Classes

### Detection head classes

| ID | Class |
| ---: | --- |
| 0 | Road-defect-general |
| 1 | Person |
| 2 | Car |

### Subtype head classes

| ID | Class |
| ---: | --- |
| 0 | Crocodile Crack |
| 1 | Single Crack |
| 2 | Pothole |

### Final output classes

| ID | Class |
| ---: | --- |
| 0 | Crocodile Crack |
| 1 | Single Crack |
| 2 | Pothole |
| 3 | Person |
| 4 | Car |

## Label mapping

The dataset keeps the original five-class labels. During training, they are mapped as follows:

| Original fine label | Detection target | Subtype target |
| --- | --- | --- |
| Crocodile Crack | Road-defect-general | Crocodile Crack |
| Single Crack | Road-defect-general | Single Crack |
| Pothole | Road-defect-general | Pothole |
| Person | Person | ignored |
| Car | Car | ignored |

Person and Car boxes do not contribute to the subtype classification loss.

## Input and output

### Input

- Input image: BGR image as a NumPy array in the Python implementation.
- Default training/evaluation image size: `640`.
- Letterboxing is applied before passing the image to the YOLO model.

### Output

The Python model returns final five-class predictions with:

- bounding box coordinates;
- final class label;
- final confidence;
- detection confidence;
- detection class ID;
- final class ID;
- subtype confidence for road defects.

For road defects:

```text
final_confidence = detection_confidence × subtype_confidence
```

For Person and Car:

```text
final_confidence = detection_confidence
```

The Unity inference server exposes the predictions using this JSON contract:

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

## Training objective

The model is trained with a multi-task loss:

```text
loss_total = loss_detection + lambda_subtype × loss_subtype
```

Where:

- `loss_detection` is the standard YOLO detection loss over the three detection classes;
- `loss_subtype` is cross-entropy loss over road-defect ROIs only;
- `lambda_subtype` is configurable and defaults to `1.0`.

## Training configuration used for current checkpoint

The current deployed checkpoint is `model_finetuned.pt`.

The best validation checkpoint was:

| Field | Value |
| --- | --- |
| Best epoch | 8 |
| Validation selection metric | final 5-class mAP50 |
| Validation final 5-class mAP50 | 0.9535 |

The deployed artifact is:

```text
models/model_finetuned.pt
```

## Test-set performance

### Primary metrics

| Metric | Value |
| --- | ---: |
| final 5-class mAP50 | 0.6980 |
| final 5-class mAP50-95 | 0.5548 |
| final 5-class macro F1 | 0.7248 |
| detection mAP50 | 0.8525 |
| detection mAP50-95 | 0.7498 |

### Detection head metrics

| Class | Precision | Recall | F1 |
| --- | ---: | ---: | ---: |
| Road-defect-general | 0.6418 | 0.5605 | 0.5984 |
| Person | 0.9858 | 0.9761 | 0.9809 |
| Car | 0.9986 | 0.9972 | 0.9979 |
| Macro | 0.8754 | 0.8446 | 0.8591 |

### Final five-class metrics

| Class | Precision | Recall | F1 |
| --- | ---: | ---: | ---: |
| Crocodile Crack | 0.5589 | 0.4187 | 0.4787 |
| Single Crack | 0.4075 | 0.3099 | 0.3521 |
| Pothole | 0.8375 | 0.7927 | 0.8145 |
| Person | 0.9858 | 0.9761 | 0.9809 |
| Car | 0.9986 | 0.9972 | 0.9979 |
| Macro | 0.7577 | 0.6989 | 0.7248 |

### Subtype metrics

Subtype classification is strong when a valid road-defect ROI is available.

| Evaluation mode | Accuracy | Macro F1 | Samples |
| --- | ---: | ---: | ---: |
| Ground-truth ROIs | 0.9070 | 0.9063 | 2257 |
| Matched predicted ROIs | 0.9360 | 0.9151 | 1265 |

## Known limitations

- Road-defect localization is the current main bottleneck.
- Crocodile Crack and Single Crack are substantially weaker than Pothole, Person, and Car.
- Thin cracks can be missed, especially when image resolution or contrast is insufficient.
- Many crack errors appear as false negatives against the background column in the final confusion matrix.
- The model is validated primarily in the project dataset and simulator context. Real-world UAV deployment requires additional validation.
- The current Unity deployment uses a Python/FastAPI server with a PyTorch checkpoint, not native Unity ONNX inference.

## Recommended next improvements

The next metric-improvement experiments are documented in:

```text
notebooks/multimodal_uav_detector.ipynb
```

The recommended order is:

1. class-specific threshold sweep;
2. higher-resolution training;
3. hard-example crack fine-tuning.

## Responsible use

This model should be used as an inspection-assistance tool, not as a final authority for road-maintenance decisions. Outputs should be reviewed by a qualified operator before being used for real infrastructure prioritization, safety assessment, or public-road intervention planning.
