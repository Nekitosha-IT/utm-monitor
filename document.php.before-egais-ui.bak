<?php
header('Content-Type: application/json; charset=utf-8');

$db = require __DIR__ . '/includes/database.php';

$id = (int)($_GET['id'] ?? 0);

if ($id <= 0) {
    http_response_code(400);
    echo json_encode([
        'success' => false,
        'error' => 'е указан id документа'
    ], JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
    exit;
}

/*
 * ============================================================
 * DOCUMENT
 * ============================================================
 */

$stmt = $db->prepare("
    SELECT *
    FROM documents
    WHERE id = ?
    LIMIT 1
");

$stmt->execute([$id]);

$document = $stmt->fetch(PDO::FETCH_ASSOC);

if (!$document) {
    http_response_code(404);

    echo json_encode([
        'success' => false,
        'error' => 'окумент не найден',
        'id' => $id
    ], JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);

    exit;
}

/*
 * ============================================================
 * ITEMS
 * ============================================================
 */

$stmt = $db->prepare("
    SELECT *
    FROM document_items
    WHERE document_id = ?
    ORDER BY item_index, id
");

$stmt->execute([$id]);

$items = $stmt->fetchAll(PDO::FETCH_ASSOC);

/*
 * ============================================================
 * MARKS
 * ============================================================
 */

$markStmt = $db->prepare("
    SELECT
        id,
        amc,
        created_at
    FROM document_item_marks
    WHERE document_item_id = ?
    ORDER BY id
");

$totalQuantity = 0.0;
$totalMarks = 0;
$totalAmount = 0.0;

foreach ($items as &$item) {

    $markStmt->execute([
        (int)$item['id']
    ]);

    $marksRows = $markStmt->fetchAll(PDO::FETCH_ASSOC);

    $marks = [];

    foreach ($marksRows as $mark) {
        $marks[] = $mark['amc'];
    }

    $item['marks'] = $marks;
    $item['marks_count'] = count($marks);

    $item['quantity'] =
        $item['quantity'] !== null
            ? (float)$item['quantity']
            : null;

    $item['price'] =
        $item['price'] !== null
            ? (float)$item['price']
            : null;

    $item['quantity_marks_match'] =
        $item['quantity'] !== null
            ? ((int)$item['quantity'] === count($marks))
            : null;

    $totalQuantity += $item['quantity'] ?? 0;
    $totalMarks += count($marks);

    if ($item['quantity'] !== null && $item['price'] !== null) {
        $totalAmount +=
            $item['quantity'] * $item['price'];
    }
}

unset($item);

/*
 * ============================================================
 * HISTORY
 * ============================================================
 */

$stmt = $db->prepare("
    SELECT
        id,
        old_status,
        new_status,
        message,
        created_at
    FROM document_status_history
    WHERE document_id = ?
    ORDER BY id ASC
");

$stmt->execute([$id]);

$history = $stmt->fetchAll(PDO::FETCH_ASSOC);

/*
 * ============================================================
 * LINKS
 * ============================================================
 */

$stmt = $db->prepare("
    SELECT
        dl.id AS link_id,
        dl.link_type,

        CASE
            WHEN dl.document_id = ?
            THEN dl.linked_document_id
            ELSE dl.document_id
        END AS related_document_id,

        d.document_id AS external_document_id,
        d.document_type,
        d.direction,
        d.number,
        d.document_date,
        d.status,
        d.status_code,
        d.sender,
        d.receiver

    FROM document_links dl

    JOIN documents d
      ON d.id = CASE
            WHEN dl.document_id = ?
            THEN dl.linked_document_id
            ELSE dl.document_id
         END

    WHERE dl.document_id = ?
       OR dl.linked_document_id = ?

    ORDER BY dl.id
");

$stmt->execute([
    $id,
    $id,
    $id,
    $id
]);

$links = $stmt->fetchAll(PDO::FETCH_ASSOC);

/*
 * ============================================================
 * CONTROL
 * ============================================================
 */

$marksMatch = true;

foreach ($items as $item) {

    if (
        $item['quantity'] !== null &&
        (int)$item['quantity'] !== (int)$item['marks_count']
    ) {
        $marksMatch = false;
        break;
    }
}

/*
 * ============================================================
 * RESULT
 * ============================================================
 */

$result = [

    'id' => (int)$document['id'],

    'utm_id' => (int)$document['utm_id'],

    'document_id' => $document['document_id'],

    'document_type' => $document['document_type'],

    'direction' => $document['direction'],

    'number' => $document['number'],

    'document_date' => $document['document_date'],

    'status' => $document['status'],

    'status_code' => $document['status_code'],

    'sender' => [
        'name' =>
            $document['sender_name']
            ?: $document['sender'],

        'short_name' =>
            $document['sender_short_name'],

        'inn' =>
            $document['sender_inn'],

        'kpp' =>
            $document['sender_kpp'],

        'reg_id' =>
            $document['sender_reg_id'],

        'address' =>
            $document['sender_address']
    ],

    'receiver' => [
        'name' =>
            $document['receiver_name']
            ?: $document['receiver'],

        'short_name' =>
            $document['receiver_short_name'],

        'inn' =>
            $document['receiver_inn'],

        'kpp' =>
            $document['receiver_kpp'],

        'reg_id' =>
            $document['receiver_reg_id'],

        'address' =>
            $document['receiver_address']
    ],

    'summary' => [

        'items_count' =>
            count($items),

        'quantity' =>
            $totalQuantity,

        'marks_count' =>
            $totalMarks,

        'amount' =>
            $totalAmount,

        'quantity_marks_match' =>
            $marksMatch
    ],

    'items' =>
        $items,

    'history' =>
        $history,

    'links' =>
        $links,

    'created_at' =>
        $document['created_at'],

    'updated_at' =>
        $document['updated_at']
];

echo json_encode(
    [
        'success' => true,
        'document' => $result
    ],
    JSON_UNESCAPED_UNICODE |
    JSON_UNESCAPED_SLASHES |
    JSON_PRETTY_PRINT
);
