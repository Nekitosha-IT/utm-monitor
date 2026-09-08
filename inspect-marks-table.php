<?php
$db = new PDO('sqlite:C:/utm-monitor/data/utm-monitor.sqlite');
$db->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

echo "=== TABLE STRUCTURE ===" . PHP_EOL;

foreach ($db->query("PRAGMA table_info(document_item_marks)") as $r) {
    echo
        $r['cid'] . " | " .
        $r['name'] . " | " .
        $r['type'] . " | " .
        $r['notnull'] . " | " .
        $r['dflt_value'] . " | " .
        $r['pk'] . PHP_EOL;
}

echo PHP_EOL . "=== CREATE TABLE ===" . PHP_EOL;

$sql = $db->query("
    SELECT sql
    FROM sqlite_master
    WHERE type = 'table'
      AND name = 'document_item_marks'
")->fetchColumn();

echo $sql . PHP_EOL;
