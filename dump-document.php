<?php
$db = new PDO('sqlite:' . __DIR__ . '/data/utm-monitor.sqlite');
$db->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

$stmt = $db->prepare("SELECT id, document_type, document_id, raw_xml FROM documents WHERE id = ?");
$stmt->execute([8]);
$row = $stmt->fetch(PDO::FETCH_ASSOC);

echo "ID: " . $row['id'] . PHP_EOL;
echo "TYPE: " . $row['document_type'] . PHP_EOL;
echo "DOCUMENT: " . $row['document_id'] . PHP_EOL;
echo "XML SIZE: " . strlen($row['raw_xml']) . PHP_EOL;

file_put_contents(__DIR__ . '/debug-document-8.xml', $row['raw_xml']);

echo "SAVED: " . __DIR__ . "/debug-document-8.xml" . PHP_EOL;
