import os
from datetime import datetime
from contextlib import asynccontextmanager

import cv2
import numpy as np
import torch
import torch.nn as nn
import uvicorn

from fastapi import FastAPI, UploadFile, File
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import JSONResponse
from fastapi.staticfiles import StaticFiles
from ultralytics import YOLO

try:
    from torchvision.ops import roi_align
except Exception:
    roi_align = None

try:
    from ultralytics.nn.tasks import DetectionModel, yaml_model_load
except Exception:
    DetectionModel = None
    yaml_model_load = None


def get_optimal_device():
    if os.getenv("DEVICE"):
        return os.getenv("DEVICE")
    if torch.cuda.is_available():
        return "cuda"
    if hasattr(torch.backends, "mps") and torch.backends.mps.is_available():
        return "mps"
    return "cpu"

DEVICE = get_optimal_device()

IMG_SIZE = 640
YOLO_CONF = 0.40
IOU_THRESHOLD = 0.45
MAX_DET = 300

CAR_CONF = 0.90
PERSON_CONF = 0.90
SUBTYPE_CONF = 0.80

# IMPORTANTE:
# El model.pt tiene roi_size=7 y subtype_head espera features ROI, no crops.
# Si AUTO_FEATURE_LAYER no da buenos resultados, prueba FEATURE_LAYER_INDEX=4 o 15.
FEATURE_LAYER_INDEX = None

BASE_DIR = os.path.dirname(os.path.abspath(__file__))
MODEL_PATH = os.getenv("MODEL_PATH")
if not MODEL_PATH or not os.path.exists(MODEL_PATH):
    MODEL_PATH = os.path.join(BASE_DIR, "models", "model_finetuned.pt")
if not os.path.exists(MODEL_PATH):
    MODEL_PATH = os.path.join(BASE_DIR, "model_finetuned.pt")

DETECTIONS_DIR = os.getenv("DETECTIONS_DIR", os.path.join(BASE_DIR, "detections"))

DEFAULT_DETECTION_NAMES = (
    "Road-defect-general",
    "Person",
    "Car",
)

DEFAULT_SUBTYPE_NAMES = (
    "Pothole",
    "Crocodile Crack",
    "Single Crack",
)


def safe_output_name(filename):
    base = os.path.basename(filename or "imagen.png")
    name, _ = os.path.splitext(base)
    safe = "".join(ch if ch.isalnum() or ch in ("-", "_") else "_" for ch in name)
    ts = datetime.now().strftime("%Y%m%d_%H%M%S_%f")
    return f"{ts}_{safe}_model_pt.jpg"


def color_for_label(label):
    if label == "Person":
        return (255, 0, 0)
    if label == "Car":
        return (0, 0, 255)
    if label == "Pothole":
        return (0, 255, 255)
    if label == "Single Crack":
        return (0, 255, 0)
    if label == "Crocodile Crack":
        return (255, 255, 0)
    if label == "Uncertain":
        return (180, 180, 180)
    return (255, 255, 255)


def draw_detection(img, x1, y1, x2, y2, label, det_conf, cls_conf=None):
    color = color_for_label(label)
    cv2.rectangle(img, (x1, y1), (x2, y2), color, 2)

    text = f"{label} D:{det_conf:.2f}"
    if cls_conf is not None:
        text += f" C:{cls_conf:.2f}"

    cv2.putText(
        img,
        text,
        (x1, max(20, y1 - 10)),
        cv2.FONT_HERSHEY_SIMPLEX,
        0.7,
        color,
        2
    )


# =========================================================
# VARIABLES GLOBALES
# =========================================================
checkpoint = None
detector = None
subtype_head = None
detection_names = DEFAULT_DETECTION_NAMES
subtype_names = DEFAULT_SUBTYPE_NAMES
model_load_mode = "not_loaded"
roi_size = 7


# =========================================================
# SUBTIPO
# =========================================================
class SubtypeHead(nn.Module):
    def __init__(self, in_channels, conv_channels, hidden_units, out_classes):
        super().__init__()

        self.network = nn.Sequential(
            nn.Conv2d(in_channels, conv_channels, kernel_size=3, padding=1),
            nn.BatchNorm2d(conv_channels),
            nn.SiLU(inplace=True),
            nn.AdaptiveAvgPool2d((1, 1)),
            nn.Flatten(),
            nn.Linear(conv_channels, hidden_units),
            nn.ReLU(inplace=True),
            nn.Dropout(0.0),
            nn.Linear(hidden_units, out_classes),
        )

    def forward(self, x):
        return self.network(x)


def build_subtype_head(state):
    conv_w = state.get("network.0.weight")
    linear_w = state.get("network.5.weight")
    out_w = state.get("network.8.weight")

    if conv_w is None or linear_w is None or out_w is None:
        raise RuntimeError("El state_dict del subtype_head no tiene la estructura esperada.")

    in_channels = int(conv_w.shape[1])
    conv_channels = int(conv_w.shape[0])
    hidden_units = int(linear_w.shape[0])
    out_classes = int(out_w.shape[0])

    head = SubtypeHead(
        in_channels,
        conv_channels,
        hidden_units,
        out_classes
    )

    missing, unexpected = head.load_state_dict(state, strict=False)

    if unexpected:
        print(f"[model.pt] subtype_head claves inesperadas: {unexpected}")

    if missing:
        print(f"[model.pt] subtype_head claves faltantes: {missing}")

    return head.to(DEVICE).eval()


def image_to_tensor_full(img_bgr, size=IMG_SIZE):
    """
    Convierte la imagen completa a tensor.
    Se usa la imagen completa porque el subtype_head fue entrenado con ROI features,
    no con crops sueltos redimensionados.
    """
    resized = cv2.resize(img_bgr, (size, size), interpolation=cv2.INTER_LINEAR)
    rgb = cv2.cvtColor(resized, cv2.COLOR_BGR2RGB)
    tensor = torch.from_numpy(rgb).permute(2, 0, 1).unsqueeze(0)
    return tensor.to(DEVICE).float() / 255.0


def forward_yolo_collect_features(tensor, target_channels):
    """
    Ejecuta el YOLO manualmente y recoge feature maps compatibles.
    Antes el código devolvía la primera capa con 64 canales.
    Eso causaba subtipo incorrecto y saturación tipo Single Crack 1.00.

    Ahora:
    - Si FEATURE_LAYER_INDEX está definido, usa esa capa.
    - Si no, elige automáticamente un feature map compatible con el subtype_head.
    """
    if detector is None:
        raise RuntimeError("Detector no cargado.")

    layers = detector.model.model
    y = []
    x = tensor
    candidates = []

    for idx, layer in enumerate(layers):
        if getattr(layer, "f", -1) != -1:
            if isinstance(layer.f, int):
                x_in = y[layer.f]
            else:
                x_in = [x if j == -1 else y[j] for j in layer.f]
        else:
            x_in = x

        x = layer(x_in)
        y.append(x)

        if torch.is_tensor(x) and x.dim() == 4:
            _, c, fh, fw = x.shape

            if FEATURE_LAYER_INDEX is not None and idx == FEATURE_LAYER_INDEX:
                if int(c) != int(target_channels):
                    raise RuntimeError(
                        f"FEATURE_LAYER_INDEX={FEATURE_LAYER_INDEX} tiene {c} canales, "
                        f"pero subtype_head espera {target_channels}."
                    )
                print(f"[subtype] Usando feature layer fija idx={idx}, shape={tuple(x.shape)}")
                return x, idx

            if int(c) == int(target_channels):
                candidates.append((idx, x, int(fh), int(fw)))

    if FEATURE_LAYER_INDEX is not None:
        raise RuntimeError(f"No existe FEATURE_LAYER_INDEX={FEATURE_LAYER_INDEX} en el modelo.")

    if not candidates:
        raise RuntimeError(
            f"No se encontró feature map compatible con {target_channels} canales."
        )

    # Elegimos el candidato con mayor resolución espacial.
    # Si hay empate, usamos el más tardío, porque suele tener features más semánticas.
    candidates.sort(key=lambda item: (item[2] * item[3], item[0]))
    idx, feature, fh, fw = candidates[-1]

    print(
        f"[subtype] Feature layer auto idx={idx}, "
        f"shape={tuple(feature.shape)}, candidates={[(c[0], c[2], c[3]) for c in candidates]}"
    )

    return feature, idx


def classify_damage_rois(img_bgr, boxes_xyxy):
    """
    Clasifica subtipos usando ROI Align sobre feature map.
    boxes_xyxy deben estar en coordenadas de la imagen original.
    """
    if subtype_head is None:
        return [("Road-defect-general", None) for _ in boxes_xyxy]

    if roi_align is None:
        print("[subtype] torchvision.ops.roi_align no está disponible.")
        return [("Road-defect-general", None) for _ in boxes_xyxy]

    if not boxes_xyxy:
        return []

    h, w = img_bgr.shape[:2]

    try:
        first_weight = subtype_head.network[0].weight
        target_channels = int(first_weight.shape[1])

        tensor = image_to_tensor_full(img_bgr)

        with torch.no_grad():
            feature_map, feature_idx = forward_yolo_collect_features(tensor, target_channels)

            _, _, fh, fw = feature_map.shape

            rois = []
            for x1, y1, x2, y2 in boxes_xyxy:
                # Escalamos caja original a la imagen 640x640 usada para el forward manual.
                sx1 = float(x1) * IMG_SIZE / float(w)
                sy1 = float(y1) * IMG_SIZE / float(h)
                sx2 = float(x2) * IMG_SIZE / float(w)
                sy2 = float(y2) * IMG_SIZE / float(h)

                # Evita ROIs degenerados.
                sx1 = max(0.0, min(float(IMG_SIZE - 1), sx1))
                sy1 = max(0.0, min(float(IMG_SIZE - 1), sy1))
                sx2 = max(sx1 + 1.0, min(float(IMG_SIZE), sx2))
                sy2 = max(sy1 + 1.0, min(float(IMG_SIZE), sy2))

                # roi_align espera [batch_index, x1, y1, x2, y2]
                rois.append([0.0, sx1, sy1, sx2, sy2])

            rois_tensor = torch.tensor(rois, dtype=torch.float32, device=DEVICE)

            spatial_scale_x = fw / float(IMG_SIZE)
            spatial_scale_y = fh / float(IMG_SIZE)

            # Normalmente fw == fh y la imagen fue redimensionada cuadrada.
            # Si no fueran iguales, promediamos para evitar romper ejecución.
            spatial_scale = float((spatial_scale_x + spatial_scale_y) / 2.0)

            roi_features = roi_align(
                feature_map,
                rois_tensor,
                output_size=(roi_size, roi_size),
                spatial_scale=spatial_scale,
                aligned=True
            )

            logits = subtype_head(roi_features)
            probs = torch.softmax(logits, dim=1)

            conf_tensor, pred_tensor = torch.max(probs, dim=1)

        outputs = []

        for i in range(len(boxes_xyxy)):
            cls_conf = float(conf_tensor[i].item())
            pred = int(pred_tensor[i].item())

            debug_probs = {
                subtype_names[j] if j < len(subtype_names) else f"Subtype_{j}": round(float(probs[i, j].item()), 4)
                for j in range(probs.shape[1])
            }

            print(
                f"[subtype] box={i} pred={pred} conf={cls_conf:.4f} "
                f"probs={debug_probs}"
            )

            if cls_conf < SUBTYPE_CONF:
                outputs.append(("Uncertain", cls_conf))
            elif pred < len(subtype_names):
                outputs.append((subtype_names[pred], cls_conf))
            else:
                outputs.append((f"Subtype_{pred}", cls_conf))

        return outputs

    except Exception as exc:
        print(f"[model.pt] No se pudo clasificar subtipo con ROI Align: {exc}")
        return [("Road-defect-general", None) for _ in boxes_xyxy]


# =========================================================
# CARGA DE MODELO
# =========================================================
def clean_yolo_state_dict(state_dict):
    clean = {}

    for key, value in state_dict.items():
        if key.startswith("yolo."):
            clean[key[len("yolo."):]] = value

    return clean


def load_yolo_from_checkpoint(ckpt):
    global model_load_mode

    if DetectionModel is None or yaml_model_load is None:
        raise RuntimeError(
            "La version instalada de ultralytics no expone DetectionModel/yaml_model_load."
        )

    yolo_state = clean_yolo_state_dict(ckpt["state_dict"])

    if not yolo_state:
        raise RuntimeError("model.pt no contiene pesos con prefijo 'yolo.'.")

    base_cfg = "yolov8n.yaml"
    cfg = yaml_model_load(base_cfg)
    nn_model = DetectionModel(
        cfg,
        ch=3,
        nc=len(detection_names),
        verbose=False
    ).to(DEVICE)

    current = nn_model.state_dict()
    compatible = {
        key: value
        for key, value in yolo_state.items()
        if key in current and tuple(current[key].shape) == tuple(value.shape)
    }

    skipped = len(yolo_state) - len(compatible)
    missing, unexpected = nn_model.load_state_dict(compatible, strict=False)

    print(f"[model.pt] Compatible YOLO weights loaded: {len(compatible)}")
    print(f"[model.pt] Skipped YOLO weights by shape/name: {skipped}")

    if missing:
        print(f"[model.pt] YOLO missing keys: {len(missing)}")

    if unexpected:
        print(f"[model.pt] YOLO unexpected keys: {unexpected}")

    yolo = YOLO(base_cfg)
    yolo.model = nn_model
    yolo.model.names = {idx: name for idx, name in enumerate(detection_names)}
    yolo.overrides["mode"] = "predict"
    yolo.overrides["save"] = False
    yolo.overrides["verbose"] = False

    model_load_mode = "checkpoint_state_dict"

    return yolo


def load_model():
    global checkpoint
    global detector
    global subtype_head
    global detection_names
    global subtype_names
    global model_load_mode
    global roi_size

    print("\n========================================")
    print("LOADING API model.pt")
    print("========================================")
    print(f"Model path: {MODEL_PATH}")
    print(f"Device: {DEVICE}")

    if not os.path.exists(MODEL_PATH):
        raise RuntimeError(f"model.pt not found at: {MODEL_PATH}")

    os.makedirs(DETECTIONS_DIR, exist_ok=True)

    checkpoint = torch.load(
        MODEL_PATH,
        map_location=DEVICE,
        weights_only=False
    )

    if isinstance(checkpoint, dict):
        detection_names = tuple(checkpoint.get("detection_names", detection_names))
        checkpoint_subtype_names = tuple(checkpoint.get("subtype_names", ()))
        subtype_names = DEFAULT_SUBTYPE_NAMES
        roi_size = int(checkpoint.get("roi_size", roi_size))

        if checkpoint_subtype_names and checkpoint_subtype_names != subtype_names:
            print(
                f"Checkpoint subtypes ignored: {checkpoint_subtype_names}. "
                f"Using corrected order: {subtype_names}"
            )

        print(f"Detector classes: {detection_names}")
        print(f"Subtype classes: {subtype_names}")
        print(f"ROI size: {roi_size}")

        subtype_state = {
            key[len("subtype_head."):]: value
            for key, value in checkpoint.get("state_dict", {}).items()
            if key.startswith("subtype_head.")
        }

        if subtype_state:
            subtype_head = build_subtype_head(subtype_state)
            print("subtype_head loaded from model.pt")

        detector = load_yolo_from_checkpoint(checkpoint)
    else:
        detector = YOLO(MODEL_PATH)
        model_load_mode = "ultralytics_direct"

    print(f"Loading mode: {model_load_mode}")
    print("API model.pt ready.")
    print("========================================\n")


# =========================================================
# FASTAPI
# =========================================================
@asynccontextmanager
async def lifespan(app: FastAPI):
    load_model()
    yield


app = FastAPI(
    title="AI Pothole & Road Defect Detector",
    description="Inference API using PyTorch model.pt checkpoint",
    version="2.0",
    lifespan=lifespan
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

os.makedirs(DETECTIONS_DIR, exist_ok=True)
app.mount("/detections", StaticFiles(directory=DETECTIONS_DIR), name="detections")


# =========================================================
# PREDICT
# =========================================================
@app.post("/predict")
@app.post("/api/predict")
@torch.inference_mode()
def predict_image(file: UploadFile = File(...)):
    now_str = datetime.now().strftime("%Y-%m-%d %H:%M:%S.%f")[:-3]
    print(f"\n==================================================")
    print(f"[REQ /predict] {now_str} | File: '{file.filename}'")
    try:
        contents = file.file.read()
        nparr = np.frombuffer(contents, np.uint8)
        img = cv2.imdecode(nparr, cv2.IMREAD_COLOR)

        if img is None:
            print(f"[REQ /predict] ❌ ERROR: Invalid or empty image (bytes: {len(contents)})")
            print(f"==================================================\n")
            return JSONResponse(
                status_code=400,
                content={"error": "Invalid image format"}
            )

        h, w = img.shape[:2]
        print(f"[REQ /predict] 🖼️  Dimensions: {w}x{h} px | Size: {len(contents)} bytes")

        results = detector.predict(
            img,
            imgsz=IMG_SIZE,
            conf=YOLO_CONF,
            iou=IOU_THRESHOLD,
            max_det=MAX_DET,
            agnostic_nms=False,
            verbose=False,
            save=False
        )

        raw_boxes = []

        for box in results[0].boxes:
            cls_id = int(box.cls.item())
            det_conf = float(box.conf.item())
            x1, y1, x2, y2 = map(int, box.xyxy[0].tolist())

            class_name = detector.names.get(cls_id, f"Clase_{cls_id}")
            class_lower = class_name.lower()

            is_damage = (
                "road-defect" in class_lower
                or "damage" in class_lower
                or "crack" in class_lower
                or "pothole" in class_lower
            )
            is_person = "person" in class_lower
            is_car = "car" in class_lower

            if is_car and det_conf < CAR_CONF:
                continue

            if is_person and det_conf < PERSON_CONF:
                continue

            raw_boxes.append({
                "class_name": class_name,
                "is_damage": is_damage,
                "is_person": is_person,
                "is_car": is_car,
                "det_conf": det_conf,
                "box": [x1, y1, x2, y2],
            })

        damage_boxes = [item["box"] for item in raw_boxes if item["is_damage"]]
        damage_subtypes = classify_damage_rois(img, damage_boxes)

        damage_idx = 0
        detections = []
        draw_img = img.copy()

        for item in raw_boxes:
            x1, y1, x2, y2 = item["box"]
            det_conf = item["det_conf"]
            cls_conf = None
            label = item["class_name"]

            if item["is_damage"]:
                label, cls_conf = damage_subtypes[damage_idx]
                damage_idx += 1
            elif item["is_person"]:
                label = "Person"
            elif item["is_car"]:
                label = "Car"

            detections.append({
                "class": label,
                "clase": label,
                "det_conf": round(det_conf, 3),
                "cls_conf": round(cls_conf, 3) if cls_conf is not None else None,
                "box": [x1, y1, x2, y2],
                "caja": [x1, y1, x2, y2]
            })

            draw_detection(draw_img, x1, y1, x2, y2, label, det_conf, cls_conf)

        output_path = os.path.join(DETECTIONS_DIR, safe_output_name(file.filename))
        cv2.imwrite(output_path, draw_img)

        print(
            f"[REQ /predict] 🎯 Inference completed: raw={len(results[0].boxes)} | "
            f"filtered={len(detections)}"
        )
        print(f"[REQ /predict] 💾 ANNOTATED CAPTURE SAVED TO DISK -> {output_path}")
        print(f"==================================================\n")

        return JSONResponse(content=detections)

    except Exception as exc:
        print(f"[REQ /predict] ❌ INFERENCE ERROR: {exc}\n")
        print(f"==================================================\n")

        return JSONResponse(
            status_code=500,
            content={"error": str(exc)}
        )


@app.get("/health")
@app.get("/api/health")
def health():
    return JSONResponse(
        content={
            "status": "ok",
            "ready": detector is not None,
            "modelo": os.path.basename(MODEL_PATH),
            "modo_carga": model_load_mode,
            "device": DEVICE
        }
    )


@app.get("/")
def root():
    return JSONResponse(
        content={
            "status": "AI model.pt inference server active",
            "model": os.path.basename(MODEL_PATH),
            "load_mode": model_load_mode,
            "device": DEVICE,
            "detector_classes": list(detection_names),
            "subtype_classes": list(subtype_names),
            "roi_size": roi_size,
            "feature_layer_index": FEATURE_LAYER_INDEX
        }
    )


# =========================================================
# MAIN
# =========================================================
if __name__ == "__main__":
    uvicorn.run(
        app,
        host="0.0.0.0",
        port=5000
    )
