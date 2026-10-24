# Contexto del proyecto: Overlay de rendimiento y tareas de Claude

> Nombre provisional: por definir. Evitar nombres o logos que sugieran afiliación con Anthropic.

## Objetivo
Aplicación de escritorio para Windows, muy ligera, en una ventana pequeña siempre encima de otras apps. Muestra métricas del equipo y el estado de las tareas que Claude (vía Claude Code o agentes propios) está ejecutando o ha terminado.

## Alcance
- Proyecto de portafolio open source en GitHub (README, GIFs, releases).
- Solo Windows en la primera versión.
- Fuera de alcance: historial de chats de claude.ai (no hay API pública; no se hace scraping).

## Stack
- C# con .NET y WPF (ventana sin bordes, transparente, `Topmost`).
- LibreHardwareMonitor (NuGet, licencia MPL 2.0) para temperaturas y sensores.
- Python solo para scripts auxiliares de hooks, si hace falta.
- Spring Boot queda fuera: no aporta a este proyecto.

## Métricas del equipo
CPU (uso y temperatura), GPU (uso, VRAM, temperatura), RAM, disco (lectura/escritura) y red (tráfico).

Nota: las temperaturas requieren ejecutar como administrador. Sin permisos, la app debe degradarse con elegancia (mostrar "N/D") en lugar de fallar.

## Tareas de Claude
Por cada tarea se muestra: **nombre, estado, duración y progreso**.

- Estados previstos: en curso, esperando al usuario, terminada, con error.
- Duración: se calcula en la app entre el evento de inicio y el de fin.
- Progreso: los hooks no entregan un porcentaje por sí solos. Opciones a validar en la documentación oficial (docs.claude.com):
  1. Derivarlo de la lista de pasos de la tarea (completados / totales) si los eventos de herramientas lo permiten.
  2. Mostrar un indicador por estado (sin porcentaje) como alternativa.
  Riesgo abierto: confirmar qué eventos existen realmente antes de prometer porcentajes.

## Fuente de datos (decisión)
**Hooks de Claude Code**, configurados por el propio usuario. La app solo recibe lo que el hook envía.

Principios:
- El hook envía solo metadatos (nombre, estado, hora, proyecto), nunca el contenido de prompts ni respuestas.
- Comunicación solo local: `127.0.0.1` o tubería con nombre, con un token simple para evitar eventos falsos de otras apps.
- Sin telemetría. Nada sale de la máquina. Esto se declara en el README.
- Interfaz de "fuentes" intercambiable, para sumar luego agentes propios (API/SDK) sin reescribir.
- No leer tokens ni credenciales, no interceptar tráfico, no depender de formatos internos no documentados. Si se usan los registros locales de sesión, es una opción secundaria y tolerante a fallos.

## Requisitos de ligereza (objetivos a medir, no promesas)
- Uso de CPU en reposo cercano a 0-1%.
- RAM en el orden de decenas de MB.
- Muestreo lento y configurable (por ejemplo, cada 1-2 s), y pausar el muestreo cuando la ventana esté oculta o minimizada.
- Activar solo los sensores necesarios en LibreHardwareMonitor (cada tipo de hardware habilitado cuesta).
- Sin animaciones continuas ni redibujado innecesario; actualizar la interfaz solo cuando cambie un valor.
- Medir con el Administrador de tareas y un perfilador antes de cada release, y registrar los números en el README.

## Ventana
- Siempre encima de otras aplicaciones.
- Pequeña, arrastrable, sin bordes, con transparencia ajustable.
- Icono en bandeja: mostrar/ocultar, restablecer posición y salir. Con la ventana oculta, los eventos de tareas se avisan con una notificación de la bandeja.
- Descartado por ahora: click-through y modo compacto/expandido.

## Arquitectura (borrador)
1. `Sources/`: receptores de eventos (hooks vía servidor local).
2. `Metrics/`: lectura de sensores con muestreo configurable.
3. `Core/`: modelo de tareas (nombre, estado, inicio, fin, progreso) y almacenamiento en memoria con un historial corto.
4. `UI/`: ventana overlay en WPF.

## Fases
1. Overlay con CPU y RAM, arrastrable y siempre encima.
2. Resto de métricas (GPU, disco, red, temperaturas) con degradación sin admin.
3. Servidor local de eventos y lista de tareas con estado y duración.
4. Progreso de tareas, notificaciones y configuración.
5. Medición de rendimiento, README con GIFs, empaquetado y release.

## Legal y buenas prácticas
- Revisar los términos de uso de Anthropic y las licencias de las dependencias antes de publicar.
- No es asesoría legal.
- No usar marcas ni logos de terceros sin permiso.

## Decisiones pendientes
- Nombre del proyecto.
- Resuelto: el progreso se deriva de los eventos `TaskCreated`/`TaskCompleted` (completadas/totales); sin ellos, solo se muestra el estado.
