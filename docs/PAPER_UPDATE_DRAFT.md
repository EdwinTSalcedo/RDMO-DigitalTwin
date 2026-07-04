# Paper Update Draft: Shared-Backbone Multi-Task Perception System

This document contains paper-ready replacement text for the sections that currently describe the older two-stage detector plus crop-classifier pipeline.

The current implementation and deployment use a shared-backbone multi-task YOLO model served through a Python FastAPI endpoint and consumed by the Unity simulator.

## Key change to reflect in the paper

The current paper text describes:

```text
YOLO detector
→ crop detected road defects from RGB image
→ run crop through second classifier/backbone
```

The implemented system now uses:

```text
Image
→ shared YOLO backbone/neck once
→ detection head: Road-defect-general / Person / Car
→ ROIAlign from shared feature map
→ subtype head: Crocodile Crack / Single Crack / Pothole
```

This distinction is important. The new system is not simply YOLO trained with five classes. It is a multi-task detector that separates coarse localization from fine-grained road-defect subtype classification while sharing the image encoding.

## Suggested revised abstract paragraph

Replace the current perception-model description in the abstract with:

> The perception module uses a shared-backbone multi-task YOLO architecture. A single YOLO backbone and neck encode each UAV image once, after which a detection head localizes road-defect regions, pedestrians, and vehicles using three coarse classes: Road-defect-general, Person, and Car. For detected road-defect regions, ROI-aligned features are extracted from the same shared feature maps and passed to a subtype head that distinguishes Crocodile Crack, Single Crack, and Pothole. This avoids the redundant crop-and-reclassify stage used in conventional two-stage pipelines while preserving fine-grained road-defect labels. On the simulator test set, the detector achieved 0.8525 mAP@0.50 for the three-class detection task and 0.6980 mAP@0.50 for the final five-class output. The subtype head achieved 0.9151 macro-F1 on matched predicted road-defect ROIs, indicating that fine-grained classification is reliable once valid defect regions are localized.

If space is limited, use this shorter version:

> The perception module uses a shared-backbone multi-task YOLO architecture that encodes each image once, detects Road-defect-general, Person, and Car, and applies ROIAlign-based subtype classification to road-defect features to distinguish Crocodile Crack, Single Crack, and Pothole. The final deployed model achieved 0.8525 mAP@0.50 for three-class detection and 0.6980 mAP@0.50 for final five-class predictions on the simulator test set.

## Replacement text for Section IV: Road Damage Detection

### IV. Road Damage Detection

Road-damage detection provides the perception layer of the proposed digital-twin framework. During UAV inspection, the perception module processes RGB frames from the simulated camera and identifies both road-surface defects and traffic agents that may affect inspection quality. Road-defect detections support automatic inspection and dataset generation, while pedestrian and vehicle detections help represent dynamic occlusions caused by road users. The perception module therefore links the visual simulation environment with the traffic-aware decision layer of the digital twin.

The implemented perception system follows a shared-backbone multi-task design. Rather than training a single five-class detector or applying a detector followed by an independent crop classifier, the model separates coarse localization from fine-grained road-defect subtype classification. A YOLO backbone and neck first encode the full image once and produce shared feature maps. A detection head then predicts bounding boxes and three coarse detection classes: Road-defect-general, Person, and Car. For regions predicted or annotated as road defects, ROIAlign pools fixed-size features from the shared feature map. These pooled features are passed to a lightweight subtype head that predicts Crocodile Crack, Single Crack, or Pothole.

This design preserves the original five-class road-monitoring output while avoiding redundant image encoding. In the previous two-stage crop-classifier approach, each road-defect detection required cropping the RGB image and passing the crop through a second model or backbone. In contrast, the proposed model performs fine-grained classification using features already computed by the shared YOLO backbone. The full image is therefore encoded once per inference request, and both the detection head and subtype head consume features derived from that same forward pass.

### A. Label Mapping and Multi-Task Targets

The dataset preserves the original fine-grained labels: Crocodile Crack, Single Crack, Pothole, Person, and Car. For the detection head, the three road-defect classes are merged into a single Road-defect-general class, while Person and Car remain independent detection classes. For the subtype head, only road-defect boxes contribute to the subtype classification loss. Person and Car boxes are ignored by the subtype branch.

The mapping is:

| Original label | Detection-head target | Subtype-head target |
| --- | --- | --- |
| Crocodile Crack | Road-defect-general | Crocodile Crack |
| Single Crack | Road-defect-general | Single Crack |
| Pothole | Road-defect-general | Pothole |
| Person | Person | ignored |
| Car | Car | ignored |

The final reported labels are Crocodile Crack, Single Crack, Pothole, Person, and Car. For road-defect predictions, the final label is obtained by combining the Road-defect-general detection with the subtype prediction. For Person and Car, the detection-head class is used directly.

### B. Shared-Backbone Multi-Task Architecture

The architecture consists of a YOLO detection model augmented with an ROI-based subtype classification branch. During the forward pass, the feature pyramid tensors entering the YOLO detection head are captured and reused by the subtype branch. The detection head predicts coarse boxes and classes, while the subtype branch applies ROIAlign to road-defect boxes on the shared feature map and classifies the resulting fixed-size feature tensors.

During training, ground-truth road-defect boxes are used for ROI feature extraction, allowing the subtype head to learn from correctly localized defect regions. During inference, predicted boxes classified as Road-defect-general are used as ROIAlign inputs. The pooled ROI features are passed through a small neural classifier to predict one of three road-defect subtypes.

The multi-task loss is:

```text
L_total = L_detection + λ_subtype L_subtype
```

where `L_detection` is the standard YOLO detection loss, `L_subtype` is a cross-entropy loss over road-defect subtype labels, and `λ_subtype` controls the contribution of the subtype branch. In the reported experiments, `λ_subtype` was set to 1.0.

The implementation includes a runtime assertion that verifies that the YOLO backbone is executed once per image batch. This check ensures that the subtype branch does not re-encode cropped RGB regions and that the detection and subtype outputs are derived from the same shared feature maps.

### C. Deployment in the Unity Digital Twin

For deployment, the Unity simulator sends captured frames to a local Python inference service implemented with FastAPI. The server loads the trained shared-backbone multi-task checkpoint and returns detections as JSON objects containing the final class label, confidence score, and bounding box coordinates. The active deployment uses the PyTorch checkpoint rather than native Unity ONNX inference. This server-based design allows the simulator to use the full multi-task architecture, including ROIAlign and custom postprocessing, while keeping the Unity-side integration simple.

The deployment path is:

```text
Unity simulator
→ HTTP image upload
→ FastAPI inference server
→ shared-backbone multi-task YOLO
→ JSON detections
→ Unity simulator
```

## Suggested architecture figure caption

> Fig. X. Shared-backbone multi-task YOLO architecture used by the proposed digital twin. The input image is encoded once by a YOLO backbone and neck. The detection head predicts Road-defect-general, Person, and Car. Road-defect boxes are then used to apply ROIAlign to the shared feature map, and a subtype head predicts Crocodile Crack, Single Crack, or Pothole. Unlike a crop-based two-stage classifier, the subtype branch does not reprocess RGB crops through a second backbone.

## Replacement text for perception results

### B. Perception Model Evaluation

The shared-backbone multi-task model was evaluated on the simulator test split using IoU-based matching. Metrics were computed in three complementary spaces. First, the detection head was evaluated over the three coarse detection classes: Road-defect-general, Person, and Car. Second, the final output was evaluated over the five classes used by the simulator: Crocodile Crack, Single Crack, Pothole, Person, and Car. Third, the subtype head was evaluated separately on road-defect ROIs to determine whether fine-grained classification remained reliable once a valid road-defect region was available.

Table X reports the three-class detection-head results. The detector achieved 0.8525 mAP@0.50 and 0.7498 mAP@0.50:0.95. Detection performance was very high for Person and Car, with F1 scores of 0.9809 and 0.9979, respectively. Road-defect-general was more challenging, reaching 0.5984 F1. This indicates that traffic-agent detection is robust in the simulator, while road-defect localization remains the main limitation of the current perception module.

| Class | Precision | Recall | F1 |
| --- | ---: | ---: | ---: |
| Road-defect-general | 0.6418 | 0.5605 | 0.5984 |
| Person | 0.9858 | 0.9761 | 0.9809 |
| Car | 0.9986 | 0.9972 | 0.9979 |
| Macro | 0.8754 | 0.8446 | 0.8591 |

The final five-class evaluation is shown in Table Y. The model achieved 0.6980 mAP@0.50 and 0.5548 mAP@0.50:0.95. Person and Car remained highly accurate in the final output. Among road defects, Pothole achieved the strongest performance, with an F1 score of 0.8145. Crocodile Crack and Single Crack were more difficult, reaching F1 scores of 0.4787 and 0.3521, respectively. These results suggest that the model can reliably detect traffic agents and potholes, but thin and visually ambiguous crack patterns require further improvement.

| Class | Precision | Recall | F1 |
| --- | ---: | ---: | ---: |
| Crocodile Crack | 0.5589 | 0.4187 | 0.4787 |
| Single Crack | 0.4075 | 0.3099 | 0.3521 |
| Pothole | 0.8375 | 0.7927 | 0.8145 |
| Person | 0.9858 | 0.9761 | 0.9809 |
| Car | 0.9986 | 0.9972 | 0.9979 |
| Macro | 0.7577 | 0.6989 | 0.7248 |

To isolate the performance of the subtype branch, the subtype head was also evaluated on road-defect ROIs. When ground-truth road-defect boxes were used as ROIAlign inputs, the subtype head achieved 0.9070 accuracy and 0.9063 macro-F1 over 2257 samples. When matched predicted road-defect boxes were used, the subtype head achieved 0.9360 accuracy and 0.9151 macro-F1 over 1265 samples. This indicates that fine-grained subtype classification is reliable when an appropriate road-defect ROI is available. The lower final five-class performance is therefore mainly explained by missed or unmatched road-defect detections, particularly for Crocodile Crack and Single Crack, rather than by subtype classification alone.

| ROI source | Accuracy | Macro F1 | Samples |
| --- | ---: | ---: | ---: |
| Ground-truth road-defect ROIs | 0.9070 | 0.9063 | 2257 |
| Matched predicted road-defect ROIs | 0.9360 | 0.9151 | 1265 |

Overall, the shared-backbone design provides a deployable perception module for the digital twin. It avoids the repeated backbone executions required by crop-based two-stage classifiers while preserving final five-class outputs. The current results also identify a clear direction for future improvement: increasing road-defect recall, especially for thin cracks and crocodile-crack patterns.

## Suggested LaTeX table snippets

### Detection-head table

```latex
\begin{table}[t]
\centering
\caption{Three-class detection-head performance on the simulator test set.}
\begin{tabular}{lccc}
\hline
Class & Precision & Recall & F1 \\
\hline
Road-defect-general & 0.6418 & 0.5605 & 0.5984 \\
Person & 0.9858 & 0.9761 & 0.9809 \\
Car & 0.9986 & 0.9972 & 0.9979 \\
Macro & 0.8754 & 0.8446 & 0.8591 \\
\hline
\end{tabular}
\label{tab:detection_head_results}
\end{table}
```

### Final five-class table

```latex
\begin{table}[t]
\centering
\caption{Final five-class performance on the simulator test set.}
\begin{tabular}{lccc}
\hline
Class & Precision & Recall & F1 \\
\hline
Crocodile Crack & 0.5589 & 0.4187 & 0.4787 \\
Single Crack & 0.4075 & 0.3099 & 0.3521 \\
Pothole & 0.8375 & 0.7927 & 0.8145 \\
Person & 0.9858 & 0.9761 & 0.9809 \\
Car & 0.9986 & 0.9972 & 0.9979 \\
Macro & 0.7577 & 0.6989 & 0.7248 \\
\hline
\end{tabular}
\label{tab:final_five_class_results}
\end{table}
```

### Summary metric table

```latex
\begin{table}[t]
\centering
\caption{Summary metrics for the shared-backbone multi-task perception model.}
\begin{tabular}{lc}
\hline
Metric & Value \\
\hline
Detection mAP@0.50 & 0.8525 \\
Detection mAP@0.50:0.95 & 0.7498 \\
Final five-class mAP@0.50 & 0.6980 \\
Final five-class mAP@0.50:0.95 & 0.5548 \\
Subtype macro-F1 on ground-truth ROIs & 0.9063 \\
Subtype macro-F1 on matched predicted ROIs & 0.9151 \\
\hline
\end{tabular}
\label{tab:multitask_summary_metrics}
\end{table}
```

## Suggested conclusion update

Replace the current perception conclusion with:

> The perception module was upgraded from a crop-based two-stage pipeline to a shared-backbone multi-task YOLO architecture. This model encodes each image once, performs coarse detection of road defects and traffic agents, and refines road-defect detections through an ROIAlign-based subtype head. The deployed system achieved strong traffic-agent detection and reliable subtype classification when road-defect ROIs were available. The main remaining limitation is the localization of thin and visually ambiguous crack patterns, which reduced final five-class performance for Crocodile Crack and Single Crack. Future work will therefore focus on higher-resolution training, threshold calibration, and hard-example fine-tuning for crack-like defects.

## Claims to avoid or qualify

Avoid presenting the previous `99.26% accuracy` as the main result for the current deployed model unless it is explicitly described as an earlier baseline or a different evaluation protocol.

The current paper should not claim that the deployed multi-task model runs natively in Unity through ONNX. The working deployment uses:

```text
Unity → FastAPI → PyTorch checkpoint
```

The paper can mention ONNX as a future deployment optimization, but not as the current path for the shared-backbone multi-task model.
