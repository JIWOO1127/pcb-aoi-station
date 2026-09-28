CREATE TABLE product (
    product_id VARCHAR(50) PRIMARY KEY,
    name VARCHAR(120) NOT NULL,
    description VARCHAR(500),
    active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE recipe (
    recipe_id UUID PRIMARY KEY,
    product_id VARCHAR(50) NOT NULL REFERENCES product(product_id),
    version INTEGER NOT NULL,
    active BOOLEAN NOT NULL DEFAULT FALSE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT uq_recipe_product_version UNIQUE (product_id, version)
);

CREATE TABLE recipe_part (
    recipe_id UUID NOT NULL REFERENCES recipe(recipe_id),
    part_id VARCHAR(50) NOT NULL,
    expected_class VARCHAR(100) NOT NULL,
    x_mm DECIMAL(10, 3) NOT NULL,
    y_mm DECIMAL(10, 3) NOT NULL,
    tolerance_mm DECIMAL(10, 3) NOT NULL CHECK (tolerance_mm >= 0),
    check_mode VARCHAR(30) NOT NULL,
    PRIMARY KEY (recipe_id, part_id)
);

CREATE TABLE worker (
    worker_id VARCHAR(50) PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    pin_hash VARCHAR(100) NOT NULL,
    role VARCHAR(20) NOT NULL CHECK (role IN ('ADMIN', 'MANAGER', 'WORKER')),
    active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE inspection (
    inspection_id UUID PRIMARY KEY,
    product_id VARCHAR(50) NOT NULL REFERENCES product(product_id),
    recipe_id UUID REFERENCES recipe(recipe_id),
    worker_id VARCHAR(50) NOT NULL REFERENCES worker(worker_id),
    verdict VARCHAR(10) NOT NULL CHECK (verdict IN ('PASS', 'FAIL')),
    defect_count INTEGER NOT NULL DEFAULT 0 CHECK (defect_count >= 0),
    image_path VARCHAR(1000),
    latency_ms INTEGER CHECK (latency_ms >= 0),
    captured_at TIMESTAMP WITH TIME ZONE NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE part_result (
    part_result_id UUID PRIMARY KEY,
    inspection_id UUID NOT NULL REFERENCES inspection(inspection_id),
    part_id VARCHAR(50) NOT NULL,
    detected_class VARCHAR(100),
    confidence DECIMAL(6, 5) CHECK (confidence BETWEEN 0 AND 1),
    offset_mm DECIMAL(10, 3),
    defect_type VARCHAR(30),
    CONSTRAINT uq_part_result_inspection_part UNIQUE (inspection_id, part_id)
);

CREATE TABLE confirmation (
    confirmation_id UUID PRIMARY KEY,
    inspection_id UUID NOT NULL REFERENCES inspection(inspection_id),
    worker_id VARCHAR(50) NOT NULL REFERENCES worker(worker_id),
    decision VARCHAR(20) NOT NULL,
    reason_code VARCHAR(50),
    confirmed_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_inspection_captured_at ON inspection(captured_at);
CREATE INDEX idx_inspection_product_id ON inspection(product_id);
CREATE INDEX idx_part_result_inspection_id ON part_result(inspection_id);
CREATE INDEX idx_confirmation_inspection_id ON confirmation(inspection_id);
