# StreamControlC#

Aplicación de escritorio para Windows diseñada para gestionar y automatizar marcadores, brackets de Top 8 y datos de comentaristas en torneos de deportes electrónicos y fighting games, sincronizándolos en tiempo real con overlays para **OBS Studio** (HTML5 + Canvas). Incluye integración con la API GraphQL de **start.gg**.

---

## Características Principales

- **Control Total del Marcador:** Nombres, tags/equipos, banderas por país (ISO), puntuación rápida, swap de lados y reset con atajos de teclado.
- **Top 8 Bracket:** Gestión completa de llaves de doble eliminación (Winners, Losers, Grand Finals y Reset).
- **Integración con start.gg:** Sincronización automática o manual de enfrentamientos mediante Stream Queue o selección directa por fases.
- **Overlays en Tiempo Real para OBS:** Vistas web transparentes a 1920×1080 aceleradas por Canvas (EaselJS / TweenJS).
- **Ejecutable Autocontenido:** No requiere instalar el SDK de .NET para su ejecución diaria.

---

## Arquitectura

```
┌──────────────────────────────┐
│  StreamControlCSharp.exe     │  (WPF, Windows x64, .NET 8)
│  Marcador / Top 8 / start.gg │
└──────────────┬───────────────┘
               │ Guarda al pulsar Ctrl + S (UTF-8)
               ▼
┌──────────────────────────────┐
│ browserfiles/                │
│   streamcontrol.json         │  ← Estado compartido
│   *.html + js/ + images/     │
└──────────────┬───────────────┘
               │ Polling XHR cada 250 ms
               ▼
┌──────────────────────────────┐
│ OBS Studio                   │
│ Fuentes de Navegador         │  (1920×1080 Canvas transparente)
└──────────────────────────────┘
```

---

## Overlays Incluidos (`browserfiles/`)

| Archivo | Descripción |
|---|---|
| `scoreboard.html` | Marcador principal: jugadores, tags, banderas, puntuación, ronda y texto inferior. |
| `Bracket-Top8.html` | Cuadro completo de Top 8 (Winners, Losers, Grand Finals y Reset). |
| `Casters.html` | Comentaristas 1 y 2 con sus usuarios de redes (X / Twitter). |
| `nextfight.html` | Notificación de próxima pelea en cola y ronda. |
| `infoBelow.html` | Barra de información o patrocinadores para el Top 8. |

---

## Requisitos

- **Sistema Operativo:** Windows 10 u 11 (64 bits).
- **Para emitir:** [OBS Studio](https://obsproject.com/) (versión 28 o superior recomendada).
- **Para compilar:** [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (solo si modificas el código en C#).

---

## Inicio Rápido

1. **Descarga el proyecto:** Asegúrate de extraer la carpeta completa (el ejecutable necesita las carpetas adyacentes `browserfiles/` y los `.csv`).
2. **Ejecuta `StreamControlCSharp.exe`:**
   - En la pestaña **SCOREBOARD**, ingresa los nombres, tags y ronda.
   - Pulsa **GUARDAR EN OBS** o `Ctrl + S`.
3. **Agrega la vista en OBS Studio:**
   - Añade una nueva fuente de tipo **Navegador** (*Browser Source*).
   - Marca **Archivo local** y selecciona `browserfiles/scoreboard.html` (o la vista deseada).
   - Ajusta la resolución a **Ancho: 1920** y **Alto: 1080**.
   - En **CSS personalizado**, ingresa:
     ```css
     body { background: transparent; margin: 0; overflow: hidden; }
     ```
   - Desmarca *Actualizar el navegador cuando la escena se active* para evitar repeticiones innecesarias de animaciones.

---

## Atajos de Teclado

| Atajo | Acción |
|---|---|
| `Ctrl + S` | Guardar cambios en el JSON para OBS |
| `F1` / `Shift + F1` | +1 / -1 punto al Jugador 1 |
| `F2` / `Shift + F2` | +1 / -1 punto al Jugador 2 |
| `F5` | Swap (intercambiar lados y puntuaciones de jugadores) |
| `F6` | Reset de marcador a 0 - 0 |

---

## Estructura del Repositorio

```
StreamControlCSharp/
├─ StreamControlCSharp.exe     # Aplicación compilada (Release x64)
├─ compilar.bat                # Script para compilar el proyecto
├─ config.example.json         # Plantilla de configuración
├─ config.md                   # Guía técnica de configuración y directivas
├─ players.csv                 # Base de datos de jugadores
├─ commentators.csv            # Opciones de comentaristas
├─ round.csv                   # Opciones de rondas
├─ text.csv                    # Textos del marcador
├─ textos-top8.csv             # Textos del Top 8
├─ browserfiles/               # Overlays y recursos web para OBS
│  ├─ *.html                   # Vistas de navegador (Scoreboard, Bracket, etc.)
│  ├─ js/                      # Lógica de actualización y países
│  └─ images/GoSquared/        # Banderas por país
└─ src/StreamControlCSharp/    # Código fuente C# / WPF (.NET 8)
```

---

## Personalización, Directivas y Desarrollo

Para instrucciones detalladas sobre:
- Configuración de API tokens y Stream Queues de **start.gg**.
- Formato y precarga de archivos **CSV**.
- Personalización de fuentes, coordenadas, colores y fondos en las vistas HTML.
- Referencia completa de claves del archivo `streamcontrol.json`.
- Adición de nuevos campos en el modelo C# y la interfaz WPF.
- Guía de compilación desde el código fuente y solución de problemas.

👉 Consulta la **[Guía de Configuración y Directivas (`config.md`)](config.md)**.

---

## Créditos y Agradecimientos

- **[StreamControl](https://github.com/farpenoodle/StreamControl)** (por [farpenoodle](https://github.com/farpenoodle)): Proyecto original que inspiró la idea, estructura del JSON y flujo de trabajo para el control de streams de torneos.
- **[Shieldbreakers-StreamControl-setup](https://github.com/MiggL/Shieldbreakers-StreamControl-setup)** (por [MiggL](https://github.com/MiggL)): Por las plantillas base de las vistas HTML, los scripts de renderizado y animación con EaselJS/TweenJS y el conjunto de banderas de países (`browserfiles/images/GoSquared/`).

---

## Licencia

Distribuido bajo la licencia [GNU General Public License v3.0](LICENSE).
