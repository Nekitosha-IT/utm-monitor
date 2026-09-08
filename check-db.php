<?php

$db = new PDO('sqlite:C:\utm-monitor\data\utm-monitor.sqlite');
$db->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

echo "=== document_items ===" . PHP_EOL;

foreach ($db->query('PRAGMA table_info(document_items)') as $r) {
    echo $r['name'] . ' | ' . $r['type'] . PHP_EOL;
}

echo PHP_EOL . "=== documents ===" . PHP_EOL;

foreach ($db->query('PRAGMA table_info(documents)') as $r) {
    echo $r['name'] . ' | ' . $r['type'] . PHP_EOL;
}

echo PHP_EOL . "=== tables ===" . PHP_EOL;

foreach ($db->query("SELECT name FROM sqlite_master WHERE type='table' ORDER BY name") as $r) {
    echo $r['name'] . PHP_EOL;
}
