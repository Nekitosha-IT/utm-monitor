<?php

declare(strict_types=1);

/*
 * API safety net: api.php must never leak PHP warnings/HTML into a JSON
 * response. This keeps browser requests parseable even when a legacy branch
 * throws a warning or unexpected exception.
 */
if (PHP_SAPI !== 'cli') {
    $script = basename((string)($_SERVER['SCRIPT_FILENAME'] ?? ''));

    if ($script === 'api.php') {
        ob_start();

        register_shutdown_function(static function (): void {
            $output = ob_get_clean();
            $trimmed = trim((string)$output);

            if ($trimmed !== '') {
                $decoded = json_decode($trimmed, true);

                if (json_last_error() === JSON_ERROR_NONE && is_array($decoded)) {
                    header('Content-Type: application/json; charset=utf-8');
                    echo $trimmed;
                    return;
                }
            }

            http_response_code(http_response_code() >= 400 ? http_response_code() : 500);
            header('Content-Type: application/json; charset=utf-8');

            echo json_encode([
                'success' => false,
                'error' => 'API вернул некорректный ответ',
                'details' => $trimmed !== '' ? mb_substr(strip_tags($trimmed), 0, 1000) : null,
            ], JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_INVALID_UTF8_SUBSTITUTE);
        });
    }
}

return [
    'refresh' => 600,
    'timeout' => 1,
    'storage' => [
        'file' => __DIR__ . '/utms.json'
    ],
    'utms' => [
        1 => [
            'id' => 1,
            'name' => 'Сникерс',
            'ip' => '10.0.0.100',
            'port' => 8086,
            'enabled' => true
        ],
        2 => ['id'=>2,'name'=>'УТМ №2','ip'=>'','port'=>8086,'enabled'=>false],
        3 => ['id'=>3,'name'=>'УТМ №3','ip'=>'','port'=>8086,'enabled'=>false],
        4 => ['id'=>4,'name'=>'УТМ №4','ip'=>'','port'=>8086,'enabled'=>false],
        5 => ['id'=>5,'name'=>'УТМ №5','ip'=>'','port'=>8086,'enabled'=>false],
        6 => ['id'=>6,'name'=>'УТМ №6','ip'=>'','port'=>8086,'enabled'=>false],
        7 => ['id'=>7,'name'=>'УТМ №7','ip'=>'','port'=>8086,'enabled'=>false],
        8 => ['id'=>8,'name'=>'УТМ №8','ip'=>'','port'=>8086,'enabled'=>false],
        9 => ['id'=>9,'name'=>'УТМ №9','ip'=>'','port'=>8086,'enabled'=>false],
        10 => ['id'=>10,'name'=>'УТМ №10','ip'=>'','port'=>8086,'enabled'=>false]
    ]
];
