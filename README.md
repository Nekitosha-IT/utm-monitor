# UTM Monitor

Новая версия проекта — нативное Windows-приложение на **C# / .NET 8 WinForms**.

## Что уже есть
- SQLite без внешнего сервера
- до 10 УТМ
- добавление / изменение / удаление УТМ
- асинхронная проверка `/api/info/list`
- жёсткий HTTP timeout, интерфейс не блокируется
- ONLINE / OFFLINE
- версия, HTTP-код, время ответа
- автоматическая проверка каждые 10 секунд
- self-contained Windows x64 publish

## Запуск из исходников
```powershell
dotnet restore .\UTMMonitor\UTMMonitor.csproj
dotnet run --project .\UTMMonitor\UTMMonitor.csproj
```

## Один EXE
```powershell
dotnet publish .\UTMMonitor\UTMMonitor.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\publish
```

Настройки и SQLite находятся в `%LOCALAPPDATA%\UTMMonitor`.

Старый PHP-код больше не является runtime-частью нового приложения.
