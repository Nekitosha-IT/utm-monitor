<?php
declare(strict_types=1);

$db = require __DIR__ . '/includes/database.php';

function addColumn(PDO $db, string $table, string $column, string $definition): void
{
    $columns = $db->query("PRAGMA table_info($table)")->fetchAll(PDO::FETCH_ASSOC);

    foreach ($columns as $c) {
        if ($c['name'] === $column) {
            echo "EXISTS: {$table}.{$column}\n";
            return;
        }
    }

    $db->exec("ALTER TABLE {$table} ADD COLUMN {$column} {$definition}");
    echo "ADDED: {$table}.{$column}\n";
}

/*
 * анные позиции накладной
 */
addColumn($db, 'document_items', 'price', 'REAL NULL');
addColumn($db, 'document_items', 'fa_reg_id', 'TEXT NULL');
addColumn($db, 'document_items', 'party_f2_reg_id', 'TEXT NULL');
addColumn($db, 'document_items', 'amc_count', 'INTEGER NULL');

/*
 * ормализованные данные отправителя/получателя
 */
addColumn($db, 'documents', 'sender_inn', 'TEXT NULL');
addColumn($db, 'documents', 'sender_kpp', 'TEXT NULL');
addColumn($db, 'documents', 'sender_reg_id', 'TEXT NULL');
addColumn($db, 'documents', 'sender_name', 'TEXT NULL');
addColumn($db, 'documents', 'sender_short_name', 'TEXT NULL');
addColumn($db, 'documents', 'sender_address', 'TEXT NULL');

addColumn($db, 'documents', 'receiver_inn', 'TEXT NULL');
addColumn($db, 'documents', 'receiver_kpp', 'TEXT NULL');
addColumn($db, 'documents', 'receiver_reg_id', 'TEXT NULL');
addColumn($db, 'documents', 'receiver_name', 'TEXT NULL');
addColumn($db, 'documents', 'receiver_short_name', 'TEXT NULL');
addColumn($db, 'documents', 'receiver_address', 'TEXT NULL');

/*
 * Связи документов:
 *
 * WayBill -> FORM2REGINFO
 * WayBill -> TTNHISTORYF2REG
 * WayBill -> Ticket
 */
$db->exec(<<<SQL
CREATE TABLE IF NOT EXISTS document_links (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    document_id INTEGER NOT NULL,
    linked_document_id INTEGER NOT NULL,
    link_type TEXT NOT NULL,
    created_at TEXT NOT NULL,
    UNIQUE(document_id, linked_document_id, link_type),
    FOREIGN KEY(document_id) REFERENCES documents(id) ON DELETE CASCADE,
    FOREIGN KEY(linked_document_id) REFERENCES documents(id) ON DELETE CASCADE
)
SQL);

$db->exec(
    'CREATE INDEX IF NOT EXISTS idx_document_links_document
     ON document_links(document_id)'
);

$db->exec(
    'CREATE INDEX IF NOT EXISTS idx_document_links_linked
     ON document_links(linked_document_id)'
);

/*
 * тдельное хранение DataMatrix / AMC
 */
$db->exec(<<<SQL
CREATE TABLE IF NOT EXISTS document_item_marks (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    document_item_id INTEGER NOT NULL,
    amc TEXT NOT NULL,
    created_at TEXT NOT NULL,
    UNIQUE(document_item_id, amc),
    FOREIGN KEY(document_item_id) REFERENCES document_items(id) ON DELETE CASCADE
)
SQL);

$db->exec(
    'CREATE INDEX IF NOT EXISTS idx_document_item_marks_item
     ON document_item_marks(document_item_id)'
);

echo "\n=== MIGRATION COMPLETE ===\n";

echo "\n=== DOCUMENT_ITEMS COLUMNS ===\n";
foreach ($db->query("PRAGMA table_info(document_items)") as $row) {
    echo $row['name'] . " " . $row['type'] . "\n";
}

echo "\n=== DOCUMENTS COLUMNS ===\n";
foreach ($db->query("PRAGMA table_info(documents)") as $row) {
    echo $row['name'] . " " . $row['type'] . "\n";
}
