<?php
$db = require __DIR__ . '/includes/database.php';

function localName(string $name): string
{
    $p = strrpos($name, ':');
    return $p === false ? $name : substr($name, $p + 1);
}

function firstElement(DOMElement $root, string $wanted): ?DOMElement
{
    $xp = new DOMXPath($root->ownerDocument);

    foreach ($xp->query('.//*', $root) as $node) {
        if ($node instanceof DOMElement && localName($node->nodeName) === $wanted) {
            return $node;
        }
    }

    return null;
}

function textOf(?DOMElement $el): ?string
{
    if (!$el) {
        return null;
    }

    $v = trim($el->textContent);
    return $v === '' ? null : $v;
}

function childText(DOMElement $root, string $name): ?string
{
    foreach ($root->childNodes as $node) {
        if ($node instanceof DOMElement && localName($node->nodeName) === $name) {
            return textOf($node);
        }
    }

    return null;
}

function findDirect(DOMElement $root, string $name): ?DOMElement
{
    foreach ($root->childNodes as $node) {
        if ($node instanceof DOMElement && localName($node->nodeName) === $name) {
            return $node;
        }
    }

    return null;
}

function allElements(DOMElement $root, string $name): array
{
    $result = [];

    foreach ($root->getElementsByTagName('*') as $node) {
        if ($node instanceof DOMElement && localName($node->nodeName) === $name) {
            $result[] = $node;
        }
    }

    return $result;
}

function firstChildByLocalName(DOMElement $root, string $name): ?DOMElement
{
    foreach ($root->childNodes as $node) {
        if ($node instanceof DOMElement && localName($node->nodeName) === $name) {
            return $node;
        }
    }

    return null;
}

$docs = $db->query("
    SELECT id, document_type, raw_xml
    FROM documents
    WHERE raw_xml IS NOT NULL
      AND document_type = 'WayBill_v4'
    ORDER BY id
")->fetchAll(PDO::FETCH_ASSOC);

$update = $db->prepare("
    UPDATE document_items
    SET
        price = :price,
        fa_reg_id = :fa_reg_id,
        party_f2_reg_id = :party_f2_reg_id,
        amc_count = :amc_count,
        updated_at = :updated_at
    WHERE id = :id
");

$insertMark = $db->prepare("
    INSERT OR IGNORE INTO document_item_marks
        (document_item_id, amc, created_at)
    VALUES
        (:item_id, :amc, :created_at)
");

$itemsUpdated = 0;
$marksSaved = 0;

foreach ($docs as $doc) {

    $xml = trim((string)$doc['raw_xml']);

    if ($xml === '') {
        continue;
    }

    libxml_use_internal_errors(true);

    $dom = new DOMDocument();

    if (!$dom->loadXML($xml)) {
        echo "XML ERROR document #{$doc['id']}\n";
        continue;
    }

    $root = $dom->documentElement;

    if (!$root) {
        continue;
    }

    $positions = [];

    foreach ($root->getElementsByTagName('*') as $node) {
        if ($node instanceof DOMElement && localName($node->nodeName) === 'Position') {
            $positions[] = $node;
        }
    }

    echo "\nDOCUMENT #{$doc['id']}: positions=" . count($positions) . "\n";

    foreach ($positions as $position) {

        $identity = childText($position, 'Identity');

        if ($identity === null) {
            continue;
        }

        $itemIndex = (int)$identity - 1;

        $item = $db->prepare("
            SELECT id
            FROM document_items
            WHERE document_id = ?
              AND item_index = ?
            LIMIT 1
        ");

        $item->execute([
            (int)$doc['id'],
            $itemIndex
        ]);

        $itemRow = $item->fetch(PDO::FETCH_ASSOC);

        /*
         * а всякий случай ищем ещё по item_index + 1,
         * если старая нумерация отличалась.
         */
        if (!$itemRow) {
            $itemIndex = (int)$identity;

            $item->execute([
                (int)$doc['id'],
                $itemIndex
            ]);

            $itemRow = $item->fetch(PDO::FETCH_ASSOC);
        }

        if (!$itemRow) {
            echo "  ITEM {$identity}: DB ROW NOT FOUND\n";
            continue;
        }

        $product = findDirect($position, 'Product');

        $price = childText($position, 'Price');
        $faRegId = childText($position, 'FARegId');

        $partyF2 = childText($position, 'Party');

        $f2 = null;

        if ($partyF2) {
            $f2 = $partyF2;
        }

        if (!$f2 && $product) {
            $informF2 = firstChildByLocalName($position, 'InformF2');

            if ($informF2) {
                $f2 = childText($informF2, 'F2RegId');
            }
        }

        /*
         * Собираем все AMC внутри этой позиции.
         */
        $amcs = [];

        foreach ($position->getElementsByTagName('*') as $node) {
            if ($node instanceof DOMElement && localName($node->nodeName) === 'amc') {
                $amc = trim($node->textContent);

                if ($amc !== '') {
                    $amcs[$amc] = true;
                }
            }
        }

        $amcList = array_keys($amcs);
        $amcCount = count($amcList);

        $now = date('Y-m-d H:i:s');

        $update->execute([
            ':price' => $price !== null ? (float)$price : null,
            ':fa_reg_id' => $faRegId,
            ':party_f2_reg_id' => $f2,
            ':amc_count' => $amcCount,
            ':updated_at' => $now,
            ':id' => (int)$itemRow['id']
        ]);

        foreach ($amcList as $amc) {
            $insertMark->execute([
                ':item_id' => (int)$itemRow['id'],
                ':amc' => $amc,
                ':created_at' => $now
            ]);

            if ($insertMark->rowCount() > 0) {
                $marksSaved++;
            }
        }

        echo sprintf(
            "  ITEM %s -> DB #%d | FA=%s | F2=%s | price=%s | marks=%d\n",
            $identity,
            $itemRow['id'],
            $faRegId ?? '-',
            $f2 ?? '-',
            $price ?? '-',
            $amcCount
        );

        $itemsUpdated++;
    }
}

echo "\n=== NORMALIZATION COMPLETE ===\n";
echo "Items updated: {$itemsUpdated}\n";
echo "Marks saved:   {$marksSaved}\n";

echo "\n=== MARK STATISTICS ===\n";

$row = $db->query("
    SELECT
        COUNT(*) AS total_marks,
        COUNT(DISTINCT document_item_id) AS items_with_marks
    FROM document_item_marks
")->fetch(PDO::FETCH_ASSOC);

print_r($row);

echo "\n=== WAYBILL ITEMS ===\n";

$stmt = $db->query("
    SELECT
        i.id,
        i.item_index,
        i.quantity,
        i.price,
        i.fa_reg_id,
        i.party_f2_reg_id,
        i.amc_count,
        i.full_name
    FROM document_items i
    INNER JOIN documents d ON d.id = i.document_id
    WHERE d.document_type = 'WayBill_v4'
    ORDER BY i.document_id, i.item_index
    LIMIT 20
");

while ($row = $stmt->fetch(PDO::FETCH_ASSOC)) {
    print_r($row);
}

