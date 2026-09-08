<?php
$files = Get-ChildItem "C:\utm-monitor\data" -Recurse -Filter "*.sqlite";
foreach ($file in $files) {
    $db = new PDO("sqlite:" . $file.FullName);
    $tables = $db->query("SELECT name FROM sqlite_master WHERE type='table'")->fetchAll(PDO::FETCH_COLUMN);

    Write-Host ""
    Write-Host "=== $($file.FullName) ===" -ForegroundColor Cyan
    Write-Host "TABLES: $($tables -join ', ')"

    if ($tables -contains "utms") {
        $rows = $db->query("SELECT id,name,ip,port,enabled FROM utms ORDER BY id")->fetchAll(PDO::FETCH_ASSOC);
        $rows | ConvertTo-Json -Depth 5
    }
}
