import os
import cv2
import torch
import uvicorn
import numpy as np
import torch.nn as nn

from datetime import datetime
from fastapi import FastAPI, UploadFile, File
from fastapi.responses import JSONResponse
from contextlib import asynccontextmanager
from torchvision import transforms
from ultralytics import YOLO

# =========================================================
# CONFIG
# =========================================================
DEVICE = "cuda" if torch.cuda.is_available() else "cpu"

IMG_SIZE = 640
CROP_SIZE = 320
CROP_PADDING = 0

# =========================================================
# THRESHOLDS
# =========================================================
YOLO_CONF = 0.40

CAR_CONF = 0.90
PERSON_CONF = 0.90

CLASSIFIER_CONF = 0.80

# =========================================================
# CLASES
# =========================================================
# IMPORTANTE:
# MISMO ORDEN DEL ENTRENAMIENTO
# =========================================================
CRACK_NAMES = [
    "Pothole",
    "Crocodile Crack",
    "Single Crack"
]

BASE_DIR = os.path.dirname(
    os.path.abspath(__file__)
)

DETECTIONS_DIR = os.path.join(
    BASE_DIR,
    "Detecciones IA"
)

DEBUG_CROPS_DIR = os.path.join(
    BASE_DIR,
    "debug_crops"
)

# =========================================================
# TRANSFORMACIONES
# =========================================================
val_transform = transforms.Compose([

    transforms.ToPILImage(),

    transforms.Resize(
        (CROP_SIZE, CROP_SIZE)
    ),

    transforms.ToTensor(),

    transforms.Normalize(
        [0.485, 0.456, 0.406],
        [0.229, 0.224, 0.225]
    )
])

# =========================================================
# CLASIFICADOR EXPORTABLE
# =========================================================
class ExportableClassifier(nn.Module):

    def __init__(
        self,
        backbone,
        pool,
        cls_head
    ):

        super().__init__()

        self.backbone = backbone
        self.pool = pool
        self.cls_head = cls_head

    def forward(self, x):

        feat = x

        for layer in self.backbone:
            feat = layer(feat)

        feat = self.pool(feat)

        out = self.cls_head(feat)

        return out

# =========================================================
# VARIABLES GLOBALES
# =========================================================
model = None
detector = None

# =========================================================
# CARGAR MODELOS
# =========================================================
def load_model():

    global model
    global detector

    classifier_path = os.path.join(
        BASE_DIR,
        "best_multitask_model7.pt"
    )

    det_path = os.path.join(
        BASE_DIR,
        "best7.pt"
    )

    print("\n========================================")
    print("CARGANDO MODELOS")
    print("========================================")

    if not os.path.exists(classifier_path):

        raise RuntimeError(
            f"No se encontro: {classifier_path}"
        )

    if not os.path.exists(det_path):

        raise RuntimeError(
            f"No se encontro: {det_path}"
        )

    os.makedirs(
        DETECTIONS_DIR,
        exist_ok=True
    )

    os.makedirs(
        DEBUG_CROPS_DIR,
        exist_ok=True
    )

    # =====================================================
    # DETECTOR
    # =====================================================
    detector = YOLO(det_path)

    yolo_nn = detector.model.to(DEVICE)

    for p in yolo_nn.parameters():
        p.requires_grad = False

    yolo_nn.eval()

    # =====================================================
    # MISMO BACKBONE QUE inference.py
    # =====================================================
    backbone = yolo_nn.model[:10]

    # =====================================================
    # POOL
    # =====================================================
    pool = nn.AdaptiveAvgPool2d((1, 1))

    # =====================================================
    # HEAD
    # =====================================================
    cls_head = nn.Sequential(

        nn.Flatten(),

        nn.Linear(256, 256),
        nn.ReLU(),
        nn.Dropout(0.4),

        nn.Linear(256, 128),
        nn.ReLU(),
        nn.Dropout(0.3),

        nn.Linear(128, 3)
    )

    # =====================================================
    # CARGAR CHECKPOINT
    # =====================================================
    ckpt = torch.load(
        classifier_path,
        map_location=DEVICE,
        weights_only=True
    )

    cls_head.load_state_dict(
        ckpt["cls_head"]
    )

    pool.load_state_dict(
        ckpt["pool"]
    )

    cls_head.eval()
    pool.eval()

    # =====================================================
    # MODELO FINAL
    # =====================================================
    model = ExportableClassifier(
        backbone,
        pool,
        cls_head
    ).to(DEVICE)

    model.eval()

    detector.overrides["mode"] = "predict"
    detector.overrides["save"] = False
    detector.overrides["verbose"] = False

    print("\n========================================")
    print("CLASES CLASIFICADOR")
    print("========================================")

    for i, name in enumerate(CRACK_NAMES):

        print(f"{i} -> {name}")

    print("========================================")

    print("\nMODELOS CARGADOS.\n")

# =========================================================
# FASTAPI
# =========================================================
@asynccontextmanager
async def lifespan(app: FastAPI):

    load_model()

    yield

app = FastAPI(
    lifespan=lifespan
)

# =========================================================
# DIBUJAR DETECCIONES
# =========================================================
def draw_detection(
    img,
    x1,
    y1,
    x2,
    y2,
    label,
    det_conf,
    cls_conf=None
):

    if label == "Person":

        color = (255, 0, 0)

    elif label == "Car":

        color = (0, 0, 255)

    elif label == "Pothole":

        color = (0, 255, 255)

    elif label == "Single Crack":

        color = (0, 255, 0)

    elif label == "Crocodile Crack":

        color = (255, 255, 0)

    elif label == "Uncertain":

        color = (180, 180, 180)

    else:

        color = (255, 255, 255)

    cv2.rectangle(
        img,
        (x1, y1),
        (x2, y2),
        color,
        2
    )

    text = (
        f"{label} "
        f"D:{det_conf:.2f}"
    )

    if cls_conf is not None:

        text += (
            f" C:{cls_conf:.2f}"
        )

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
# PREDICT
# =========================================================
@app.post("/predict")
def predict_image(
    file: UploadFile = File(...)
):

    try:

        contents = file.file.read()

        nparr = np.frombuffer(
            contents,
            np.uint8
        )

        img = cv2.imdecode(
            nparr,
            cv2.IMREAD_COLOR
        )

        if img is None:

            return JSONResponse(

                status_code=400,

                content={
                    "error": "Imagen invalida"
                }
            )

        h, w = img.shape[:2]

        # =================================================
        # YOLO DETECTION
        # =================================================
        results = detector.predict(

            img,

            imgsz=IMG_SIZE,

            conf=YOLO_CONF,

            iou=0.45,

            max_det=300,

            agnostic_nms=False,

            verbose=False,

            save=False
        )

        detections = []

        draw_img = img.copy()

        raw_count = len(
            results[0].boxes
        )

        kept_count = 0

        # =================================================
        # RECORRER DETECCIONES
        # =================================================
        for box in results[0].boxes:

            cls_id = int(
                box.cls.item()
            )

            det_conf = float(
                box.conf.item()
            )

            x1, y1, x2, y2 = map(
                int,
                box.xyxy[0].tolist()
            )

            class_name = detector.names[
                cls_id
            ].lower()

            # =============================================
            # IDENTIFICAR CLASES
            # =============================================
            is_damage = (
                "damage" in class_name
                or "crack" in class_name
                or "pothole" in class_name
            )

            is_person = (
                "person" in class_name
            )

            is_car = (
                "car" in class_name
            )

            # =============================================
            # FILTROS
            # =============================================
            if is_car and det_conf < CAR_CONF:
                continue

            if is_person and det_conf < PERSON_CONF:
                continue

            cls_conf = None

            # =============================================
            # CLASIFICADOR DE DAÑOS
            # =============================================
            if is_damage:

                crop = img[
                    max(0, y1):min(h, y2),
                    max(0, x1):min(w, x2)
                ]

                # =========================================
                # DEBUG CROPS - COMENTADO PARA NO GUARDAR
                # =========================================
                # debug_name = (
                #     f"crop_"
                #     f"{datetime.now().strftime('%H%M%S_%f')}"
                #     f".jpg"
                # )

                # cv2.imwrite(
                #     os.path.join(
                #         DEBUG_CROPS_DIR,
                #         debug_name
                #     ),
                #     crop
                # )

                if crop.size == 0:

                    label = "Pothole"

                else:

                    crop_rgb = cv2.cvtColor(
                        crop,
                        cv2.COLOR_BGR2RGB
                    )

                    tensor = val_transform(
                        crop_rgb
                    ).unsqueeze(0).to(DEVICE)

                    # =====================================
                    # CLASIFICADOR
                    # =====================================
                    with torch.no_grad():

                        outputs = model(
                            tensor
                        )

                        probs = torch.softmax(
                            outputs,
                            dim=1
                        )

                        probs_np = probs.cpu().numpy()[0]

                        print("\n========================")
                        print("DEBUG CLASIFICADOR")
                        print("========================")

                        for i, p in enumerate(probs_np):

                            print(
                                f"{CRACK_NAMES[i]}: "
                                f"{p:.4f}"
                            )

                        conf_cls_tensor, pred_tensor = torch.max(
                            probs,
                            dim=1
                        )

                        cls_conf = conf_cls_tensor.item()

                        pred = pred_tensor.item()

                        print(f"PRED: {pred}")

                        print(
                            f"LABEL: "
                            f"{CRACK_NAMES[pred]}"
                        )

                        print(
                            f"CONF: "
                            f"{cls_conf:.4f}"
                        )

                    # =====================================
                    # THRESHOLD
                    # =====================================
                    if cls_conf >= CLASSIFIER_CONF:

                        label = CRACK_NAMES[
                            pred
                        ]

                    else:

                        label = "Uncertain"

            elif is_person:

                label = "Person"

            elif is_car:

                label = "Car"

            else:

                label = class_name

            # =============================================
            # GUARDAR DETECCION
            # =============================================
            detections.append({

                "clase": label,

                "det_conf": round(
                    det_conf,
                    3
                ),

                "cls_conf": (
                    round(cls_conf, 3)
                    if cls_conf is not None
                    else None
                ),

                "caja": [
                    x1,
                    y1,
                    x2,
                    y2
                ]
            })

            kept_count += 1

            # =============================================
            # DIBUJAR
            # =============================================
            draw_detection(

                draw_img,

                x1,
                y1,
                x2,
                y2,

                label,

                det_conf,

                cls_conf
            )

        # =================================================
        # GUARDAR RESULTADO - COMENTADO PARA NO GUARDAR
        # =================================================
        # ts = datetime.now().strftime(
        #     "%Y%m%d_%H%M%S_%f"
        # )

        # output_name = (
        #     f"resultado_api_{ts}.jpg"
        # )

        # output_path = os.path.join(
        #     DETECTIONS_DIR,
        #     output_name
        # )

        # cv2.imwrite(
        #     output_path,
        #     draw_img
        # )

        dropped_count = max(
            0,
            raw_count - kept_count
        )

        print(
            f"\n[predict] "
            f"raw={raw_count} "
            f"kept={kept_count} "
            f"dropped={dropped_count}"
        )

        return JSONResponse(
            content=detections
        )

    except Exception as e:

        print(f"\nERROR:\n{e}\n")

        return JSONResponse(

            status_code=500,

            content={
                "error": str(e)
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