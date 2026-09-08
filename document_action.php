<?php

function actionDocument(PDO $db): void
{
    $id = isset($_GET['id']) ? (int)$_GET['id'] : 0;

    if ($id <= 0) {
        http_response_code(400);

        echo json_encode([
            'success' => false,
            'error' => 'е указан корректный id документа'
        ], JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);

        return;
    }

    /*
     * =========================
     * DOCUMENT
     * =========================
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
        ], JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);

        return;
    }

    /*
     * =========================
     * ITEMS
     * =========================
     */

    $stmt = $db->prepare("
        SELECT *
        FROM document_items
        WHERE document_id = ?
        ORDER BY item_index, id
    ");

    $stmt->execute([$id]);

    $items = $stmt->fetchAll(PDO::FETCH_ASSOC);

    $totalQuantity = 0.0;
    $totalAmount = 0.0;
    $totalMarks = 0;

    $quantityMarksMatch = true;

    foreach ($items as &$item) {

        /*
         * MARKS
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

        $markStmt->execute([
            (int)$item['id']
        ]);

        $marks = $markStmt->fetchAll(PDO::FETCH_ASSOC);

        $item['marks'] = $marks;
        $item['marks_count'] = count($marks);

        /*
         * QUANTITY / MARKS CHECK
         */

        $quantity = null;

        if ($item['quantity'] !== null) {
            $quantity = (float)$item['quantity'];
        }

        $marksCount = count($marks);

        if ($quantity !== null) {
            $matches = abs($quantity - $marksCount) < 0.000001;
        } else {
            $matches = true;
        }

        $item['quantity_marks_match'] = $matches;

        if (!$matches) {
            $quantityMarksMatch = false;
        }

        /*
         * TOTAL QUANTITY
         */

        if ($quantity !== null) {
            $totalQuantity += $quantity;
        }

        /*
         * TOTAL AMOUNT
         */

        if (
            $item['price'] !== null &&
            $quantity !== null
        ) {
            $totalAmount +=
                (float)$item['price'] *
                $quantity;
        }

        /*
         * TOTAL MARKS
         */

        $totalMarks += $marksCount;
    }

    unset($item);

    /*
     * =========================
     * STATUS HISTORY
     * =========================
     */

    $stmt = $db->prepare("
        SELECT *
        FROM document_status_history
        WHERE document_id = ?
        ORDER BY id ASC
    ");

    $stmt->execute([$id]);

    $history = $stmt->fetchAll(PDO::FETCH_ASSOC);

    /*
     * =========================
     * DOCUMENT LINKS
     * =========================
     *
     * ерём связи в обе стороны.
     * UNION складываем во внешний SELECT,
     * чтобы SQLite спокойно разрешил ORDER BY.
     */

    $stmt = $db->prepare("
        SELECT *
        FROM (
            SELECT
                dl.id AS link_id,
                dl.document_id AS document_id,
                dl.linked_document_id AS linked_document_id,
                dl.link_type AS link_type,
                dl.created_at AS link_created_at,

                d.id AS linked_id,
                d.document_type AS linked_document_type,
                d.number AS linked_number,
                d.document_date AS linked_document_date,
                d.status AS linked_status,
                d.status_code AS linked_status_code

            FROM document_links dl

            JOIN documents d
                ON d.id = dl.linked_document_id

            WHERE dl.document_id = ?

            UNION

            SELECT
                dl.id AS link_id,
                dl.linked_document_id AS document_id,
                dl.document_id AS linked_document_id,
                dl.link_type AS link_type,
                dl.created_at AS link_created_at,

                d.id AS linked_id,
                d.document_type AS linked_document_type,
                d.number AS linked_number,
                d.document_date AS linked_document_date,
                d.status AS linked_status,
                d.status_code AS linked_status_code

            FROM document_links dl

            JOIN documents d
                ON d.id = dl.document_id

            WHERE dl.linked_document_id = ?
        )
        ORDER BY link_id
    ");

    $stmt->execute([
        $id,
        $id
    ]);

    $links = $stmt->fetchAll(PDO::FETCH_ASSOC);

    /*
     * =========================
     * REMOVE RAW XML FROM ITEMS
     * =========================
     */

    foreach ($items as &$item) {
        unset($item['raw_xml']);
    }

    unset($item);

    /*
     * =========================
     * SUMMARY
     * =========================
     */

    $document['items'] = $items;

    $document['history'] = $history;

    $document['links'] = $links;

    $document['summary'] = [
        'items_count' => count($items),
        'quantity' => $totalQuantity,
        'marks_count' => $totalMarks,
        'amount' => round($totalAmount, 2),
        'quantity_marks_match' => $quantityMarksMatch
    ];

    $document['success'] = true;

    /*
     * =========================
     * JSON
     * =========================
     */

    echo json_encode(
        $document,
        JSON_UNESCAPED_UNICODE |
        JSON_UNESCAPED_SLASHES |
        JSON_PRESERVE_ZERO_FRACTION
    );
}