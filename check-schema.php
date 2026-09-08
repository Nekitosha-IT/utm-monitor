<?php
$db = new PDO('sqlite:' . __DIR__ . '/data/utm-monitor.sqlite');
$db->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

foreach (['document_items','document_item_marks','documents'] as $table) {
    echo "`n=== $table ===`n";
    foreach ($db->query("PRAGMA table_info($table)") as $row) {
        echo implode(' | ', $row), "`n";
    }
}

echo "`n=== foreign_keys document_item_marks ===`n";
foreach ($db->query("PRAGMA foreign_key_list(document_item_marks)") as $row) {
    print_r($row);
}
?>
