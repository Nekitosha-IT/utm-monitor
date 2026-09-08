<?php

declare(strict_types=1);

function saveDocument(
    PDO $db,
    int $utmId,
    ?string $documentId,
    ?string $documentType,
    string $direction,
    ?string $number,
    ?string $documentDate,
    ?string $sender,
    ?string $receiver,
    ?string $status,
    ?string $statusCode,
    ?string $rawXml
): int {
    $documentId = $documentId !== null && trim($documentId) !== '' ? trim($documentId) : null;
    $existing = null;
    if ($documentId !== null) {
        $s = $db->prepare('SELECT id,status FROM documents WHERE utm_id=? AND document_id=? LIMIT 1');
        $s->execute([$utmId, $documentId]);
        $existing = $s->fetch() ?: null;
    }

    if ($existing) {
        $oldStatus = $existing['status'];
        $s = $db->prepare('UPDATE documents SET document_type=?,direction=?,number=?,document_date=?,status=?,status_code=?,sender=?,receiver=?,raw_xml=?,updated_at=CURRENT_TIMESTAMP WHERE id=?');
        $s->execute([$documentType,$direction,$number,$documentDate,$status,$statusCode,$sender,$receiver,$rawXml,(int)$existing['id']]);
        if ($status !== null && $status !== $oldStatus) {
            $h = $db->prepare('INSERT INTO document_status_history(document_id,old_status,new_status,message) VALUES(?,?,?,?)');
            $h->execute([(int)$existing['id'],$oldStatus,$status,'Статус изменён при синхронизации']);
        }
        return (int)$existing['id'];
    }

    $s = $db->prepare('INSERT INTO documents(utm_id,document_id,document_type,direction,number,document_date,status,status_code,sender,receiver,raw_xml) VALUES(?,?,?,?,?,?,?,?,?,?,?)');
    $s->execute([$utmId,$documentId,$documentType,$direction,$number,$documentDate,$status,$statusCode,$sender,$receiver,$rawXml]);
    return (int)$db->lastInsertId();
}

function addEvent(PDO $db, ?int $utmId, string $level, string $eventType, string $message, ?string $details = null): void
{
    $s = $db->prepare('INSERT INTO events(utm_id,level,event_type,message,details) VALUES(?,?,?,?,?)');
    $s->execute([$utmId,$level,$eventType,$message,$details]);
}

function getDocument(PDO $db, int $id): ?array
{
    $s = $db->prepare('SELECT * FROM documents WHERE id=? LIMIT 1');
    $s->execute([$id]);
    $row = $s->fetch();
    return $row ?: null;
}
