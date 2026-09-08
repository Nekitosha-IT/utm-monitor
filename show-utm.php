<?php
$db = new PDO('sqlite:C:\utm-monitor\data\utm-monitor.sqlite');
$r = $db->query(
    'SELECT id,name,ip,port,enabled FROM utms ORDER BY id'
)->fetchAll(PDO::FETCH_ASSOC);
echo json_encode(
    $r,
    JSON_UNESCAPED_UNICODE |
    JSON_PRETTY_PRINT
);
