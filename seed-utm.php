<?php
declare(strict_types=1);

$db = new PDO(
    'sqlite:C:\utm-monitor\data\utm-monitor.sqlite',
    null,
    null,
    [
        PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION,
        PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC
    ]
);

$stmt = $db->prepare(
    'SELECT id FROM utms WHERE ip = :ip AND port = :port LIMIT 1'
);

$stmt->execute([
    ':ip' => '10.0.0.100',
    ':port' => 8086
]);

$existing = $stmt->fetch();

if ($existing) {
    echo "UTM уже существует. ID = " . $existing['id'] . PHP_EOL;
    exit;
}

$stmt = $db->prepare(
    'INSERT INTO utms
    (name, ip, port, external_id, enabled, created_at, updated_at)
    VALUES
    (:name, :ip, :port, :external_id, 1, :created_at, :updated_at)'
);

$now = date('Y-m-d H:i:s');

$stmt->execute([
    ':name' => 'Сникерс',
    ':ip' => '10.0.0.100',
    ':port' => 8086,
    ':external_id' => 'f955a282713b464a',
    ':created_at' => $now,
    ':updated_at' => $now
]);

echo "UTM добавлен. ID = " . $db->lastInsertId() . PHP_EOL;
