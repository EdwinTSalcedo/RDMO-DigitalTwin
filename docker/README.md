# 🐳 Guía de Despliegue en Docker con Aceleración por GPU

Esta guía detalla los pasos para desplegar y ejecutar el Gemelo Digital y plataforma de percepción en tiempo real **RDMO** utilizando **Docker Compose** y aprovechando al 100% la tarjeta gráfica (**NVIDIA / AMD / Intel**).

---

## 🏗️ Arquitectura de Servicios

| Servicio | Contenedor | Puerto | Descripción |
| :--- | :--- | :--- | :--- |
| **Frontend WebGL** | `rdmo-web-frontend` | `http://localhost/` | Servidor Nginx que entrega la simulación 3D interactiva compilada en WebGL 2.0 (versión 2.0 con vehículos, personas y terrenos). |
| **Backend YOLO API** | `rdmo-yolo-backend` | `http://localhost:5000/` | API REST servida con FastAPI + PyTorch para la inferencia y clasificación de baches/defectos viales en tiempo real. |

---

## 🚀 1. Despliegue Estándar con Docker Compose

Para iniciar todos los servicios del proyecto con un solo comando:

```bash
docker compose up -d
```

Una vez iniciados los contenedores, abre tu navegador e ingresa a:  
👉 **`http://localhost/`**

---

## ⚡ 2. Configuración de Aceleración por GPU

Para obtener el máximo rendimiento tanto en la simulación 3D como en la velocidad de detección de la IA, se debe habilitar la GPU en ambos extremos:

### A. Backend de IA (Docker Container + NVIDIA CUDA)
Habilita que el modelo YOLO procese las imágenes en la GPU en lugar de la CPU (pasando de ~200ms a solo **10-15ms por imagen**).

1. **Instalar el Toolkit de NVIDIA para Docker (Linux):**
   ```bash
   curl -fsSL https://nvidia.github.io/libnvidia-container/gpgkey | sudo gpg --dearmor --yes -o /etc/apt/trusted.gpg.d/nvidia-container-toolkit.gpg
   echo "deb https://nvidia.github.io/libnvidia-container/stable/ubuntu18.04/amd64 /" | sudo tee /etc/apt/sources.list.d/nvidia-container-toolkit.list
   sudo apt-get update && sudo apt-get install -y nvidia-container-toolkit
   sudo nvidia-ctk runtime configure --runtime=docker
   sudo systemctl restart docker
   ```

2. **Verificar que `docker-compose.yml` tenga habilitada la GPU:**
   ```yaml
   services:
     yolo-backend:
       deploy:
         resources:
           reservations:
             devices:
               - driver: nvidia
                 count: 1
                 capabilities: [gpu]
   ```

3. **Iniciar o reiniciar el contenedor:**
   ```bash
   docker compose up -d
   ```

---

### B. Frontend WebGL (Renderizado 3D a 60+ FPS en Navegador)
En computadoras portátiles con **Gráficos Híbridos (Intel Integrated + GPU NVIDIA Dedicada)**, Linux inicia los navegadores usando la gráfica integrada Intel para ahorrar energía. Para forzar el renderizado en la tarjeta NVIDIA dedicada:

#### 🦊 Opción 1: Firefox (Recomendado en Linux)
- **Modo Gráfico:** Haz clic derecho sobre el icono de Firefox en el menú de inicio -> Selecciona **"Ejecutar con tarjeta gráfica dedicada"** (*Run with Dedicated Graphics Card*).
- **Modo Terminal:**
  ```bash
  MOZ_ENABLE_WAYLAND=0 __NV_PRIME_RENDER_OFFLOAD=1 __GLX_VENDOR_LIBRARY_NAME=nvidia firefox http://localhost &
  ```

#### 🌐 Opción 2: Chrome / Microsoft Edge / Brave
1. En `Configuración -> Sistema`, asegúrate de tener activada: **"Usar aceleración por hardware cuando esté disponible"**.
2. En la barra de direcciones, entra a `chrome://flags` (o `edge://flags`) y activa:
   - **Override software rendering list:** `Enabled`
   - **GPU rasterization:** `Enabled`
3. Reinicia el navegador.

---

## 📁 3. Almacenamiento de Capturas y Detecciones en el Equipo Local

El volumen de almacenamiento para guardar las capturas está **100% preconfigurado** en `docker-compose.yml`:

```yaml
volumes:
  - ./detecciones_output:/app/detecciones
```

### 🎯 Comportamiento para Releases de GitHub y Nuevos Usuarios:
1. Cuando cualquier usuario descargue el proyecto o Release de GitHub y ejecute `docker compose up -d`:
2. Docker creará **automáticamente** una carpeta llamada **`detecciones_output/`** en la raíz del directorio donde se levantó el `docker-compose.yml`.
3. Cada vez que el simulador WebGL tome una captura o detecte un bache, el backend de IA dibujará los recuadros y la imagen anotada se guardará directamente en su computadora física en:
   📁 **`<directorio_donde_corriste_compose>/detecciones_output/`**
4. **Acceso Web / HTTP:** Cualquier captura también se puede visualizar desde el navegador mediante:
   👉 `http://localhost/api/detecciones/<nombre_de_captura>.jpg`

---

## 🛠️ Comandos Útiles de Administración

- **Ver estado y salud de los contenedores:**
  ```bash
  docker compose ps
  ```
- **Ver registros en tiempo real del backend de IA:**
  ```bash
  docker compose logs -f yolo-backend
  ```
- **Detener los servicios:**
  ```bash
  docker compose down
  ```
