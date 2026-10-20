# SPEC 01 — MVP conversor de ROMs PlayStation y PSP

> **Status:** Implementado
> **Depends on:** —
> **Date:** 2026-10-15
> **Objective:** Aplicación WPF que, tras elegir consola (PlayStation o PSP), convierte en cola juegos soltados como archivos sueltos o comprimidos (zip/7z/rar) a `.chd` (PlayStation) o `.cso` (PSP), por defecto junto al archivo original y con su mismo nombre (editable por ítem).

## Por qué existe esta spec

Es el primer MVP del proyecto. Fija el motor de conversión (herramientas externas), el modelo de cola y las validaciones, para que las specs siguientes (más consolas, ajustes) no rediseñen la base.

## Alcance

**Dentro:**

- Pantalla inicial que pregunta la consola: PlayStation o PSP.
- Zona de arrastrar y soltar (y botón "Agregar archivos") que acepta archivos sueltos y comprimidos `.zip`, `.7z`, `.rar`.
- PlayStation: entrada `.cue` + `.bin` (uno o varios `.bin` referenciados por el `.cue`), salida `.chd`.
- PSP: entrada `.iso`, salida `.cso`.
- Cola de conversión procesada en serie, un ítem a la vez.
- Ruta de salida por ítem (un archivo `.chd`/`.cso`): por defecto, la misma carpeta que el archivo de entrada y el mismo nombre base (del `.zip/.7z/.rar`, `.cue` o `.iso`). El usuario puede cambiarla por ítem.
- Tema visual neutro y de bajo contraste (sin fondos blancos).
- Validaciones que bloquean la conversión de un ítem (ver criterios de aceptación).
- Progreso por ítem y botón cancelar (cancela el ítem actual y detiene la cola).
- Herramientas `chdman.exe` y `maxcso.exe` empaquetadas junto a la app.
- El archivo original nunca se modifica ni se borra.

**Fuera de alcance (para specs futuras):**

- Otras consolas o formatos (GameCube/Wii `.rvz`, Saturn, `.pbp`, etc.).
- Conversión inversa (CHD → BIN/CUE, CSO → ISO).
- Compresión en paralelo o configurable.
- Ajustes de nivel de compresión en la UI.
- Archivos comprimidos con más de un juego (se rechazan).
- Persistencia de ajustes entre sesiones.
- Instalador, auto-actualización, localización a otros idiomas.
- Verificación de integridad contra bases de datos (Redump, No-Intro).

## Modelo de datos

```csharp
// RomsConverter.Core
public enum ConsoleKind { PlayStation, Psp }

public enum ConversionStatus { Pending, Validating, Extracting, Converting, Completed, Failed, Cancelled }

public sealed record ConversionJob(
    Guid Id,
    ConsoleKind Console,
    string SourcePath,        // archivo suelto (.cue/.iso) o comprimido
    string OutputPath);       // archivo de salida (.chd/.cso)

public sealed record ValidationResult(bool IsValid, IReadOnlyList<string> Errors);

public sealed record ConversionProgress(Guid JobId, ConversionStatus Status, double? Percent, string? Message);
```

Contratos nuevos en Core (se reutiliza `IFileConverter` existente para el paso final):

- `IFileConverter` implementado por `ChdConverter` (llama `chdman createcd -i <cue> -o <chd>`) y `CsoConverter` (llama `maxcso <iso> -o <cso>`).
- `IArchiveExtractor`: extrae `.zip/.7z/.rar` a una carpeta temporal (SharpCompress).
- `IJobValidator`: devuelve `ValidationResult` por job y consola.
- `ConversionQueue`: ejecuta jobs en serie, emite `IProgress<ConversionProgress>`.

Convenciones:

- Salida por defecto: misma carpeta que `SourcePath`, nombre base de `SourcePath` (comprimido, `.cue` o `.iso`) + extensión nueva (`.chd`/`.cso`). El usuario puede reemplazarla por ítem; la extensión debe coincidir con la consola.
- Carpeta temporal por job: `%TEMP%\RomsConverter\<JobId>\`, se borra al terminar, fallar o cancelar.
- Herramientas externas se buscan en `<carpeta del exe>\tools\`.

## Plan de implementación

1. [x] Agregar `ConsoleKind`, `ConversionJob`, `ValidationResult`, `ConversionProgress` en Core. Probar: `dotnet build` verde.
2. [x] Implementar `IJobValidator` para archivos sueltos: existencia de `.cue`/`.iso`, parseo de `FILE` del `.cue` y existencia de cada `.bin`, salida ya existente. Tests xUnit con carpetas temporales.
3. [x] Implementar `IArchiveExtractor` con SharpCompress (zip/7z/rar). Rechazar con error si el contenido tiene más de un `.cue`/`.iso`, o ninguno. Tests con zip generado en el test.
4. [x] Implementar un runner de proceso externo genérico (lanzar exe, leer stdout/stderr, parsear porcentaje, soportar `CancellationToken` matando el proceso). Test con un exe falso.
5. [x] Implementar `ChdConverter` sobre el runner. Al cancelar o fallar, borrar salida parcial.
6. [x] Implementar `CsoConverter` sobre el runner con el mismo comportamiento.
7. [x] Implementar `ConversionQueue` (serie, estados, cancelación, limpieza de temporales). Tests con convertidores falsos.
8. [x] Incluir `chdman.exe` y `maxcso.exe` en `src/RomsConverter.App/tools/` y copiarlos a la salida en el `.csproj`. Verificar que se resuelven por ruta al ejecutar.
   Agregar `THIRD-PARTY-NOTICES.txt` en la raíz del repo y copiarlo a la salida junto al exe. Debe incluir nombre, versión, copyright y texto de licencia de `chdman`, `maxcso` y sus dependencias empaquetadas, más SharpCompress y CommunityToolkit.Mvvm. Verificar cada licencia en su repo oficial al redactarlo.
9. [x] Pantalla de selección de consola en `MainViewModel` + vista (dos botones). Al elegir, pasa a la pantalla de cola.
10. [x] Pantalla de cola: drag & drop, botón agregar, lista de ítems con estado/progreso/errores, selector de carpeta de salida, botones Convertir y Cancelar, botón para volver a elegir consola (vacía la cola).

11. [x] (Revisión 2026-10-15) Salida por ítem: `ConversionJob.OutputPath` reemplaza a `OutputDirectory`; por defecto junto al original con el mismo nombre; botón "Cambiar…" por ítem; dos ítems no pueden compartir salida. Se retira el selector global de carpeta.
12. [x] (Revisión 2026-10-15) Tema visual neutro: paleta oscura suave y estilos de controles en un diccionario de recursos.

## Criterios de aceptación

- [x] Al abrir la app se muestra la pregunta de consola con opciones PlayStation y PSP, y no se puede agregar archivos antes de elegir.
- [x] Con PlayStation, soltar un `.zip` con un `.cue` y su `.bin` agrega un ítem y, al convertir, genera un `.chd` en la carpeta de salida.
- [x] Con PSP, soltar un `.zip` con un `.iso` agrega un ítem y genera un `.cso` en la carpeta de salida.
- [x] `.7z` y `.rar` equivalentes producen el mismo resultado que `.zip`.
- [x] Soltar `.cue` + `.bin` sueltos (PlayStation) o un `.iso` suelto (PSP) funciona igual que el comprimido.
- [x] Un `.cue` con varios `.bin` se convierte en un único `.chd`.
- [x] Un `.cue` cuyo `.bin` referenciado no existe marca el ítem como inválido, muestra el nombre faltante y no se convierte.
- [x] Un comprimido sin `.cue`/`.bin` (PlayStation) o sin `.iso` (PSP) se marca inválido con mensaje y no se convierte.
- [x] Un comprimido con más de un juego se marca inválido con mensaje.
- [x] Si el archivo de salida ya existe, el ítem se marca inválido y no se sobrescribe.
- [x] Soltar archivos de la consola equivocada (ej. `.iso` estando en PlayStation) se rechaza con mensaje.
- [x] Varios juegos en cola se convierten uno después de otro, nunca dos a la vez.
- [x] Cada ítem muestra porcentaje de progreso mientras convierte.
- [x] Cancelar detiene el proceso externo, borra la salida parcial y la carpeta temporal.
- [x] Los archivos de entrada originales no cambian (mismo hash antes y después).
- [x] La carpeta temporal del job no existe tras completar, fallar o cancelar.
- [x] Al agregar un ítem, su salida por defecto está en la misma carpeta que la entrada y con el mismo nombre base (zip, cue o iso) y extensión `.chd`/`.cso`.
- [x] La salida de cada ítem se puede cambiar; la conversión escribe en la ruta elegida.
- [x] Dos ítems con la misma ruta de salida se marcan inválidos con mensaje.
- [x] La interfaz no usa fondos blancos: tema neutro de bajo contraste.
- [x] `THIRD-PARTY-NOTICES.txt` existe junto al exe y lista `chdman`, `maxcso`, SharpCompress y CommunityToolkit.Mvvm con su licencia y copyright.
- [x] `dotnet test` pasa en `RomsConverter.Core.Tests`.

## Decisiones tomadas y descartadas

- **Sí:** `chdman` y `maxcso` como procesos externos. Formatos maduros y compatibles con emuladores; escribir encoders propios es costoso y riesgoso.
- **No:** encoder CHD/CSO en C# puro. Fuera de proporción para un MVP.
- **Sí:** herramientas empaquetadas en `tools/`. Funciona offline y sin configuración del usuario.
- **No:** selector de ruta de herramientas por el usuario / descarga automática. Peor UX o dependen de red.
- **Sí:** distribuir herramientas permisivas como procesos separados con `THIRD-PARTY-NOTICES.txt`. Cumple avisos de licencia sin contaminar el código propio.
- **Sí:** SharpCompress para zip/7z/rar. Solo NuGet, sin binario extra.
- **No:** `7z.exe` empaquetado. Un binario más a distribuir.
- **Sí:** cola en serie. Las herramientas ya usan varios núcleos; evita saturar CPU y disco.
- **No:** paralelo configurable. Complejidad y progreso difícil de leer.
- **Sí (revisada 2026-10-15):** salida por ítem, por defecto junto al archivo original y con su mismo nombre, editable por ítem. Reemplaza la decisión inicial de una carpeta de salida global elegida por el usuario, tras revisar el MVP.
- **No:** selector global de carpeta de salida. Obliga a elegirla siempre y mezcla juegos de orígenes distintos.
- **Sí:** un `.cue` o un `.iso` = un juego; un comprimido = un ítem.
- **Sí:** error si un comprimido contiene más de un juego. Evita ambigüedad en el MVP.
- **Sí:** no sobrescribir salidas existentes. Evita pérdida de datos.
- **Sí:** soporte de `.cue` con varios `.bin`. Muchos juegos PSX tienen pistas de audio separadas.
- **Sí:** cancelar detiene toda la cola. Más simple que cancelar un ítem y seguir.
- **Sí:** cambiar de consola vacía la cola. Evita mezclar jobs de consolas distintas.

## Riesgos identificados

| Riesgo                                                        | Mitigación                                                                                 |
| ------------------------------------------------------------- | ------------------------------------------------------------------------------------------ |
| Licencias de `chdman` (BSD-3-Clause) y `maxcso` (ISC) al redistribuirlos | Ejecutarlos como procesos separados; incluir `THIRD-PARTY-NOTICES.txt`; verificar licencias en repos oficiales. |
| Formato de salida de progreso cambia entre versiones de tools | Fijar versión empaquetada; el parseo de porcentaje es tolerante y cae a indeterminado.     |
| Archivos de varios GB llenan `%TEMP%`                         | Validar espacio libre antes de extraer; mostrar error claro si no alcanza.                 |
| `.rar` con compresión sólida o cifrado no soportado           | Capturar excepción de SharpCompress y marcar el ítem como fallido con mensaje.             |
| Antivirus bloquea o borra los ejecutables empaquetados        | Verificar existencia de `tools\*.exe` al iniciar y mostrar error accionable.               |
| `.cue` con rutas relativas o nombres con tildes/espacios      | Resolver rutas relativas al directorio del `.cue`; tests con nombres no ASCII.             |

## Lo que **no** está en esta spec

- Otras consolas o formatos de salida.
- Conversión inversa.
- Conversión en paralelo y ajustes de compresión.
- Persistencia de configuración.
- Archivos comprimidos con múltiples juegos.
- Instalador y auto-actualización.
