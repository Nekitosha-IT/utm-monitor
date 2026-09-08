<?php

declare(strict_types=1);

/*
 * Fast, isolated UTM CRUD endpoint.
 * It never contacts the UTM, so adding/removing a UTM cannot hang on a network timeout.
 * Every code path returns JSON only.
 */

ob_start();
header('Content-Type: application/json; charset=utf-8');
header('Cache-Control: no-store, no-cache, must-revalidate, max-age=0');

function crudJson(array $payload, int $status = 200): never
{
    while (ob_get_level() > 0) {
        ob_end_clean();
    }

    http_response_code($status);
    echo json_encode(
        $payload,
        JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES
    );
    exit;
}

set_error_handler(static function (int $severity, string $message, string $file, int $line): bool {
    throw new ErrorException($message, 0, $severity, $file, $line);
});

set_exception_handler(static function (Throwable $e): never {
    crudJson([
        'success' => false,
        'error' => 'Ошибка API: ' . $e->getMessage(),
    ], 500);
});

try {
    $db = require __DIR__ . '/includes/database.php';
    require_once __DIR__ . '/includes/utm_repository.php';

    $action = trim((string)($_GET['action'] ?? ''));
    $raw = file_get_contents('php://input');
    $input = json_decode($raw ?: '', true);

    if (!is_array($input)) {
        $input = $_POST;
    }

    switch ($action) {
        case 'add':
            $name = trim((string)($input['name'] ?? ''));
            $ip = trim((string)($input['ip'] ?? ''));
            $port = (int)($input['port'] ?? 8086);
            $externalId = trim((string)($input['external_id'] ?? ''));

            if ($name === '') {
                crudJson(['success' => false, 'error' => 'Не указано название УТМ'], 400);
            }
            if ($ip === '' || filter_var($ip, FILTER_VALIDATE_IP) === false) {
                crudJson(['success' => false, 'error' => 'Укажите корректный IP-адрес УТМ'], 400);
            }
            if ($port < 1 || $port > 65535) {
                crudJson(['success' => false, 'error' => 'Порт должен быть от 1 до 65535'], 400);
            }

            $id = addUtm(
                $db,
                $name,
                $ip,
                $port,
                $externalId !== '' ? $externalId : null
            );

            crudJson([
                'success' => true,
                'message' => 'УТМ добавлен',
                'id' => $id,
            ]);

        case 'update':
            $id = (int)($input['id'] ?? 0);
            $name = trim((string)($input['name'] ?? ''));
            $ip = trim((string)($input['ip'] ?? ''));
            $port = (int)($input['port'] ?? 8086);

            if ($id <= 0) {
                crudJson(['success' => false, 'error' => 'Не указан ID УТМ'], 400);
            }
            if ($name === '') {
                crudJson(['success' => false, 'error' => 'Не указано название УТМ'], 400);
            }
            if ($ip === '' || filter_var($ip, FILTER_VALIDATE_IP) === false) {
                crudJson(['success' => false, 'error' => 'Укажите корректный IP-адрес УТМ'], 400);
            }
            if ($port < 1 || $port > 65535) {
                crudJson(['success' => false, 'error' => 'Порт должен быть от 1 до 65535'], 400);
            }

            $updated = updateUtm($db, $id, $name, $ip, $port);
            if (!$updated) {
                crudJson(['success' => false, 'error' => 'УТМ не найден'], 404);
            }

            crudJson(['success' => true, 'updated' => true]);

        case 'delete':
            $id = (int)($input['id'] ?? ($_GET['id'] ?? 0));
            if ($id <= 0) {
                crudJson(['success' => false, 'error' => 'Не указан ID УТМ'], 400);
            }

            $deleted = deleteUtm($db, $id);
            if (!$deleted) {
                crudJson(['success' => false, 'error' => 'УТМ не найден'], 404);
            }

            crudJson(['success' => true, 'deleted' => true]);

        case 'list':
            $utms = getAllUtms($db);
            crudJson([
                'success' => true,
                'utms' => $utms,
                'count' => count($utms),
            ]);

        default:
            crudJson([
                'success' => false,
                'error' => 'Неизвестное действие',
                'available_actions' => ['list', 'add', 'update', 'delete'],
            ], 400);
    }
} catch (Throwable $e) {
    crudJson([
        'success' => false,
        'error' => 'Ошибка API: ' . $e->getMessage(),
    ], 500);
}
