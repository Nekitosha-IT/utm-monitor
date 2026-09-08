<?php
$db = new PDO("sqlite:C:/utm-monitor/data/utm-monitor.sqlite");

echo "MARKS=" . $db->query("SELECT COUNT(*) FROM document_item_marks")->fetchColumn() . PHP_EOL;

echo "ITEMS=" . $db->query("SELECT COUNT(*) FROM document_items")->fetchColumn() . PHP_EOL;

echo PHP_EOL . "MARKS BY DOCUMENT:" . PHP_EOL;

$sql = "
SELECT
    d.id,
    d.document_type,
    d.external_document_id,
    COUNT(m.id) AS marks
FROM documents d
JOIN document_items i ON i.document_id = d.id
LEFT JOIN document_item_marks m ON m.document_item_id = i.id
GROUP BY d.id
HAVING marks > 0
ORDER BY d.id
";

foreach ($db->query($sql) as $row) {
    echo $row['id'] . " | "
       . $row['document_type'] . " | "
       . $row['external_document_id'] . " | marks="
       . $row['marks'] . PHP_EOL;
}
