# RomsConverter

Aplicación de escritorio para Windows (WPF, .NET 10) que convierte juegos de **PlayStation** y **PSP** a formatos comprimidos, listos para usar en emuladores y consolas con CFW, ocupando bastante menos espacio en disco.

## Qué hace y para qué sirve

Los juegos de disco suelen venir como imágenes sin comprimir (`.bin/.cue`, `.iso`), a menudo dentro de un `.zip`, `.7z` o `.rar`. Ocupan mucho y están repartidos en varios archivos. RomsConverter los pasa a un único archivo comprimido, sin pérdida de datos:

| Consola     | Entrada                                   | Salida | Herramienta |
|-------------|-------------------------------------------|--------|-------------|
| PlayStation | `.cue` + uno o varios `.bin`              | `.chd` | chdman      |
| PSP         | `.iso`                                    | `.cso` | maxcso      |

Sirve para:

- Ahorrar espacio: un `.chd` o `.cso` suele ocupar bastante menos que la imagen original.
- Ordenar la colección: un `.cue` con muchos `.bin` termina en un solo `.chd`.
- Convertir muchos juegos de una vez, sin tener que usar las herramientas de línea de comandos a mano.

## Cómo se usa

1. Al abrir la app, elegí la consola: **PlayStation** o **PSP**.
2. Arrastrá los archivos a la ventana (o usá **Agregar archivos**). Acepta archivos sueltos y comprimidos `.zip`, `.7z` y `.rar`.
3. Cada juego queda como un ítem de la cola. Por defecto la salida se guarda **junto al archivo original y con el mismo nombre**; podés cambiarla por ítem con **Cambiar…**.
4. Presioná **Convertir**. Los ítems se procesan de a uno, con porcentaje de progreso. **Cancelar** detiene el ítem actual y la cola.

### Validaciones

Un ítem se marca como inválido, con un mensaje, y no se convierte si:

- el `.cue` referencia un `.bin` que no existe;
- el comprimido no contiene el tipo de archivo esperado, o contiene más de un juego;
- el archivo es de la otra consola (por ejemplo un `.iso` estando en PlayStation);
- el archivo de salida ya existe (nunca se sobrescribe) o dos ítems comparten la misma salida.

Los archivos originales **nunca se modifican ni se borran**. Los comprimidos se extraen a una carpeta temporal (`%TEMP%\RomsConverter\<JobId>\`) que se elimina al terminar, fallar o cancelar.

## Compilar y ejecutar

Requiere Windows y el SDK de .NET 10.

```
dotnet build RomsConverter.sln
dotnet run --project src/RomsConverter.App
dotnet test
```

`chdman.exe` y `maxcso.exe` van empaquetados en `src/RomsConverter.App/tools/` y se copian junto al ejecutable.

## Estructura

- `src/RomsConverter.Core`: lógica de conversión sin dependencias de UI (validación, extracción, ejecución de procesos externos, cola).
- `src/RomsConverter.App`: interfaz WPF con MVVM (CommunityToolkit.Mvvm).
- `tests/RomsConverter.Core.Tests`: pruebas xUnit del Core.
- `specs/`: especificaciones de cada funcionalidad.

## Agradecimientos

Este proyecto es solo una interfaz cómoda; el trabajo pesado lo hacen estas herramientas y bibliotecas de terceros. Muchas gracias a sus autores:

- **[chdman](https://github.com/mamedev/mame)** (MAME 0.289), del equipo de **MAME** y **Aaron Giles**: crea los archivos `.chd` de PlayStation. Código de chdman bajo BSD-3-Clause; MAME como conjunto bajo GPL-2.0.
- **[maxcso](https://github.com/unknownbrackets/maxcso)** 1.13.0, de **Unknown W. Brackets**: crea los archivos `.cso` de PSP. Licencia ISC.
- **[SharpCompress](https://github.com/adamhathcock/sharpcompress)** 0.50.5, de **Adam Hathcock**: extrae `.zip`, `.7z` y `.rar`. Licencia MIT.
- **[CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet)** 8.4.2, de **.NET Foundation y colaboradores**: base MVVM de la interfaz. Licencia MIT.

`chdman` y `maxcso` se distribuyen sin modificar y se ejecutan como procesos separados. Incluyen a su vez bibliotecas (expat, zlib, zstd, FLAC, utf8proc, LZMA SDK, libdeflate, libuv, LZ4, Zopfli, 7-Zip, entre otras). El detalle completo de copyright y licencias está en [`THIRD-PARTY-NOTICES.txt`](THIRD-PARTY-NOTICES.txt), que también se copia junto al ejecutable.

## Aviso

RomsConverter no incluye ni distribuye juegos. Usalo únicamente con copias de respaldo de juegos que poseas legalmente.
