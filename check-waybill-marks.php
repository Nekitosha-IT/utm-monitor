<?php
$db = new PDO("sqlite:C:/utm-monitor/data/utm-monitor.sqlite");

$sql = "
SELECT
    i.id,
    i.document_id,
    i.item_index,
    i.product_name,
    i.quantity,
    COUNT(m.id) AS marks
FROM document_items i
LEFT JOIN document_item_marks m
    ON m.document_item_id = i.id
WHERE i.document_id = 10
GROUP BY i.id
ORDER BY i.item_index
";

foreach ($db->query($sql) as $row) {
    echo "ITEM_ID=" . $row['id']
       . " | INDEX=" . $row['item_index']
       . " | QTY=" . $row['quantity']
       . " | MARKS=" . $row['marks']
       . " | PRODUCT=" . $row['product_name']
       . PHP_EOL;
}
