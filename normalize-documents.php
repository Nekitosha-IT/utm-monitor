<?php

$db = require __DIR__ . '/includes/database.php';

$db->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

function localName(string $name): string
{
    $p = strrpos($name, ':');
    return $p === false ? $name : substr($name, $p + 1);
}

function firstElement(?DOMElement $root, string $wanted): ?DOMElement
{
    if (!$root) {
        return null;
    }

    foreach ($root->getElementsByTagName('*') as $el) {
        if (localName($el->nodeName) === $wanted) {
            return $el;
        }
    }

    return null;
}

function childText(?DOMElement $root, string $wanted): ?string
{
    if (!$root) {
        return null;
    }

    foreach ($root->childNodes as $child) {
        if ($child instanceof DOMElement && localName($child->nodeName) === $wanted) {
            return trim($child->textContent);
        }
    }

    return null;
}

function textFromAny(?DOMElement $root, string $wanted): ?string
{
    $el = firstElement($root, $wanted);
    return $el ? trim($el->textContent) : null;
}

function attr(?DOMElement $el, string $name): ?string
{
    if (!$el || !$el->hasAttribute($name)) {
        return null;
    }

    return trim($el->getAttribute($name));
}

function parseXml(string $xml): ?DOMDocument
{
    $dom = new DOMDocument();

    libxml_use_internal_errors(true);

    if (!$dom->loadXML($xml)) {
        return null;
    }

    libxml_clear_errors();

    return $dom;
}

function findByLocalName(DOMDocument $dom, string $wanted): ?DOMElement
{
    foreach ($dom->getElementsByTagName('*') as $el) {
        if (localName($el->nodeName) === $wanted) {
            return $el;
        }
    }

    return null;
}

function findDirectChild(?DOMElement $root, string $wanted): ?DOMElement
{
    if (!$root) {
        return null;
    }

    foreach ($root->childNodes as $child) {
        if ($child instanceof DOMElement && localName($child->nodeName) === $wanted) {
            return $child;
        }
    }

    return null;
}

function parseParty(?DOMElement $party): array
{
    if (!$party) {
        return [
            'inn' => null,
            'kpp' => null,
            'reg_id' => null,
            'name' => null,
            'short_name' => null,
            'address' => null,
        ];
    }

    $ul = firstElement($party, 'UL');

    if (!$ul) {
        $ul = $party;
    }

    $addressParts = [];

    foreach ($ul->getElementsByTagName('*') as $el) {
        $name = localName($el->nodeName);

        if (in_array($name, [
            'Country',
            'RegionCode',
            'Description',
            'Index',
            'City',
            'Street',
            'House',
            'Building',
            'Flat'
        ], true)) {
            $value = trim($el->textContent);

            if ($value !== '') {
                $addressParts[] = $name . '=' . $value;
            }
        }
    }

    return [
        'inn' => textFromAny($ul, 'INN'),
        'kpp' => textFromAny($ul, 'KPP'),
        'reg_id' => textFromAny($ul, 'ClientRegId'),
        'name' => textFromAny($ul, 'FullName'),
        'short_name' => textFromAny($ul, 'ShortName'),
        'address' => $addressParts ? implode(', ', $addressParts) : null,
    ];
}

function findParty(DOMElement $header, array $names): ?DOMElement
{
    foreach ($header->getElementsByTagName('*') as $el) {
        $name = localName($el->nodeName);

        if (in_array($name, $names, true)) {
            return $el;
        }
    }

    return null;
}

$documents = $db->query("
    SELECT id, document_id, document_type, raw_xml
    FROM documents
    WHERE raw_xml IS NOT NULL
      AND TRIM(raw_xml) <> ''
    ORDER BY id
")->fetchAll(PDO::FETCH_ASSOC);

$update = $db->prepare("
    UPDATE documents
    SET
        sender = COALESCE(?, sender),
        receiver = COALESCE(?, receiver),
        sender_inn = ?,
        sender_kpp = ?,
        sender_reg_id = ?,
        sender_name = ?,
        sender_short_name = ?,
        sender_address = ?,
        receiver_inn = ?,
        receiver_kpp = ?,
        receiver_reg_id = ?,
        receiver_name = ?,
        receiver_short_name = ?,
        receiver_address = ?,
        updated_at = ?
    WHERE id = ?
");

$normalized = 0;

foreach ($documents as $doc) {

    if (!in_array($doc['document_type'], [
        'WayBill_v4',
        'FORM2REGINFO'
    ], true)) {
        continue;
    }

    $dom = parseXml($doc['raw_xml']);

    if (!$dom) {
        echo "DOCUMENT #{$doc['id']}: invalid XML\n";
        continue;
    }

    $header = findByLocalName($dom, 'Header');

    if (!$header) {
        echo "DOCUMENT #{$doc['id']}: Header not found\n";
        continue;
    }

    $shipper = findParty($header, [
        'Shipper',
        'ShipperInfo'
    ]);

    $receiver = findParty($header, [
        'Consignee',
        'Receiver',
        'ConsigneeInfo'
    ]);

    $sender = parseParty($shipper);
    $receiverData = parseParty($receiver);

    /*
     * FORM2REGINFO может иметь другую структуру.
     * сли в Header не нашли стороны — ищем по всему XML.
     */
    if (!$sender['inn']) {
        $sender['inn'] = textFromAny($dom->documentElement, 'INN');
    }

    $now = date('c');

    $update->execute([
        $sender['name'],
        $receiverData['name'],

        $sender['inn'],
        $sender['kpp'],
        $sender['reg_id'],
        $sender['name'],
        $sender['short_name'],
        $sender['address'],

        $receiverData['inn'],
        $receiverData['kpp'],
        $receiverData['reg_id'],
        $receiverData['name'],
        $receiverData['short_name'],
        $receiverData['address'],

        $now,
        $doc['id']
    ]);

    echo "\nDOCUMENT #{$doc['id']} {$doc['document_type']}\n";

    echo "  SENDER:\n";
    echo "    Name: {$sender['name']}\n";
    echo "    INN:  {$sender['inn']}\n";
    echo "    KPP:  {$sender['kpp']}\n";
    echo "    REG:  {$sender['reg_id']}\n";

    echo "  RECEIVER:\n";
    echo "    Name: {$receiverData['name']}\n";
    echo "    INN:  {$receiverData['inn']}\n";
    echo "    KPP:  {$receiverData['kpp']}\n";
    echo "    REG:  {$receiverData['reg_id']}\n";

    $normalized++;
}

echo "\n=== DOCUMENT NORMALIZATION COMPLETE ===\n";
echo "Documents normalized: {$normalized}\n";

/*
 * ============================================================
 * Создаём связи документов
 * ============================================================
 */

$docs = $db->query("
    SELECT
        id,
        document_id,
        document_type,
        number,
        raw_xml
    FROM documents
    ORDER BY id
")->fetchAll(PDO::FETCH_ASSOC);

$byIdentity = [];
$byWbRegId = [];
$byNumber = [];

foreach ($docs as $doc) {

    $dom = $doc['raw_xml'] ? parseXml($doc['raw_xml']) : null;

    if (!$dom) {
        continue;
    }

    foreach ([
        'Identity',
        'WBRegId',
        'WBNUMBER',
        'EGAISFixNumber'
    ] as $field) {

        $value = textFromAny($dom->documentElement, $field);

        if (!$value) {
            continue;
        }

        if ($field === 'Identity') {
            $byIdentity[$value][] = $doc['id'];
        }

        if ($field === 'WBRegId') {
            $byWbRegId[$value][] = $doc['id'];
        }

        if ($field === 'WBNUMBER') {
            $byNumber[$value][] = $doc['id'];
        }
    }
}

$link = $db->prepare("
    INSERT OR IGNORE INTO document_links
    (
        document_id,
        linked_document_id,
        link_type,
        created_at
    )
    VALUES (?, ?, ?, ?)
");

$links = 0;

function addLink(
    PDOStatement $stmt,
    int $a,
    int $b,
    string $type,
    int &$counter
): void {
    if ($a === $b) {
        return;
    }

    $stmt->execute([
        $a,
        $b,
        $type,
        date('c')
    ]);

    if ($stmt->rowCount() > 0) {
        $counter++;
    }
}

/*
 * WayBill <-> FORM2REGINFO
 *
 * ба документа имеют одинаковый Identity.
 */
foreach ($byIdentity as $identity => $ids) {

    $waybills = [];
    $forms = [];

    foreach ($ids as $id) {
        $type = null;

        foreach ($docs as $d) {
            if ((int)$d['id'] === (int)$id) {
                $type = $d['document_type'];
                break;
            }
        }

        if ($type === 'WayBill_v4') {
            $waybills[] = $id;
        }

        if ($type === 'FORM2REGINFO') {
            $forms[] = $id;
        }
    }

    foreach ($waybills as $wb) {
        foreach ($forms as $form) {
            addLink(
                $link,
                (int)$wb,
                (int)$form,
                'FORM2REGINFO',
                $links
            );

            addLink(
                $link,
                (int)$form,
                (int)$wb,
                'WAYBILL',
                $links
            );

            echo "LINK: WayBill #{$wb} <-> FORM2REGINFO #{$form}\n";
        }
    }
}

/*
 * WayBill / FORM2REGINFO <-> TTNHISTORYF2REG
 *
 * Связываем по WBRegId.
 */
foreach ($byWbRegId as $wbRegId => $ids) {

    $waybills = [];
    $forms = [];
    $history = [];

    foreach ($ids as $id) {

        foreach ($docs as $d) {
            if ((int)$d['id'] !== (int)$id) {
                continue;
            }

            if ($d['document_type'] === 'WayBill_v4') {
                $waybills[] = $id;
            }

            if ($d['document_type'] === 'FORM2REGINFO') {
                $forms[] = $id;
            }

            if ($d['document_type'] === 'TTNHISTORYF2REG') {
                $history[] = $id;
            }
        }
    }

    foreach ($history as $h) {

        foreach ($waybills as $wb) {
            addLink(
                $link,
                (int)$wb,
                (int)$h,
                'F2_HISTORY',
                $links
            );

            addLink(
                $link,
                (int)$h,
                (int)$wb,
                'WAYBILL',
                $links
            );

            echo "LINK: WayBill #{$wb} <-> TTNHISTORYF2REG #{$h}\n";
        }

        foreach ($forms as $form) {
            addLink(
                $link,
                (int)$form,
                (int)$h,
                'F2_HISTORY',
                $links
            );

            addLink(
                $link,
                (int)$h,
                (int)$form,
                'FORM2REGINFO',
                $links
            );

            echo "LINK: FORM2REGINFO #{$form} <-> TTNHISTORYF2REG #{$h}\n";
        }
    }
}

/*
 * оказываем итоговые связи.
 */
echo "\n=== DOCUMENT LINKS ===\n";

$rows = $db->query("
    SELECT
        dl.id,
        dl.document_id,
        d1.document_type AS document_type,
        d1.number AS document_number,
        dl.linked_document_id,
        d2.document_type AS linked_type,
        d2.number AS linked_number,
        dl.link_type
    FROM document_links dl
    JOIN documents d1 ON d1.id = dl.document_id
    JOIN documents d2 ON d2.id = dl.linked_document_id
    ORDER BY dl.id
")->fetchAll(PDO::FETCH_ASSOC);

foreach ($rows as $row) {
    echo sprintf(
        "#%d: %s #%s -> %s #%s [%s]\n",
        $row['id'],
        $row['document_type'],
        $row['document_number'] ?? '-',
        $row['linked_type'],
        $row['linked_number'] ?? '-',
        $row['link_type']
    );
}

echo "\n=== COMPLETE ===\n";
echo "New links: {$links}\n";
echo "Total links: " . count($rows) . "\n";
