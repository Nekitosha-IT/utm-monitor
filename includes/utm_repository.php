<?php

declare(strict_types=1);

function getAllUtms(PDO $db): array
{
    return $db->query('SELECT * FROM utms ORDER BY id')->fetchAll(PDO::FETCH_ASSOC);
}
function getUtm(PDO $db, int $id): ?array
{
    $s=$db->prepare('SELECT * FROM utms WHERE id=? LIMIT 1'); $s->execute([$id]); $r=$s->fetch(); return $r?:null;
}
function getUtmById(PDO $db, int $id): ?array { return getUtm($db,$id); }
function addUtm(PDO $db,string $name,string $ip,int $port,?string $externalId=null):int
{
    $name=trim($name); $ip=trim($ip);
    if($name===''||$ip===''||$port<1||$port>65535) throw new InvalidArgumentException('Некорректные параметры УТМ');
    $s=$db->prepare('INSERT INTO utms(external_id,name,ip,port,enabled) VALUES(?,?,?,?,1)');
    $s->execute([$externalId?:null,$name,$ip,$port]); return (int)$db->lastInsertId();
}
function updateUtm(PDO $db,int $id,string $name,string $ip,int $port):bool
{
    if($id<=0||trim($name)===''||trim($ip)===''||$port<1||$port>65535)return false;
    $s=$db->prepare('UPDATE utms SET name=?,ip=?,port=?,updated_at=CURRENT_TIMESTAMP WHERE id=?');
    $s->execute([trim($name),trim($ip),$port,$id]); return $s->rowCount()>0;
}
function deleteUtm(PDO $db,int $id):bool
{
    if($id<=0)return false; $s=$db->prepare('DELETE FROM utms WHERE id=?'); $s->execute([$id]); return $s->rowCount()>0;
}
function setUtmEnabled(PDO $db,int $id,bool $enabled):bool
{
    $s=$db->prepare('UPDATE utms SET enabled=?,updated_at=CURRENT_TIMESTAMP WHERE id=?'); $s->execute([$enabled?1:0,$id]); return $s->rowCount()>0;
}
function updateUtmHealth(PDO $db,int $id,string $status,?string $error=null):void
{
    $s=$db->prepare('UPDATE utms SET last_status=?,last_check_at=CURRENT_TIMESTAMP,last_error=?,updated_at=CURRENT_TIMESTAMP WHERE id=?');
    $s->execute([$status,$error,$id]);
}
