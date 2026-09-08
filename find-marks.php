<?php
$db = new PDO('sqlite:' . __DIR__ . '/data/utm-monitor.sqlite');
$db->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

echo "=== DOCUMENTS WITH ITEMS ===" . PHP_EOL;

$sql = "
SELECT
    d.id,
    d.document_type,
    d.document_id,
    d.number,
    COUNT(di.id) AS items
FROM documents d
LEFT JOIN document_items di ON di.document_id = d.id
GROUP BY d.id
ORDER BY d.id
";

foreach ($db->query($sql) as $row) {
    echo sprintf(
        "%-4s %-25s %-45s items=%s%s",
        $row['id'],
        $row['document_type'],
        (string)$row['document_id'],
        $row['items'],
        PHP_EOL
    );
}

echo PHP_EOL . "=== ALL XML TAGS RELATED TO MARKS ===" . PHP_EOL;

foreach ($db->query("
    SELECT id, document_type, raw_xml
    FROM documents
    WHERE raw_xml IS NOT NULL
") as $doc) {

    $xml = $doc['raw_xml'];

    preg_match_all('/<([A-Za-z0-9_:.-]*(?:mark|Mark|MARK|barcode|Barcode|amc|AMC|Code|code)[A-Za-z0-9_:.-]*)\\b[^>]*>/u', $xml, $m);

    $tags = array_values(array_unique($m[1] ?? []));

    if ($tags) {
        echo PHP_EOL;
        echo "DOCUMENT ID: {$doc['id']} TYPE: {$doc['document_type']}" . PHP_EOL;
        echo implode(', ', $tags) . PHP_EOL;
    }
}

echo PHP_EOL . "=== CURRENT MARK COUNT ===" . PHP_EOL;
echo $db->query("SELECT COUNT(*) FROM document_item_marks")->fetchColumn() . PHP_EOL;
