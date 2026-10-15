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

Project is at an early scaffold stage: only the converter interface and a stub `MainViewModel` exist.

## Workflow

Feature work is spec-driven via project skills in `.agents/skills` (linked from `.claude/skills`): `/spec` designs a spec into `specs/`, `/spec-impl` implements it. Write no code during `/spec`.
