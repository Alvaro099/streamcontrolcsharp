# Guía de Configuración, Personalización y Desarrollo

Esta guía detalla cómo configurar, modificar y extender **StreamControlC#**, incluyendo la estructura de datos, personalización de overlays en HTML/Canvas, configuración de start.gg, directivas de desarrollo y compilación desde el código fuente.

---

## Índice

1. [Configuración General: `config.json`](#1-configuración-general-configjson)
2. [Archivos de Datos CSV](#2-archivos-de-datos-csv)
3. [Integración con Start.gg](#3-integración-con-startgg)
4. [Arquitectura de los Overlays (HTML + Canvas)](#4-arquitectura-de-los-overlays-html--canvas)
5. [Directivas y Funciones de las Vistas HTML](#5-directivas-y-funciones-de-las-vistas-html)
6. [Referencia Completa de Claves del JSON](#6-referencia-completa-de-claves-del-json)
7. [Cómo Modificar y Personalizar Overlays](#7-cómo-modificar-y-personalizar-overlays)
8. [Cómo Agregar un Campo Nuevo al Proyecto (C# + XAML + JSON + HTML)](#8-cómo-agregar-un-campo-nuevo-al-proyecto-c--xaml--json--html)
9. [Compilación desde el Código Fuente](#9-compilación-desde-el-código-fuente)
10. [Solución de Problemas y Límites Conocidos](#10-solución-de-problemas-y-límites-conocidos)
11. [Créditos y Agradecimientos](#11-créditos-y-agradecimientos)

---

## 1. Configuración General: `config.json`

El archivo `config.json` se ubica en la raíz del proyecto (junto a `StreamControlCSharp.exe`). Se genera automáticamente al cerrar o guardar desde la aplicación, utilizando `config.example.json` como plantilla.

> **Seguridad:** `config.json` contiene credenciales personales (como tu token de start.gg). Está ignorado en `.gitignore` y nunca debe subirse al repositorio.

### Estructura de `config.json`

```json
{
  "StartGgToken": "",
  "TournamentSlug": "",
  "StreamName": "",
  "AutoSyncEnabled": false,
  "AutoSyncIntervalSeconds": 15,
  "OutputDirectory": "browserfiles"
}
```

### Parámetros y Directivas

| Parámetro | Tipo | Descripción |
|---|---|---|
| `StartGgToken` | string | Token de acceso personal generado en start.gg. |
| `TournamentSlug` | string | Slug o enlace completo del torneo o evento en start.gg. |
| `StreamName` | string | Nombre del canal para asociar a la Stream Queue correspondiente. |
| `AutoSyncEnabled` | bool | Activa la sincronización en segundo plano al iniciar (`true` o `false`). |
| `AutoSyncIntervalSeconds` | int | Intervalo de sincronización en segundos (mínimo 5, recomendado 15). |
| `OutputDirectory` | string | Carpeta donde se genera `streamcontrol.json` (por defecto `"browserfiles"`). |

> **Nota:** Si modificas `OutputDirectory`, debes actualizar también las rutas relativas en las fuentes de navegador de OBS para que lean el archivo generado en esa nueva ubicación.

---

## 2. Archivos de Datos CSV

Los archivos `.csv` en la raíz permiten pre-cargar listas en los desplegables de la interfaz y registrar jugadores. Deben guardarse en formato **UTF-8**.

| Archivo | Formato de Columnas | Función |
|---|---|---|
| `players.csv` | `Nombre,Pais,Tag` | Base de datos local de jugadores. La app registra automáticamente los jugadores que guardes. |
| `commentators.csv` | `Nombre,Twitter` | Opciones del menú desplegable de comentaristas (carga nombre y usuario de X). |
| `round.csv` | Una opción por línea | Opciones predefinidas para el desplegable de **Ronda Actual**. |
| `text.csv` | Una opción por línea | Opciones para el desplegable de **Texto Abajo** en el Scoreboard. |
| `textos-top8.csv` | Una opción por línea | Opciones para el desplegable de **Texto Abajo** en el Top 8. |

### Ejemplos

**`commentators.csv`**:
```csv
ASAELITO,@Asaelito
Soka,@Soka
```

**`round.csv`**:
```csv
Winners Round 1
Winners Quarters
Winners Semis
Winners Finals
Losers Top 8
Losers Quarters
Losers Semis
Losers Finals
Grand Finals
Grand Finals Reset
```

---

## 3. Integración con Start.gg

La sincronización se realiza mediante la API GraphQL v4 de start.gg.

1. **Obtención del Token:**
   - Ve a [start.gg/admin/profile/developer](https://start.gg/admin/profile/developer).
   - Genera un *Personal Access Token* y pégalo en la pestaña **Start.gg**.
2. **Formatos de Slug aceptados:**
   - Slug simple: `torneo-ejemplo-2026`
   - Ruta relativa: `tournament/torneo-ejemplo-2026`
   - Ruta de evento: `tournament/torneo-ejemplo-2026/event/tekken-8-singles`
   - URL completa: `https://www.start.gg/tournament/torneo-ejemplo-2026/event/...`
3. **Stream Queue:**
   - Si introduces tu nombre de canal en `StreamName` (por ejemplo `mi_canal`), la aplicación filtrará los sets asignados a esa Stream Queue específica. Si queda vacío, tomará el set activo más prioritario.
4. **Auto-sincronización:**
   - La directiva de auto-sincronización se inicia al arrancar la aplicación si `AutoSyncEnabled` es `true`. Consulta sets activos y actualiza marcador, tags, países y ronda sin intervención manual.

---

## 4. Arquitectura de los Overlays (HTML + Canvas)

El flujo de renderizado entre la aplicación y OBS funciona de la siguiente manera:

```
[StreamControlCSharp.exe]
           │
           │  Escribe al guardar (UTF-8 sin BOM)
           ▼
[browserfiles/streamcontrol.json]
           │
           │  Polling XHR cada 250 ms (compara campo 'timestamp')
           ▼
[browserfiles/*.html + EaselJS Canvas]
           │
           │  Renderiza en Canvas 1920x1080
           ▼
[OBS Studio - Browser Source]
```

- **Independencia:** La aplicación de escritorio nunca modifica los archivos HTML ni JS directamente; solo actualiza `streamcontrol.json`.
- **Detección de cambios:** Las vistas leen el campo `timestamp` cada 250 ms. Solo cuando el timestamp cambia se disparan las animaciones y el redibujado.
- **Librerías gráficas:** Las vistas utilizan **EaselJS** y **TweenJS** (suite CreateJS) para el control del Canvas HTML5 a 60 FPS.

---

## 5. Directivas y Funciones de las Vistas HTML

Los scripts `browserfiles/js/scoreboard-updater.js` y `browserfiles/js/bracket-updater.js` proporcionan las siguientes funciones para componer elementos en el canvas:

### `newDynText`
Crea un texto dinámico vinculado a una clave de `streamcontrol.json`:
```js
newDynText(nombreClave, x, y, anchoMaximo, tamanoFuente, color, alineacion)
```
- `nombreClave` *(string)*: Clave exacta en el JSON (ej. `"pName1"`, `"round"`).
- `x`, `y` *(number)*: Posición en píxeles dentro del lienzo de 1920×1080.
- `anchoMaximo` *(number)*: Ancho máximo en píxeles antes de realizar salto de línea automático.
- `tamanoFuente` *(number)*: Tamaño de la tipografía en píxeles.
- `color` *(string)*: Color en formato CSS (`"#FFFFFF"`, `"#FFD166"`, `rgba(...)`).
- `alineacion` *(string, opcional)*: `"center"` (por defecto), `"left"` o `"right"`.

> **Referencia de Posición:** Los textos utilizan `textBaseline = "hanging"`. Esto significa que la coordenada `y` corresponde al límite superior del texto, no a la línea base.

### `newDynImage`
Crea una imagen dinámica (como banderas) cuyo nombre de archivo proviene de una clave del JSON:
```js
newDynImage(nombreClave, x, y, rutaBase)
```
- `nombreClave` *(string)*: Clave del JSON con el país o nombre del archivo.
- `x`, `y` *(number)*: Coordenadas del extremo izquierdo/superior.
- `rutaBase` *(string)*: Directorio donde residen los recursos (ej. `"images/GoSquared/cropped/iso/64shiny/"`).

### `newMirroredDynImage`
Crea una imagen simétrica (espejada) respecto al eje X, ideal para la bandera del Jugador 2:
```js
newMirroredDynImage(nombreClave, idReferencia)
```

### `createScoreboard(board)`
Inicializa el ciclo de polling, agrega el contenedor principal `board` al `stage` y ejecuta la animación de entrada suave:
```js
createScoreboard(board);
```
> Debe llamarse una sola vez al finalizar `init()` en cada vista HTML.

---

## 6. Referencia Completa de Claves del JSON

Todas las claves expuestas en `browserfiles/streamcontrol.json`:

### Marcador y General
| Clave | Tipo | Contenido / Uso |
|---|---|---|
| `timestamp` | int | Timestamp Unix en segundos. Controla el refresco en OBS. |
| `pName1`, `pName2` | string | Nombres del Jugador 1 y Jugador 2. |
| `p1Team`, `p2Team` | string | Prefijos de equipo o patrocinador (Tags). |
| `pCountry1`, `pCountry2` | string | País de origen (nombre completo o código ISO de 2 letras). |
| `pScore1`, `pScore2` | string | Puntuaciones de los jugadores. |
| `round` | string | Ronda actual (ej. "Winners Finals"). |
| `textbottom` | string | Mensaje o texto secundario inferior del marcador. |
| `comm1name`, `comm1twit` | string | Nombre y usuario de X del Comentarista 1. |
| `comm2name`, `comm2twit` | string | Nombre y usuario de X del Comentarista 2. |
| `comm3name`, `comm3twit` | string | Nombre y usuario de X del Comentarista 3 (almacenado en JSON). |
| `TOP8text` | string | Texto inferior descriptivo en la vista de Top 8. |
| `champeon`, `segundo`, `tercero` | string | Nombres del podio (1°, 2° y 3° lugar). |
| `pCountryW1`, `pCountryW2`, `pCountryW3` | string | Países del podio. |
| `pChar1`, `pChar2`, `pChar3` | string | Personajes elegidos en el podio. |

### Partidos del Top 8 Bracket
Cada serie del bracket maneja 4 claves con el patrón `{prefijo}p1`, `{prefijo}p1s`, `{prefijo}p2`, `{prefijo}p2s` (Jugador 1, Puntos 1, Jugador 2, Puntos 2):

| Prefijo | Enfrentamiento |
|---|---|
| `ws1` | Winners Semifinals 1 |
| `ws2` | Winners Semifinals 2 |
| `wf` | Winners Finals |
| `ltop81` | Losers Top 8 - Partido 1 |
| `ltop82` | Losers Top 8 - Partido 2 |
| `lq1` | Losers Quarters 1 |
| `lq2` | Losers Quarters 2 |
| `ls` | Losers Semifinals |
| `lf` | Losers Finals |
| `gf` | Grand Finals |
| `gfr` | Grand Finals Reset |

*Ejemplo para Winners Finals:* `wfp1`, `wfp1s`, `wfp2`, `wfp2s`.

---

## 7. Cómo Modificar y Personalizar Overlays

### Cambiar Tipografías
Por defecto, los scripts utilizan la fuente **Bebas Neue**:
```js
`${fontsize}px Bebas Neue`
```
Para cambiar la tipografía:
1. Abre `browserfiles/js/scoreboard-updater.js` y `browserfiles/js/bracket-updater.js`.
2. Reemplaza `"Bebas Neue"` por tu fuente deseada (ej. `"Montserrat"`, `"Teko"`, `"Roboto"`).
3. Asegúrate de que la fuente esté instalada en el sistema operativo donde corre OBS.

### Agregar Fondos Personalizados
1. Coloca tu imagen (1920×1080 PNG con transparencia recomendado) en `browserfiles/images/`.
2. En el archivo `.html` deseado (`scoreboard.html`, `Casters.html`, etc.), descomenta o agrega al inicio de `init()`:
```js
const fondo = new createjs.Bitmap("images/tu-fondo.png");
board.addChild(fondo);
```

### Probar Overlays en Local
Dado que los navegadores bloquean peticiones locales `file://` debido a CORS, levanta un servidor HTTP local para depurar:
```bash
cd browserfiles
python -m http.server 8765
```
Luego abre `http://localhost:8765/scoreboard.html` en Chrome o Edge y usa la consola para inspeccionar errores.

---

## 8. Cómo Agregar un Campo Nuevo al Proyecto (C# + XAML + JSON + HTML)

Si deseas añadir un dato nuevo (por ejemplo, el nombre del juego o torneo actual):

1. **Modelo C# (`src/StreamControlCSharp/Models/StreamControlData.cs`):**
   ```csharp
   [JsonPropertyName("gameName")]
   public string GameName { get; set; } = "";
   ```

2. **Vista WPF (`src/StreamControlCSharp/MainWindow.xaml`):**
   Agrega el control para editarlo:
   ```xml
   <TextBox Text="{Binding Data.GameName, UpdateSourceTrigger=PropertyChanged}" />
   ```

3. **Compilar:**
   Ejecuta `compilar.bat` para regenerar `StreamControlCSharp.exe`.

4. **Overlay HTML (`browserfiles/scoreboard.html`):**
   Agrega el renderizado del nuevo campo:
   ```js
   board.addChild(newDynText("gameName", 960, 50, 600, 32, "#FFFFFF", "center"));
   ```

---

## 9. Compilación desde el Código Fuente

### Requisitos de Compilación
- Windows 10/11 x64.
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

### Compilar con `compilar.bat`
Ejecuta el archivo por lotes en la raíz:
```cmd
compilar.bat
```

### Compilar manualmente por consola
```cmd
dotnet publish "src\StreamControlCSharp\StreamControlCSharp.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o "publish"
```
Luego mueve `publish\StreamControlCSharp.exe` a la raíz del repositorio y elimina la carpeta temporal `publish`.

---

## 10. Solución de Problemas y Límites Conocidos

| Problema | Causa | Solución |
|---|---|---|
| **El overlay no se actualiza en OBS** | OBS apunta a un JSON distinto o caché | Verifica la ruta de `browserfiles/streamcontrol.json`. Clic derecho en la fuente de OBS → *Actualizar*. |
| **Overlay en blanco** | Error de sintaxis JS | Abre la vista en un navegador web mediante HTTP y revisa la consola de desarrollador (`F12`). |
| **Fondo negro en OBS** | Falta estilo de transparencia | En la fuente de OBS, define en CSS personalizado: `body { background: transparent; margin: 0; overflow: hidden; }`. |
| **La fuente se ve estándar / no cambia** | La fuente no está instalada en Windows | Instala el archivo `.ttf` o `.otf` de la fuente en el sistema y reinicia OBS. |
| **No cargan las banderas** | Nombre de país no reconocido | Usa el código de dos letras ISO (ej. `AR`, `US`, `CL`, `MX`). Consulta `browserfiles/js/countries.js`. |
| **start.gg responde 401 Unauthorized** | Token inválido o vencido | Genera un token nuevo en el panel de desarrollador de start.gg y actualízalo en `config.json`. |
| **Pérdida de cambios al cerrar la app** | Los campos de texto no guardan en tiempo real | Presiona `Ctrl + S` o **GUARDAR EN OBS** antes de cerrar la ventana. |

---

## 11. Créditos y Agradecimientos

- **[StreamControl](https://github.com/farpenoodle/StreamControl)** (por [farpenoodle](https://github.com/farpenoodle)): Proyecto original que inspiró la arquitectura, estructura de datos JSON y flujo de control para torneos y emisiones en vivo.
- **[Shieldbreakers-StreamControl-setup](https://github.com/MiggL/Shieldbreakers-StreamControl-setup)** (por [MiggL](https://github.com/MiggL)): Por el diseño base de las vistas HTML, los scripts de renderizado y animación de canvas con EaselJS/TweenJS, y la librería de banderas de países (`browserfiles/images/GoSquared/`).
