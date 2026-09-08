<?php

$db = 'C:\utm-monitor\data\utm-monitor.sqlite';

if (!file_exists($db)) {
    die("DB NOT FOUND: $db\n");
}

$pdo = new PDO('sqlite:' . $db);
$pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

echo "=== DATABASE ===\n";
echo $db . "\n";
echo "SIZE: " . filesize($db) . " bytes\n\n";

echo "=== TABLES ===\n";

$tables = $pdo->query("
    SELECT name
    FROM sqlite_master
    WHERE type = 'table'
    ORDER BY name
")->fetchAll(PDO::FETCH_COLUMN);

foreach ($tables as $table) {
    echo $table . "\n";
}

echo "\n=== COUNTS ===\n";

$check = [
    'utms',
    'documents',
    'document_items',
    'document_item_marks',
    'document_status_history',
    'document_links',
    'events'
];

foreach ($check as $table) {
    if (in_array($table, $tables, true)) {
        $count = $pdo->query("SELECT COUNT(*) FROM \"$table\"")->fetchColumn();
        echo str_pad($table, 30) . " = " . $count . "\n";
    } else {
        echo str_pad($table, 30) . " = TABLE NOT FOUND\n";
    }
}

echo "\n=== UTMS ===\n";

foreach ($pdo->query("
    SELECT id, name, ip, port, external_id, enabled
    FROM utms
    ORDER BY id
") as $utm) {
    echo json_encode($utm, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES) . "\n";
}

echo "\n=== DOCUMENTS ===\n";

if (in_array('documents', $tables, true)) {
    foreach ($pdo->query("
        SELECT id, utm_id, document_id, document_type,
               direction, number, document_date, status
        FROM documents
        ORDER BY id DESC
        LIMIT 20
    ") as $doc) {
        echo json_encode($doc, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES) . "\n";
    }
}
