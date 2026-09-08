<?php
$db = new PDO('sqlite:' . __DIR__ . '/data/utm-monitor.sqlite');
$db->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

$stmt = $db->prepare("SELECT raw_xml FROM documents WHERE id = ?");
$stmt->execute([10]);

$xml = $stmt->fetchColumn();

file_put_contents(__DIR__ . '/debug-waybill-10.xml', $xml);

echo "XML SIZE: " . strlen($xml) . PHP_EOL;
echo "SAVED: debug-waybill-10.xml" . PHP_EOL;
