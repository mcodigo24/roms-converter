# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

Solution: `RomsConverter.sln` (.NET 10, nullable + implicit usings on).

```
dotnet build RomsConverter.sln
dotnet run --project src/RomsConverter.App
dotnet test
dotnet test --filter "FullyQualifiedName~ClassName.MethodName"   # single test
```

The App project targets `net10.0-windows` (WPF), so it only builds/runs on Windows.

## Architecture

Three projects, MVVM, dependencies flow one way: `App -> Core`, `Tests -> Core`.

- `src/RomsConverter.Core` — UI-free conversion logic. Conversions go behind `IFileConverter` (`Conversion/IFileConverter.cs`): `ConvertAsync(sourcePath, destinationPath, CancellationToken)`. Keep Core free of WPF references.
- `src/RomsConverter.App` — WPF shell. ViewModels in `ViewModels/` use CommunityToolkit.Mvvm source generators (`[ObservableProperty]` on `partial` classes). Views stay thin; logic belongs in ViewModels/Core.
- `tests/RomsConverter.Core.Tests` — xUnit tests for Core (`Xunit` is a global using).

MVP implementado (spec 01, `specs/01-mvp-conversor-roms.md`): convierte PlayStation (`.cue`+`.bin` -> `.chd`) y PSP (`.iso` -> `.cso`), desde archivos sueltos o `.zip/.7z/.rar`, en cola serial. Ver `README.md` para el uso.

Piezas clave de Core:

- `Conversion/`: `ConversionJob`, `JobValidator` (validaciones que bloquean un ítem), `ConversionQueue` (serie, cancelación, limpieza de temporales), `ChdConverter`/`CsoConverter` sobre la base `ExternalToolConverter`, `OutputPaths` (salida por defecto junto al original).
- `Archives/`: `ArchiveExtractor` con SharpCompress; rechaza comprimidos con cero o más de un juego.
- `Processes/`: `ProcessRunner` (stdout/stderr, cancelación matando el proceso), `ProgressParser`, `ToolLocator` (busca en `<carpeta del exe>\tools\`).

Herramientas de terceros: `chdman.exe` y `maxcso.exe` viven en `src/RomsConverter.App/tools/` y se ejecutan como procesos externos sin modificar. Las dependencias (chdman, maxcso, SharpCompress, CommunityToolkit.Mvvm) deben figurar con versión, copyright y licencia en `THIRD-PARTY-NOTICES.txt` (raíz, se copia junto al exe) y en los agradecimientos del `README.md`; al agregar o actualizar una, actualizar ambos.

Invariantes: el archivo original nunca se modifica ni se borra; nunca se sobrescribe una salida existente; la carpeta temporal del job (`%TEMP%\RomsConverter\<JobId>\`) se elimina al completar, fallar o cancelar; la UI usa el tema oscuro neutro de `Themes/Neutral.xaml` (sin fondos blancos).

## Workflow

Feature work is spec-driven via project skills in `.agents/skills` (linked from `.claude/skills`): `/spec` designs a spec into `specs/`, `/spec-impl` implements it. Write no code during `/spec`.
