<?php

$db = new PDO('sqlite:C:/utm-monitor/data/utm-monitor.sqlite');

$sql = "
    SELECT
        id,
        full_name,
        producer_full_name
    FROM document_items
    WHERE id IN (728,729,730,731)
    ORDER BY id
";

$rows = $db->query($sql)->fetchAll(PDO::FETCH_ASSOC);

foreach ($rows as $row) {
    echo "ITEM " . $row['id'] . PHP_EOL;
    echo "FULL: " . $row['full_name'] . PHP_EOL;
    echo "PRODUCER: " . $row['producer_full_name'] . PHP_EOL;
    echo "HEX: " . bin2hex(substr($row['full_name'], 0, 30)) . PHP_EOL;
    echo PHP_EOL;
}
