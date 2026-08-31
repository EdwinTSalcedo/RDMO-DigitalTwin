import cv2
import numpy as np
import torch
import torch.nn as nn
import pickle
import os
import uvicorn

from contextlib import asynccontextmanager
from fastapi import FastAPI, UploadFile, File
from fastapi.responses import JSONResponse
from torchvision.ops import nms

# ==============================================================================
# CONFIGURACIÓN
# ==============================================================================

DEVICE = "cuda" if torch.cuda.is_available() else "cpu"

IMG_SIZE = 640
CONF_THRESHOLD = 0.25
NMS_IOU = 0.45

BASE_DIR = os.path.dirname(os.path.abspath(__file__))

MODEL_PATH = os.path.join(BASE_DIR, "unity_modelo2.pt")

DETECTIONS_DIR = os.path.join(BASE_DIR, "Detecciones_IA")
os.makedirs(DETECTIONS_DIR, exist_ok=True)

# ==============================================================================
# CLASES
# ==============================================================================

CLASS_NAMES = {
    0: "Crocodile Crack",
    1: "Single Crack",
    2: "Pothole",
    3: "Person",
    4: "Car"
}

DETECTABLE_CLASSES = [
    CLASS_NAMES[i]
    for i in sorted(CLASS_NAMES.keys())
]

# ==============================================================================
# PLACEHOLDER
# ==============================================================================

class PlaceholderModule(nn.Module):

    def __init__(self, *args, **kwargs):

        super().__init__()

        for k, v in kwargs.items():

            try:
                setattr(self, k, v)
            except:
                pass

    def __getattr__(self, name):

        return lambda *a, **kw: None

    def __setattr__(self, name, value):

        try:
            super().__setattr__(name, value)
        except:
            object.__setattr__(self, name, value)

    def forward(self, x):

        return torch.zeros(
            1,
            8400,
            10,
            device=x.device if hasattr(x, "device") else "cpu"
        )

# ==============================================================================
# SAFE PICKLE
# ==============================================================================

class SafeUnpickler(pickle.Unpickler):

    def find_class(self, module, name):

        if module == "__main__":
            return PlaceholderModule

        return super().find_class(module, name)

class SafePickle:
    Unpickler = SafeUnpickler

# ==============================================================================
# VARIABLES GLOBALES
# ==============================================================================

model = None
MODEL_TYPE = "unknown"
_fallback_weights = None

# ==============================================================================
# FASTAPI
# ==============================================================================

@asynccontextmanager
async def lifespan(app: FastAPI):

    load_model_on_startup()

    yield

app = FastAPI(
    title="Detector IA",
    description="Detector de baches, grietas, personas y vehículos",
    version="1.0",
    lifespan=lifespan
)

# ==============================================================================
# CARGAR MODELO
# ==============================================================================

def load_model_on_startup():

    global model
    global MODEL_TYPE
    global _fallback_weights

    print("=" * 60)
    print("🚀 INICIANDO SERVIDOR")
    print("=" * 60)

    print(f"📂 Modelo: {MODEL_PATH}")
    print(f"🖥️ Device: {DEVICE}")

    print("\n🎯 CLASES CONFIGURADAS:")

    for idx, name in CLASS_NAMES.items():
        print(f"   [{idx}] {name}")

    print()

    if not os.path.exists(MODEL_PATH):

        raise FileNotFoundError(
            f"❌ No existe: {MODEL_PATH}"
        )

    try:

        raw_obj = torch.load(
            MODEL_PATH,
            map_location=DEVICE,
            weights_only=False,
            pickle_module=SafePickle
        )

        print(f"📦 Tipo cargado: {type(raw_obj)}")

        extracted_model = None

        # ==========================================================
        # MODELO DIRECTO
        # ==========================================================

        if isinstance(raw_obj, nn.Module):

            extracted_model = raw_obj

            MODEL_TYPE = "real"

            print("✅ nn.Module detectado")

        # ==========================================================
        # CHECKPOINT DICT
        # ==========================================================

        elif isinstance(raw_obj, dict):

            print("📂 Checkpoint tipo dict")

            print("📌 Claves:")

            for k in raw_obj.keys():
                print(f"   - {k}")

            for key in [
                "model",
                "ema",
                "net",
                "module"
            ]:

                if key in raw_obj:

                    candidate = raw_obj[key]

                    print(
                        f"🔍 Revisando '{key}' -> {type(candidate)}"
                    )

                    if isinstance(candidate, nn.Module):

                        extracted_model = candidate

                        MODEL_TYPE = "real"

                        print(f"✅ Modelo encontrado en '{key}'")

                        break

            # Buscar state dict
            if extracted_model is None:

                for key in [
                    "state_dict",
                    "model_state_dict",
                    "weights"
                ]:

                    if key in raw_obj:

                        _fallback_weights = raw_obj[key]

                        MODEL_TYPE = "placeholder_with_weights"

                        print(
                            f"⚠️ Solo state_dict encontrado en '{key}'"
                        )

                        break

        # ==========================================================
        # PLACEHOLDER
        # ==========================================================

        elif type(raw_obj).__name__ == "PlaceholderModule":

            extracted_model = raw_obj

            MODEL_TYPE = "placeholder"

            print("⚠️ Placeholder detectado")

        # ==========================================================
        # VALIDACIÓN
        # ==========================================================

        if extracted_model is None:

            raise RuntimeError(
                "❌ No se pudo extraer modelo válido"
            )

        # ==========================================================
        # PREPARAR MODELO
        # ==========================================================

        model = extracted_model.to(DEVICE)

        model.eval()

        # ==========================================================
        # MOSTRAR DTYPE
        # ==========================================================

        try:

            model_dtype = next(model.parameters()).dtype

            print(f"🧠 DTYPE DEL MODELO: {model_dtype}")

        except:

            print("⚠️ No se pudo detectar dtype")

        print("\n🟢 MODELO LISTO")
        print(f"📌 Tipo: {MODEL_TYPE}")

        print("\n🎯 CLASES DETECTABLES:")

        for idx, cls_name in CLASS_NAMES.items():
            print(f"   [{idx}] {cls_name}")

        print("=" * 60)

    except Exception as e:

        MODEL_TYPE = "failed"

        print("=" * 60)
        print("❌ ERROR CRÍTICO")
        print(str(e))
        print("=" * 60)

        raise

# ==============================================================================
# PREPROCESAMIENTO
# ==============================================================================

def preprocess_image(img):

    h, w = img.shape[:2]

    scale = min(
        IMG_SIZE / w,
        IMG_SIZE / h
    )

    new_w = int(w * scale)
    new_h = int(h * scale)

    resized = cv2.resize(
        img,
        (new_w, new_h),
        interpolation=cv2.INTER_LINEAR
    )

    dw = (IMG_SIZE - new_w) / 2
    dh = (IMG_SIZE - new_h) / 2

    top = int(round(dh - 0.1))
    bottom = int(round(dh + 0.1))

    left = int(round(dw - 0.1))
    right = int(round(dw + 0.1))

    padded = cv2.copyMakeBorder(
        resized,
        top,
        bottom,
        left,
        right,
        cv2.BORDER_CONSTANT,
        value=(114, 114, 114)
    )

    rgb = cv2.cvtColor(
        padded,
        cv2.COLOR_BGR2RGB
    )

    tensor = torch.from_numpy(rgb)

    tensor = tensor.permute(2, 0, 1)

    tensor = tensor.unsqueeze(0)

    tensor = tensor.to(DEVICE)

    # ==========================================================
    # NORMALIZAR
    # ==========================================================

    tensor = tensor.float() / 255.0

    # ==========================================================
    # FP16 SI EL MODELO ESTÁ EN HALF
    # ==========================================================

    try:

        model_dtype = next(model.parameters()).dtype

        if model_dtype == torch.float16:

            tensor = tensor.half()

            print("⚡ Input convertido a FP16")

    except Exception as e:

        print(f"⚠️ Error dtype: {e}")

    return tensor, scale, left, top

# ==============================================================================
# INFERENCIA SEGURA
# ==============================================================================

def safe_inference(tensor):

    global model

    try:

        with torch.no_grad():

            outputs = model(tensor)

        if isinstance(outputs, (list, tuple)):
            outputs = outputs[0]

        if not torch.is_tensor(outputs):

            print("⚠️ Output inválido")

            return torch.zeros(0, 6)

        if outputs.numel() == 0:

            print("⚠️ Output vacío")

            return torch.zeros(0, 6)

        if outputs.abs().sum().item() == 0:

            print("⚠️ Placeholder detectado")

            return torch.zeros(0, 6)

        return outputs

    except Exception as e:

        print(f"❌ Error inferencia: {e}")

        return torch.zeros(0, 6)

# ==============================================================================
# ENDPOINT PRINCIPAL
# ==============================================================================

@app.post("/predict")
async def predict_image(file: UploadFile = File(...)):

    try:

        contents = await file.read()

        nparr = np.frombuffer(contents, np.uint8)

        img = cv2.imdecode(
            nparr,
            cv2.IMREAD_COLOR
        )

        if img is None:

            return JSONResponse(
                content={"error": "Imagen corrupta"},
                status_code=400
            )

        tensor, scale, pad_left, pad_top = preprocess_image(img)

        outputs = safe_inference(tensor)

        # ==========================================================
        # SIN DETECCIONES
        # ==========================================================

        if outputs.numel() == 0:

            print("📭 Sin detecciones")

            save_path = os.path.join(
                DETECTIONS_DIR,
                file.filename
            )

            cv2.imwrite(save_path, img)

            return JSONResponse(
                content={
                    "detecciones": [],
                    "clases_disponibles": DETECTABLE_CLASSES
                }
            )

        # ==========================================================
        # AJUSTAR FORMATO
        # ==========================================================

        if outputs.dim() == 4:

            outputs = outputs.permute(
                0,
                2,
                3,
                1
            ).flatten(1, 2)

        elif outputs.dim() == 3 and outputs.shape[1] > 100:

            outputs = outputs.permute(0, 2, 1)

        preds = outputs[0].cpu()

        boxes = preds[:, :4]

        conf_matrix = preds[:, 4:5] * preds[:, 5:]

        conf, cls_idx = conf_matrix.max(dim=1)

        conf = conf.unsqueeze(1)

        mask = (
            conf.squeeze() > CONF_THRESHOLD
        ).nonzero(as_tuple=False).squeeze(1)

        detections = []

        # ==========================================================
        # DETECCIONES
        # ==========================================================

        if mask.numel() > 0:

            boxes = boxes[mask]
            conf = conf[mask]
            cls_idx = cls_idx[mask]

            # xywh -> xyxy
            boxes[:, 2] = boxes[:, 0] + boxes[:, 2]
            boxes[:, 3] = boxes[:, 1] + boxes[:, 3]

            boxes[:, [0, 2]] = (
                boxes[:, [0, 2]] - pad_left
            ) / scale

            boxes[:, [1, 3]] = (
                boxes[:, [1, 3]] - pad_top
            ) / scale

            # ==========================================================
            # NMS NECESITA FLOAT32
            # ==========================================================

            boxes = boxes.float()
            conf = conf.float()

            keep = nms(
                boxes,
                conf.squeeze(),
                NMS_IOU
            )

            boxes = boxes[keep]
            conf = conf[keep]
            cls_idx = cls_idx[keep]

            for b, c, cid in zip(
                boxes,
                conf,
                cls_idx
            ):

                x1, y1, x2, y2 = b.tolist()

                cls_id = int(cid.item())

                final_label = CLASS_NAMES.get(
                    cls_id,
                    f"Clase_{cls_id}"
                )

                conf_score = float(c.item())

                detections.append({
                    "clase": final_label,
                    "confianza": round(conf_score, 3),
                    "caja": [
                        int(x1),
                        int(y1),
                        int(x2),
                        int(y2)
                    ]
                })

                # ==================================================
                # COLOR
                # ==================================================

                color = (0, 255, 0)

                if final_label == "Person":
                    color = (255, 0, 0)

                elif final_label == "Car":
                    color = (0, 0, 255)

                elif "Crocodile" in final_label:
                    color = (0, 255, 255)

                # ==================================================
                # DIBUJAR
                # ==================================================

                cv2.rectangle(
                    img,
                    (int(x1), int(y1)),
                    (int(x2), int(y2)),
                    color,
                    2
                )

                cv2.putText(
                    img,
                    f"{final_label} {conf_score:.2f}",
                    (
                        int(x1),
                        max(15, int(y1) - 10)
                    ),
                    cv2.FONT_HERSHEY_SIMPLEX,
                    0.5,
                    color,
                    2
                )

        # ==========================================================
        # GUARDAR IMAGEN
        # ==========================================================

        save_path = os.path.join(
            DETECTIONS_DIR,
            file.filename
        )

        cv2.imwrite(save_path, img)

        print(f"💾 Guardado: {save_path}")

        # ==========================================================
        # RESPUESTA
        # ==========================================================

        return JSONResponse(
            content={
                "detecciones": detections,
                "clases_disponibles": DETECTABLE_CLASSES,
                "modelo": MODEL_TYPE
            }
        )

    except Exception as e:

        print(f"💥 Error general: {e}")

        return JSONResponse(
            content={
                "error": str(e)
            },
            status_code=500
        )

# ==============================================================================
# VER CLASES
# ==============================================================================

@app.get("/classes")
async def get_classes():

    return JSONResponse(
        content={
            "cantidad_clases": len(CLASS_NAMES),
            "clases": CLASS_NAMES
        }
    )

# ==============================================================================
# ESTADO
# ==============================================================================

@app.get("/")
async def root():

    return JSONResponse(
        content={
            "estado": "Servidor IA activo",
            "modelo": MODEL_TYPE,
            "device": DEVICE,
            "clases_detectables": DETECTABLE_CLASSES
        }
    )

# ==============================================================================
# MAIN
# ==============================================================================

if __name__ == "__main__":

    uvicorn.run(
        app,
        host="0.0.0.0",
        port=5000
    )