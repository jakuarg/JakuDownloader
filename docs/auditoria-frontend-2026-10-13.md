# Auditoría del frontend de Jaku Downloader

Fecha: 13 de octubre de 2026.

## Contexto para el agente que retome el trabajo

Este documento registra una auditoría solicitada por el usuario, exclusivamente del frontend. No autoriza por sí mismo a implementar cambios. La solicitud original fue inspeccionar y diagnosticar sin modificar código, agregar dependencias ni refactorizar.

La auditoría examinó el directorio de trabajo, incluidos cambios sin confirmar y archivos nuevos que ya existían. No tomar HEAD como equivalente al estado auditado. No se modificaron código, dependencias ni archivos durante la auditoría; este informe se agregó después, por pedido expreso del usuario.

Las rutas son relativas a la raíz del repositorio. Las líneas corresponden al estado inspeccionado y pueden desplazarse: verificar también el símbolo y el fragmento indicado antes de actuar.

Estados de evidencia utilizados:

- **Confirmado por código:** la implementación demuestra la condición descrita. No implica haber reproducido todos sus efectos.
- **Observado durante la ejecución:** visto en la sesión de la aplicación existente.
- **Pendiente de validación:** riesgo razonado que requiere reproducción o medición.
- **Opcional:** mejora sin defecto demostrado o dependiente de requisitos aún no documentados.

## 1. Diagnóstico ejecutivo

El frontend tiene una base adecuada para la versión 0.2.0, pero necesita correcciones de robustez, accesibilidad y adaptación antes de considerarse listo para uso general. El problema principal no es el estilo visual: son los errores que pueden escapar de los comandos, el cierre durante una descarga y la diferencia entre funciones operativas y demostraciones del roadmap.

### Fortalezas verificadas

- Arquitectura pequeña y comprensible: una ventana, navegación centralizada, cuatro páginas y dos ViewModels.
- Separación razonable entre vistas y presentación. El code-behind maneja navegación, ventana y Enter; no justifica una migración arquitectónica.
- La descarga tiene validación inicial, progreso determinado e indeterminado, cancelación y resultado mediante `InfoBar`.
- `DownloadViewModel` es singleton y `DownloadPage` usa caché: navegar no crea otra operación ni descarta deliberadamente su estado.
- Los errores conservan la URL; una descarga exitosa la limpia.
- Uso coherente de recursos temáticos de WinUI, estilos compartidos y controles estándar, sin reemplazos extensos de plantillas.
- Los reportes de progreso tienen una limitación de frecuencia de 150 ms en el contrato consumido por la UI. No se encontraron `.Wait()` o `.Result` bloqueantes en el frontend escrito por el proyecto.
- Los ejemplos de Cola e Historial están identificados como ejemplos; no se presentan explícitamente como descargas reales.

### Problemas de mayor impacto

1. Operaciones de presentación que pueden fallar fuera del manejo de errores.
2. Cierre de ventana que cancela una descarga sin aviso ni espera coordinada.
3. Controles de demostración que aparentan estar deshabilitados, pero conservan comportamiento de controles habilitados.
4. Layout horizontal rígido incompatible con una adaptación confiable al tamaño mínimo admitido.
5. Accesibilidad del flujo principal insuficientemente especificada y todavía sin validación efectiva con tecnologías de asistencia.

### Limitaciones

La auditoría es principalmente estática. Se observó la ventana existente en Historial, en tema oscuro, y se examinaron las capturas de Descargar y Configuración del repositorio. La herramienta de control devolvió únicamente un árbol de accesibilidad superficial y la navegación por coordenadas no produjo el resultado esperado.

Eso **no demuestra que la app carezca de UI Automation ni que su navegación falle**. Impidió validar esas cuestiones mediante esta sesión. Tampoco se verificó que el ejecutable abierto correspondiera exactamente al código actual.

No se ejecutaron descargas, cancelaciones, borrados, cambios de configuración del sistema ni pruebas con datos reales. No se compiló ni se ejecutó una suite de pruebas. No hay métricas de rendimiento ni mediciones de contraste obtenidas en esta auditoría.

## 2. Cobertura y mapa del frontend

### Versiones reales

Verificadas en `src/JakuDownloader/JakuDownloader.csproj` y, para los paquetes principales, en `src/JakuDownloader/obj/project.assets.json`:

| Elemento | Versión/configuración |
|---|---|
| Aplicación | `0.2.0` |
| Framework | `net10.0-windows10.0.26100.0` |
| Windows mínimo declarado | `10.0.17763.0` |
| Windows App SDK | `1.8.260804001` |
| CommunityToolkit.Mvvm | `8.4.2` |
| SettingsControls | `8.2.251219` |
| Microsoft.Extensions.Hosting | `10.0.12` |
| Windows SDK BuildTools | `10.0.26100.7175` |
| Distribución | Sin empaquetar; Windows App SDK autocontenido |
| Arquitecturas declaradas | x86, x64 y ARM64 |
| Entorno inspeccionado | Windows `10.0.26100`, .NET SDK `10.0.400` |

La consulta inicial `dotnet --info` falló parcialmente por restricciones del sandbox; se repitió con aprobación y terminó correctamente. No se encontró `global.json`.

No se encontró `AGENTS.md` en la raíz, sus carpetas inspeccionadas ni el directorio padre. Se revisaron las convenciones y el alcance documentados en `README.md`.

### Organización

| Área | Implementación y responsabilidad |
|---|---|
| Entrada | `App`: recursos, contenedor DI, inicialización y apertura de ventana |
| Shell | `MainWindow`: `NavigationView`, `Frame`, barra de título y tamaño |
| Descargar | `DownloadPage` + `DownloadViewModel`: único flujo operativo principal |
| Configuración | `SettingsPage` + `SettingsViewModel`: información, carpetas y opciones futuras |
| Cola e Historial | Páginas de demostración, sin colecciones ni operaciones implementadas |
| Componente compartido | `FeatureBadge`, con propiedades de dependencia |
| Diseño | `App.xaml`: estilos de página, títulos, secciones, tarjetas y ancho de opciones |
| Presentación | MVVM Toolkit, `ObservableProperty`, `RelayCommand`, `x:Bind` |
| Localización | Cadenas españolas incrustadas; no se encontraron recursos `.resw` |
| Accesibilidad explícita | Sin declaraciones de `AutomationProperties`, access keys o aceleradores en las vistas inspeccionadas |
| Pruebas | No se encontraron proyectos ni archivos de pruebas del frontend |

No hay diálogos, ventanas secundarias, `TabView`, editores con guardado ni listas operativas que permitan evaluar selección múltiple, virtualización o conflictos de edición.

### Archivos y módulos inspeccionados

- `README.md`, `src/JakuDownloader/JakuDownloader.csproj` y paquetes resueltos de `obj/project.assets.json`.
- `src/JakuDownloader/App.xaml` y `App.xaml.cs`.
- XAML y code-behind de `MainWindow`, `DownloadPage`, `SettingsPage`, `QueuePage` e `HistoryPage`.
- `Views/Controls/FeatureBadge.xaml` y su code-behind.
- `ViewModels/DownloadViewModel.cs` y `SettingsViewModel.cs`.
- `Helpers/Features.cs`, `ShellHelper.cs`, `UrlValidator.cs`, `Formatters.cs`, `AppPaths.cs` y `YtDlpErrorTranslator.cs`.
- `Models/ToolInfo.cs`.
- Partes de `DownloadService`, `ToolLocator` y `NotificationService` necesarias para comprender el contrato con la presentación. No se realizó una auditoría del motor, backend o infraestructura.
- Capturas `docs/screenshots/descargar.png` y `docs/screenshots/configuracion.png`.

### Propósito y prioridades

El README describe un descargador de audio y video para Windows, inicialmente orientado a YouTube. En esta versión, el flujo operativo es descargar un video con las opciones predeterminadas.

La frecuencia siguiente es una **suposición de priorización**, no una métrica de usuarios:

| Prioridad | Flujo | Motivo |
|---|---|---|
| 1 | Pegar URL → descargar → seguir progreso → abrir carpeta | Tarea principal |
| 1 | Error, cancelación y cierre durante una operación | Riesgo de interrupción y pérdida de progreso |
| 2 | Consultar herramientas y destino | Recuperación y diagnóstico |
| 3 | Cola, Historial y opciones futuras | Todavía son demostraciones |

### Estados del flujo principal

| Estado | Evidencia |
|---|---|
| Inicial | URL vacía y comando Descargar no ejecutable |
| Validación | URL inválida y herramientas ausentes producen `InfoBar` |
| Carga | Texto de etapa y progreso determinado/indeterminado |
| Completado | Resultado, acceso a carpeta y notificación |
| Cancelación | “Cancelando…” y resultado de cancelación |
| Error de descarga | Resultado con URL conservada |
| Permisos/destino inaccesible | Manejo incompleto; H01 |
| Sin resultados/listas vacías | No aplica al flujo individual; Cola e Historial no están implementados |
| Recuperación tras cierre | Sin restauración de operación o URL documentada en el frontend |

### Validación práctica realizada

- Inventario de ventanas y localización de una instancia existente de Jaku Downloader.
- Activación y observación visual de Historial en tema oscuro.
- Intento de inspección UIA: solo ventana y barra de título expuestas por la herramienta. Resultado insuficiente para evaluar accesibilidad de la app.
- Intento de navegación por coordenadas a Descargar: no produjo el resultado esperado; no se consideró reproducción de un defecto.
- Revisión de las capturas guardadas de Descargar y Configuración; no equivalen a escenarios ejecutados durante esta auditoría.

Quedaron pendientes tema claro y contraste, tamaños de ventana, DPI, escalado de texto, Narrator, teclado completo, errores inducidos, listas voluminosas y operaciones lentas o concurrentes. La versión del binario abierto no se cotejó con el código fuente auditado.

## 3. Hallazgos priorizados

### H01 — Los errores de preparación y acciones auxiliares pueden escapar del manejo de presentación

- **Categoría:** robustez y feedback.
- **Severidad:** alta.
- **Evidencia:** confirmado por código; cierre inesperado pendiente de reproducción.
- **Flujos:** iniciar descarga, pegar y abrir carpetas.

**Ubicación y evidencia:**

- `src/JakuDownloader/ViewModels/DownloadViewModel.cs:84`, `DownloadAsync`: `Directory.CreateDirectory` se ejecuta antes del `try`.
- `src/JakuDownloader/ViewModels/DownloadViewModel.cs:139`, `PasteAsync`: lectura asíncrona del portapapeles sin captura.
- `src/JakuDownloader/Helpers/ShellHelper.cs:7`, `OpenFolder`: creación y apertura de carpeta sin manejo.
- `src/JakuDownloader/ViewModels/SettingsViewModel.cs:19`: comandos llaman directamente al helper.

**Comportamiento actual e impacto:** un destino sin permisos puede generar una excepción sin convertirla en un resultado visible. Problemas del portapapeles o del shell tienen el mismo vacío. El usuario puede perder la sesión en lugar de recibir una recuperación guiada.

**Comportamiento esperado y solución:** incluir la preparación dentro del límite de errores del comando; manejar fallos esperables de portapapeles y apertura de carpetas; conservar la URL y ofrecer un mensaje específico. No ocultar el error mediante una opción global del comando.

**Esfuerzo:** pequeño a mediano. **Riesgo de modificación:** bajo, cuidando el restablecimiento del estado y la liberación del token.

**Criterio de aceptación:** ante acceso denegado, carpeta no disponible o portapapeles inaccesible, la ventana sigue operativa, conserva el contenido y permite volver a intentar. No hay excepción sin manejar.

### H02 — Cerrar la ventana interrumpe la descarga sin decisión informada

- **Categoría:** ciclo de vida y prevención de pérdida de progreso.
- **Severidad:** alta.
- **Evidencia:** confirmado por código; efectos finales sobre procesos y archivos pendientes de ejecución.
- **Flujo:** cerrar durante descarga o procesamiento.

**Ubicación:** `src/JakuDownloader/Views/MainWindow.xaml.cs:37` y `src/JakuDownloader/ViewModels/DownloadViewModel.cs:163`, `CancelDownload`.

**Comportamiento actual e impacto:** `Closed` solicita cancelación, pero la ventana ya se está cerrando. No hay aviso previo ni espera de `DownloadCommand.ExecutionTask`. El usuario no puede elegir continuar y el frontend no coordina la finalización antes de salir.

**Comportamiento esperado y solución:** interceptar el cierre del sistema con `Microsoft.UI.Windowing.AppWindow.Closing`, cancelar inicialmente ese cierre y mostrar una decisión: continuar descargando o cancelar y salir. Tras confirmar, solicitar cancelación, esperar la tarea y cerrar con protección frente a reentrada. El `ContentDialog` debe usar el `XamlRoot` del contenido de la ventana. Estas APIs corresponden a WinUI 3/Windows App SDK.

Referencias: [AppWindow.Closing](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.windowing.appwindow.closing?view=windows-app-sdk-1.8), [ContentDialog](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/dialogs-and-flyouts/dialogs).

**Esfuerzo:** mediano. **Riesgo de modificación:** medio por carreras entre terminar, cancelar y cerrar.

**Criterio de aceptación:** probar X y Alt+F4 durante información, descarga y unión. Continuar conserva la operación; cancelar y salir espera su resolución. No aparecen diálogos duplicados ni se cierra antes de tiempo.

### H03 — Las demostraciones bloquean el mouse, pero no deshabilitan sus botones

- **Categoría:** teclado, semántica e interacción.
- **Severidad:** media.
- **Evidencia:** confirmado por código; recorrido real de Tab pendiente.
- **Pantallas:** Cola e Historial.

**Ubicación:** `src/JakuDownloader/Views/QueuePage.xaml:86`, también línea 139; `src/JakuDownloader/Views/HistoryPage.xaml:74`.

**Comportamiento actual e impacto:** los contenedores usan `IsHitTestVisible="False"`, mientras sus botones siguen habilitados y sin comandos. La opacidad refuerza la apariencia de deshabilitado, pero no establece ese estado. Un usuario de teclado puede encontrar acciones sin resultado.

`IsHitTestVisible` controla la detección del puntero; no sustituye el estado deshabilitado. [Referencia de Windows App SDK](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.uielement.ishittestvisible?view=windows-app-sdk-1.8).

**Comportamiento esperado y solución:** usar contenido no interactivo para las demostraciones o establecer `IsEnabled="False"` en los botones. Mantener la explicación “ejemplo” disponible para lectores de pantalla.

**Esfuerzo:** pequeño. **Riesgo de modificación:** bajo.

**Criterio de aceptación:** Tab no se detiene en acciones ficticias; la inspección UIA no las presenta como acciones disponibles; mouse y teclado transmiten el mismo estado.

### H04 — El tamaño mínimo admitido no tiene una estrategia de adaptación suficiente

- **Categoría:** layout, ventana y escalado.
- **Severidad:** media.
- **Evidencia:** restricciones confirmadas por código; recortes concretos pendientes de validación.
- **Pantallas:** todas, especialmente Descargar.

**Ubicación y evidencia:**

- `src/JakuDownloader/Views/MainWindow.xaml:45`: panel lateral de 220 y `PaneDisplayMode="Left"`.
- `src/JakuDownloader/Views/MainWindow.xaml.cs:14`: mínimo de 640 × 500, escalado al DPI inicial.
- `src/JakuDownloader/Views/DownloadPage.xaml:62`: destino horizontal sin ajuste.
- `src/JakuDownloader/Views/DownloadPage.xaml:91`: detalles de progreso en columna `Auto`.
- `src/JakuDownloader/Views/DownloadPage.xaml:171`: badge y selector de al menos 220 dentro de un panel horizontal.

**Comportamiento actual e impacto:** la navegación no se compacta automáticamente. Con el panel abierto, el ancho mínimo deja aproximadamente 348 unidades para contenido después del padding, antes de otros bordes. Es una estimación de layout, no una medición en ejecución. Varios grupos tienen tamaños deseados mayores y no reorganizan sus hijos. Es probable perder legibilidad de URL, etapa, ruta u opciones.

**Comportamiento esperado y solución:** usar `PaneDisplayMode="Auto"` y estados adaptativos para apilar entrada/acciones, destino y detalles de progreso. Reducir padding en ventanas estrechas. El cambio de `NavigationView` por sí solo no resuelve los paneles internos. [Comportamiento adaptativo oficial](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/navigationview).

**Esfuerzo:** mediano. **Riesgo de modificación:** medio, especialmente por la interacción con `SettingsCard`.

**Criterio de aceptación:** a 640 × 500 y con texto ampliado siguen visibles y operables URL, Descargar/Cancelar, etapa y resultado. Las rutas largas pueden leerse completas por un mecanismo accesible. Verificar también movimiento entre monitores: el mínimo se calcula una sola vez con el DPI inicial.

### H05 — El flujo principal no define suficientemente sus nombres y anuncios accesibles

- **Categoría:** accesibilidad.
- **Severidad:** media; elevar si la prueba confirma bloqueo.
- **Evidencia:** carencias explícitas confirmadas por código; resultado con Narrator pendiente.
- **Pantalla:** Descargar.

**Ubicación:** `src/JakuDownloader/Views/DownloadPage.xaml:24`, botón Pegar en línea 30 y progreso en línea 87.

**Comportamiento actual e impacto:**

- La URL solo tiene placeholder, sin etiqueta persistente.
- Pegar usa un glifo y tooltip, sin nombre accesible explícito.
- El progreso no tiene nombre explícito ni un mecanismo propio de anuncio de etapas.
- No hay gestión explícita del foco cuando desaparece Descargar y aparece Cancelar.

No se afirma que todos estos controles resulten anónimos: los peers estándar pueden aportar información. Tampoco se afirma que `InfoBar` necesite anuncios adicionales sin comprobar su comportamiento.

**Comportamiento esperado y solución:** añadir `Header` a la URL, nombre a Pegar y al progreso; probar el anuncio nativo del resultado. Si hace falta, anunciar cambios de etapa y finalización, evitando anunciar cada porcentaje. Comprobar foco tras iniciar y terminar.

Referencias: [Checklist de Windows](https://learn.microsoft.com/en-us/windows/apps/design/accessibility/accessibility-checklist), [WCAG 4.1.3: mensajes de estado](https://www.w3.org/WAI/WCAG22/Understanding/status-messages.html).

**Esfuerzo:** pequeño a mediano. **Riesgo de modificación:** bajo; evitar anuncios duplicados.

**Criterio de aceptación:** Narrator identifica entrada y acciones sin depender del tooltip, informa resultado y cancelación, y el foco permanece predecible. Verificar con Accessibility Insights o Inspect; no usar el árbol incompleto de esta sesión como diagnóstico.

### H06 — El roadmap ocupa el espacio principal y ofrece navegación sin tareas operativas

- **Categoría:** arquitectura de información y claridad.
- **Severidad:** media.
- **Evidencia:** confirmado por código y visible en capturas; impacto sobre usuarios no medido.
- **Pantallas:** Descargar, Configuración, Cola e Historial.

**Ubicación:** `src/JakuDownloader/Helpers/Features.cs`, `src/JakuDownloader/Views/DownloadPage.xaml:118` y `src/JakuDownloader/Views/MainWindow.xaml:54`.

**Comportamiento actual e impacto:** todos los flags futuros están en `false`. Sin embargo, las opciones y vistas previas ocupan gran parte de Descargar, y Cola e Historial aparecen como destinos principales. Los badges aclaran su disponibilidad, pero después de que el usuario dedica atención o navega.

**Comportamiento esperado y solución:** concentrar la pantalla principal en la tarea disponible y agrupar el roadmap en una sección plegada o una página informativa. Otra alternativa válida es indicar “Próximamente” desde la navegación. No hace falta eliminar el trabajo XAML existente.

**Esfuerzo:** pequeño a mediano. **Riesgo de modificación:** bajo.

**Criterio de aceptación:** desde el primer vistazo se distingue qué puede hacerse hoy; no es necesario abrir una sección para descubrir que toda su funcionalidad es futura.

Esto es un problema de carga informativa y expectativas, no una objeción a colores, tarjetas o estilo Fluent.

### H07 — Hay trabajo síncrono previo a mostrar la ventana

- **Categoría:** rendimiento percibido.
- **Severidad:** media, provisional.
- **Evidencia:** trabajo síncrono confirmado; lentitud pendiente de medición.
- **Flujo:** inicio de la aplicación.

**Ubicación:** `src/JakuDownloader/App.xaml.cs`, `OnLaunched`, llama a inicialización de notificaciones y limpieza antes de crear y activar la ventana.

**Comportamiento actual e impacto:** la UI no puede mostrar feedback durante esas operaciones. La limpieza invocada puede eliminar directorios recursivamente. Con muchos restos o almacenamiento lento, existe riesgo de arranque aparentemente detenido. No se midió una demora.

**Comportamiento esperado y solución:** medir primero; si el costo es relevante, activar la ventana y ejecutar la preparación mediante un estado explícito, bloqueando Descargar hasta terminar. No mover la limpieza a segundo plano sin coordinación: podría competir con una nueva descarga.

**Esfuerzo:** mediano. **Riesgo de modificación:** medio por orden de inicialización.

**Criterio de aceptación:** medir tiempo hasta primera ventana y hasta disponibilidad con Visual Studio Performance Profiler o WPR/WPA, usando un destino de prueba vacío y otro con restos controlados. Durante preparación lenta debe verse el estado y no admitirse una descarga prematura.

### H08 — Algunos errores ofrecen instrucciones que la versión actual no permite completar

- **Categoría:** microcopy y recuperación.
- **Severidad:** media.
- **Evidencia:** confirmado por código.
- **Flujos:** error de descarga y herramientas faltantes.

**Ubicación:**

- `src/JakuDownloader/ViewModels/DownloadViewModel.cs:80` y `:115`.
- `src/JakuDownloader/Helpers/YtDlpErrorTranslator.cs`.
- `src/JakuDownloader/Views/SettingsPage.xaml:101`.

**Comportamiento actual e impacto:** algunos errores recomiendan actualizar yt-dlp o usar una sesión, pero las funciones correspondientes están deshabilitadas. El fallback muestra errores originales o `ex.Message`, potencialmente técnicos y sin siguiente paso. “Faltan herramientas” identifica la carpeta, pero no ofrece una ruta de recuperación completa.

**Comportamiento esperado y solución:** separar explicación para el usuario de detalles técnicos copiables. Ofrecer acciones existentes —abrir carpeta, consultar instrucciones manuales, reintentar— y aclarar cuando la app todavía no admite la recuperación sugerida.

**Esfuerzo:** pequeño a mediano. **Riesgo de modificación:** bajo.

**Criterio de aceptación:** cada error frecuente explica qué pasó y una acción realizable en 0.2.0; un error desconocido conserva el diagnóstico en detalles sin convertirlo en el único mensaje.

## 4. Matriz por pantalla

Las valoraciones combinan inspección de código y evidencia visual limitada; no equivalen a aprobación funcional.

| Pantalla | Navegación | Jerarquía visual | Estados | Accesibilidad | Adaptación | Consistencia | Rendimiento |
|---|---|---|---|---|---|---|---|
| Shell | Correcto: selección y rutas simples | Correcto: ubicación clara | Requiere mejoras: cierre H02 | No evaluado: teclado real | Requiere mejoras: H04 | Correcto: shell compartido | No evaluado: arranque H07 |
| Descargar | Correcto: vista cacheada | Requiere mejoras: roadmap H06 | Requiere mejoras: H01/H08 | Requiere mejoras: H05 | Requiere mejoras: H04 | Correcto: estilos y comandos coherentes | No evaluado: sin mediciones; progreso limitado por código |
| Configuración | Correcto: acceso directo | Correcto: grupos claros en captura | Requiere mejoras: errores al abrir carpetas | No evaluado: peers del Toolkit | Requiere mejoras: grupos horizontales | Requiere mejoras: recuperación futura H08 | No evaluado: lectura síncrona de información |
| Cola | Requiere mejoras: destino futuro | Correcto: ejemplos identificados | Requiere mejoras: H03 | Requiere mejoras: H03 | Requiere mejoras: filas rígidas | Requiere mejoras: habilitación inconsistente | No evaluado: no hay lista operativa |
| Historial | Requiere mejoras: destino futuro | Correcto: ejemplo identificado | Requiere mejoras: H03 | Requiere mejoras: H03 | Requiere mejoras: acciones y metadatos en fila | Requiere mejoras: habilitación inconsistente | No evaluado: no hay búsqueda real |

### Temas, materiales y localización

El uso de `ThemeResource` es una buena base. No se verificaron contraste numérico, tema claro, contraste de Windows ni fallback de Mica. `Window.SystemBackdrop` es una vía soportada en la versión detectada; no hay motivo demostrado para reemplazarla por un controlador manual. [Documentación de materiales](https://learn.microsoft.com/en-us/windows/apps/develop/ui/system-backdrops).

Las cadenas incrustadas implican trabajo futuro si se incorporan idiomas. Sin requisito documentado de multilenguaje, extraer todo a recursos es una mejora **opcional**, no un defecto de severidad alta. RTL queda fuera del alcance confirmado.

## 5. Plan de mejora

| Orden | Acción | Hallazgos |
|---|---|---|
| Inmediato | Encerrar preparación y acciones auxiliares en manejo de errores recuperables | H01 |
| Inmediato | Coordinar cierre, decisión del usuario y finalización de la tarea | H02 |
| Inmediato | Deshabilitar realmente las acciones de demostración | H03 |
| Corto plazo | Añadir etiquetas/nombres y validar foco y anuncios | H05 |
| Corto plazo | Adaptar navegación, fila principal, destino y progreso | H04 |
| Corto plazo | Reescribir errores alrededor de recuperaciones disponibles | H08 |
| Corto plazo | Reducir protagonismo del roadmap | H06 |
| Después de medir | Separar preparación del arranque visible | H07 |

### Cambios estructurales justificados y dependencias

Conservar MVVM y los controles existentes. Para probar errores y carreras, puede ser útil introducir interfaces pequeñas alrededor de descarga, portapapeles y apertura de carpetas. El beneficio sería simular resultados de presentación; no hace falta reestructurar toda la aplicación.

Antes de habilitar cada flag futuro deben existir bindings, comandos y persistencia reales. Por ejemplo, cambiar `Features.Settings` no vuelve editable de forma persistente la plantilla, y habilitar los selectores de Descargar no conecta sus valores al comando.

Resolver el manejo de errores y la finalización de tareas antes de ampliar concurrencia o modificar el arranque. Validar los peers de los controles antes de agregar anuncios accesibles propios. Medir H07 antes de cambiar su ejecución.

No se recomienda introducir un framework de navegación, sustituir todos los `x:Bind`, crear un sistema de diseño completo ni implementar virtualización para las dos tarjetas estáticas actuales.

## 6. Ejemplos de solución

**Son ilustrativos: no fueron aplicados ni compilados.** Usan conceptos disponibles en WinUI 3 y las versiones detectadas. Su integración y compilación siguen pendientes.

### Nombre y etiqueta del flujo principal

```xml
<TextBox
    Header="Enlace del video"
    PlaceholderText="https://www.youtube.com/watch?v=..."
    IsEnabled="{x:Bind ViewModel.IsIdle, Mode=OneWay}"
    KeyDown="UrlBox_KeyDown"
    Text="{x:Bind ViewModel.Url, Mode=TwoWay,
                  UpdateSourceTrigger=PropertyChanged}" />

<Button
    AutomationProperties.Name="Pegar enlace del portapapeles"
    ToolTipService.ToolTip="Pegar del portapapeles"
    Command="{x:Bind ViewModel.PasteCommand}">
    <FontIcon FontSize="16" Glyph="&#xE77F;" />
</Button>
```

La etiqueta visible evita depender del placeholder, que desaparece al escribir. Los nombres deben verificarse con el peer real. [Texto accesible en Windows](https://learn.microsoft.com/en-us/windows/apps/design/accessibility/accessible-text-requirements).

### Acción de demostración realmente deshabilitada

```xml
<Button
    IsEnabled="False"
    AutomationProperties.Name="Pausar descarga">
    <FontIcon FontSize="16" Glyph="&#xE769;" />
</Button>
```

Para un ejemplo puramente visual, reemplazar el botón por un icono puede expresar mejor que no existe interacción.

### Límite de errores que incluye la preparación

Fragmento conceptual para reorganizar el comando existente:

```csharp
try
{
    Directory.CreateDirectory(outputDirectory);

    // Inicializar estado y ejecutar la descarga existente.
    // Mantener aqui tambien el manejo del resultado.
}
catch (UnauthorizedAccessException)
{
    ShowResult(
        InfoBarSeverity.Error,
        "No se puede escribir en la carpeta",
        "Revisá los permisos de la carpeta de destino y volvé a intentar.");
}
catch (IOException)
{
    ShowResult(
        InfoBarSeverity.Error,
        "La carpeta de destino no está disponible",
        "Comprobá que la unidad esté conectada y tenga espacio.");
}
finally
{
    _cancellation?.Dispose();
    _cancellation = null;
    IsDownloading = false;
    IsCancelling = false;
}
```

Al integrarlo deben conservarse el tratamiento específico de cancelación y el fallback de errores. La liberación pasa a ser tolerante a una preparación fallida.

Para H02, `DownloadCommand.ExecutionTask` permite esperar la operación existente sin introducir otro mecanismo de ejecución. [AsyncRelayCommand](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/asyncrelaycommand).

## 7. Checklist de aceptación

### Pruebas manuales necesarias

- [ ] URL vacía, inválida, válida y larga: validación comprensible y datos conservados.
- [ ] Descargar repetidamente o pulsar Enter rápido inicia una sola operación.
- [ ] Cancelar cambia el estado inmediatamente y termina de forma coherente.
- [ ] Cerrar durante cada etapa respeta la decisión del usuario y espera la operación.
- [ ] Navegar a Configuración y volver conserva URL, progreso y resultado.
- [ ] Recorrido completo con Tab, Shift+Tab, Enter, Espacio y Escape donde corresponda.
- [ ] No hay paradas de foco en acciones ficticias.
- [ ] Narrator identifica controles y anuncia resultados sin repetir cada actualización.
- [ ] Ventana mínima, habitual y maximizada; DPI 100 %, 150 % y 200 % cuando estén disponibles.
- [ ] Escalado de texto y movimiento entre monitores sin perder acciones esenciales.
- [ ] Tema claro, oscuro y contraste de Windows; medir texto, iconos y foco.
- [ ] Herramientas ausentes, destino inaccesible, almacenamiento lento y error desconocido usando entornos de prueba.
- [ ] Probar funcionamiento con notificaciones del sistema deshabilitadas.
- [ ] Verificar Windows mínimo declarado, barra de título y fallback de materiales.

### Automatización útil

- [ ] Pruebas de comportamiento del ViewModel para éxito, fallo y cancelación.
- [ ] Fallos simulados antes de iniciar la descarga: sin excepción no manejada y con URL conservada.
- [ ] Cancelación y finalización casi simultáneas: un único resultado coherente.
- [ ] Portapapeles lento: comprobar que su respuesta no modifica inesperadamente una operación ya iniciada. Es un escenario pendiente, no un defecto reproducido.
- [ ] Pruebas UIA de nombres, disponibilidad y recorrido de foco del flujo esencial.
- [ ] Pruebas de layout con rutas y mensajes largos.
- [ ] Medición repetible de arranque antes de decidir cambios de concurrencia.

No corresponde declarar conformidad WCAG ni accesibilidad completa con esta cobertura. La prioridad es corregir H01–H03 y después validar el flujo principal con teclado, Narrator y tamaños reducidos, cuando el usuario autorice la fase de implementación.

## 8. Estado de implementación

Agregado el 13 de octubre de 2026, al ejecutar el plan por pedido del usuario. Verificado con UI Automation sobre una compilación de prueba; no se probó con Narrator ni con tema claro.

| Hallazgo | Estado | Qué se hizo | Verificación |
|---|---|---|---|
| H01 | Resuelto | La preparación de la carpeta queda dentro del `try` con mensajes para acceso denegado y carpeta no disponible. Portapapeles y apertura de carpetas devuelven errores como mensaje (`ShellHelper.TryOpenFolder`/`TryShowInFolder`). Configuración muestra un `InfoBar` propio. | Por código. No se pudo simular un destino inaccesible porque la carpeta todavía no es configurable. |
| H02 | Resuelto | `AppWindow.Closing` cancela el cierre y pregunta “Seguir descargando” / “Cancelar y salir”. Al confirmar, espera `DownloadCommand.ExecutionTask` y cierra. Estados `Open/Confirming/Finishing/Ready` evitan reentrada. | Diálogo visible; continuar mantiene la descarga; doble cierre no duplica el diálogo; cancelar y salir cierra en 0,8 s sin procesos ni temporales. Una primera corrida tardó 7,4 s y no se reprodujo. |
| H03 | Resuelto | Botones de ejemplo con `IsEnabled="False"` y nombre accesible “(ejemplo)”; sin `IsHitTestVisible`. Textos aclaran que son ejemplos. | Los cuatro botones de Cola: habilitado y enfocable en `False`. |
| H04 | Resuelto | `PaneDisplayMode="Auto"` sin modo mínimo. Estados adaptativos bajo 720: botones debajo del link, detalles debajo de la etapa, menos padding y sin miniaturas de ejemplo. Ruta de destino recortada con tooltip y ayuda accesible. El mínimo se recalcula con `XamlRoot.Changed` al cambiar el DPI. | Capturas a 640 × 500. Movimiento entre monitores no probado. |
| H05 | Parcial | `Header` visible en el link, nombres accesibles en Pegar, Cancelar, progreso y selectores. Anuncio de cambios de etapa con `RaiseNotificationEvent`, sin porcentajes. El foco pasa a Cancelar al empezar y vuelve al link al terminar si estaba en esa fila. El resultado usa el anuncio nativo de `InfoBar`. | Pendiente validar con Narrator y Accessibility Insights. |
| H06 | Resuelto | Vista previa, opciones y extras quedan en un `Expander` plegado “Más opciones (próximamente)”. Cola e Historial muestran “Pronto” en el menú y tienen nombre accesible “próximamente”. | Captura de Descargar. |
| H07 | Medido y ajustado | Sin restos: notificaciones 75–102 ms, limpieza 1 ms, ventana ~280 ms; total 0,8–1 s desde el proceso. Con 30 restos (150 MB) la limpieza tarda 24–30 ms. La limpieza se mantiene antes de la ventana; la inicialización de notificaciones pasa a después de activarla. | Cuatro corridas con instrumentación temporal, ya retirada. |
| H08 | Resuelto | Mensajes con acciones posibles en la versión actual o aclaración de cuándo llegan. Errores desconocidos muestran un texto general y los detalles técnicos aparte, con “Copiar detalles técnicos”. El cartel ofrece “Reintentar”, “Abrir carpeta” o “Abrir carpeta tools” según el caso. | “Reintentar” relanza la descarga; la copia deja el error original en el portapapeles. |

Siguen pendientes de la sección 7: Narrator, tema claro y contraste, escalado de texto, movimiento entre monitores y pruebas automatizadas del ViewModel.
