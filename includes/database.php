<?php

declare(strict_types=1);

$dir = __DIR__ . '/../data/db';
if (!is_dir($dir)) mkdir($dir, 0775, true);

$db = new PDO('sqlite:' . $dir . '/utm-monitor.sqlite');
$db->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
$db->setAttribute(PDO::ATTR_DEFAULT_FETCH_MODE, PDO::FETCH_ASSOC);
$db->exec('PRAGMA foreign_keys = ON');
$db->exec('PRAGMA journal_mode = WAL');
$db->exec('PRAGMA busy_timeout = 5000');
$db->exec('PRAGMA synchronous = NORMAL');
$db->exec('PRAGMA temp_store = MEMORY');
$db->exec('PRAGMA cache_size = -32768');

$db->exec(<<<'SQL'
CREATE TABLE IF NOT EXISTS utms (
 id INTEGER PRIMARY KEY AUTOINCREMENT, external_id TEXT UNIQUE, name TEXT NOT NULL, ip TEXT NOT NULL,
 port INTEGER NOT NULL DEFAULT 8086, enabled INTEGER NOT NULL DEFAULT 1,
 created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, updated_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
 last_status TEXT, last_check_at TEXT, last_error TEXT
);
CREATE TABLE IF NOT EXISTS documents (
 id INTEGER PRIMARY KEY AUTOINCREMENT, utm_id INTEGER NOT NULL, document_id TEXT, document_type TEXT,
 direction TEXT, number TEXT, document_date TEXT, status TEXT, status_code TEXT, sender TEXT, receiver TEXT,
 sender_name TEXT, sender_short_name TEXT, sender_inn TEXT, sender_kpp TEXT, sender_reg_id TEXT, sender_address TEXT,
 receiver_name TEXT, receiver_short_name TEXT, receiver_inn TEXT, receiver_kpp TEXT, receiver_reg_id TEXT,
 receiver_address TEXT, raw_xml TEXT, created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
 updated_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, UNIQUE(utm_id,document_id),
 FOREIGN KEY(utm_id) REFERENCES utms(id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS idx_documents_utm_direction ON documents(utm_id,direction);
CREATE INDEX IF NOT EXISTS idx_documents_date ON documents(document_date);
CREATE INDEX IF NOT EXISTS idx_documents_status ON documents(status);
CREATE TABLE IF NOT EXISTS document_items (
 id INTEGER PRIMARY KEY AUTOINCREMENT, document_id INTEGER NOT NULL, item_index INTEGER NOT NULL, quantity REAL,
 inform_f1_reg_id TEXT, inform_f2_reg_id TEXT, alc_percent REAL, alc_percent_min REAL, alc_percent_max REAL,
 full_name TEXT, alc_code TEXT, capacity REAL, unit_type TEXT, alc_volume REAL, product_v_code TEXT,
 producer_client_reg_id TEXT, producer_inn TEXT, producer_kpp TEXT, producer_full_name TEXT,
 producer_short_name TEXT, producer_country TEXT, producer_region_code TEXT, producer_address TEXT,
 product_name TEXT, product_code TEXT, price REAL, measure TEXT, raw_xml TEXT,
 created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, updated_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
 UNIQUE(document_id,item_index), FOREIGN KEY(document_id) REFERENCES documents(id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS idx_document_items_document ON document_items(document_id);
CREATE INDEX IF NOT EXISTS idx_document_items_index ON document_items(document_id,item_index);
CREATE INDEX IF NOT EXISTS idx_document_items_f1 ON document_items(inform_f1_reg_id);
CREATE INDEX IF NOT EXISTS idx_document_items_f2 ON document_items(inform_f2_reg_id);
CREATE INDEX IF NOT EXISTS idx_document_items_product_code ON document_items(product_code);
CREATE TABLE IF NOT EXISTS document_item_marks (
 id INTEGER PRIMARY KEY AUTOINCREMENT, document_item_id INTEGER NOT NULL, amc TEXT NOT NULL,
 created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, UNIQUE(document_item_id,amc),
 FOREIGN KEY(document_item_id) REFERENCES document_items(id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS idx_marks_amc ON document_item_marks(amc);
CREATE INDEX IF NOT EXISTS idx_marks_item ON document_item_marks(document_item_id);
CREATE TABLE IF NOT EXISTS document_status_history (
 id INTEGER PRIMARY KEY AUTOINCREMENT, document_id INTEGER NOT NULL, old_status TEXT, new_status TEXT,
 message TEXT, created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
 FOREIGN KEY(document_id) REFERENCES documents(id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS idx_status_history_document ON document_status_history(document_id,created_at);
CREATE TABLE IF NOT EXISTS document_links (
 id INTEGER PRIMARY KEY AUTOINCREMENT, document_id INTEGER NOT NULL, linked_document_id INTEGER NOT NULL,
 link_type TEXT, created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
 UNIQUE(document_id,linked_document_id,link_type), FOREIGN KEY(document_id) REFERENCES documents(id) ON DELETE CASCADE,
 FOREIGN KEY(linked_document_id) REFERENCES documents(id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS idx_document_links_document ON document_links(document_id);
CREATE INDEX IF NOT EXISTS idx_document_links_linked ON document_links(linked_document_id);
CREATE TABLE IF NOT EXISTS events (
 id INTEGER PRIMARY KEY AUTOINCREMENT, utm_id INTEGER, level TEXT NOT NULL DEFAULT 'info', event_type TEXT NOT NULL,
 message TEXT NOT NULL, details TEXT, created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
 FOREIGN KEY(utm_id) REFERENCES utms(id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS idx_events_utm_created ON events(utm_id,created_at);
CREATE INDEX IF NOT EXISTS idx_events_type_created ON events(event_type,created_at);
SQL);

$columns = $db->query('PRAGMA table_info(document_items)')->fetchAll();
$existing = [];
foreach ($columns as $column) $existing[$column['name']] = true;
$required = [
 'inform_f1_reg_id'=>'TEXT','inform_f2_reg_id'=>'TEXT','alc_percent'=>'REAL','alc_percent_min'=>'REAL','alc_percent_max'=>'REAL',
 'full_name'=>'TEXT','alc_code'=>'TEXT','capacity'=>'REAL','unit_type'=>'TEXT','alc_volume'=>'REAL','product_v_code'=>'TEXT',
 'producer_client_reg_id'=>'TEXT','producer_inn'=>'TEXT','producer_kpp'=>'TEXT','producer_full_name'=>'TEXT','producer_short_name'=>'TEXT',
 'producer_country'=>'TEXT','producer_region_code'=>'TEXT','producer_address'=>'TEXT','updated_at'=>'TEXT'
];
foreach ($required as $name=>$type) if (!isset($existing[$name])) $db->exec("ALTER TABLE document_items ADD COLUMN {$name} {$type}");

$count = (int)$db->query('SELECT COUNT(*) FROM utms')->fetchColumn();
$jsonFile = __DIR__ . '/../utms.json';
if ($count === 0 && is_file($jsonFile)) {
    $items = json_decode((string)file_get_contents($jsonFile), true);
    if (is_array($items)) {
        $insert = $db->prepare('INSERT OR IGNORE INTO utms(external_id,name,ip,port,enabled,created_at) VALUES(?,?,?,?,?,COALESCE(?,CURRENT_TIMESTAMP))');
        foreach ($items as $item) {
            if (!is_array($item) || empty($item['ip'])) continue;
            $insert->execute([(string)($item['id'] ?? ''),trim((string)($item['name'] ?? 'УТМ')),trim((string)$item['ip']),(int)($item['port'] ?? 8086),1,$item['created_at'] ?? null]);
        }
    }
}

require_once __DIR__ . '/document_repository.php';

return $db;
