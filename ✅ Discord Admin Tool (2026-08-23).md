# Discord Admin Tool

## Que es
Aplicacion de escritorio (Windows) para administrar servidores de Discord a gran escala: purgar usuarios inactivos, gestionar roles masivamente, moderar miembros, limpiar canales y llevar un registro de todas las acciones. Pensada para un admin/mod unico que gestiona su propio bot.

## Como se ve y funciona
1. Al abrir la app por primera vez aparece una pantalla de bienvenida pidiendo el token del bot.
2. Se valida el token contra Discord, se guarda encriptado localmente (DPAPI de Windows) y se conecta.
3. Se elige el servidor (guild) donde esta el bot.
4. Se entra al shell principal: barra lateral con 7 secciones (Dashboard, Usuarios, Roles, Acciones Masivas, Limpiar Canales, Logs, Configuracion) y una barra superior con el estado del bot (conectado/conectando/desconectado).
5. **Dashboard**: KPIs (usuarios totales/activos/inactivos/bots, roles totales), barra de distribucion de actividad, accesos rapidos a otras secciones, actividad reciente. Se refresca solo cada 10s.
6. **Usuarios**: tabla filtrable (busqueda + estado + roles) y paginada, seleccion multiple. Con usuarios seleccionados aparecen 2 botones que abren popups:
   - **Agregar/Quitar Roles**: pestañas Agregar/Quitar, la lista de roles bloquea (deshabilita) los que no aplican segun el modo.
   - **Suspender/Banear/Expulsar**: pestañas con las 3 acciones, cada una con sus campos (duracion de suspension, dias de borrado de mensajes al banear, motivo).
7. **Roles**: lista de roles con crear/editar/eliminar, asignar/quitar/reemplazar rol de forma masiva por filtro (con filtro de inactividad opcional), backup/restauracion de roles a JSON (via selector de archivos nativo).
8. **Acciones Masivas**: purga por inactividad con preview antes de ejecutar (dry run por defecto, doble filtro mensajes/conexion) + envio de mensaje masivo por rol.
9. **Limpiar Canales**: lista todos los canales de texto/anuncios/voz e indica si tienen contenido. Boton "Limpiar" por canal abre popup: borrar todo, borrar cantidad especifica, o "Clonar y Reemplazar" (crea un canal identico y borra el original, instantaneo).
10. **Logs**: timeline de todas las acciones ejecutadas por la app, filtrable por tipo/busqueda, exportable a JSON, opcion de limpiar.
11. **Configuracion**: General/Purga/Notificaciones/Seguridad/Apariencia — incluye "Olvidar token guardado".

Todas las acciones destructivas o que afectan a >50 usuarios piden escribir "CONFIRMAR" antes de ejecutarse.

## Stack
- Avalonia 11.3.20 (C#, MVVM con CommunityToolkit.Mvvm) — reemplaza el Electron + Vanilla JS original
- Discord.Net 3.15.3 (WebSocket + Rest) — reemplaza discord.js
- Velopack — auto-actualizacion via GitHub Releases (no la tenia antes)
- `Avalonia.Controls.DataGrid` — tablas de Usuarios y Canales
- `ProtectedData` (DPAPI de Windows) — encriptacion del token del bot, en vez de `safeStorage` de Electron
- JSON plano en `%AppData%\discord-admin-tool-app\` — configuracion, logs de accion, activity/presence tracker (antes `electron-store`)

## Estructura
```
Discord Admin Tool/
├── .github/workflows/release.yml
└── project/
    └── DiscordAdminTool/
        ├── Program.cs, App.axaml(.cs), ViewLocator.cs, app.manifest
        ├── Models/ (MemberInfo, RoleInfo, ChannelInfo, LogEntry, GuildInfo, AppConfig, OperationResult, RoleBackup, ToastItem, Extra)
        ├── Services/
        │   ├── DiscordService.cs   (toda la logica de negocio contra Discord.Net)
        │   ├── ConfigStore.cs, TokenManager.cs, LogStore.cs, ActivityStore.cs, PresenceStore.cs
        │   ├── ErrorLogger.cs, UpdateService.cs
        ├── ViewModels/ (uno por seccion + MainWindowViewModel shell)
        ├── Views/
        │   ├── MainWindow.axaml(.cs)   (Welcome / GuildSelector / Main shell, todo en un solo Window)
        │   ├── DashboardView, UserManagerView, RoleManagerView, MassActionsView, ChannelCleanerView, LogsViewerView, SettingsPanelView
        │   ├── ConfirmDialogWindow, RolesPopupWindow, MemberActionPopupWindow, RoleFormPopupWindow, CleanChannelPopupWindow, ProgressPopupWindow, UpdateAvailableWindow
        │   ├── EnumEqualsConverter, StatusColorConverter, FractionToWidthConverter
```

## Archivos clave
- `Services/DiscordService.cs`: puerto 1:1 de la logica original de `discord-client.js` — cache de miembros 60s con fallback a rate limit, `RunBatchAsync` con concurrencia limitada, deteccion de inactividad combinada (mensajes + presencia + voz via `ActivityStore`/`PresenceStore`), purga por doble filtro, limpieza de canales respetando el limite de 14 dias de Discord (`bulkDelete` + borrado uno por uno con tolerancia a 2 fallos seguidos), "Clonar y Reemplazar" canal.
- `ViewModels/MainWindowViewModel.cs`: shell de la app — maneja el flujo Welcome→GuildSelector→Main, la lista de navegacion, el estado del bot y los toasts. `Navigate(key)` construye el ViewModel de cada seccion on-demand y hace `Dispose()` del anterior si implementa `IDisposable` (ej. `DashboardViewModel` detiene su timer de refresco).
- `Views/*PopupWindow.axaml(.cs)`: cada popup del original (Roles, Kick/Ban/Timeout, formulario de rol, limpieza de canal, progreso) es un `Window` modal propio mostrado via `ShowDialog<T>(owner)`, siguiendo el mismo patron reutilizable de Botrix Refill/D2Sync.
- No tiene pantalla de "Novedades" — es un proyecto 100% personal (uso exclusivo del dueño del bot), igual que estaba documentado en el original.

## Instalar y correr
Dentro de `project/DiscordAdminTool/`:
```
dotnet run
```
Build de produccion (mismo patron que los demas proyectos Avalonia):
```
dotnet publish DiscordAdminTool.csproj -c Release -r win-x64 --self-contained -o publish
vpk pack -u DiscordAdminTool -v X.Y.Z -p publish -e DiscordAdminTool.exe
```

## Env vars
Ninguna. El token del bot se ingresa desde la UI en el primer arranque y se guarda encriptado con DPAPI — nunca en archivo `.env` ni en el codigo.

## Auto-actualizacion
Si, via GitHub Releases (Velopack) — repo: `AndreDiaz11/discord-admin-tool` (publico, necesario para que el exe consulte Releases sin token embebido). No la tenia en la version Electron original.

## Novedades para el usuario
No aplica — proyecto 100% personal (uso exclusivo del dueño del bot).

## Despliegue
Standby local por ahora — pendiente de preguntar si se publica ya un primer Release v1.0.0.

## Claves secretas
Ninguna en el cliente. El token del bot vive encriptado localmente (DPAPI), nunca se sube a ningun lado.

## Estado
Funcional: si — verificado en vivo por el usuario contra un bot y servidor reales (147 usuarios, lista de canales, conexion estable) | Beta: no (cerrado) | Ultima revision: ajustes de diseño en tabla de Usuarios y Canales tras feedback visual real.

## Integraciones externas
Discord API (via Discord.Net) — bot creado en el Discord Developer Portal. Requiere:
- Intents privilegiados: `Server Members Intent`, `Message Content Intent`, `Presence Intent`
- Permisos del rol del bot en el servidor: Expulsar miembros, Banear miembros, Suspender miembros, Gestionar roles, Gestionar mensajes, Ver canales, Leer historial de mensajes, Enviar mensajes
- El rol del bot debe estar por encima en la jerarquia de cualquier rol que se quiera asignar/quitar/gestionar

Sin credenciales de terceros adicionales.

## Escalabilidad
- Nueva seccion: crear `XxxViewModel`/`XxxView.axaml`, agregarla a `MainWindowViewModel.NavItems` y al switch de `Navigate()`.
- Nuevo popup: seguir el patron `Window` + `ShowDialog<T>(owner)` de los popups existentes.
- Nuevo canal IPC del original no aplica — ya no hay proceso main/renderer separados, todo es una sola app .NET.

## Compatibilidad
Solo Windows x64 (self-contained via Velopack, igual que Botrix Refill y D2Sync).

## Datos de prueba
No aplica — opera en vivo contra la API de Discord real.

## Snapshot — estado anterior (Electron, previo a esta migracion)
- Stack: Electron 28 + discord.js 14 + Vanilla JS/Webpack + electron-store + `safeStorage`.
- 3634 lineas de JS repartidas en `src/main` (discord-client.js de 948 lineas con toda la logica de negocio), `src/renderer/components` (10 componentes de UI hechos a mano con `innerHTML`), `src/preload`, `src/shared`.
- Empaquetado con electron-builder a un `.exe` portable sin auto-actualizacion.
- Codigo viejo eliminado por completo de este repo tras la migracion (reescritura total, no quedaba nada reutilizable al cambiar de lenguaje).

## Version
1.0.5 — segunda pasada de diseño tras feedback real: roles limitados a 2 + contador "+N" (se quitó el scroll horizontal, se veía mal), columna Canal muestra el nombre exacto de Discord sin icono agregado encima, columna Tipo con badge propio.

## Cambios
1. (23/08/2026) Ajustes de diseño tras feedback del usuario viendo la app real: en Usuarios, la columna Roles ahora muestra maximo 2 chips + un chip "+N" con el resto en el tooltip (el scroll horizontal anterior se veia mal, tapaba el texto). En Limpiar Canales, la columna Canal ya no antepone un icono propio — muestra el nombre tal cual esta en Discord (los servidores suelen tener su propio emoji/separador en el nombre); el icono de tipo (texto/voz/anuncio) se movio a un badge propio en la columna Tipo.
2. (23/08/2026) Rediseño de la tabla de Usuarios a pedido del usuario tras la primera verificacion visual real: columna "Usuario" ahora muestra el avatar de Discord (`Behaviors/WebImageBehavior.cs`, mismo patron que Botrix Refill), badges de "Estado" rediseñados con fondo solido del color de la variante (verde/ambar/rojo) y texto blanco/oscuro segun contraste, en vez del fondo oscuro con texto de color que se veia apagado.
3. (23/08/2026) Fix real de las tablas vacias: agregado `<StyleInclude Source="avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml" />` en `App.axaml` — el `DataGrid` de Avalonia no trae sus estilos por defecto con `FluentTheme`, hay que registrarlos aparte.
4. (23/08/2026) Fix: `DataGrid` de Usuarios y de Canales no renderizaba ninguna fila — bug conocido de Avalonia: un `DataGrid` dentro de un `ScrollViewer`/`StackPanel` recibe altura infinita y decide no dibujar filas aunque `ItemsSource` tenga datos (la paginacion sí mostraba el total correcto). Se le puso `Height` fija a ambos `DataGrid` para que puedan virtualizar y mostrar las filas.
5. (23/08/2026) Fix critico: la app crasheaba al abrir (y tambien justo despues de conectar el bot). Causa: los botones de seleccion de servidor y de navegacion usaban un binding `$parent[ItemsControl].((vm:Tipo)DataContext)` que Avalonia no lograba resolver en tiempo de ejecucion (aunque compilaba sin errores) — se reemplazo por `$parent[Window].DataContext.Comando` / `$parent[UserControl].DataContext.Comando`, sin necesidad del cast explicito.
6. (23/08/2026) Migracion completa de Electron/discord.js a Avalonia/Discord.Net (C#): las 7 secciones originales, toda la logica de negocio de `discord-client.js` portada 1:1, todos los popups (Roles, Kick/Ban/Timeout, formulario de rol, limpieza de canal con progreso en vivo, confirmaciones con "CONFIRMAR"). Se agrego el pipeline de auto-actualizacion que no tenia (GitHub Releases + Velopack). Repo git creado desde cero. Renombrado el proyecto de "Discord bot management" a "Discord Admin Tool" (nombre final de la app) a pedido del usuario, sin referencias a marcas externas.
