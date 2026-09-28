-- 현재 시연 환경은 실제 AOI 스테이션 한 대(ST-01)만 운용한다.
-- V5에서 화면 예시용으로 넣었던 추가 스테이션은 제거하고 시드 검사도 실제 스테이션으로 통합한다.
UPDATE inspection
SET station_id = 'ST-01'
WHERE inspection_id IN (
    '20000000-0000-0000-0000-000000000001',
    '20000000-0000-0000-0000-000000000002',
    '20000000-0000-0000-0000-000000000003',
    '20000000-0000-0000-0000-000000000004',
    '20000000-0000-0000-0000-000000000005',
    '20000000-0000-0000-0000-000000000006',
    '20000000-0000-0000-0000-000000000007',
    '20000000-0000-0000-0000-000000000008'
);

DELETE FROM station WHERE station_id <> 'ST-01';

UPDATE station
SET name = 'AOI 검사 스테이션',
    status = 'RUNNING',
    product_id = 'NE555-B',
    recipe_id = '7bafaa9d-4d9f-4d2e-bc04-0db47d401003',
    camera_status = 'ONLINE',
    ai_status = 'ONLINE',
    mes_status = 'ONLINE',
    last_seen = CURRENT_TIMESTAMP
WHERE station_id = 'ST-01';
