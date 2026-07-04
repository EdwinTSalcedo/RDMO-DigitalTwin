# inference_pipeline.py
# =========================================================
# Uso:
# python inference_pipeline.py modelo_unico.pt best_det_extraido.pt imagen.jpg
# =========================================================

import cv2
import sys
import torch
import torch.nn as nn

from torchvision import transforms
from ultralytics import YOLO


# =========================================================
# CONFIG
# =========================================================

DEVICE = "cuda" if torch.cuda.is_available() else "cpu"

# Tamaño YOLO
IMG_SIZE = 640

# Tamaño clasificador
CROP_SIZE = 224

# Padding extra alrededor del objeto
CROP_PADDING = 40

CRACK_NAMES = [
    "Crocodile Crack",
    "Single Crack",
    "Pothole"
]


# =========================================================
# CLASIFICADOR
# =========================================================

class ClassificationHead(nn.Module):

    def __init__(self, in_channels, num_classes=3):

        super().__init__()

        self.pool = nn.AdaptiveAvgPool2d((1,1))

        self.net = nn.Sequential(

            nn.Flatten(),

            nn.Linear(in_channels, 256),
            nn.ReLU(),
            nn.Dropout(0.4),

            nn.Linear(256, 128),
            nn.ReLU(),
            nn.Dropout(0.3),

            nn.Linear(128, num_classes)
        )

    def forward(self, x):

        x = self.pool(x)

        return self.net(x)


# =========================================================
# MODELO MULTITAREA
# =========================================================

class YOLOv8MultiTask(nn.Module):

    def __init__(self, yolo_weights="yolov8n.pt", num_cls_classes=3):

        super().__init__()

        # =================================================
        # CARGAR YOLO
        # =================================================

        yolo = YOLO(yolo_weights)

        self.yolo_model = yolo.model

        layers = list(self.yolo_model.model)

        self.backbone_neck = nn.ModuleList(layers[:-1])

        self.detect_head = layers[-1]

        self._feat = None

        # =================================================
        # HOOK FEATURES
        # =================================================

        self.backbone_neck[-1].register_forward_hook(
            self._hook
        )

        # =================================================
        # DETECTAR DIMENSIÓN FEATURES
        # =================================================

        with torch.no_grad():

            dummy = torch.zeros(
                1,
                3,
                IMG_SIZE,
                IMG_SIZE
            )

            self._run_backbone(dummy)

            emb_dim = self._feat.shape[1]

        # =================================================
        # CABEZA CLASIFICADORA
        # =================================================

        self.cls_head = ClassificationHead(
            emb_dim,
            num_cls_classes
        )

        # =================================================
        # TRANSFORMACIONES
        # =================================================

        self._transform = transforms.Compose([

            transforms.ToPILImage(),

            transforms.Resize(
                (IMG_SIZE, IMG_SIZE)
            ),

            transforms.ToTensor(),

            transforms.Normalize(

                [0.485, 0.456, 0.406],

                [0.229, 0.224, 0.225]
            )
        ])

    # =====================================================
    # HOOK
    # =====================================================

    def _hook(self, module, input, output):

        self._feat = output

    # =====================================================
    # BACKBONE
    # =====================================================

    def _run_backbone(self, x):

        y = []

        for layer in self.backbone_neck:

            if layer.f != -1:

                if isinstance(layer.f, int):

                    x_in = y[layer.f]

                else:

                    x_in = [
                        x if j == -1 else y[j]
                        for j in layer.f
                    ]

            else:

                x_in = x

            x = layer(x_in)

            y.append(x)

        return x, y

    # =====================================================
    # CLASIFICACIÓN
    # =====================================================

    def forward_cls(self, x):

        self._run_backbone(x)

        return self.cls_head(self._feat)

    # =====================================================
    # INFERENCIA
    # =====================================================

    @torch.no_grad()
    def predict_unified(

        self,

        img_bgr,

        conf_thresh=0.10,

        iou_thresh=0.15,

        show=True
    ):

        self.eval()

        h, w = img_bgr.shape[:2]

        # =================================================
        # DETECCIÓN YOLO
        # =================================================

        results = object.__getattribute__(
            self,
            "_det_yolo"
        ).predict(

            img_bgr,

            imgsz=IMG_SIZE,

            conf=conf_thresh,

            iou=iou_thresh,

            max_det=300,

            agnostic_nms=True,

            verbose=False
        )

        detections = []

        draw_img = img_bgr.copy()

        # =================================================
        # RECORRER DETECCIONES
        # =================================================

        for box in results[0].boxes:

            cls_id = int(box.cls.item())

            conf_score = float(box.conf.item())

            x1, y1, x2, y2 = map(
                int,
                box.xyxy[0].tolist()
            )

            # =================================================
            # NOMBRE REAL DE CLASE
            # =================================================

            class_name = self._det_yolo.names[cls_id].lower()

            print(
                f"cls_id={cls_id} | "
                f"class={class_name} | "
                f"conf={conf_score:.2f}"
            )

            # =================================================
            # DAMAGE / CRACK / POTHOLE
            # =================================================

            if (

                "damage" in class_name

                or "crack" in class_name

                or "pothole" in class_name
            ):

                # =============================================
                # PADDING
                # =============================================

                xx1 = max(0, x1 - CROP_PADDING)
                yy1 = max(0, y1 - CROP_PADDING)

                xx2 = min(w, x2 + CROP_PADDING)
                yy2 = min(h, y2 + CROP_PADDING)

                crop = img_bgr[yy1:yy2, xx1:xx2]

                # =============================================
                # VALIDACIÓN
                # =============================================

                if crop.size == 0:

                    label = "Pothole"

                else:

                    # =========================================
                    # RGB
                    # =========================================

                    crop_rgb = cv2.cvtColor(
                        crop,
                        cv2.COLOR_BGR2RGB
                    )

                    # =========================================
                    # TRANSFORM
                    # =========================================

                    t = self._transform(crop_rgb)

                    t = t.unsqueeze(0).to(
                        next(self.parameters()).device
                    )

                    # =========================================
                    # BACKBONE
                    # =========================================

                    self._run_backbone(t)

                    # =========================================
                    # CLASIFICACIÓN
                    # =========================================

                    pred = self.cls_head(
                        self._feat
                    ).argmax(1).item()

                    label = CRACK_NAMES[pred]

            # =================================================
            # PERSON
            # =================================================

            elif "person" in class_name:

                label = "Person"

            # =================================================
            # CAR
            # =================================================

            elif "car" in class_name:

                label = "Car"

            else:

                label = class_name

            # =================================================
            # GUARDAR RESULTADO
            # =================================================

            detections.append({

                "label": label,

                "confidence": conf_score,

                "bbox": [x1, y1, x2, y2]
            })

            # =================================================
            # COLORES
            # =================================================

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

            else:

                color = (255, 255, 255)

            # =================================================
            # RECTÁNGULO
            # =================================================

            cv2.rectangle(

                draw_img,

                (x1, y1),

                (x2, y2),

                color,

                2
            )

            # =================================================
            # TEXTO
            # =================================================

            text = f"{label} {conf_score:.2f}"

            cv2.putText(

                draw_img,

                text,

                (x1, max(20, y1 - 10)),

                cv2.FONT_HERSHEY_SIMPLEX,

                0.7,

                color,

                2
            )

        # =================================================
        # MOSTRAR RESULTADO
        # =================================================

        if show:

            max_width = 1400
            max_height = 900

            disp = draw_img.copy()

            h_img, w_img = disp.shape[:2]

            scale = min(

                max_width / w_img,

                max_height / h_img,

                1.0
            )

            if scale < 1.0:

                disp = cv2.resize(

                    disp,

                    (
                        int(w_img * scale),
                        int(h_img * scale)
                    )
                )

            # =============================================
            # GUARDAR IMAGEN
            # =============================================

            output_path = "resultado_deteccion.jpg"

            cv2.imwrite(
                output_path,
                disp
            )

            print(
                f"\nImagen guardada: {output_path}"
            )

            # =============================================
            # VENTANA
            # =============================================

            cv2.namedWindow(
                "Detections",
                cv2.WINDOW_NORMAL
            )

            cv2.imshow(
                "Detections",
                disp
            )

            print(
                "\nPresiona cualquier tecla para cerrar..."
            )

            cv2.waitKey(0)

            cv2.destroyAllWindows()

        return detections


# =========================================================
# LOAD MODEL
# =========================================================

def load_model(modelo_path, det_path):

    model = torch.load(

        modelo_path,

        map_location=DEVICE,

        weights_only=False
    )

    det_yolo = YOLO(det_path)

    print("\nClases del detector:\n")

    print(det_yolo.names)

    object.__setattr__(

        model,

        "_det_yolo",

        det_yolo
    )

    model.eval()

    return model


# =========================================================
# MAIN
# =========================================================

if __name__ == "__main__":

    if len(sys.argv) < 4:

        print(
            "Uso:\n"
            "python inference_pipeline.py "
            "modelo_unico.pt "
            "best_det_extraido.pt "
            "imagen.jpg"
        )

        sys.exit(1)

    modelo_path = sys.argv[1]

    det_path = sys.argv[2]

    img_path = sys.argv[3]

    # =====================================================
    # CARGAR MODELO
    # =====================================================

    print("\nCargando modelo...\n")

    model = load_model(
        modelo_path,
        det_path
    )

    # =====================================================
    # LEER IMAGEN
    # =====================================================

    img = cv2.imread(img_path)

    if img is None:

        print(f"\nNo se pudo leer: {img_path}")

        sys.exit(1)

    # =====================================================
    # INFERENCIA
    # =====================================================

    print("\nEjecutando inferencia...\n")

    detections = model.predict_unified(

        img,

        conf_thresh=0.10,

        iou_thresh=0.15,

        show=True
    )

    # =====================================================
    # RESULTADOS
    # =====================================================

    print("\n======================================")

    print(f"Detecciones encontradas: {len(detections)}")

    print("======================================\n")

    for d in detections:

        print(

            f"{d['label']} | "

            f"conf: {d['confidence']:.2f} | "

            f"bbox: {d['bbox']}"
        )

    print("\nProceso finalizado.\n")