# 🚗 Simulador de Patrullas - Detector de Baches

**Herramienta de Simulación 3D para Análisis de Patrullaje Urbano con Detección Automática de Infraestructura Dañada**

---

## 📋 Tabla de Contenidos

- [Visión General](#-visión-general)
- [Características Principales](#-características-principales)
- [Arquitectura de Escenas](#-arquitectura-de-escenas)
- [Guía Completa de Uso](#-guía-completa-de-uso)
- [Descripción Detallada de Modos](#-descripción-detallada-de-modos)
- [Controles de Teclado](#-controles-de-teclado)
- [FAQ y Solución de Problemas](#-faq-y-solución-de-problemas)

---

## 🎯 Visión General

### ¿Qué es este Simulador?

Este es un **simulador 3D avanzado de patrullas urbanas** que modela el comportamiento realista de vehículos y peatones en una ciudad generada proceduralmente. El sistema está diseñado para:

- ✅ **Simular patrullas** de vehículos siguiendo rutas inteligentes
- ✅ **Detectar automáticamente baches** y daños en el pavimento
- ✅ **Recopilar datos** detallados sobre comportamiento y eventos (CSV, JSON, LOG)
- ✅ **Visualizar en 3D** toda la simulación en tiempo real
- ✅ **Capturar Datasets** para entrenamiento de modelos de IA

### Objetivos

| Objetivo | Descripción |
|----------|-------------|
| 🚗 **Patrullas Realistas** | Vehículos que se mueven naturalmente siguiendo waypoints con evasión de obstáculos |
| 🚶 **Peatones Inteligentes** | Agentes que navegan por la ciudad evitando colisiones |
| 🔍 **Detección de Baches** | Sistema automático que identifica y registra daños viales |
| 📊 **Análisis de Datos** | Exportación de eventos, estadísticas y logs detallados |
| 🎮 **Navegación Manual** | Control de cámara y drone mediante teclado para inspección detallada |

---

## ⚡ Características Principales

### 🏙️ Generación Procedural de Ciudades
- Generación automática de manzanas, calles y casas.
- NavMesh dinámico horneado en tiempo real para navegación óptima.
- Sistemas de colisiones y obstáculos realistas.

### 🤖 Sistemas de Movimiento Avanzados
- **RVO2** (Reciprocal Collision Avoidance) para evitar colisiones fluidas entre agentes.
- **CarPatrol**: Movimiento de vehículos con evasión de aceras y obstáculos.
- **RectangularPatrol**: Movimiento de peatones en patrones definidos.

### 📸 Captura de Imágenes Automatizada
- Generación de screenshots de baches en alta resolución (**1270x950**).
- Metadata JSON con información de cada captura (posición, severidad, conteo).
- Modo manual y automático para compilación de datasets.

### 📊 Sistema de Datos Robusto
- Exportación a **CSV** con todos los eventos de la simulación.
- Exportación a **JSON** con estadísticas agregadas de rendimiento y agentes.
- **Logs detallados** con timestamps de precisión de milisegundos.

---

## 🏗️ Arquitectura de Escenas

El simulador funciona con **una interfaz central (Mode_Menu)** que controla el acceso a los modos principales de operación. El **Modo Debug** es una escena independiente para propósitos de desarrollo.

```
                    ╔═══════════════════════════════════════╗
                    ║       MODE_MENU (Punto de Entrada)   ║
                    ║      Selecciona qué modo ejecutar     ║
                    ╚═════════════════╤═════════════════════╝
                                      │
                    ┌─────────────────┼─────────────────┐
                    │                 │                 │
                ┌───▼───┐         ┌───▼────┐      ┌───▼────┐
                │ Model │         │  Data  │      │Capture │
                │(Visual)│        │(Sin UI)│      │(Fotos) │
                └───────┘        └────────┘      └────────┘
                    │                 │
                    └────────┬────────┘
                             │
                      ┌──────▼──────┐
                      │  MODE_LOAD  │
                      │(Transición) │
                      └─────────────┘
```

### Escenas Disponibles

| Escena | Propósito | Acceso |
|--------|-----------|--------|
| **Mode_Menu** | Interfaz principal de selección de modos. | Punto de entrada. |
| **Mode_Load** | Pantalla de carga que activa objetos progresivamente. | Automático entre escenas. |
| **Mode_Model** | Simulación visual interactiva con múltiples cámaras. | Desde Mode_Menu. |
| **Mode_Data** | Simulación headless optimizada para recolección de datos. | Desde Mode_Menu. |
| **Mode_Capture** | Sistema especializado para capturar imágenes de baches. | Desde Mode_Menu. |
| **Mode_Debug** | Escena de desarrollo para pruebas técnicas y gizmos. | Escena independiente. |

---

## 🎮 Guía Completa de Uso

### 🔴 Paso 1: Iniciar la Aplicación

1. Abre el proyecto en **Unity 2022 LTS** o superior.
2. Carga la escena `Mode_Menu.unity` desde `Assets/Scenes/`.
3. Presiona **Play (▶️)**.

**Resultado**: Se abre la interfaz principal con los botones de acceso a los modos.

---

### 🔵 Paso 2: Seleccionar un Modo desde Mode_Menu

La pantalla inicial muestra las siguientes opciones:

1.  **🎬 INICIAR SIMULACIÓN**: Carga `Mode_Model` para ver la simulación en 3D.
2.  **📊 RECOLECCIÓN DE DATOS**: Carga `Mode_Data` para generar archivos de datos sin carga visual.
3.  **📷 MODO CAPTURA**: Carga `Mode_Capture` para tomar fotos de baches.
4.  **⚙️ CONFIGURACIÓN**: Panel de ajustes globales.
5.  **❌ SALIR**: Cierra la aplicación.

---

## 📍 Descripción Detallada de Modos

### 1️⃣ MODE_MODEL - Simulación Visual Principal

Es el modo interactivo diseñado para **probar modelos ONNX directamente dentro de Unity** (usando Sentis o Barracuda). 

- **Inferencia Nativa**: Ejecuta redes neuronales en formato `.onnx` sin depender de procesos externos.
- **Vistas de Cámara**: Cicla entre 3 cámaras predefinidas presionando la tecla `v`:
    - **Espectador**: Vista aérea/libre.
    - **Recogedor**: Vista desde el vehículo principal.
    - **Delado**: Vista lateral de seguimiento.
- **Propósito**: Validar el rendimiento de modelos de detección de objetos en tiempo real sobre el motor de físicas de Unity.

---

### 2️⃣ MODE_DATA - Recopilación de Datos

Diseñado para ejecuciones de alto rendimiento con **conexión opcional a Python**.

- **Conexión API (FastAPI)**: Puede conectarse a un servidor externo (`.py`) para realizar inferencia remota y validación de datos.
- **Captura de Imágenes**: Si no se detecta conexión con el archivo Python, el modo funciona como un sistema de captura de imágenes puras.
- **Archivos Generados**:
    - **CSV del Gemelo Digital**: Se guardan en `Assets/DigitalTwin_Logs/` (historial de eventos, telemetría y tráfico).
    - **Capturas y Datasets**: Se guardan en la carpeta de datos persistentes del sistema (`PersistentDataPath`).
    - `simulation.log`: Log técnico detallado de la ejecución.

---

### 3️⃣ MODE_CAPTURE - Sistema de Etiquetado Automático

Herramienta especializada en la creación de datasets masivos con **etiquetado automático estilo Roboflow/YOLO**.

- **Detección Multiclase**: Identifica y etiqueta automáticamente:
    - 🕳️ **Baches** (Potholes, Crocodiles, Cracks).
    - 🚶 **Personas**.
    - 🚗 **Vehículos**.
- **Dataset Listo para Usar**: Genera archivos `.txt` con coordenadas normalizadas (`class x_center y_center width height`) compatibles con frameworks de entrenamiento como YOLOv8.
- **Generación On-Demand**: Botón para crear nuevos baches aleatorios y refrescar el entorno instantáneamente.
- **Modo Automático**: Captura ráfagas de imágenes etiquetadas basadas en criterios de visibilidad.

---

### 4️⃣ MODE_LOAD - Transición Inteligente

Esta escena gestiona la transición entre el menú y los modos de simulación. Utiliza el script `SceneInitializer` para:
- Activar objetos de forma progresiva para evitar congelamientos del motor.
- Hornear el NavMesh dinámico.
- Inicializar generadores de terreno y baches.

---

## ⌨️ Controles de Teclado

A continuación se detallan los únicos controles funcionales en el simulador:

### Movimiento y Navegación (Drone/Cámara)
| Tecla | Acción |
|-------|--------|
| **W / S** | Mover hacia adelante / atrás |
| **A / D** | Mover hacia la izquierda / derecha |
| **Q / E** | Rotar o desplazamiento lateral (según el modo) |
| **I / K** | Subir / Bajar altura (Control de Altitud) |

### Control de Visualización
| Tecla | Acción |
|-------|--------|
| **V** | Ciclar entre cámaras (Espectador → Recogedor → Delado) |
| **↑ / ↓ (Flechas)** | Ajustar altura de cámara específicamente en **Mode_Capture** |

### Otros
| Tecla | Acción |
|-------|--------|
| **SPACE** | Pausar movimiento / Alternar estados |
| **ESC** | Regresar al menú principal / Salir |

---

## ❓ FAQ y Solución de Problemas

### ❓ "La pantalla de carga se queda en 99%"
**Causa**: El NavMesh o la generación de baches está tomando más tiempo del esperado.
**Solución**: Espera unos segundos adicionales; la carga es asíncrona y finalizará automáticamente.

### ❓ "No puedo mover la cámara con el mouse"
**Respuesta**: En este simulador, el control de navegación es principalmente por teclado mediante **WASD** e **IK** para garantizar precisión en la inspección.

### ❓ "¿Dónde encuentro los archivos de la simulación?"
**Respuesta**: Los archivos se dividen según su tipo:
1.  **Logs y CSV**: Se encuentran directamente en el proyecto en la carpeta `Assets/DigitalTwin_Logs/`.
2.  **Imágenes y Etiquetas (Datasets)**: Unity los guarda en la carpeta de datos de usuario. En Windows, puedes acceder rápidamente pegando esto en el explorador de archivos:
    `%AppData%\..\LocalLow\DefaultCompany\Recogedor de Basura\Dataset_Baches\`

### ❓ "¿Cómo activo el Modo Debug?"
**Respuesta**: El modo Debug debe cargarse directamente desde el Editor de Unity abriendo la escena `Assets/Scenes/Mode_Debug.unity`. No es accesible desde el menú de usuario final.

---

**Versión**: 3.0 (Mayo 2026)  
**Estado**: ✅ Documentación validada con el código fuente.
