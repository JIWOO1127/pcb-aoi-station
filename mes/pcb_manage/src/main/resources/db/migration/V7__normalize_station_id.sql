-- 같은 물리 스테이션에 사용된 이전 표기(STATION-01)를 현재 ID(ST-01)로 통합한다.
UPDATE inspection SET station_id = 'ST-01' WHERE station_id = 'STATION-01';
