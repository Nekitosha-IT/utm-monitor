<?php
$db = require __DIR__ . '/includes/database.php';

echo "=== DOCUMENT COUNTS ===\n";
foreach ($db->query("
    SELECT document_type, COUNT(*) cnt
    FROM documents
    GROUP BY document_type
    ORDER BY document_type
") as $r) {
    echo $r['document_type'] . " = " . $r['cnt'] . PHP_EOL;
}

echo "\n=== ITEMS BY DOCUMENT ===\n";
foreach ($db->query("
    SELECT
        d.id,
        d.document_type,
        d.number,
        COUNT(i.id) AS items,
        COALESCE(SUM(i.quantity),0) AS quantity
    FROM documents d
    LEFT JOIN document_items i ON i.document_id = d.id
    GROUP BY d.id
    ORDER BY d.id
") as $r) {
    echo sprintf(
        "#%d %-22s number=%-20s items=%d qty=%s\n",
        $r['id'],
        $r['document_type'],
        $r['number'] ?? '',
        $r['items'],
        $r['quantity']
    );
}

echo "\n=== WAYBILL / FORM2 ===\n";
$stmt = $db->query("
    SELECT
        id,
        document_type,
        document_id,
        number,
        document_date,
        sender,
        receiver,
        status,
        status_code
    FROM documents
    WHERE document_type IN ('WayBill_v4','FORM2REGINFO','TTNHISTORYF2REG')
    ORDER BY id
");

while ($r = $stmt->fetch()) {
    echo "\n--- DOCUMENT #{$r['id']} {$r['document_type']} ---\n";
    print_r($r);
}

echo "\n=== ITEMS FOR WAYBILL / FORM2 ===\n";
$stmt = $db->query("
    SELECT
        i.*
    FROM document_items i
    INNER JOIN documents d ON d.id = i.document_id
    WHERE d.document_type IN ('WayBill_v4','FORM2REGINFO')
    ORDER BY i.document_id, i.item_index
    LIMIT 30
");

while ($r = $stmt->fetch()) {
    print_r($r);
}
