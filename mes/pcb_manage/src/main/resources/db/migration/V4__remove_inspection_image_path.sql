-- 검사 결과는 구조화된 데이터로 저장하며 이미지와 이미지 경로는 MES에 저장하지 않는다.
ALTER TABLE inspection DROP COLUMN IF EXISTS image_path;
ALTER TABLE inspection DROP CONSTRAINT IF EXISTS inspection_verdict_check;
ALTER TABLE inspection ADD CONSTRAINT inspection_verdict_check
    CHECK (verdict IN ('PASS', 'FAIL', 'INVALID'));
ALTER TABLE inspection ADD COLUMN station_id VARCHAR(50);
ALTER TABLE inspection ADD COLUMN error_code VARCHAR(50);
ALTER TABLE inspection ALTER COLUMN worker_id DROP NOT NULL;

ALTER TABLE part_result ADD COLUMN expected_class VARCHAR(100);
ALTER TABLE part_result ADD COLUMN expected_holes JSONB NOT NULL DEFAULT '[]';
ALTER TABLE part_result ADD COLUMN covered_holes JSONB NOT NULL DEFAULT '[]';

-- 레시피는 제품, 버전, 비고와 부품별 기대 클래스 및 홀 목록으로 관리한다.
ALTER TABLE recipe ADD COLUMN note TEXT;
ALTER TABLE recipe_part ADD COLUMN holes JSONB NOT NULL DEFAULT '[]';
ALTER TABLE recipe_part ALTER COLUMN x_mm DROP NOT NULL;
ALTER TABLE recipe_part ALTER COLUMN y_mm DROP NOT NULL;
ALTER TABLE recipe_part ALTER COLUMN tolerance_mm DROP NOT NULL;
ALTER TABLE recipe_part ALTER COLUMN check_mode DROP NOT NULL;

CREATE INDEX idx_inspection_station_id ON inspection(station_id);
CREATE INDEX idx_inspection_verdict ON inspection(verdict);

INSERT INTO product (product_id, name, description, active)
VALUES ('NE555-B', 'NE555-B', 'NE555 시연용 브레드보드', TRUE);

INSERT INTO recipe (recipe_id, product_id, version, active, note)
VALUES ('7bafaa9d-4d9f-4d2e-bc04-0db47d401003', 'NE555-B', 3, TRUE,
        'R1 470→330: 조명에 따라 470 오인, 시연 범위에서 저항 값 판별 제외');

UPDATE recipe SET active = FALSE
WHERE product_id = 'NE555-B' AND version <> 3;

INSERT INTO recipe_part (recipe_id, part_id, expected_class, holes) VALUES
('7bafaa9d-4d9f-4d2e-bc04-0db47d401003', 'U1',   'ic_chip',       '["H6","H7","I6","I7","J6","J7","K6","K7","L6","L7","M6","M7","N6","N7"]'),
('7bafaa9d-4d9f-4d2e-bc04-0db47d401003', 'R1',   'resistance330', '["E9","H9"]'),
('7bafaa9d-4d9f-4d2e-bc04-0db47d401003', 'R2',   'resistance330', '["N3","Q3"]'),
('7bafaa9d-4d9f-4d2e-bc04-0db47d401003', 'R3',   'resistance330', '["N8","Q8"]'),
('7bafaa9d-4d9f-4d2e-bc04-0db47d401003', 'C1',   'capacitor',     '["Q10","R10"]'),
('7bafaa9d-4d9f-4d2e-bc04-0db47d401003', 'LED1', 'led',           '["D7","E7"]'),
('7bafaa9d-4d9f-4d2e-bc04-0db47d401003', 'W1',   'black_wire',    '["D9","D13"]'),
('7bafaa9d-4d9f-4d2e-bc04-0db47d401003', 'W2',   'red_wire',      '["H0","H2"]'),
('7bafaa9d-4d9f-4d2e-bc04-0db47d401003', 'W3',   'black_wire',    '["J10","J13"]'),
('7bafaa9d-4d9f-4d2e-bc04-0db47d401003', 'W4',   'white_wire',    '["M4","Q4"]'),
('7bafaa9d-4d9f-4d2e-bc04-0db47d401003', 'W5',   'red_wire',      '["N0","N2"]'),
('7bafaa9d-4d9f-4d2e-bc04-0db47d401003', 'W6',   'black_wire',    '["R11","R13"]');
