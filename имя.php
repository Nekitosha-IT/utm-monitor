<?php
declare(strict_types=1);

/**
 * Синхронизация Т -> SQLite
 *
 * олучает:
 *   /api/db/in/list
 *   /api/db/out/list
 *   /opt/out
 *
 * атем скачивает реальные XML-документы:
 *   /opt/out/Ticket/{id}
 *   /opt/out/ReplyRests_v3/{id}
 *   /opt/out/ReplyRestsShop_v2/{id}
 */

function syncHttpGet(
    string $ip,
    int $port,
    string $path,
    int $timeout = 15
): array {
    $url = 'http://' . $ip . ':' . $port . $path;

    $context = stream_context_create([
        'http' => [
            'method' => 'GET',
            'timeout' => $timeout,
            'ignore_errors' => true,
            'protocol_version' => 1.1,
            'header' =>
                "Accept: */*\r\n" .
                "User-Agent: UTM-MONITOR/2.0\r\n" .
                "Connection: close\r\n"
        ]
    ]);

    $start = microtime(true);

    $data = @file_get_contents(
        $url,
        false,
        $context
    );

    $time = (int)round(
        (microtime(true) - $start) * 1000
    );

    $headers = [];

    if (function_exists('http_get_last_response_headers')) {
        $headers = http_get_last_response_headers();

        if (!is_array($headers)) {
            $headers = [];
        }
    }

    $httpCode = null;

    foreach ($headers as $header) {
        if (
            preg_match(
                '/HTTP\/\d+(?:\.\d+)?\s+(\d+)/i',
                $header,
                $m
            )
        ) {
            $httpCode = (int)$m[1];
        }
    }

    if ($data === false) {
        return [
            'ok' => false,
            'http_code' => $httpCode,
            'time' => $time,
            'data' => null,
            'error' => 'е удалось получить данные от Т',
            'url' => $url
        ];
    }

    if ($httpCode !== null && $httpCode >= 400) {
        return [
            'ok' => false,
            'http_code' => $httpCode,
            'time' => $time,
            'data' => $data,
            'error' => 'Т вернул HTTP ' . $httpCode,
            'url' => $url
        ];
    }

    return [
        'ok' => true,
        'http_code' => $httpCode ?? 200,
        'time' => $time,
        'data' => $data,
        'error' => null,
        'url' => $url
    ];
}


/**
 * тение списка  Т.
 */
function syncGetDbList(
    string $ip,
    int $port,
    string $direction,
    int $limit = 100,
    int $offset = 0
): array {
    $endpoint = $direction === 'incoming'
        ? '/api/db/in/list'
        : '/api/db/out/list';

    $path = $endpoint .
        '?limit=' . $limit .
        '&offset=' . $offset;

    $response = syncHttpGet(
        $ip,
        $port,
        $path
    );

    if (!$response['ok']) {
        return $response;
    }

    $json = json_decode(
        (string)$response['data'],
        true
    );

    if (!is_array($json)) {
        return [
            'ok' => false,
            'http_code' => $response['http_code'],
            'time' => $response['time'],
            'data' => null,
            'error' => 'Т вернул не JSON',
            'url' => $response['url']
        ];
    }

    return [
        'ok' => true,
        'http_code' => $response['http_code'],
        'time' => $response['time'],
        'data' => $json,
        'error' => null,
        'url' => $response['url']
    ];
}


/**
 * олучение /opt/out.
 *
 * десь Т возвращает XML:
 *
 * <A>
 *   <url replyId="...">http://.../opt/out/Ticket/7758</url>
 *   ...
 * </A>
 */
function syncGetOutQueue(
    string $ip,
    int $port
): array {
    return syncHttpGet(
        $ip,
        $port,
        '/opt/out'
    );
}


/**
 * пределяет относительный URL документа.
 */
function syncNormalizeDocumentPath(
    string $url,
    string $ip,
    int $port
): ?string {
    $url = trim($url);

    if ($url === '') {
        return null;
    }

    $parsed = parse_url($url);

    if (is_array($parsed) && isset($parsed['path'])) {
        $path = $parsed['path'];

        if ($path !== '') {
            return $path;
        }
    }

    if (str_starts_with($url, '/')) {
        return $url;
    }

    return null;
}


/**
 * езопасно получает XML из URL документа.
 */
function syncFetchDocument(
    string $ip,
    int $port,
    string $url
): array {
    $path = syncNormalizeDocumentPath(
        $url,
        $ip,
        $port
    );

    if ($path === null) {
        return [
            'ok' => false,
            'error' => 'екорректный URL документа',
            'url' => $url,
            'data' => null
        ];
    }

    return syncHttpGet(
        $ip,
        $port,
        $path,
        30
    );
}


/**
 * озвращает локальное имя XML-элемента без namespace.
 */
function syncLocalName(
    DOMElement $element
): string {
    $name = $element->localName;

    if ($name !== null && $name !== '') {
        return $name;
    }

    return $element->nodeName;
}


/**
 * щет первый элемент с указанным localName.
 */
function syncFindFirst(
    DOMDocument $dom,
    string $localName
): ?DOMElement {
    $xpath = new DOMXPath($dom);

    $nodes = $xpath->query(
        '//*[local-name()="' .
        $localName .
        '"]'
    );

    if ($nodes === false || $nodes->length === 0) {
        return null;
    }

    $node = $nodes->item(0);

    return $node instanceof DOMElement
        ? $node
        : null;
}


/**
 * Текст первого элемента.
 */
function syncText(
    DOMDocument $dom,
    string $localName
): ?string {
    $element = syncFindFirst(
        $dom,
        $localName
    );

    if (!$element) {
        return null;
    }

    $value = trim(
        $element->textContent
    );

    return $value === ''
        ? null
        : $value;
}


/**
 * Собирает атрибут из первого подходящего элемента.
 */
function syncAttribute(
    DOMDocument $dom,
    string $localName,
    string $attribute
): ?string {
    $element = syncFindFirst(
        $dom,
        $localName
    );

    if (!$element) {
        return null;
    }

    if (!$element->hasAttribute($attribute)) {
        return null;
    }

    $value = trim(
        $element->getAttribute($attribute)
    );

    return $value === ''
        ? null
        : $value;
}


/**
 * пределяем тип документа по URL / XML.
 */
function syncDetectDocumentType(
    string $url,
    DOMDocument $dom
): string {
    $path = parse_url(
        $url,
        PHP_URL_PATH
    );

    if (is_string($path)) {
        if (str_contains($path, '/Ticket/')) {
            return 'Ticket';
        }

        if (str_contains($path, '/ReplyRests_v3/')) {
            return 'ReplyRests_v3';
        }

        if (str_contains($path, '/ReplyRestsShop_v2/')) {
            return 'ReplyRestsShop_v2';
        }
    }

    $root = $dom->documentElement;

    if ($root instanceof DOMElement) {
        $rootName = syncLocalName($root);

        if ($rootName !== '') {
            return $rootName;
        }
    }

    return 'Unknown';
}


/**
 * звлекаем основные реквизиты документа.
 */
function syncParseDocument(
    string $xml,
    string $sourceUrl
): array {
    $dom = new DOMDocument();

    $previous = libxml_use_internal_errors(true);

    $loaded = $dom->loadXML(
        $xml,
        LIBXML_NONET |
        LIBXML_NOBLANKS
    );

    libxml_clear_errors();
    libxml_use_internal_errors($previous);

    if (!$loaded) {
        throw new RuntimeException(
            'е удалось разобрать XML документа'
        );
    }

    $documentType = syncDetectDocumentType(
        $sourceUrl,
        $dom
    );

    $number =
        syncText($dom, 'NUMBER')
        ?? syncText($dom, 'Number')
        ?? syncText($dom, 'Identity')
        ?? null;

    $documentDate =
        syncText($dom, 'DocumentDate')
        ?? syncText($dom, 'DocDate')
        ?? syncText($dom, 'TicketDate')
        ?? null;

    $sender =
        syncText($dom, 'Sender')
        ?? syncText($dom, 'ClientRegId')
        ?? null;

    $receiver =
        syncText($dom, 'Receiver')
        ?? null;

    $status =
        syncText($dom, 'Status')
        ?? syncText($dom, 'State')
        ?? null;

    $statusCode =
        syncText($dom, 'StatusCode')
        ?? null;

    /*
     * ля Ticket Identity обычно является хорошим
     * идентификатором документа.
     */
    $documentId =
        syncText($dom, 'Identity')
        ?? syncText($dom, 'DocId')
        ?? null;

    /*
     * сли явного ID нет — используем replyId из URL.
     */
    if (
        $documentId === null ||
        trim($documentId) === ''
    ) {
        $documentId =
            syncAttribute(
                $dom,
                'url',
                'replyId'
            );
    }

    return [
        'document_id' => $documentId,
        'document_type' => $documentType,
        'number' => $number,
        'document_date' => $documentDate,
        'sender' => $sender,
        'receiver' => $receiver,
        'status' => $status,
        'status_code' => $statusCode,
        'raw_xml' => $xml,
        'dom' => $dom
    ];
}


/**
 * Синхронизация одного документа.
 */
function syncSaveOneDocument(
    PDO $db,
    int $utmId,
    string $direction,
    string $sourceUrl,
    string $xml
): array {
    $parsed = syncParseDocument(
        $xml,
        $sourceUrl
    );

    $documentDbId = saveDocument(
        $db,
        $utmId,
        $parsed['document_id'],
        $parsed['document_type'],
        $direction,
        $parsed['number'],
        $parsed['document_date'],
        $parsed['sender'],
        $parsed['receiver'],
        $parsed['status'],
        $parsed['status_code'],
        $parsed['raw_xml']
    );

    return [
        'db_id' => $documentDbId,
        'document_id' => $parsed['document_id'],
        'document_type' => $parsed['document_type'],
        'direction' => $direction
    ];
}


/**
 * лавная функция синхронизации.
 */
function syncUtm(
    PDO $db,
    array $utm,
    int $limit = 100
): array {
    $utmId = (int)$utm['id'];
    $ip = (string)$utm['ip'];
    $port = (int)$utm['port'];

    $result = [
        'success' => true,
        'utm' => [
            'id' => $utmId,
            'name' => $utm['name'] ?? '',
            'ip' => $ip,
            'port' => $port
        ],
        'incoming' => [
            'found' => 0,
            'saved' => 0,
            'errors' => 0
        ],
        'outgoing' => [
            'found' => 0,
            'saved' => 0,
            'errors' => 0
        ],
        'queue' => [
            'found' => 0,
            'saved' => 0,
            'errors' => 0
        ],
        'documents' => [],
        'errors' => []
    ];

    /*
     * -----------------------------------------------------
     * 1. ходящие
     * -----------------------------------------------------
     */
    $incoming = syncGetDbList(
        $ip,
        $port,
        'incoming',
        $limit,
        0
    );

    if ($incoming['ok']) {
        $data = $incoming['data'];

        $rows =
            $data['data']['rows']
            ?? $data['data']
            ?? $data['rows']
            ?? [];

        if (is_array($rows)) {
            $result['incoming']['found'] =
                count($rows);
        }
    } else {
        $result['incoming']['errors']++;

        $result['errors'][] = [
            'stage' => 'incoming_list',
            'error' => $incoming['error']
        ];
    }

    /*
     * -----------------------------------------------------
     * 2. сходящие
     * -----------------------------------------------------
     */
    $outgoing = syncGetDbList(
        $ip,
        $port,
        'outgoing',
        $limit,
        0
    );

    if ($outgoing['ok']) {
        $data = $outgoing['data'];

        $rows =
            $data['data']['rows']
            ?? $data['data']
            ?? $data['rows']
            ?? [];

        if (is_array($rows)) {
            $result['outgoing']['found'] =
                count($rows);
        }
    } else {
        $result['outgoing']['errors']++;

        $result['errors'][] = [
            'stage' => 'outgoing_list',
            'error' => $outgoing['error']
        ];
    }

    /*
     * -----------------------------------------------------
     * 3. чередь /opt/out
     * -----------------------------------------------------
     */
    $queue = syncGetOutQueue(
        $ip,
        $port
    );

    if (!$queue['ok']) {
        $result['queue']['errors']++;

        $result['errors'][] = [
            'stage' => 'opt_out',
            'error' => $queue['error']
        ];

        addEvent(
            $db,
            $utmId,
            'error',
            'sync',
            'шибка получения /opt/out',
            json_encode(
                $queue,
                JSON_UNESCAPED_UNICODE |
                JSON_UNESCAPED_SLASHES
            )
        );

        return $result;
    }

    $queueXml = (string)$queue['data'];

    $dom = new DOMDocument();

    $previous = libxml_use_internal_errors(true);

    $loaded = $dom->loadXML(
        $queueXml,
        LIBXML_NONET |
        LIBXML_NOBLANKS
    );

    libxml_clear_errors();
    libxml_use_internal_errors($previous);

    if (!$loaded) {
        $result['queue']['errors']++;

        $result['errors'][] = [
            'stage' => 'opt_out_xml',
            'error' => 'екорректный XML /opt/out'
        ];

        return $result;
    }

    $xpath = new DOMXPath($dom);

    $nodes = $xpath->query(
        '//*[local-name()="url"]'
    );

    if ($nodes === false) {
        $nodes = [];
    }

    $result['queue']['found'] =
        $nodes instanceof DOMNodeList
            ? $nodes->length
            : 0;

    /*
     * -----------------------------------------------------
     * 4. Скачиваем каждый документ
     * -----------------------------------------------------
     */
    if ($nodes instanceof DOMNodeList) {
        foreach ($nodes as $node) {
            if (!$node instanceof DOMElement) {
                continue;
            }

            $sourceUrl = trim(
                $node->textContent
            );

            if ($sourceUrl === '') {
                continue;
            }

            $replyId = null;

            if ($node->hasAttribute('replyId')) {
                $replyId = trim(
                    $node->getAttribute('replyId')
                );
            }

            $document = syncFetchDocument(
                $ip,
                $port,
                $sourceUrl
            );

            if (!$document['ok']) {
                $result['queue']['errors']++;

                $result['errors'][] = [
                    'stage' => 'document',
                    'url' => $sourceUrl,
                    'reply_id' => $replyId,
                    'error' => $document['error']
                ];

                continue;
            }

            try {
                /*
                 *  /opt/out могут находиться как входящие
                 * ответы, так и служебные документы.
                 *
                 * ока сохраняем их как outgoing,
                 * потому что это фактический выходной
                 * поток Т.
                 */
                $saved = syncSaveOneDocument(
                    $db,
                    $utmId,
                    'outgoing',
                    $sourceUrl,
                    (string)$document['data']
                );

                $result['queue']['saved']++;

                $result['documents'][] = [
                    'reply_id' => $replyId,
                    'url' => $sourceUrl,
                    'document' => $saved
                ];
            } catch (Throwable $e) {
                $result['queue']['errors']++;

                $result['errors'][] = [
                    'stage' => 'save_document',
                    'url' => $sourceUrl,
                    'reply_id' => $replyId,
                    'error' => $e->getMessage()
                ];
            }
        }
    }

    addEvent(
        $db,
        $utmId,
        count($result['errors']) > 0
            ? 'warning'
            : 'info',
        'sync',
        'Синхронизация Т завершена',
        json_encode(
            $result,
            JSON_UNESCAPED_UNICODE |
            JSON_UNESCAPED_SLASHES
        )
    );

    return $result;
}
