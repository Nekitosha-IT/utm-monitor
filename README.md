# UTM Monitor

Нативное Windows-приложение для мониторинга УТМ ЕГАИС. PHP runtime удалён из новой архитектуры: приложение работает напрямую с УТМ по HTTP.

## Возможности
- Windows x64, C# / .NET 8 WinForms
- SQLite в `%LOCALAPPDATA%\UTMMonitor`
- несколько УТМ: добавление, изменение, удаление, включение/выключение
- параллельная проверка доступности без блокировки интерфейса
- `/api/info/list`: ONLINE/OFFLINE, HTTP, задержка, версия, FSRAR/client id
- автоматический контроль каждые 30 секунд
- документы: входящие и исходящие, локальное сохранение и обновление
- RSA/GOST: получение списка сертификатов
- DataMatrix: разбор Type/Rank/Number
- QueryBarcode: формирование XML и отправка в `/opt/in/QueryBarcode`
- журнал операций
- self-contained single-file EXE для Windows x64
- GitHub Actions автоматически собирает и публикует артефакт `UTM-Monitor-win-x64`

## Запуск
```powershell
dotnet restore .\UTMMonitor\UTMMonitor.csproj
dotnet run --project .\UTMMonitor\UTMMonitor.csproj
```

## Сборка одного EXE
```powershell
dotnet publish .\UTMMonitor\UTMMonitor.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\publish
```

После сборки запускается `publish\UTM Monitor.exe`. На целевом ПК .NET устанавливать не требуется.
