<?php

$db = new PDO('sqlite:C:\utm-monitor\data\utm-monitor.sqlite');
$db->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

function addColumn(PDO $db, string $table, string $column, string $definition): void
{
    $columns = $db->query("PRAGMA table_info(" . $table . ")")->fetchAll(PDO::FETCH_ASSOC);

    foreach ($columns as $row) {
        if ($row['name'] === $column) {
            echo "EXISTS: {$table}.{$column}\n";
            return;
        }
    }

    $db->exec("ALTER TABLE {$table} ADD COLUMN {$column} {$definition}");
    echo "ADDED: {$table}.{$column}\n";
}

/*
 * document_items
 */
addColumn($db, 'document_items', 'price', 'REAL');
addColumn($db, 'document_items', 'fa_reg_id', 'TEXT');
addColumn($db, 'document_items', 'party_f2_reg_id', 'TEXT');
addColumn($db, 'document_items', 'amc_count', 'INTEGER');

/*
 * documents — отправитель
 */
addColumn($db, 'documents', 'sender_inn', 'TEXT');
addColumn($db, 'documents', 'sender_kpp', 'TEXT');
addColumn($db, 'documents', 'sender_reg_id', 'TEXT');
addColumn($db, 'documents', 'sender_name', 'TEXT');
addColumn($db, 'documents', 'sender_short_name', 'TEXT');
addColumn($db, 'documents', 'sender_address', 'TEXT');

/*
 * documents — получатель
 */
addColumn($db, 'documents', 'receiver_inn', 'TEXT');
addColumn($db, 'documents', 'receiver_kpp', 'TEXT');
addColumn($db, 'documents', 'receiver_reg_id', 'TEXT');
addColumn($db, 'documents', 'receiver_name', 'TEXT');
addColumn($db, 'documents', 'receiver_short_name', 'TEXT');
addColumn($db, 'documents', 'receiver_address', 'TEXT');

/*
 * Связи документов
 */
$db->exec("
CREATE TABLE IF NOT EXISTS document_links (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    document_id INTEGER NOT NULL,
    linked_document_id INTEGER NOT NULL,
    link_type TEXT NOT NULL,
    created_at TEXT NOT NULL,
    UNIQUE(document_id, linked_document_id, link_type),
    FOREIGN KEY(document_id)
        REFERENCES documents(id)
        ON DELETE CASCADE,
    FOREIGN KEY(linked_document_id)
        REFERENCES documents(id)
        ON DELETE CASCADE
)
");

/*
 * арки
 */
$db->exec("
CREATE TABLE IF NOT EXISTS document_item_marks (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    document_item_id INTEGER NOT NULL,
    amc TEXT NOT NULL,
    created_at TEXT NOT NULL,
    UNIQUE(document_item_id, amc),
    FOREIGN KEY(document_item_id)
        REFERENCES document_items(id)
        ON DELETE CASCADE
)
");

/*
 * ндексы
 */
$db->exec("
CREATE INDEX IF NOT EXISTS idx_document_links_document
ON document_links(document_id)
");

$db->exec("
CREATE INDEX IF NOT EXISTS idx_document_links_linked
ON document_links(linked_document_id)
");

$db->exec("
CREATE INDEX IF NOT EXISTS idx_document_item_marks_item
ON document_item_marks(document_item_id)
");

echo "\n=== MIGRATION COMPLETE ===\n\n";

echo "document_items:\n";
foreach ($db->query("PRAGMA table_info(document_items)") as $row) {
    echo "  {$row['name']} {$row['type']}\n";
}

echo "\ndocuments:\n";
foreach ($db->query("PRAGMA table_info(documents)") as $row) {
    echo "  {$row['name']} {$row['type']}\n";
}

echo "\nTables:\n";
foreach ($db->query("
    SELECT name
    FROM sqlite_master
    WHERE type='table'
    ORDER BY name
") as $row) {
    echo "  {$row['name']}\n";
}
